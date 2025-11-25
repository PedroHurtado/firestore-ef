using System;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Firestore.EntityFrameworkCore.Metadata.Conventions;

public class EnumToStringConvention : IPropertyAddedConvention
{
    public void ProcessPropertyAdded(
        IConventionPropertyBuilder propertyBuilder,
        IConventionContext<IConventionPropertyBuilder> context)
    {
        var property = propertyBuilder.Metadata;

        // Solo aplicar si la propiedad es un enum y no tiene una conversión configurada
        if (property.ClrType.IsEnum && property.GetValueConverter() == null)
        {
            // Aplicar conversión a string
            var converterType = typeof(EnumToStringConverter<>).MakeGenericType(property.ClrType);
            var converter = (ValueConverter)Activator.CreateInstance(converterType)!;
            propertyBuilder.HasConversion(converter);
        }
    }
}
