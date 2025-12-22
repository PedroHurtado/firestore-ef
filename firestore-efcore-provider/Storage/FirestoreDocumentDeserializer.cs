using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Firestore.EntityFrameworkCore.Infrastructure.Internal;
using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Metadata.Conventions;

namespace Firestore.EntityFrameworkCore.Storage
{
    /// <summary>
    /// Deserializa DocumentSnapshot de Firestore a entidades C#.
    /// Es el proceso inverso de FirestoreDocumentSerializer.
    /// </summary>
    public class FirestoreDocumentDeserializer : IFirestoreDocumentDeserializer
    {
        private readonly IModel _model;
        private readonly ITypeMappingSource _typeMappingSource;
        private readonly IFirestoreCollectionManager _collectionManager;
        private readonly ILogger<FirestoreDocumentDeserializer> _logger;

        public FirestoreDocumentDeserializer(
            IModel model,
            ITypeMappingSource typeMappingSource,
            IFirestoreCollectionManager collectionManager,
            ILogger<FirestoreDocumentDeserializer> logger)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _typeMappingSource = typeMappingSource ?? throw new ArgumentNullException(nameof(typeMappingSource));
            _collectionManager = collectionManager ?? throw new ArgumentNullException(nameof(collectionManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Deserializa un DocumentSnapshot a una entidad del tipo especificado.
        /// Soporta:
        /// - Constructor sin parámetros (new() + property setters)
        /// - Constructor con parámetros que coinciden con propiedades
        /// - Constructor parcial (algunos parámetros + property setters para el resto)
        /// - Records (constructor con todos los parámetros)
        /// </summary>
        public T DeserializeEntity<T>(DocumentSnapshot document) where T : class
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            if (!document.Exists)
                throw new InvalidOperationException($"Document does not exist: {document.Reference.Path}");

            var entityType = _model.FindEntityType(typeof(T));
            if (entityType == null)
                throw new InvalidOperationException($"Entity type {typeof(T).Name} not found in model");

            var data = document.ToDictionary();

            // Crear entidad usando el constructor apropiado
            var entity = CreateEntityInstance<T>(document.Id, data, entityType);

            // Poblar propiedades que no fueron seteadas por el constructor
            return DeserializeIntoEntity(document, entity);
        }

        /// <summary>
        /// Crea una instancia de la entidad usando el constructor apropiado.
        /// Prioridad:
        /// 1. Constructor sin parámetros (si existe)
        /// 2. Constructor con parámetros que coinciden con propiedades del documento
        /// </summary>
        private T CreateEntityInstance<T>(string documentId, IDictionary<string, object> data, IEntityType entityType) where T : class
        {
            var type = typeof(T);
            var constructors = type.GetConstructors();

            // Primero intentar constructor sin parámetros
            var parameterlessConstructor = constructors.FirstOrDefault(c => c.GetParameters().Length == 0);
            if (parameterlessConstructor != null)
            {
                return (T)parameterlessConstructor.Invoke(Array.Empty<object>());
            }

            // Si no hay constructor sin parámetros, buscar el mejor constructor con parámetros
            var constructor = SelectBestConstructor(constructors, data, entityType);
            if (constructor == null)
            {
                throw new InvalidOperationException(
                    $"No suitable constructor found for type {type.Name}. " +
                    "Ensure it has a parameterless constructor or a constructor with parameters matching property names.");
            }

            // Preparar argumentos para el constructor
            var args = PrepareConstructorArguments(constructor, documentId, data, entityType);
            return (T)constructor.Invoke(args);
        }

        /// <summary>
        /// Selecciona el mejor constructor basado en los parámetros disponibles.
        /// Busca constructores cuyos parámetros coincidan con propiedades de la entidad.
        /// </summary>
        private static ConstructorInfo? SelectBestConstructor(
            ConstructorInfo[] constructors,
            IDictionary<string, object> data,
            IEntityType entityType)
        {
            var keyPropertyName = entityType.FindPrimaryKey()?.Properties.FirstOrDefault()?.Name ?? "Id";

            // Filtrar constructores cuyos parámetros coincidan con propiedades
            var validConstructors = new List<(ConstructorInfo constructor, int matchCount)>();

            foreach (var constructor in constructors)
            {
                var parameters = constructor.GetParameters();
                var allMatch = true;
                var matchCount = 0;

                foreach (var param in parameters)
                {
                    // Verificar si el parámetro coincide con una propiedad (case-insensitive)
                    var matchesKey = param.Name?.Equals(keyPropertyName, StringComparison.OrdinalIgnoreCase) ?? false;
                    var matchesData = data.Keys.Any(k => k.Equals(param.Name, StringComparison.OrdinalIgnoreCase));

                    if (matchesKey || matchesData)
                    {
                        matchCount++;
                    }
                    else
                    {
                        allMatch = false;
                        break;
                    }
                }

                if (allMatch)
                {
                    validConstructors.Add((constructor, matchCount));
                }
            }

            // Retornar el constructor con más parámetros que coincidan
            return validConstructors
                .OrderByDescending(x => x.matchCount)
                .Select(x => x.constructor)
                .FirstOrDefault();
        }

        /// <summary>
        /// Prepara los argumentos para invocar el constructor.
        /// </summary>
        private object?[] PrepareConstructorArguments(
            ConstructorInfo constructor,
            string documentId,
            IDictionary<string, object> data,
            IEntityType entityType)
        {
            var parameters = constructor.GetParameters();
            var args = new object?[parameters.Length];
            var keyPropertyName = entityType.FindPrimaryKey()?.Properties.FirstOrDefault()?.Name ?? "Id";

            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                var paramName = param.Name;

                // ¿Es la clave primaria (Id)?
                if (paramName?.Equals(keyPropertyName, StringComparison.OrdinalIgnoreCase) ?? false)
                {
                    args[i] = ConvertToParameterType(documentId, param.ParameterType);
                    continue;
                }

                // Buscar en el diccionario de datos (case-insensitive)
                var dataKey = data.Keys.FirstOrDefault(k => k.Equals(paramName, StringComparison.OrdinalIgnoreCase));
                if (dataKey != null && data.TryGetValue(dataKey, out var value))
                {
                    // Buscar la propiedad correspondiente para aplicar conversiones
                    var property = entityType.GetProperties()
                        .FirstOrDefault(p => p.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase));

                    if (property != null && value != null)
                    {
                        args[i] = ApplyReverseConverter(property, value);
                    }
                    else
                    {
                        args[i] = ConvertToParameterType(value, param.ParameterType);
                    }
                }
                else
                {
                    // Usar valor por defecto del tipo
                    args[i] = param.ParameterType.IsValueType
                        ? Activator.CreateInstance(param.ParameterType)
                        : null;
                }
            }

