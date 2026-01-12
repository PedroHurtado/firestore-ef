using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;

namespace Fudie.Firestore.EntityFrameworkCore.Storage
{
    public class FirestoreEnumTypeMapping : RelationalTypeMapping
    {
        public FirestoreEnumTypeMapping(Type enumType)
            : base(
                new RelationalTypeMappingParameters(
                    new CoreTypeMappingParameters(
                        clrType: enumType,
                        converter: CreateConverter(enumType),
                        jsonValueReaderWriter: null
                    ),
                    storeType: "string",
                    dbType: null
                ))
        {
        }

        protected FirestoreEnumTypeMapping(RelationalTypeMappingParameters parameters)
            : base(parameters)
        {
        }

        protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
            => new FirestoreEnumTypeMapping(parameters.CoreParameters.ClrType);

        private static ValueConverter CreateConverter(Type enumType)
        {
            var converterType = typeof(EnumToStringConverter<>).MakeGenericType(enumType);
            return (ValueConverter)Activator.CreateInstance(converterType)!;
        }
    }
}