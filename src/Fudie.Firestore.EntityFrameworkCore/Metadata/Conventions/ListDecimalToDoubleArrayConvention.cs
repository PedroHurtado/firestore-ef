using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

public class ListDecimalToDoubleArrayConvention : IPropertyAddedConvention
{
    public void ProcessPropertyAdded(
        IConventionPropertyBuilder propertyBuilder,
        IConventionContext<IConventionPropertyBuilder> context)
    {
        var property = propertyBuilder.Metadata;
        var propertyType = property.ClrType;

        // Detectar List<decimal> o List<decimal?>
        if (property.GetValueConverter() == null)
        {
            if (propertyType == typeof(List<decimal>))
            {
                var converter = new ValueConverter<List<decimal>, List<double>>(
                    v => v.Select(d => (double)d).ToList(),
                    v => v.Select(d => (decimal)d).ToList()
                );
                propertyBuilder.HasConversion(converter);
            }
            else if (propertyType == typeof(List<decimal?>))
            {
                var converter = new ValueConverter<List<decimal?>, List<double?>>(
                    v => v.Select(d => d.HasValue ? (double?)d.Value : null).ToList(),
                    v => v.Select(d => d.HasValue ? (decimal?)d.Value : null).ToList()
                );
                propertyBuilder.HasConversion(converter);
            }
        }
    }
}
