using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Metadata.Conventions;

namespace Firestore.EntityFrameworkCore.Storage
{
    /// <summary>
    /// Deserializa DocumentSnapshot de Firestore a entidades C#.
    /// Es el proceso inverso de FirestoreDocumentSerializer.
    /// Usa IFirestoreValueConverter para centralizar todas las conversiones de tipos.
    /// </summary>
    public class FirestoreDocumentDeserializer : IFirestoreDocumentDeserializer
    {
        private readonly IModel _model;
        private readonly IFirestoreValueConverter _valueConverter;
        private readonly IFirestoreCollectionManager _collectionManager;
        private readonly ILogger<FirestoreDocumentDeserializer> _logger;

        public FirestoreDocumentDeserializer(
            IModel model,
            IFirestoreValueConverter valueConverter,
            IFirestoreCollectionManager collectionManager,
            ILogger<FirestoreDocumentDeserializer> logger)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _valueConverter = valueConverter ?? throw new ArgumentNullException(nameof(valueConverter));
            _collectionManager = collectionManager ?? throw new ArgumentNullException(nameof(collectionManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public T DeserializeEntity<T>(DocumentSnapshot document, IReadOnlyDictionary<string, object> relatedEntities) where T : class
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            if (!document.Exists)
                throw new InvalidOperationException($"Document does not exist: {document.Reference.Path}");

            var entityType = _model.FindEntityType(typeof(T));
            if (entityType == null)
                throw new InvalidOperationException($"Entity type {typeof(T).Name} not found in model");

            var data = document.ToDictionary();

            // Crear entidad usando el constructor apropiado (con entidades relacionadas si las necesita)
            var entity = CreateEntityInstance<T>(document.Id, data, entityType, relatedEntities);

            // Poblar propiedades que no fueron seteadas por el constructor (pasando relatedEntities para ComplexTypes)
            DeserializeIntoEntityWithRelated(document, entity, relatedEntities);

            // Asignar navegaciones desde entidades relacionadas
            AssignNavigations(entity, document, entityType, relatedEntities);

            return entity;
        }

        /// <summary>
        /// Crea una instancia de la entidad usando el constructor apropiado,
        /// incluyendo entidades relacionadas para parámetros de navegación.
        /// </summary>
        private T CreateEntityInstance<T>(
            string documentId,
            IDictionary<string, object> data,
            IEntityType entityType,
            IReadOnlyDictionary<string, object> relatedEntities) where T : class
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
            var constructor = SelectBestConstructor(constructors, data, entityType, relatedEntities);
            if (constructor == null)
            {
                throw new InvalidOperationException(
                    $"No suitable constructor found for type {type.Name}. " +
                    "Ensure it has a parameterless constructor or a constructor with parameters matching property names.");
            }

            // Preparar argumentos para el constructor (incluyendo navegaciones)
            var args = PrepareConstructorArguments(constructor, documentId, data, entityType, relatedEntities);
            return (T)constructor.Invoke(args);
        }

        /// <summary>
        /// Selecciona el mejor constructor basado en los parámetros disponibles,
        /// incluyendo navegaciones de entidades relacionadas.
        /// </summary>
        private static ConstructorInfo? SelectBestConstructor(
            ConstructorInfo[] constructors,
            IDictionary<string, object> data,
            IEntityType entityType,
            IReadOnlyDictionary<string, object> relatedEntities)
        {
            var keyPropertyName = entityType.FindPrimaryKey()?.Properties.FirstOrDefault()?.Name ?? "Id";

            // Obtener nombres de navegaciones disponibles
            var navigationNames = entityType.GetNavigations()
                .Select(n => n.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
                    var matchesNavigation = param.Name != null && navigationNames.Contains(param.Name);

                    if (matchesKey || matchesData || matchesNavigation)
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
        /// Prepara los argumentos para invocar el constructor,
        /// incluyendo entidades relacionadas para parámetros de navegación.
        /// </summary>
        private object?[] PrepareConstructorArguments(
            ConstructorInfo constructor,
            string documentId,
            IDictionary<string, object> data,
            IEntityType entityType,
            IReadOnlyDictionary<string, object> relatedEntities)
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
                    args[i] = ConvertToTargetType(documentId, param.ParameterType);
                    continue;
                }

                // ¿Es una navegación? Buscar en relatedEntities
                var navigation = entityType.GetNavigations()
                    .FirstOrDefault(n => n.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase));

