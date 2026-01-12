using System;
using System.Collections.Generic;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Converters;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

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
