using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Firestore.EntityFrameworkCore.Metadata.Conventions;

public class ListEnumToStringArrayConvention : IPropertyAddedConvention
{
    public void ProcessPropertyAdded(
        IConventionPropertyBuilder propertyBuilder,
        IConventionContext<IConventionPropertyBuilder> context)
    {
        var property = propertyBuilder.Metadata;
        var propertyType = property.ClrType;

        // Solo aplicar si no tiene conversión configurada
        if (property.GetValueConverter() != null)
            return;

        // Detectar List<TEnum> donde TEnum es un enum
        if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(List<>))
        {
            var elementType = propertyType.GetGenericArguments()[0];

            // Verificar si el elemento es un enum
            if (elementType.IsEnum)
            {
                // Crear el converter genérico List<TEnum> → List<string>
                var converterType = typeof(ListEnumToStringConverter<>).MakeGenericType(elementType);
                var converter = (ValueConverter)Activator.CreateInstance(converterType)!;
                propertyBuilder.HasConversion(converter);
            }
            // También manejar List<TEnum?> (enums nullable)
            else if (Nullable.GetUnderlyingType(elementType)?.IsEnum == true)
            {
                var underlyingEnumType = Nullable.GetUnderlyingType(elementType)!;
                var converterType = typeof(ListNullableEnumToStringConverter<>).MakeGenericType(underlyingEnumType);
                var converter = (ValueConverter)Activator.CreateInstance(converterType)!;
                propertyBuilder.HasConversion(converter);
            }
        }
    }
}

// Converter para List<TEnum> → List<string>
public class ListEnumToStringConverter<TEnum> : ValueConverter<List<TEnum>, List<string>>
    where TEnum : struct, Enum
{
    public ListEnumToStringConverter()
        : base(
            v => v.Select(e => e.ToString()).ToList(),
            v => v.Select(s => Enum.Parse<TEnum>(s)).ToList()
        )
    {
    }
}

// Converter para List<TEnum?> → List<string?>
public class ListNullableEnumToStringConverter<TEnum> : ValueConverter<List<TEnum?>, List<string?>>
    where TEnum : struct, Enum
{
    public ListNullableEnumToStringConverter()
        : base(
            v => v.Select(e => e.HasValue ? e.Value.ToString() : null).ToList(),
            v => v.Select(s => string.IsNullOrEmpty(s) ? (TEnum?)null : Enum.Parse<TEnum>(s)).ToList()
        )
    {
    }
}
