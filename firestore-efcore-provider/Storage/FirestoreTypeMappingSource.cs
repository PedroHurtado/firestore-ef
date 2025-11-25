using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;

namespace Firestore.EntityFrameworkCore.Storage
{
    public class FirestoreTypeMappingSource : TypeMappingSource
    {
        private readonly Dictionary<Type, CoreTypeMapping> _clrTypeMappings;

        public FirestoreTypeMappingSource(TypeMappingSourceDependencies dependencies)
            : base(dependencies)
        {
            _clrTypeMappings = new Dictionary<Type, CoreTypeMapping>
            {
                { typeof(string), new StringTypeMapping("string", null) },                
                { typeof(DateTime), new DateTimeTypeMapping("timestamp") },
                { typeof(byte[]), new ByteArrayTypeMapping("bytes") },

                { typeof(int), new IntTypeMapping("number") },
                { typeof(long), new LongTypeMapping("number") },
                { typeof(double), new DoubleTypeMapping("number") },
                { typeof(float), new FloatTypeMapping("number") },
                { typeof(decimal), new FirestoreDecimalTypeMapping() },               
                { typeof(bool), new BoolTypeMapping("number") },
            };
        }

        protected override CoreTypeMapping? FindMapping(in TypeMappingInfo mappingInfo)
        {
            var clrType = mappingInfo.ClrType;
            if (clrType == null) 
                return base.FindMapping(mappingInfo);

            var underlyingType = Nullable.GetUnderlyingType(clrType);
            if (underlyingType != null)
                clrType = underlyingType;

            if (_clrTypeMappings.TryGetValue(clrType, out var mapping))
                return mapping;

            if (clrType.IsEnum)
                return new FirestoreEnumTypeMapping(clrType);

            // ✅ SOPORTE PARA LISTAS (arrays nativos en Firestore)
            if (clrType.IsGenericType && clrType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = clrType.GetGenericArguments()[0];
                
                // List<decimal> o List<decimal?>
                if (elementType == typeof(decimal) || elementType == typeof(decimal?))
                {
                    return new FirestoreListDecimalTypeMapping(clrType);
                }
                
                // List<TEnum> donde TEnum es enum (o nullable enum)
                var actualEnumType = Nullable.GetUnderlyingType(elementType) ?? elementType;
                if (actualEnumType.IsEnum)
                {
                    return new FirestoreListEnumTypeMapping(clrType, actualEnumType);
                }
                
                // Listas de tipos primitivos (int, string, double, etc.) ya son soportadas por EF Core
                // No necesitan mapping especial, Firestore las maneja como arrays nativos
            }

            return base.FindMapping(mappingInfo);
        }
    }
}