            return args;
        }

        /// <summary>
        /// Convierte un valor al tipo de parámetro esperado.
        /// </summary>
        private static object? ConvertToParameterType(object? value, Type targetType)
        {
            if (value == null)
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            if (targetType.IsAssignableFrom(value.GetType()))
                return value;

            // Conversión double → decimal
            if (value is double d && targetType == typeof(decimal))
                return (decimal)d;

            // Conversión Timestamp → DateTime
            if (value is Google.Cloud.Firestore.Timestamp timestamp && targetType == typeof(DateTime))
                return timestamp.ToDateTime();

            // Conversión string → enum
            if (value is string s && targetType.IsEnum)
                return Enum.Parse(targetType, s, ignoreCase: true);

            try
            {
                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }
        }

        /// <summary>
        /// Deserializa un DocumentSnapshot en una instancia de entidad existente.
        /// Útil para poblar proxies de lazy loading.
        /// </summary>
        public T DeserializeIntoEntity<T>(DocumentSnapshot document, T entity) where T : class
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            if (!document.Exists)
                throw new InvalidOperationException($"Document does not exist: {document.Reference.Path}");

            var entityType = _model.FindEntityType(typeof(T));
            if (entityType == null)
                throw new InvalidOperationException($"Entity type {typeof(T).Name} not found in model");

            _logger.LogTrace("Deserializing document {DocumentId} to entity {EntityType}",
                document.Id, typeof(T).Name);

            var data = document.ToDictionary();

            // 1. Deserializar clave primaria (ID del documento)
            DeserializeKey(entity, document.Id, entityType);

            // 2. Deserializar propiedades simples
            DeserializeProperties(entity, data, entityType);

            // 3. Deserializar Complex Properties (Value Objects)
            DeserializeComplexProperties(entity, data, entityType);

            // 4. Deserializar referencias a otras entidades
            // Por ahora solo registrar que existen, carga lazy/eager requiere queries adicionales
            DeserializeReferences(entity, data, entityType);

