using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Fudie.Firestore.EntityFrameworkCore.Storage
{
    public class DecimalToDoubleConverter : ValueConverter<decimal, double>
    {
        public DecimalToDoubleConverter()
            : base(
                convertToProviderExpression: v => (double)v,
                convertFromProviderExpression: v => (decimal)v,
                mappingHints: new ConverterMappingHints(
                    size: null,
                    precision: null,
                    scale: null,
                    unicode: null,
                    valueGeneratorFactory: null))
        {
        }
    }
}