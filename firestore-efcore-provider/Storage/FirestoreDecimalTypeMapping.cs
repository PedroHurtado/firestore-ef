using Microsoft.EntityFrameworkCore.Storage;

namespace Firestore.EntityFrameworkCore.Storage
{
    public class FirestoreDecimalTypeMapping : RelationalTypeMapping
    {
        public FirestoreDecimalTypeMapping()
            : base(
                new RelationalTypeMappingParameters(
                    new CoreTypeMappingParameters(
                        clrType: typeof(decimal),
                        converter: new DecimalToDoubleConverter(),
                        jsonValueReaderWriter: null
                    ),
                    storeType: "number",
                    dbType: null  // Porque en Firestore es un double
                ))
        {
        }

        protected FirestoreDecimalTypeMapping(RelationalTypeMappingParameters parameters)
            : base(parameters)
        {
        }

        protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
            => new FirestoreDecimalTypeMapping(parameters);
    }
}