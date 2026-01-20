using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Fudie.Firestore.EntityFrameworkCore.Infrastructure;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

namespace Fudie.Firestore.EntityFrameworkCore.Storage
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

            // Crear entidad usando el constructor apropiado
            var entity = CreateEntityInstance<T>(document.Id, data, entityType);

            // Poblar propiedades que no fueron seteadas por el constructor (pasando relatedEntities para ComplexTypes)
            DeserializeIntoEntityWithRelated(document, entity, relatedEntities);

            // Asignar navegaciones desde entidades relacionadas
            AssignNavigations(entity, document, entityType, relatedEntities);

            return entity;
        }

        /// <summary>
        /// Crea una instancia de la entidad usando CreateInstanceFromData.
        /// Para entidades, añade el documentId al diccionario de datos.
        /// </summary>
        private T CreateEntityInstance<T>(
            string documentId,
            IDictionary<string, object> data,
            IEntityType entityType) where T : class
        {
            // Añadir el ID del documento al diccionario para que el constructor lo reciba
            var keyPropertyName = entityType.FindPrimaryKey()?.Properties.FirstOrDefault()?.Name ?? "Id";
            var dataWithId = new Dictionary<string, object>(data, StringComparer.OrdinalIgnoreCase);

            // Convertir el documentId al tipo de la clave
            var keyProperty = entityType.FindPrimaryKey()?.Properties.FirstOrDefault();
            if (keyProperty != null)
            {
                var convertedId = _valueConverter.FromFirestore(documentId, keyProperty.ClrType);
                if (convertedId != null)
                {
                    dataWithId[keyPropertyName] = convertedId;
                }
            }

            var instance = CreateInstanceFromData(typeof(T), dataWithId);
            if (instance == null)
            {
                throw new InvalidOperationException(
                    $"No suitable constructor found for type {typeof(T).Name}. " +
                    "Ensure it has a parameterless constructor or a constructor with parameters matching property names.");
            }

            return (T)instance;
        }

        /// <summary>
        /// Convierte un valor al tipo de parámetro esperado.
        /// Usa IFirestoreValueConverter para conversiones centralizadas.
        /// </summary>
        private object? ConvertToTargetType(object? value, Type targetType)
        {
            if (value == null)
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            // When targetType is object, return value as-is (preserve original type)
            if (targetType == typeof(object))
                return value;

            // Always use the converter for collections to ensure proper element conversion
            var isCollection = value is IEnumerable && value is not string && value is not byte[];

            // If type is already compatible and NOT a collection, return directly
            if (!isCollection && targetType.IsAssignableFrom(value.GetType()))
                return value;

            // Handle collection of embedded objects (List<object> of maps -> HashSet<T>/List<T>)
            if (isCollection && targetType.IsGenericType)
            {
                var genericDef = targetType.GetGenericTypeDefinition();
                if (genericDef == typeof(HashSet<>) || genericDef == typeof(List<>) ||
                    genericDef == typeof(IReadOnlyCollection<>) || genericDef == typeof(ICollection<>) ||
                    genericDef == typeof(IEnumerable<>))
                {
                    var elementType = targetType.GetGenericArguments()[0];

                    // Check if it's a collection of maps (embedded objects)
                    if (value is IEnumerable<object> enumerable)
                    {
                        var items = enumerable.ToList();
                        if (items.Count > 0 && items[0] is IDictionary<string, object>)
                        {
                            // Deserialize each map to the element type
                            var collection = CreateTypedCollection(elementType, targetType);
                            foreach (var item in items)
                            {
                                if (item is IDictionary<string, object> map)
                                {
                                    var element = CreateInstanceFromData(elementType, map);
                                    if (element != null)
                                    {
                                        AddToCollection(collection, element);
                                    }
                                }
                            }
                            return collection;
                        }
                    }
                }
            }

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
        /// Determina si un tipo es nullable (Nullable&lt;T&gt; o reference type).
        /// </summary>
        private static bool IsNullableType(Type type)
        {
            // Nullable<T> (e.g., int?, decimal?)
            if (Nullable.GetUnderlyingType(type) != null)
                return true;

            // Reference types are inherently nullable
            return !type.IsValueType;
        }

        /// <summary>
        /// Crea una instancia de cualquier tipo (entidad, ComplexType, record, clase) usando el mejor constructor disponible.
        /// Método unificado que reemplaza CreateEntityInstance, CreateComplexTypeInstance, CreateInstanceWithBestConstructor y CreateGeoPointInstance.
        /// </summary>
        /// <param name="type">El tipo a instanciar</param>
        /// <param name="data">Diccionario con los datos para el constructor</param>
        /// <returns>Instancia del tipo o null si no se pudo crear</returns>
        private object? CreateInstanceFromData(Type type, IDictionary<string, object> data)
        {
            // Get all constructors including non-public (protected/private) for DDD Value Objects
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            // FIRST: Try constructors WITH parameters (ordered by parameter count descending)
            // This is essential for records where properties are init-only and cannot be set after construction
            foreach (var constructor in constructors
                .Where(c => c.GetParameters().Length > 0)
                .OrderByDescending(c => c.GetParameters().Length))
            {
                var parameters = constructor.GetParameters();
                var args = new object?[parameters.Length];
                var allMatched = true;
                var matchedFromData = 0;

                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];
                    var dataKey = data.Keys.FirstOrDefault(k => k.Equals(param.Name, StringComparison.OrdinalIgnoreCase));

                    if (dataKey != null && data.TryGetValue(dataKey, out var value))
                    {
                        args[i] = ConvertToTargetType(value, param.ParameterType);
                        matchedFromData++;
                    }
                    else if (param.HasDefaultValue)
                    {
                        args[i] = param.DefaultValue;
                    }
                    else if (IsNullableType(param.ParameterType))
                    {
                        // For nullable parameters without data, pass null
                        args[i] = null;
                    }
                    else
                    {
                        allMatched = false;
                        break;
                    }
                }

                // Use this constructor if all parameters matched and at least one came from data
                if (allMatched && matchedFromData > 0)
                {
                    return constructor.Invoke(args);
                }
            }

            // FALLBACK: Use parameterless constructor only if no parameterized constructor worked
            var parameterlessConstructor = constructors.FirstOrDefault(c => c.GetParameters().Length == 0);
            if (parameterlessConstructor != null)
            {
                var instance = parameterlessConstructor.Invoke(Array.Empty<object>());

                // Populate properties from data dictionary
                PopulatePropertiesFromData(instance, type, data);

                return instance;
            }

            return null;
        }

        /// <summary>
        /// Populates writable properties of an instance from a data dictionary.
        /// Used when instance was created with parameterless constructor.
        /// </summary>
        private void PopulatePropertiesFromData(object instance, Type type, IDictionary<string, object> data)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanWrite)
                    continue;

                var dataKey = data.Keys.FirstOrDefault(k => k.Equals(property.Name, StringComparison.OrdinalIgnoreCase));
                if (dataKey != null && data.TryGetValue(dataKey, out var value))
                {
                    var convertedValue = ConvertToTargetType(value, property.PropertyType);
                    try
                    {
                        property.SetValue(instance, convertedValue);
                    }
                    catch
                    {
                        // Skip properties that can't be set
                    }
                }
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

            // 5. Deserializar propiedades ArrayOf (arrays de embedded, geopoints, references)
            DeserializeArrayOfProperties(entity, data, entityType, relatedEntities);

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

            // Convertir el documentId (string) al tipo de la clave usando el ValueConverter centralizado
            var keyValue = _valueConverter.FromFirestore(documentId, keyProperty.ClrType);
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
            // Always use the converter for collections to ensure proper element conversion
            // (e.g., List<object> elements need to preserve their actual types)
            var isCollection = value is IEnumerable && value is not string && value is not byte[];

            // Si el tipo ya es compatible y NO es una colección, retornar directamente
            if (!isCollection && property.ClrType.IsAssignableFrom(value.GetType()))
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
        /// Deserializa un GeoPoint de Firestore a un objeto C#.
        /// Convierte a diccionario y usa CreateInstanceFromData.
        /// </summary>
        private object DeserializeGeoPoint(object value, IComplexType complexType)
        {
            if (value is not Google.Cloud.Firestore.GeoPoint geoPoint)
            {
                throw new InvalidOperationException(
                    $"Expected GeoPoint but got {value.GetType().Name}");
            }

            var instance = CreateGeoPointInstance(complexType.ClrType, geoPoint);
            if (instance == null)
            {
                throw new InvalidOperationException(
                    $"Could not create instance of {complexType.ClrType.Name}. " +
                    "Ensure it has a parameterless constructor or a constructor with parameters matching Latitude/Longitude.");
            }

            return instance;
        }

        /// <summary>
        /// Deserializa un Complex Type (map de Firestore → objeto C#)
        /// Soporta Value Objects con constructor protected y parámetros (DDD pattern).
        /// </summary>
        private object DeserializeComplexType(
            IDictionary<string, object> data,
            IComplexProperty complexProperty,
            IReadOnlyDictionary<string, object>? relatedEntities = null)
        {
            var complexType = complexProperty.ComplexType;
            var clrType = complexType.ClrType;

            // Create instance using unified method (supports protected constructors)
            var instance = CreateInstanceFromData(clrType, data);
            if (instance == null)
            {
                throw new InvalidOperationException(
                    $"Could not create instance of {clrType.Name}. " +
                    "Ensure it has a parameterless constructor or a constructor with parameters matching property names.");
            }

            // For parameterless constructor, populate properties
            // For constructor with parameters, properties are already set - but we may need to handle nested complex types
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

#region ArrayOf Deserialization

        /// <summary>
        /// Deserializa propiedades marcadas como ArrayOf (Embedded, GeoPoint, Reference).
        /// </summary>
        private void DeserializeArrayOfProperties(
            object entity,
            IDictionary<string, object> data,
            IEntityType entityType,
            IReadOnlyDictionary<string, object>? relatedEntities)
        {
            var clrType = entityType.ClrType;

            foreach (var propertyInfo in clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var propertyName = propertyInfo.Name;

                // Verificar si está configurada como ArrayOf
                var arrayType = entityType.GetArrayOfType(propertyName);
                if (arrayType == null)
                    continue;

                // Obtener el valor del diccionario de datos
                if (!data.TryGetValue(propertyName, out var value) || value == null)
                    continue;

                // Verificar que es un array
                if (value is not IEnumerable<object> arrayValue)
                    continue;

                try
                {
                    var elementType = entityType.GetArrayOfElementClrType(propertyName);
                    if (elementType == null)
                    {
                        // Intentar inferir del tipo de propiedad
                        var propType = propertyInfo.PropertyType;
                        if (propType.IsGenericType)
                        {
                            elementType = propType.GetGenericArguments().FirstOrDefault();
                        }
                    }

                    if (elementType == null)
                    {
                        _logger.LogWarning("Could not determine element type for ArrayOf property {PropertyName}", propertyName);
                        continue;
                    }

                    object? deserializedCollection = arrayType switch
                    {
                        ArrayOfAnnotations.ArrayType.Embedded => DeserializeArrayOfEmbedded(arrayValue, elementType, propertyInfo.PropertyType, relatedEntities),
                        ArrayOfAnnotations.ArrayType.GeoPoint => DeserializeArrayOfGeoPoints(arrayValue, elementType, propertyInfo.PropertyType),
                        ArrayOfAnnotations.ArrayType.Reference => DeserializeArrayOfReferences(arrayValue, elementType, propertyInfo.PropertyType, relatedEntities),
                        ArrayOfAnnotations.ArrayType.Primitive => DeserializeArrayOfPrimitives(arrayValue, elementType, propertyInfo.PropertyType),
                        _ => null
                    };

                    if (deserializedCollection != null)
                    {
                        // Set value via backing field from EF Core model (for IReadOnlyCollection with backing field)
                        SetCollectionValue(entity, entityType, propertyInfo, deserializedCollection);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize ArrayOf property {PropertyName}", propertyName);
                }
            }
        }

        /// <summary>
        /// Deserializa un array de objetos embebidos (maps → List&lt;T&gt;).
        /// </summary>
        private object DeserializeArrayOfEmbedded(
            IEnumerable<object> arrayValue,
            Type elementType,
            Type propertyType,
            IReadOnlyDictionary<string, object>? relatedEntities)
        {
            var collection = CreateTypedCollection(elementType, propertyType);

            foreach (var item in arrayValue)
            {
                if (item is IDictionary<string, object> map)
                {
                    var element = DeserializeEmbeddedElement(map, elementType, relatedEntities);
                    if (element != null)
                    {
                        AddToCollection(collection, element);
                    }
                }
            }

            return collection;
        }

        /// <summary>
        /// Deserializa un elemento embebido de un array.
        /// Usa CreateInstanceFromData y luego asigna referencias desde relatedEntities.
        /// </summary>
        private object? DeserializeEmbeddedElement(
            IDictionary<string, object> map,
            Type elementType,
            IReadOnlyDictionary<string, object>? relatedEntities)
        {
            // For records with constructor parameters, we need to resolve DocumentReferences
            // BEFORE calling CreateInstanceFromData, because the constructor needs the actual entities
            var resolvedMap = ResolveReferencesInMap(map, relatedEntities);

            // CreateInstanceFromData maneja tanto records (constructor con parámetros)
            // como clases con constructor sin parámetros
            var instance = CreateInstanceFromData(elementType, resolvedMap);

            if (instance != null && relatedEntities != null && relatedEntities.Count > 0)
            {
                // Asignar referencias (DocumentReference → entidad) dentro del elemento embebido
                // Esto es para propiedades que no fueron seteadas por el constructor
                AssignReferencesInEmbeddedElement(instance, map, relatedEntities);
            }

            return instance;
        }

        /// <summary>
        /// Resolves DocumentReferences in a map to their actual entities from relatedEntities.
        /// This is needed for records where properties are set via constructor parameters.
        /// </summary>
        private static IDictionary<string, object> ResolveReferencesInMap(
            IDictionary<string, object> map,
            IReadOnlyDictionary<string, object>? relatedEntities)
        {
            if (relatedEntities == null || relatedEntities.Count == 0)
                return map;

            var resolved = new Dictionary<string, object>(map, StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in map)
            {
                if (kvp.Value is DocumentReference docRef)
                {
                    if (relatedEntities.TryGetValue(docRef.Path, out var entity))
                    {
                        resolved[kvp.Key] = entity;
                    }
                }
            }

            return resolved;
        }

        /// <summary>
        /// Asigna referencias (DocumentReference) a propiedades de navegación dentro de un elemento embebido.
        /// También procesa arrays anidados recursivamente.
        /// </summary>
        private void AssignReferencesInEmbeddedElement(
            object instance,
            IDictionary<string, object> map,
            IReadOnlyDictionary<string, object> relatedEntities)
        {
            var type = instance.GetType();

            foreach (var kvp in map)
            {
                // Caso 1: DocumentReference directo
                if (kvp.Value is DocumentReference docRef)
                {
                    var property = type.GetProperty(kvp.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (property == null || !property.CanWrite)
                        continue;

                    if (relatedEntities.TryGetValue(docRef.Path, out var relatedEntity))
                    {
                        if (property.PropertyType.IsAssignableFrom(relatedEntity.GetType()))
                        {
                            property.SetValue(instance, relatedEntity);
                        }
                    }
                }
                // Caso 2: Array anidado - puede ser de embedded objects o de DocumentReferences
                else if (kvp.Value is IEnumerable<object> nestedArray)
                {
                    var property = type.GetProperty(kvp.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (property == null)
                        continue;

                    var nestedArrayList = nestedArray.ToList();
                    if (nestedArrayList.Count == 0)
                        continue;

                    // Caso 2a: Array de DocumentReferences (ArrayOf References dentro de embedded)
                    if (nestedArrayList[0] is DocumentReference)
                    {
                        // Obtener el tipo del elemento de la colección (List<T> → T)
                        var elementType = property.PropertyType.IsGenericType
                            ? property.PropertyType.GetGenericArguments().FirstOrDefault()
                            : null;
                        if (elementType == null)
                            continue;

                        // Crear la colección de entidades resueltas
                        var resolvedCollection = CreateTypedCollection(elementType, property.PropertyType);

                        foreach (var item in nestedArrayList)
                        {
                            if (item is DocumentReference nestedDocRef &&
                                relatedEntities.TryGetValue(nestedDocRef.Path, out var resolvedEntity))
                            {
                                if (elementType.IsAssignableFrom(resolvedEntity.GetType()))
                                {
                                    AddToCollection(resolvedCollection, resolvedEntity);
                                }
                            }
                        }

                        // Asignar la colección a la propiedad
                        if (property.CanWrite)
                        {
                            property.SetValue(instance, resolvedCollection);
                        }
                    }
                    // Caso 2b: Array de objetos embebidos - procesar recursivamente
                    else if (nestedArrayList[0] is IDictionary<string, object>)
                    {
                        // Obtener la colección actual de la instancia
                        var currentCollection = property.GetValue(instance);
                        if (currentCollection is not IEnumerable currentEnumerable)
                            continue;

                        var currentList = currentEnumerable.Cast<object>().ToList();

                        for (int i = 0; i < Math.Min(nestedArrayList.Count, currentList.Count); i++)
                        {
                            if (nestedArrayList[i] is IDictionary<string, object> nestedMap)
                            {
                                AssignReferencesInEmbeddedElement(currentList[i], nestedMap, relatedEntities);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Deserializa un array de GeoPoints nativos a List&lt;T&gt; donde T tiene Lat/Lng.
        /// </summary>
        private object DeserializeArrayOfGeoPoints(
            IEnumerable<object> arrayValue,
            Type elementType,
            Type propertyType)
        {
            var collection = CreateTypedCollection(elementType, propertyType);

            foreach (var item in arrayValue)
            {
                if (item is GeoPoint geoPoint)
                {
                    var element = CreateGeoPointInstance(elementType, geoPoint);
                    if (element != null)
                    {
                        AddToCollection(collection, element);
                    }
                }
            }

            return collection;
        }

        /// <summary>
        /// Crea una instancia del tipo GeoPoint personalizado.
        /// Convierte GeoPoint nativo de Firestore a diccionario y usa CreateInstanceFromData.
        /// </summary>
        private object? CreateGeoPointInstance(Type elementType, GeoPoint geoPoint)
        {
            // Convertir GeoPoint a diccionario con nombres comunes para Latitude/Longitude
            var geoData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Latitude"] = geoPoint.Latitude,
                ["Longitude"] = geoPoint.Longitude,
                // Aliases en español
                ["Latitud"] = geoPoint.Latitude,
                ["Longitud"] = geoPoint.Longitude,
                // Aliases cortos
                ["Lat"] = geoPoint.Latitude,
                ["Lng"] = geoPoint.Longitude,
                ["Lon"] = geoPoint.Longitude
            };

            return CreateInstanceFromData(elementType, geoData);
        }

        /// <summary>
        /// Deserializa un array de DocumentReferences a List&lt;T&gt; de entidades.
        /// </summary>
        private object DeserializeArrayOfReferences(
            IEnumerable<object> arrayValue,
            Type elementType,
            Type propertyType,
            IReadOnlyDictionary<string, object>? relatedEntities)
        {
            var collection = CreateTypedCollection(elementType, propertyType);

            if (relatedEntities == null || relatedEntities.Count == 0)
                return collection;

            foreach (var item in arrayValue)
            {
                if (item is DocumentReference docRef)
                {
                    // Try exact path match first
                    if (relatedEntities.TryGetValue(docRef.Path, out var entity))
                    {
                        AddToCollection(collection, entity);
                        continue;
                    }

                    // Fallback: match by document ID (last segment of path)
                    // This handles cases where paths differ (e.g., different projects/databases)
                    var docId = docRef.Id;
                    var matchingEntity = relatedEntities
                        .FirstOrDefault(kv => kv.Key.EndsWith("/" + docId, StringComparison.Ordinal));

                    if (matchingEntity.Value != null && elementType.IsAssignableFrom(matchingEntity.Value.GetType()))
                    {
                        AddToCollection(collection, matchingEntity.Value);
                    }
                }
            }

            return collection;
        }

        /// <summary>
        /// Deserializa un array de primitivos (int, string, enum, etc.) a List&lt;T&gt; o HashSet&lt;T&gt;.
        /// </summary>
        private object DeserializeArrayOfPrimitives(
            IEnumerable<object> arrayValue,
            Type elementType,
            Type propertyType)
        {
            var collection = CreateTypedCollection(elementType, propertyType);

            foreach (var item in arrayValue)
            {
                var convertedValue = _valueConverter.FromFirestore(item, elementType);
                if (convertedValue != null)
                {
                    AddToCollection(collection, convertedValue);
                }
            }

            return collection;
        }

        /// <summary>
        /// Crea una colección del tipo apropiado (List&lt;T&gt; o HashSet&lt;T&gt;).
        /// </summary>
        private static object CreateTypedCollection(Type elementType, Type propertyType)
        {
            // Si es HashSet<T>
            if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(HashSet<>))
            {
                var hashSetType = typeof(HashSet<>).MakeGenericType(elementType);
                return Activator.CreateInstance(hashSetType)!;
            }

            // Default: List<T>
            var listType = typeof(List<>).MakeGenericType(elementType);
            return Activator.CreateInstance(listType)!;
        }

        /// <summary>
        /// Sets a collection value on an entity, using backing field from EF Core model if the property is read-only.
        /// Supports PropertyAccessMode.Field pattern used in DDD.
        /// </summary>
        private void SetCollectionValue(object entity, IEntityType entityType, PropertyInfo propertyInfo, object collection)
        {
            // If property has a setter, use it directly
            if (propertyInfo.CanWrite)
            {
                propertyInfo.SetValue(entity, collection);
                return;
            }

            // Get backing field from EF Core model annotations (configured in ArrayOfBuilder)
            var backingField = entityType.GetArrayOfBackingField(propertyInfo.Name);
            if (backingField != null)
            {
                // Convert collection to field type if needed (e.g., List<T> to HashSet<T>)
                var convertedCollection = ConvertCollectionToFieldType(collection, backingField.FieldType);
                backingField.SetValue(entity, convertedCollection);
                _logger.LogTrace("Set backing field {FieldName} for property {PropertyName}", backingField.Name, propertyInfo.Name);
                return;
            }

            _logger.LogWarning(
                "Could not set property {PropertyName} - no setter and no backing field found in model. " +
                "Ensure the property is configured with ArrayOf() and has a backing field.",
                propertyInfo.Name);
        }

        /// <summary>
        /// Converts a collection to the target field type (e.g., List to HashSet).
        /// </summary>
        private static object ConvertCollectionToFieldType(object collection, Type fieldType)
        {
            if (fieldType.IsAssignableFrom(collection.GetType()))
                return collection;

            // If field is HashSet<T> but we have List<T>, convert
            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(HashSet<>))
            {
                var elementType = fieldType.GetGenericArguments()[0];
                var hashSetType = typeof(HashSet<>).MakeGenericType(elementType);
                var hashSet = Activator.CreateInstance(hashSetType)!;
                var addMethod = hashSetType.GetMethod("Add")!;

                foreach (var item in (IEnumerable)collection)
                {
                    addMethod.Invoke(hashSet, new[] { item });
                }

                return hashSet;
            }

            return collection;
        }

        #endregion

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

            // Set value via backing field from model annotations (for IReadOnlyCollection with backing field)
            SetNavigationCollectionValue(entity, navigation, collection);
        }

        /// <summary>
        /// Sets a navigation collection value, using backing field from model annotations if property is read-only.
        /// </summary>
        private void SetNavigationCollectionValue(object entity, IReadOnlyNavigation navigation, object collection)
        {
            var propertyInfo = navigation.PropertyInfo;
            if (propertyInfo == null)
                return;

            // If property has a setter, use it directly
            if (propertyInfo.CanWrite)
            {
                propertyInfo.SetValue(entity, collection);
                return;
            }

            // Get backing field from model annotations (configured in BackingFieldConvention)
            var backingField = BackingFieldConvention.GetNavigationBackingField(
                navigation.DeclaringEntityType, navigation.Name);

            if (backingField != null)
            {
                // Convert collection to field type if needed (e.g., List<T> to HashSet<T>)
                var convertedCollection = ConvertCollectionToFieldType(collection, backingField.FieldType);
                backingField.SetValue(entity, convertedCollection);
                _logger.LogTrace("Set backing field {FieldName} for navigation {NavigationName}",
                    backingField.Name, navigation.Name);
                return;
            }

            _logger.LogWarning(
                "Could not set navigation {NavigationName} - no setter and no backing field found in model. " +
                "Ensure the entity uses backing field pattern (_fieldName) for read-only collections.",
                navigation.Name);
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