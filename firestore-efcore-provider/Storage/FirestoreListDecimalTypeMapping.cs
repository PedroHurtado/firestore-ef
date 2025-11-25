using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Firestore.EntityFrameworkCore.Storage
{
    public class FirestoreListDecimalTypeMapping : CoreTypeMapping
    {
        public FirestoreListDecimalTypeMapping(Type clrType)
            : base(new CoreTypeMappingParameters(
                clrType: clrType,
                converter: CreateConverter(clrType),
                comparer: CreateComparer(clrType)))
        {
        }

        protected FirestoreListDecimalTypeMapping(CoreTypeMappingParameters parameters)
            : base(parameters)
        {
        }

        private static ValueConverter CreateConverter(Type listType)
        {
            // List<decimal> → List<double>
            if (listType == typeof(List<decimal>))
            {
                return new ValueConverter<List<decimal>, List<double>>(
                    v => v.Select(d => (double)d).ToList(),
                    v => v.Select(d => (decimal)d).ToList()
                );
            }
            
            // List<decimal?> → List<double?>
            if (listType == typeof(List<decimal?>))
            {
                return new ValueConverter<List<decimal?>, List<double?>>(
                    v => v.Select(d => d.HasValue ? (double?)d.Value : null).ToList(),
                    v => v.Select(d => d.HasValue ? (decimal?)d.Value : null).ToList()
                );
            }

            throw new InvalidOperationException($"Unsupported list type: {listType}");
        }

        private static ValueComparer CreateComparer(Type listType)
        {
            if (listType == typeof(List<decimal>))
            {
                return new ValueComparer<List<decimal>>(
                    (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()
                );
            }
            
            if (listType == typeof(List<decimal?>))
            {
                return new ValueComparer<List<decimal?>>(
                    (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()
                );
            }

            throw new InvalidOperationException($"Unsupported list type: {listType}");
        }

        public override CoreTypeMapping WithComposedConverter(
            ValueConverter? converter, 
            ValueComparer? comparer = null, 
            ValueComparer? keyComparer = null, 
            CoreTypeMapping? elementMapping = null, 
            JsonValueReaderWriter? jsonValueReaderWriter = null)
        {
            return new FirestoreListDecimalTypeMapping(
                Parameters.WithComposedConverter(converter, comparer, keyComparer, elementMapping, jsonValueReaderWriter));
        }

        protected override CoreTypeMapping Clone(CoreTypeMappingParameters parameters)
            => new FirestoreListDecimalTypeMapping(parameters);
    }
}