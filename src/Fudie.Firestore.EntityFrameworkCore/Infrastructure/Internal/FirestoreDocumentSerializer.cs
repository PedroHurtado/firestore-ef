using Microsoft.Extensions.Logging;
using System;
using System.Reflection;
using System.Collections.Generic;

namespace Fudie.Firestore.EntityFrameworkCore.Infrastructure.Internal
{
    public class FirestoreDocumentSerializer : IFirestoreDocumentSerializer
    {
        private readonly ILogger<FirestoreDocumentSerializer> _logger;

        public FirestoreDocumentSerializer(ILogger<FirestoreDocumentSerializer> logger)
        {
            _logger = logger;
        }

        public Dictionary<string, object> Serialize(object entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            var document = new Dictionary<string, object>();
            var properties = entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                var value = property.GetValue(entity);
                if (value != null)
                {
                    var firestoreValue = ConvertToFirestoreValue(value);
                    if (firestoreValue != null)  // Error 2: validar que no sea null antes de agregar
                    {
                        document[property.Name] = firestoreValue;
                    }
                }
            }

            return document;
        }

        public T Deserialize<T>(Dictionary<string, object> document) where T : class, new()
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var entity = new T();
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                if (document.TryGetValue(property.Name, out var value))
                {
                    var convertedValue = ConvertFromFirestoreValue(value, property.PropertyType);
                    property.SetValue(entity, convertedValue);
                }
            }

            return entity;
        }

        private static object? ConvertToFirestoreValue(object value)
        {
            if (value == null) return null;

            var type = value.GetType();

            if (value is DateTime dt)
                return dt.ToUniversalTime();

            if (value is decimal decimalValue)
                return (double)decimalValue;

            if (type.IsEnum)
                return value.ToString();

            return value;
        }

        private object? ConvertFromFirestoreValue(object? value, Type targetType)
        {
            if (value == null)
            {
                // Error 1: manejar el caso cuando Activator.CreateInstance devuelve null
                return targetType.IsValueType 
                    ? Activator.CreateInstance(targetType) ?? throw new InvalidOperationException($"No se pudo crear instancia de {targetType}")
                    : null;
            }

            var underlyingType = Nullable.GetUnderlyingType(targetType);
            if (underlyingType != null)
                return ConvertFromFirestoreValue(value, underlyingType);

            if (targetType.IsAssignableFrom(value.GetType()))
                return value;

            if (targetType == typeof(string))
                return value.ToString() ?? string.Empty;  // Error 3: manejar ToString() que puede ser null

            if (targetType == typeof(DateTime) && value is Google.Cloud.Firestore.Timestamp timestamp)
                return timestamp.ToDateTime();

            if (targetType.IsEnum)
            {
                var stringValue = value.ToString();  // Error 4: validar antes de usar
                if (string.IsNullOrEmpty(stringValue))
                    throw new InvalidOperationException($"No se pudo convertir el valor a enum {targetType}");
                
                return Enum.Parse(targetType, stringValue, ignoreCase: true);
            }

            return Convert.ChangeType(value, targetType);
        }
    }
}