                if (navigation != null)
                {
                    args[i] = FindRelatedEntity(navigation, data, relatedEntities);
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
                        args[i] = ConvertToTargetType(value, param.ParameterType);
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
        /// Busca la entidad relacionada para una navegación en el diccionario de entidades.
        /// Para FK: busca por el path del DocumentReference.
        /// Para SubCollections: busca entidades hijas por path padre.
        /// </summary>
        private static object? FindRelatedEntity(
            IReadOnlyNavigation navigation,
            IDictionary<string, object> data,
            IReadOnlyDictionary<string, object> relatedEntities)
        {
            // Para FK (DocumentReference)
            if (!navigation.IsCollection)
            {
                // Buscar el DocumentReference en los datos
                if (data.TryGetValue(navigation.Name, out var refValue) && refValue is DocumentReference docRef)
                {
                    // Buscar la entidad por su path
                    if (relatedEntities.TryGetValue(docRef.Path, out var entity))
                    {
                        return entity;
                    }
                }
                return null;
            }

            // Para SubCollections: las colecciones se asignan después via AssignNavigations
            // porque necesitamos crear la colección del tipo correcto
            return null;
        }

        /// <summary>
        /// Convierte un valor al tipo de parámetro esperado.
        /// Usa IFirestoreValueConverter para conversiones centralizadas.
        /// </summary>
        private object? ConvertToTargetType(object? value, Type targetType)
        {
            if (value == null)
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            if (targetType.IsAssignableFrom(value.GetType()))
                return value;

            // Usar el converter centralizado para todas las conversiones
            // (Timestamp→DateTime, double→decimal, string→enum, long→int, etc.)
            var converted = _valueConverter.FromFirestore(value, targetType);
            if (converted != null && targetType.IsAssignableFrom(converted.GetType()))
                return converted;

            // Último recurso: Convert.ChangeType
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
        /// Deserializa un DocumentSnapshot en una instancia de entidad existente,
        /// pasando entidades relacionadas para ComplexTypes con References.
        /// </summary>
        private T DeserializeIntoEntityWithRelated<T>(
            DocumentSnapshot document,
            T entity,
            IReadOnlyDictionary<string, object>? relatedEntities) where T : class
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

            // 3. Deserializar Complex Properties (Value Objects) - con relatedEntities si disponible
            DeserializeComplexProperties(entity, data, entityType, relatedEntities);

            // 4. Deserializar referencias a otras entidades
            DeserializeReferences(entity, data, entityType);

            return entity;
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
        /// Aplica conversiones inversas usando IFirestoreValueConverter centralizado.
        /// </summary>
        private object? ApplyReverseConverter(IProperty property, object value)
        {
            // Si el tipo ya es compatible, retornar directamente
            if (property.ClrType.IsAssignableFrom(value.GetType()))
            {
                return value;
            }

            // Usar el converter centralizado para todas las conversiones
            // (Timestamp→DateTime, double→decimal, string→enum, long→int, colecciones, etc.)
            var converted = _valueConverter.FromFirestore(value, property.ClrType);
            if (converted != null && property.ClrType.IsAssignableFrom(converted.GetType()))
            {
                return converted;
            }

            // Usar converter de EF Core si existe y el centralizado no funcionó
            var converter = property.GetValueConverter() ?? property.GetTypeMapping()?.Converter;
            if (converter != null)
            {
                try
                {
                    return converter.ConvertFromProvider(value);
                }
                catch
                {
                    // Ignorar errores del converter de EF Core
                }
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
        /// Deserializa Complex Properties (Value Objects)
        /// </summary>
        private void DeserializeComplexProperties(
            object entity,
            IDictionary<string, object> data,
            ITypeBase typeBase,
            IReadOnlyDictionary<string, object>? relatedEntities = null)
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
                    if (complexProperty.FindAnnotation("Firestore:IsReference")?.Value is true)
                    {
                        _logger.LogTrace("Skipping reference property {PropertyName} - lazy loading not implemented",
                            complexProperty.Name);
                        continue;
                    }

                    // Complex Type simple (map en Firestore)
                    if (value is IDictionary<string, object> map)
                    {
                        var complexObject = DeserializeComplexType(map, complexProperty, relatedEntities);
                        complexProperty.PropertyInfo?.SetValue(entity, complexObject);
                    }
                    // Colección de Complex Types (array de maps)
                    else if (value is IEnumerable<object> enumerable)
                    {
                        var list = DeserializeComplexTypeCollection(enumerable, complexProperty, relatedEntities);
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
            IComplexProperty complexProperty,
            IReadOnlyDictionary<string, object>? relatedEntities = null)
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
            DeserializeComplexProperties(instance, data, complexType, relatedEntities);

            // Deserializar referencias a entidades dentro del ComplexType
            DeserializeNestedEntityReferences(instance, data, complexProperty, relatedEntities);

            return instance;
        }

        /// <summary>
        /// Deserializa referencias a entidades dentro de un ComplexType.
        /// Si relatedEntities tiene la entidad, la asigna directamente.
        /// </summary>
        private void DeserializeNestedEntityReferences(
            object instance,
            IDictionary<string, object> data,
            IComplexProperty complexProperty,
            IReadOnlyDictionary<string, object>? relatedEntities)
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

                // Si tenemos relatedEntities, buscar y asignar la entidad referenciada
                if (relatedEntities != null && relatedEntities.TryGetValue(docRef.Path, out var relatedEntity))
                {
                    var propertyInfo = clrType.GetProperty(refPropertyName);
                    if (propertyInfo != null && propertyInfo.CanWrite)
                    {
                        propertyInfo.SetValue(instance, relatedEntity);
                        _logger.LogTrace(
                            "Assigned related entity to {PropertyName} in ComplexType {ComplexTypeName}",
                            refPropertyName, clrType.Name);
                    }
                }
            }
        }

        /// <summary>
        /// Deserializa una colección de Complex Types
        /// </summary>
        private object DeserializeComplexTypeCollection(
            IEnumerable<object> collection,
            IComplexProperty complexProperty,
            IReadOnlyDictionary<string, object>? relatedEntities = null)
        {
            var complexType = complexProperty.ComplexType;
            var listType = typeof(List<>).MakeGenericType(complexType.ClrType);
            var list = (IList)Activator.CreateInstance(listType)!;

            foreach (var item in collection)
            {
                if (item is IDictionary<string, object> map)
                {
                    var complexObject = DeserializeComplexType(map, complexProperty, relatedEntities);
                    list.Add(complexObject);
                }
            }

            return list;
        }

        /// <summary>
        /// Deserializa referencias a otras entidades.
        /// Extrae el ID del DocumentReference y lo almacena en la FK property.
        /// La carga real se hace vía Include o Lazy Loading.
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

                _logger.LogTrace(
                    "Found reference {NavigationName} pointing to {DocumentPath}",
                    navigation.Name, docRef.Path);

                // Extract the document ID from the DocumentReference path
                var documentId = docRef.Id;

                // Set the FK property value so Include can use it later
                var fkProperty = navigation.ForeignKey.Properties.FirstOrDefault();
                if (fkProperty != null)
                {
                    SetForeignKeyValue(entity, fkProperty, documentId);
                }
            }
        }