            return entity;
        }

        /// <summary>
        /// Deserializa múltiples documentos
        /// </summary>
        public List<T> DeserializeEntities<T>(IEnumerable<DocumentSnapshot> documents) where T : class
        {
            var results = new List<T>();
            foreach (var document in documents)
            {
                if (document.Exists)
                {
                    results.Add(DeserializeEntity<T>(document));
                }
            }
            return results;
        }

        /// <summary>
        /// Deserializa la clave primaria desde el ID del documento
        /// </summary>
        private void DeserializeKey(object entity, string documentId, IEntityType entityType)
        {
            var keyProperty = entityType.FindPrimaryKey()?.Properties.FirstOrDefault();
            if (keyProperty == null)
            {
                _logger.LogWarning("Entity type {EntityType} has no primary key", entityType.ClrType.Name);
                return;
            }

            // Convertir el documentId (string) al tipo de la clave
            object keyValue = keyProperty.ClrType == typeof(string)
                ? documentId
                : Convert.ChangeType(documentId, keyProperty.ClrType);

            keyProperty.PropertyInfo?.SetValue(entity, keyValue);
        }

        /// <summary>
        /// Deserializa propiedades simples (no complejas, no navegaciones)
        /// </summary>
        private void DeserializeProperties(
            object entity,
            IDictionary<string, object> data,
            ITypeBase typeBase)
        {
            foreach (var property in typeBase.GetProperties())
            {
                // Saltar clave primaria (ya deserializada) y foreign keys
                if (property.IsPrimaryKey() || property.IsForeignKey())
                    continue;

                // Saltar propiedades de metadata de Firestore
                if (property.Name == "_createdAt" || property.Name == "_updatedAt")
                    continue;

                if (!data.TryGetValue(property.Name, out var value))
                    continue;

                if (value == null)
                    continue;

                try
                {
                    // Aplicar conversiones inversas (double → decimal, string → enum, etc.)
                    var convertedValue = ApplyReverseConverter(property, value);
                    property.PropertyInfo?.SetValue(entity, convertedValue);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to deserialize property {PropertyName} with value {Value}",
                        property.Name, value);
                }
            }
        }

        /// <summary>
        /// Aplica conversiones inversas usando los converters de EF Core
        /// </summary>
        private object? ApplyReverseConverter(IProperty property, object value)
        {
            // Conversiones especiales para tipos de Firestore
            if (value is Google.Cloud.Firestore.Timestamp timestamp)
            {
                return timestamp.ToDateTime();
            }

            // Conversión manual: double → decimal
            if (value is double d && property.ClrType == typeof(decimal))
            {
                return (decimal)d;
            }

            // Conversión manual: string → enum
            if (value is string s && property.ClrType.IsEnum)
            {
                return Enum.Parse(property.ClrType, s, ignoreCase: true);
            }

            // Conversiones para colecciones
            if (value is IEnumerable enumerable &&
                value is not string &&
                value is not byte[])
            {
                return ConvertCollection(property, enumerable);
            }

            // Usar converter de EF Core si existe
            var converter = property.GetValueConverter() ?? property.GetTypeMapping()?.Converter;
            if (converter != null)
            {
                return converter.ConvertFromProvider(value);
            }

            // Si el tipo ya es compatible, retornar directamente
            if (property.ClrType.IsAssignableFrom(value.GetType()))
            {
                return value;
            }

            // Último recurso: Convert.ChangeType
            try
            {
                return Convert.ChangeType(value, property.ClrType);
            }
            catch
            {
                _logger.LogWarning(
                    "Could not convert value {Value} of type {ValueType} to property type {PropertyType}",
                    value, value.GetType().Name, property.ClrType.Name);
                return null;
            }
        }

        /// <summary>
        /// Convierte colecciones de Firestore a colecciones de C#
        /// </summary>
        private object? ConvertCollection(IProperty property, IEnumerable collection)
        {
            var elementType = property.ClrType.GetGenericArguments().FirstOrDefault()
                              ?? property.ClrType.GetElementType();

            if (elementType == null)
                return collection;

            // Conversión: double[] → List<decimal>
            if (elementType == typeof(decimal))
            {
                var decimals = collection.Cast<object>()
                    .Select(item => item is double d ? (decimal)d : Convert.ToDecimal(item))
                    .ToList();
                return decimals;
            }

            // Conversión: string[] → List<enum>
            if (elementType.IsEnum)
            {
                var enums = collection.Cast<object>()
                    .Select(item => Enum.Parse(elementType, item.ToString()!, ignoreCase: true))
                    .ToList();

                // Crear List<TEnum> usando reflexión
                var listType = typeof(List<>).MakeGenericType(elementType);
                var list = (IList)Activator.CreateInstance(listType)!;
                foreach (var item in enums)
                {
                    list.Add(item);
                }
                return list;
            }

            // Conversión: object[] → List<int> (Firestore devuelve long)
            if (elementType == typeof(int))
            {
                var ints = collection.Cast<object>()
                    .Select(item => Convert.ToInt32(item))
                    .ToList();
                return ints;
            }

            // Conversión: object[] → List<long>
            if (elementType == typeof(long))
            {
                var longs = collection.Cast<object>()
                    .Select(item => Convert.ToInt64(item))
                    .ToList();
                return longs;
            }

            // Conversión: object[] → List<string>
            if (elementType == typeof(string))
            {
                var strings = collection.Cast<object>()
                    .Select(item => item?.ToString() ?? string.Empty)
                    .ToList();
                return strings;
            }

            // Conversión: object[] → List<double>
            if (elementType == typeof(double))
            {
                var doubles = collection.Cast<object>()
                    .Select(item => Convert.ToDouble(item))
                    .ToList();
                return doubles;
            }

            return collection;
        }

        /// <summary>
        /// Deserializa Complex Properties (Value Objects)
        /// </summary>
        private void DeserializeComplexProperties(
            object entity,
            IDictionary<string, object> data,
            ITypeBase typeBase)
        {
            foreach (var complexProperty in typeBase.GetComplexProperties())
            {
                if (!data.TryGetValue(complexProperty.Name, out var value))
                    continue;

                if (value == null)
                    continue;

                try
                {
                    // Verificar si es GeoPoint
                    if (complexProperty.FindAnnotation("Firestore:IsGeoPoint")?.Value is true)
                    {
                        var geoPointObject = DeserializeGeoPoint(value, complexProperty.ComplexType);
                        complexProperty.PropertyInfo?.SetValue(entity, geoPointObject);
                        continue;
                    }

                    // Verificar si es Reference (marcar para carga lazy)
                    // Por ahora omitir, requiere queries adicionales
                    if (complexProperty.FindAnnotation("Firestore:IsReference")?.Value is true)
                    {
                        _logger.LogTrace("Skipping reference property {PropertyName} - lazy loading not implemented",
                            complexProperty.Name);
                        continue;
                    }

                    // Complex Type simple (map en Firestore)
                    if (value is IDictionary<string, object> map)
                    {
                        var complexObject = DeserializeComplexType(map, complexProperty);
                        complexProperty.PropertyInfo?.SetValue(entity, complexObject);
                    }
                    // Colección de Complex Types (array de maps)
                    else if (value is IEnumerable<object> enumerable)
                    {
                        var list = DeserializeComplexTypeCollection(enumerable, complexProperty);
                        complexProperty.PropertyInfo?.SetValue(entity, list);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to deserialize complex property {PropertyName}",
                        complexProperty.Name);
                }
            }
        }

        /// <summary>
        /// Deserializa un GeoPoint de Firestore a un objeto C#
        /// </summary>
        private object DeserializeGeoPoint(object value, IComplexType complexType)
        {
            if (value is not Google.Cloud.Firestore.GeoPoint geoPoint)
            {
                throw new InvalidOperationException(
                    $"Expected GeoPoint but got {value.GetType().Name}");
            }

            var clrType = complexType.ClrType;

            // Buscar propiedades Latitude/Longitude
            var latProp = FindLatitudeProperty(clrType);
            var lonProp = FindLongitudeProperty(clrType);

            // Intentar crear usando constructor con parámetros (para records)
            var constructor = clrType.GetConstructors()
                .FirstOrDefault(c =>
                {
                    var parameters = c.GetParameters();
                    return parameters.Length == 2 &&
                           parameters.All(p => p.ParameterType == typeof(double));
                });

            if (constructor != null)
            {
                // Determinar el orden de los parámetros según el nombre
                var parameters = constructor.GetParameters();
                var args = new object[2];

                for (int i = 0; i < parameters.Length; i++)
                {
                    var paramName = parameters[i].Name;
                    if (paramName != null &&
                        (paramName.Equals("latitude", StringComparison.OrdinalIgnoreCase) ||
                         paramName.Equals("lat", StringComparison.OrdinalIgnoreCase) ||
                         paramName.Equals("latitud", StringComparison.OrdinalIgnoreCase)))
                    {
                        args[i] = geoPoint.Latitude;
                    }
                    else
                    {
                        args[i] = geoPoint.Longitude;
                    }
                }

                return constructor.Invoke(args);
            }

            // Fallback: usar constructor sin parámetros y setear propiedades
            var instance = Activator.CreateInstance(clrType);
            if (instance == null)
            {
                throw new InvalidOperationException(
                    $"Could not create instance of {clrType.Name}. " +
                    "Ensure it has a parameterless constructor or a constructor with (double, double) parameters.");
            }

            latProp.SetValue(instance, geoPoint.Latitude);
            lonProp.SetValue(instance, geoPoint.Longitude);

            return instance;
        }

        /// <summary>
        /// Deserializa un Complex Type (map de Firestore → objeto C#)
        /// </summary>
        private object DeserializeComplexType(
            IDictionary<string, object> data,
            IComplexProperty complexProperty)
        {
            var complexType = complexProperty.ComplexType;
            var instance = Activator.CreateInstance(complexType.ClrType);
            if (instance == null)
            {
                throw new InvalidOperationException(
                    $"Could not create instance of {complexType.ClrType.Name}");
            }

            // Deserializar propiedades simples del complex type
            DeserializeProperties(instance, data, complexType);

            // Deserializar Complex Properties anidados (recursivo)
            DeserializeComplexProperties(instance, data, complexType);

            // Deserializar referencias a entidades dentro del ComplexType
            DeserializeNestedEntityReferences(instance, data, complexProperty);

            return instance;
        }

        /// <summary>
        /// Deserializa referencias a entidades dentro de un ComplexType.
        /// Por ahora solo loggea. Requiere Include o Lazy Loading para cargar.
        /// </summary>
        private void DeserializeNestedEntityReferences(
            object instance,
            IDictionary<string, object> data,
            IComplexProperty complexProperty)
        {
            // Obtener lista de propiedades marcadas como Reference
            var nestedRefs = complexProperty.FindAnnotation("Firestore:NestedReferences")?.Value as List<string>;
            if (nestedRefs == null || nestedRefs.Count == 0)
                return;

            var clrType = complexProperty.ComplexType.ClrType;

            foreach (var refPropertyName in nestedRefs)
            {
                if (!data.TryGetValue(refPropertyName, out var value))
                    continue;

                if (value is not DocumentReference docRef)
                    continue;

                _logger.LogTrace(
                    "Found nested reference {PropertyName} in ComplexType {ComplexTypeName} pointing to {DocumentPath}",
                    refPropertyName, clrType.Name, docRef.Path);

                // TODO: Implementar con Include o Lazy Loading
            }
        }

        /// <summary>
        /// Deserializa una colección de Complex Types
        /// </summary>
        private object DeserializeComplexTypeCollection(
            IEnumerable<object> collection,
            IComplexProperty complexProperty)
        {
            var complexType = complexProperty.ComplexType;
            var listType = typeof(List<>).MakeGenericType(complexType.ClrType);
            var list = (IList)Activator.CreateInstance(listType)!;

            foreach (var item in collection)
            {
                if (item is IDictionary<string, object> map)
                {
                    var complexObject = DeserializeComplexType(map, complexProperty);
                    list.Add(complexObject);
                }
            }

            return list;
        }

        /// <summary>
        /// Deserializa referencias a otras entidades.
        /// Por ahora solo registra que existen, la carga lazy/eager requiere queries adicionales.
        /// </summary>
        private void DeserializeReferences(
            object entity,
            IDictionary<string, object> data,
            IEntityType entityType)
        {
            foreach (var navigation in entityType.GetNavigations())
            {
                // Saltar colecciones (relaciones 1:N)
                if (navigation.IsCollection)
                    continue;

                // Saltar subcollections
                if (navigation.IsSubCollection())
                    continue;

                if (!data.TryGetValue(navigation.Name, out var value))
                    continue;

                if (value is not DocumentReference docRef)
                    continue;

                // TODO: Implementar carga de referencias
                // Por ahora solo loggear que existe la referencia
                _logger.LogTrace(
                    "Found reference {NavigationName} pointing to {DocumentPath}",
                    navigation.Name, docRef.Path);

                // Opción futura: Marcar para lazy loading o cargar con Include
            }
        }

        /// <summary>
        /// Encuentra la propiedad Latitude/Latitud en un tipo
        /// </summary>
        private PropertyInfo FindLatitudeProperty(Type type)
        {
            return type.GetProperty("Latitude", BindingFlags.Public | BindingFlags.Instance)
                ?? type.GetProperty("Latitud", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException(
                    $"Type '{type.Name}' must have a 'Latitude' or 'Latitud' property to use HasGeoPoint()");
        }

        /// <summary>
        /// Encuentra la propiedad Longitude/Longitud en un tipo
        /// </summary>
        private PropertyInfo FindLongitudeProperty(Type type)
        {
            return type.GetProperty("Longitude", BindingFlags.Public | BindingFlags.Instance)
                ?? type.GetProperty("Longitud", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException(
                    $"Type '{type.Name}' must have a 'Longitude' or 'Longitud' property to use HasGeoPoint()");
        }
    }
}
