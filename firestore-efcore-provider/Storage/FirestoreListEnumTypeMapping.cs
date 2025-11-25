using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Firestore.EntityFrameworkCore.Storage
{
    public class FirestoreListEnumTypeMapping : CoreTypeMapping
    {
        private readonly Type _enumType;

        public FirestoreListEnumTypeMapping(Type clrType, Type enumType)
            : base(new CoreTypeMappingParameters(
                clrType: clrType,
                converter: CreateConverter(clrType, enumType),
                comparer: CreateComparer(clrType)))
        {
            _enumType = enumType;
        }

        protected FirestoreListEnumTypeMapping(CoreTypeMappingParameters parameters, Type enumType)
            : base(parameters)
        {
            _enumType = enumType;
        }

        private static ValueConverter CreateConverter(Type listType, Type enumType)
        {
            var elementType = listType.GetGenericArguments()[0];
            var isNullable = Nullable.GetUnderlyingType(elementType) != null;

            if (isNullable)
            {
                // List<TEnum?> → List<string?>
                var converterType = typeof(NullableEnumListConverter<>).MakeGenericType(enumType);
                return (ValueConverter)Activator.CreateInstance(converterType)!;
            }
            else
            {
                // List<TEnum> → List<string>
                var converterType = typeof(EnumListConverter<>).MakeGenericType(enumType);
                return (ValueConverter)Activator.CreateInstance(converterType)!;
            }
        }

        private static ValueComparer CreateComparer(Type listType)
        {
            var elementType = listType.GetGenericArguments()[0];
            var comparerType = typeof(ListComparer<>).MakeGenericType(elementType);
            return (ValueComparer)Activator.CreateInstance(comparerType)!;
        }

        public override CoreTypeMapping WithComposedConverter(
            ValueConverter? converter,
            ValueComparer? comparer = null,
            ValueComparer? keyComparer = null,
            CoreTypeMapping? elementMapping = null,
            JsonValueReaderWriter? jsonValueReaderWriter = null)
        {
            return new FirestoreListEnumTypeMapping(
                Parameters.WithComposedConverter(converter, comparer, keyComparer, elementMapping, jsonValueReaderWriter),
                _enumType);
        }

        protected override CoreTypeMapping Clone(CoreTypeMappingParameters parameters)
            => new FirestoreListEnumTypeMapping(parameters, _enumType);
    }

    // Comparer genérico para listas
    internal class ListComparer<T> : ValueComparer<List<T>>
    {
        public ListComparer()
            : base(
                (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v != null ? v.GetHashCode() : 0)),
                c => c.ToList())
        {
        }
    }

    // Converter para List<TEnum> → List<string>
    internal class EnumListConverter<TEnum> : ValueConverter<List<TEnum>, List<string>>
        where TEnum : struct, Enum
    {
        public EnumListConverter()
            : base(
                v => v.Select(e => e.ToString()).ToList(),
                v => v.Select(s => Enum.Parse<TEnum>(s)).ToList()
            )
        {
        }
    }

    // Converter para List<TEnum?> → List<string?>
    internal class NullableEnumListConverter<TEnum> : ValueConverter<List<TEnum?>, List<string?>>
        where TEnum : struct, Enum
    {
        public NullableEnumListConverter()
            : base(
                v => v.Select(e => e.HasValue ? e.Value.ToString() : null).ToList(),
                v => v.Select(s => string.IsNullOrEmpty(s) ? (TEnum?)null : Enum.Parse<TEnum>(s)).ToList()
            )
        {
        }
    }
}