        /// <summary>
        /// Sets the value of a FK property on an entity.
        /// Handles both regular properties and shadow properties via backing fields.
        /// </summary>
        private static void SetForeignKeyValue(object entity, IProperty fkProperty, string? value)
        {
            if (value == null)
                return;

            // Try CLR property first
            var propertyInfo = fkProperty.PropertyInfo;
            if (propertyInfo?.SetMethod != null)
            {
                var convertedValue = ConvertFkValue(value, fkProperty.ClrType);
                propertyInfo.SetValue(entity, convertedValue);
                return;
            }

            // Try backing field
            var fieldInfo = fkProperty.FieldInfo;
            if (fieldInfo != null)
            {
                var convertedValue = ConvertFkValue(value, fkProperty.ClrType);
                fieldInfo.SetValue(entity, convertedValue);
            }
        }

        private static object? ConvertFkValue(string value, Type targetType)
        {
            if (targetType == typeof(string))
                return value;
            if (targetType == typeof(int) && int.TryParse(value, out var intValue))
                return intValue;
            if (targetType == typeof(long) && long.TryParse(value, out var longValue))
                return longValue;
            if (targetType == typeof(Guid) && Guid.TryParse(value, out var guidValue))
                return guidValue;

            return value;
        }

        /// <summary>
        /// Encuentra la propiedad Latitude/Latitud en un tipo
        /// </summary>
        private static PropertyInfo FindLatitudeProperty(Type type)
        {
            return type.GetProperty("Latitude", BindingFlags.Public | BindingFlags.Instance)
                ?? type.GetProperty("Latitud", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException(
                    $"Type '{type.Name}' must have a 'Latitude' or 'Latitud' property to use HasGeoPoint()");
        }

        /// <summary>
        /// Encuentra la propiedad Longitude/Longitud en un tipo
        /// </summary>
        private static PropertyInfo FindLongitudeProperty(Type type)
        {
            return type.GetProperty("Longitude", BindingFlags.Public | BindingFlags.Instance)
                ?? type.GetProperty("Longitud", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException(
                    $"Type '{type.Name}' must have a 'Longitude' or 'Longitud' property to use HasGeoPoint()");
        }

        #region Navigation Assignment

        /// <summary>
        /// Asigna navegaciones (FK y SubCollections) a las propiedades de la entidad
        /// usando el diccionario de entidades ya deserializadas.
        /// </summary>
        private void AssignNavigations(
            object entity,
            DocumentSnapshot document,
            IEntityType entityType,
            IReadOnlyDictionary<string, object> relatedEntities)
        {
            if (relatedEntities.Count == 0)
                return;

            var data = document.ToDictionary();
            var documentPath = document.Reference.Path;

            foreach (var navigation in entityType.GetNavigations())
            {
                if (navigation.IsCollection)
                {
                    // SubCollection: buscar entidades hijas cuyo path empiece con documentPath/collectionName
                    AssignSubCollection(entity, navigation, documentPath, relatedEntities);
                }
                else
                {
                    // FK: solo asignar si no fue asignada en el constructor
                    var currentValue = navigation.PropertyInfo?.GetValue(entity);
                    if (currentValue != null)
                        continue;

                    // FK: buscar por DocumentReference
                    AssignReference(entity, navigation, data, relatedEntities);
                }
            }
        }

        /// <summary>
        /// Asigna una navegación de referencia (FK) desde el diccionario de entidades.
        /// </summary>
        private static void AssignReference(
            object entity,
            IReadOnlyNavigation navigation,
            IDictionary<string, object> data,
            IReadOnlyDictionary<string, object> relatedEntities)
        {
            if (navigation.PropertyInfo == null)
                return;

            // Buscar el DocumentReference en los datos
            if (!data.TryGetValue(navigation.Name, out var refValue))
                return;

            if (refValue is not DocumentReference docRef)
                return;

            // Buscar la entidad por su path
            if (relatedEntities.TryGetValue(docRef.Path, out var relatedEntity))
            {
                navigation.PropertyInfo.SetValue(entity, relatedEntity);
            }
        }

        /// <summary>
        /// Asigna una SubCollection desde el diccionario de entidades.
        /// Busca entidades cuyo path coincida con documentPath/collectionName/docId (hijos directos).
        /// </summary>
        private void AssignSubCollection(
            object entity,
            IReadOnlyNavigation navigation,
            string parentDocPath,
            IReadOnlyDictionary<string, object> relatedEntities)
        {
            if (navigation.PropertyInfo == null)
                return;

            // Obtener el nombre de la subcolección
            var collectionName = _collectionManager.GetCollectionName(navigation.TargetEntityType.ClrType);
            var subCollectionPrefix = $"{parentDocPath}/{collectionName}/";

            // Calcular la profundidad esperada de los hijos directos
            var parentDepth = parentDocPath.Count(c => c == '/') + 1;
            var expectedChildDepth = parentDepth + 2;

            // Buscar solo entidades hijas directas (no nietas)
            var childEntities = relatedEntities
                .Where(kv =>
                {
                    if (!kv.Key.StartsWith(subCollectionPrefix, StringComparison.Ordinal))
                        return false;

                    var keyDepth = kv.Key.Count(c => c == '/') + 1;
                    return keyDepth == expectedChildDepth;
                })
                .Select(kv => kv.Value)
                .ToList();

            if (childEntities.Count == 0)
                return;

            // Crear la colección del tipo apropiado
            var collection = CreateCollectionInstance(navigation);

            foreach (var child in childEntities)
            {
                AddToCollection(collection, child);
            }

            navigation.PropertyInfo.SetValue(entity, collection);
        }

        /// <summary>
        /// Crea una instancia de colección del tipo apropiado.
        /// </summary>
        private static object CreateCollectionInstance(IReadOnlyNavigation navigation)
        {
            var elementType = navigation.TargetEntityType.ClrType;
            var propertyType = navigation.PropertyInfo?.PropertyType
                ?? throw new InvalidOperationException($"Navigation {navigation.Name} has no PropertyInfo");

            // Si es HashSet<T>
            if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(HashSet<>))
            {
                var hashSetType = typeof(HashSet<>).MakeGenericType(elementType);
                return Activator.CreateInstance(hashSetType)!;
            }

            // Si es ICollection<T>, IEnumerable<T>, IList<T>, o List<T> -> usar List<T>
            if (propertyType.IsInterface ||
                (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(List<>)))
            {
                var listType = typeof(List<>).MakeGenericType(elementType);
                return Activator.CreateInstance(listType)!;
            }

            // Intentar crear el tipo concreto directamente
            try
            {
                return Activator.CreateInstance(propertyType)!;
            }
            catch
            {
                // Fallback a List<T>
                var listType = typeof(List<>).MakeGenericType(elementType);
                return Activator.CreateInstance(listType)!;
            }
        }

        /// <summary>
        /// Agrega un elemento a una colección usando el método Add apropiado.
        /// </summary>
        private static void AddToCollection(object collection, object element)
        {
            var collectionType = collection.GetType();
            var addMethod = collectionType.GetMethod("Add");

            if (addMethod != null)
            {
                addMethod.Invoke(collection, new[] { element });
            }
            else if (collection is IList list)
            {
                list.Add(element);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Collection type {collectionType.Name} does not have an Add method");
            }
        }

        #endregion
    }
}