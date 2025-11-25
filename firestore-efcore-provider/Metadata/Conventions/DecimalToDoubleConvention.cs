using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Firestore.EntityFrameworkCore.Metadata.Conventions;

public class DecimalToDoubleConvention : IPropertyAddedConvention
{
    public void ProcessPropertyAdded(
        IConventionPropertyBuilder propertyBuilder,
        IConventionContext<IConventionPropertyBuilder> context)
    {
        var property = propertyBuilder.Metadata;

        // Solo aplicar si la propiedad es decimal y no tiene una conversión configurada
        if (property.ClrType == typeof(decimal) && property.GetValueConverter() == null)
        {
            propertyBuilder.HasConversion(new CastingConverter<decimal, double>());
        }
        else if (property.ClrType == typeof(decimal?) && property.GetValueConverter() == null)
        {
            propertyBuilder.HasConversion(new CastingConverter<decimal?, double?>());
        }
    }
}
