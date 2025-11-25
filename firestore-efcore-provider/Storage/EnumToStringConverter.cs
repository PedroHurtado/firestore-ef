using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;

namespace Firestore.EntityFrameworkCore.Storage
{
    public class EnumToStringConverter<TEnum> : ValueConverter<TEnum, string>
        where TEnum : struct, Enum
    {
        public EnumToStringConverter()
            : base(
                convertToProviderExpression: v => v.ToString(),
                convertFromProviderExpression: v => Enum.Parse<TEnum>(v),
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