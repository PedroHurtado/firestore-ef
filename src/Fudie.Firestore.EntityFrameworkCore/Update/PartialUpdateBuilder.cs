using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fudie.Firestore.EntityFrameworkCore.Extensions;
using Fudie.Firestore.EntityFrameworkCore.Infrastructure;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;
using Fudie.Firestore.EntityFrameworkCore.Storage;
using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Update;

namespace Fudie.Firestore.EntityFrameworkCore.Update;

/// <summary>
/// Builds a dictionary of only modified fields for partial Firestore updates.
/// Uses Firestore's special operations (FieldValue.Delete, ArrayUnion, ArrayRemove)
/// to optimize updates and handle concurrent modifications.
/// </summary>
public class PartialUpdateBuilder
{
    private readonly IModel _model;
    private readonly IFirestoreClientWrapper _firestoreClient;
    private readonly IFirestoreCollectionManager _collectionManager;
    private readonly IFirestoreValueConverter _valueConverter;

    /// <summary>
    /// JSON serialization options that match the Firestore provider's serialization conventions.
    /// Uses JsonStringEnumConverter to serialize enums as strings (e.g., "Monday" instead of 1),
    /// consistent with IFirestoreValueConverter.ToFirestore() behavior and ArrayOfChangeTracker.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = null,
        Converters = { new JsonStringEnumConverter() }
    };

    public PartialUpdateBuilder(
        IModel model,
        IFirestoreClientWrapper firestoreClient,
        IFirestoreCollectionManager collectionManager,
        IFirestoreValueConverter valueConverter)
    {
        _model = model;
        _firestoreClient = firestoreClient;
        _collectionManager = collectionManager;
        _valueConverter = valueConverter;
    }

    /// <summary>
    /// Builds the partial update result containing all operations needed.
    /// For array modifications, this may include separate ArrayRemove and ArrayUnion operations.
    /// </summary>
    /// <param name="entry">The update entry with tracked changes.</param>
    /// <returns>PartialUpdateResult with main updates and optional array operations.</returns>
    public PartialUpdateResult Build(IUpdateEntry entry)
    {
        var result = new PartialUpdateResult();
        var entityEntry = entry.ToEntityEntry();

        ProcessSimpleProperties(entityEntry, result.Updates);
        ProcessComplexProperties(entityEntry, result.Updates);
        ProcessArrayOfProperties(entityEntry, result);

        // Add _updatedAt with current UTC time (backend timestamp)
        result.Updates["_updatedAt"] = DateTime.UtcNow;

        return result;
    }

    /// <summary>
    /// Builds a dictionary containing only the modified fields for a partial update.
    /// </summary>
    /// <param name="entry">The update entry with tracked changes.</param>
    /// <returns>Dictionary with field paths as keys and values/FieldValue operations.</returns>
    [Obsolete("Use Build(IUpdateEntry) which returns PartialUpdateResult for proper array handling")]
    public Dictionary<string, object> BuildLegacy(IUpdateEntry entry)
    {
        var updates = new Dictionary<string, object>();
        var entityEntry = entry.ToEntityEntry();

        ProcessSimpleProperties(entityEntry, updates);
        ProcessComplexProperties(entityEntry, updates);

        // Legacy: process arrays into single dictionary (may not use ArrayRemove/ArrayUnion properly)
        var result = new PartialUpdateResult { Updates = updates };
        ProcessArrayOfProperties(entityEntry, result);

        updates["_updatedAt"] = DateTime.UtcNow;

        return updates;
    }

    /// <summary>
    /// Checks if there are any actual changes to update (excluding _updatedAt).
    /// </summary>
    public bool HasChanges(IUpdateEntry entry)
    {
        var entityEntry = entry.ToEntityEntry();

        // Check simple properties
        foreach (var property in entityEntry.Properties)
        {
            if (property.IsModified && !property.Metadata.IsPrimaryKey())
            {
                // Skip shadow properties for ArrayOf tracking
                if (property.Metadata.FindAnnotation(ArrayOfAnnotations.JsonTrackerFor) != null)
                    continue;

                // Skip FKs
                if (property.Metadata.IsForeignKey())
                    continue;

                return true;
            }
        }

        // Check complex properties
        foreach (var complexProp in entityEntry.ComplexProperties)
        {
            if (HasModifiedComplexProperties(complexProp))
                return true;
        }

        // Check ArrayOf properties via shadow property changes
        var entityType = entityEntry.Metadata;
        foreach (var property in entityType.GetProperties())
        {
            var trackerFor = property.FindAnnotation(ArrayOfAnnotations.JsonTrackerFor)?.Value as string;
            if (trackerFor != null)
            {
                var shadowProp = entityEntry.Property(property.Name);
                if (shadowProp.IsModified)
                    return true;
            }
        }

        return false;
    }

    #region Simple Properties

    private void ProcessSimpleProperties(EntityEntry entityEntry, Dictionary<string, object> updates)
    {
        foreach (var property in entityEntry.Properties)
        {
            // Skip primary key
            if (property.Metadata.IsPrimaryKey())
                continue;

            // Skip foreign keys (handled separately as references)
            if (property.Metadata.IsForeignKey())
                continue;

            // Skip ArrayOf shadow properties (change tracking only, not persisted)
            if (property.Metadata.FindAnnotation(ArrayOfAnnotations.JsonTrackerFor) != null)
                continue;

            // Only process modified properties
            if (!property.IsModified)
                continue;

            var propertyName = property.Metadata.Name;
            var currentValue = property.CurrentValue;
            var originalValue = property.OriginalValue;

            // Value changed to null
            if (currentValue == null && originalValue != null)
            {
                // Check if property has PersistNullValues configured
                // If so, send null explicitly. Otherwise, use FieldValue.Delete to remove the field.
                if (property.Metadata.IsPersistNullValuesEnabled())
                {
                    // Firestore expects null values to be explicitly stored
                    updates[propertyName] = null!;
                }
                else
                {
                    updates[propertyName] = FieldValue.Delete;
                }
                continue;
            }

            // Value is not null -> convert and add
            if (currentValue != null)
            {
                var convertedValue = ConvertValue(property.Metadata, currentValue);
                if (convertedValue != null)
                {
                    updates[propertyName] = convertedValue;
                }
            }
        }
    }

    private object? ConvertValue(IProperty property, object value)
    {
        // Handle collections
        if (value is IEnumerable enumerable && value is not string && value is not byte[])
        {
            return ConvertCollection(enumerable);
        }

        // Get converter from property or type mapping
        var converter = property.GetValueConverter() ?? property.GetTypeMapping()?.Converter;
        if (converter != null)
        {
            return converter.ConvertToProvider(value);
        }

        // Fallback: use value converter for DateTime→UTC, decimal→double, enum→string
        return _valueConverter.ToFirestore(value);
    }

    private object ConvertCollection(IEnumerable collection)
    {
        // Use FirestoreValueConverter.ToFirestore which handles all type conversions:
        // decimal → double, enum → string, Guid → string, DateTime → UTC, etc.
        return _valueConverter.ToFirestore(collection) ?? collection;
    }

    #endregion

    #region Complex Properties

    private void ProcessComplexProperties(EntityEntry entityEntry, Dictionary<string, object> updates)
    {
        foreach (var complexProp in entityEntry.ComplexProperties)
        {
            ProcessComplexPropertyEntry(complexProp, complexProp.Metadata.Name, updates);
        }
    }

    private void ProcessComplexPropertyEntry(
        ComplexPropertyEntry complexPropEntry,
        string pathPrefix,
        Dictionary<string, object> updates)
    {
        var complexValue = complexPropEntry.CurrentValue;

        // If ComplexType is null now but wasn't before -> delete the whole complex property
        if (complexValue == null)
        {
            // Check if any property was modified (indicating it had a value before)
            var hadValueBefore = complexPropEntry.Properties.Any(p => p.OriginalValue != null);
            if (hadValueBefore)
            {
                updates[pathPrefix] = FieldValue.Delete;
            }
            return;
        }

        // Check if marked as GeoPoint
        if (complexPropEntry.Metadata.FindAnnotation("Firestore:IsGeoPoint")?.Value is true)
        {
            // For GeoPoint, if any coordinate changed, update the whole GeoPoint
            if (complexPropEntry.Properties.Any(p => p.IsModified))
            {
                updates[pathPrefix] = ConvertToGeoPoint(complexValue);
            }
            return;
        }

        // Check if marked as Reference
        if (complexPropEntry.Metadata.FindAnnotation("Firestore:IsReference")?.Value is true)
        {
            if (complexPropEntry.Properties.Any(p => p.IsModified))
            {
                var refPropertyName = complexPropEntry.Metadata.FindAnnotation("Firestore:ReferenceProperty")?.Value as string;
                updates[pathPrefix] = ConvertToDocumentReference(complexValue, refPropertyName);
            }
            return;
        }

        // Process individual properties with dot notation
        foreach (var property in complexPropEntry.Properties)
        {
            if (!property.IsModified)
                continue;

            var fullPath = $"{pathPrefix}.{property.Metadata.Name}";
            var currentValue = property.CurrentValue;
            var originalValue = property.OriginalValue;

            if (currentValue == null && originalValue != null)
            {
                // Check if property has PersistNullValues configured
                var persistNullValues = property.Metadata.FindAnnotation(FirestorePropertyBuilderExtensions.PersistNullValuesAnnotation)?.Value is true;
                if (persistNullValues)
                {
                    updates[fullPath] = null!;
                }
                else
                {
                    updates[fullPath] = FieldValue.Delete;
                }
            }
            else if (currentValue != null)
            {
                var convertedValue = _valueConverter.ToFirestore(currentValue);
                if (convertedValue != null)
                {
                    updates[fullPath] = convertedValue;
                }
            }
        }

        // Process nested complex properties recursively
        foreach (var nestedComplex in complexPropEntry.ComplexProperties)
        {
            var nestedPath = $"{pathPrefix}.{nestedComplex.Metadata.Name}";
            ProcessComplexPropertyEntry(nestedComplex, nestedPath, updates);
        }
    }

    private bool HasModifiedComplexProperties(ComplexPropertyEntry complexProp)
    {
        if (complexProp.Properties.Any(p => p.IsModified))
            return true;

        foreach (var nested in complexProp.ComplexProperties)
        {
            if (HasModifiedComplexProperties(nested))
                return true;
        }

        return false;
    }

    private GeoPoint ConvertToGeoPoint(object value)
    {
        var type = value.GetType();
        var latProp = type.GetProperty("Latitude") ?? type.GetProperty("Latitud");
        var lonProp = type.GetProperty("Longitude") ?? type.GetProperty("Longitud");

        if (latProp == null || lonProp == null)
            throw new InvalidOperationException($"Type '{type.Name}' must have Latitude/Longitude properties");

        var lat = Convert.ToDouble(latProp.GetValue(value));
        var lon = Convert.ToDouble(lonProp.GetValue(value));

        return new GeoPoint(lat, lon);
    }

    private DocumentReference ConvertToDocumentReference(object value, string? propertyName)
    {
        var type = value.GetType();

        PropertyInfo? idProperty;
        if (propertyName != null)
        {
            idProperty = type.GetProperty(propertyName);
        }
        else
        {
            var entityType = _model.FindEntityType(type);
            if (entityType != null)
            {
                var pkProperty = entityType.FindPrimaryKey()?.Properties.FirstOrDefault();
                idProperty = pkProperty?.PropertyInfo;
            }
            else
            {
                idProperty = type.GetProperty("Id") ?? type.GetProperty($"{type.Name}Id");
            }
        }

        if (idProperty == null)
            throw new InvalidOperationException($"Cannot find ID property for reference type '{type.Name}'");

        var idValue = idProperty.GetValue(value)?.ToString();
        if (string.IsNullOrEmpty(idValue))
            throw new InvalidOperationException($"ID value cannot be null or empty for reference");

        var collectionName = _collectionManager.GetCollectionName(type);
        return _firestoreClient.Database.Collection(collectionName).Document(idValue);
    }

    #endregion

    #region ArrayOf Properties

    private void ProcessArrayOfProperties(EntityEntry entityEntry, PartialUpdateResult result)
    {
        var entityType = entityEntry.Metadata;
        var entity = entityEntry.Entity;

        foreach (var property in entityType.GetProperties())
        {
            var trackerFor = property.FindAnnotation(ArrayOfAnnotations.JsonTrackerFor)?.Value as string;
            if (trackerFor == null)
                continue;

            var shadowProp = entityEntry.Property(property.Name);
            if (!shadowProp.IsModified)
                continue;

            var originalJson = shadowProp.OriginalValue as string;
            var currentJson = shadowProp.CurrentValue as string;

            // Get the array type configuration
            var arrayType = entityType.GetArrayOfType(trackerFor);
            if (arrayType == null)
                continue;

            // Get the current array value
            var arrayProperty = entity.GetType().GetProperty(trackerFor, BindingFlags.Public | BindingFlags.Instance);
            if (arrayProperty == null)
                continue;

            var currentArray = arrayProperty.GetValue(entity) as IEnumerable;

            // Compute diff and add to result
            ComputeArrayDiff(
                trackerFor,
                originalJson,
                currentArray,
                arrayType,
                entityType,
                result);
        }
    }

    private void ComputeArrayDiff(
        string propertyName,
        string? originalJson,
        IEnumerable? currentArray,
        string arrayType,
        IEntityType entityType,
        PartialUpdateResult result)
    {
        // Deserialize original and current arrays
        var originalElements = DeserializeJsonArray(originalJson);
        var currentElements = SerializeCurrentArray(currentArray, arrayType, entityType, propertyName);

        // Special case: if all elements are removed, use FieldValue.Delete instead of ArrayRemove
        // This is more efficient and cleaner
        if (currentElements.Count == 0 && originalElements.Count > 0)
        {
            result.Updates[propertyName] = FieldValue.Delete;
            return;
        }

        // Find removed elements (in original but not in current)
        var removed = originalElements
            .Where(o => !currentElements.Any(c => JsonElementsEqual(o, c)))
            .ToList();

        // Find added elements (in current but not in original)
        var added = currentElements
            .Where(c => !originalElements.Any(o => JsonElementsEqual(o, c)))
            .ToList();

        var hasRemoves = removed.Count > 0;
        var hasAdds = added.Count > 0;

        // ArrayRemove operations with raw data for logging
        if (hasRemoves)
        {
            var removeValues = ConvertJsonElementsToFirestore(removed, arrayType, entityType, propertyName);
            if (removeValues.Length > 0)
            {
                result.ArrayRemoveOperations[propertyName] = FieldValue.ArrayRemove(removeValues);
                result.ArrayRemoveData[propertyName] = removeValues;
            }
        }

        // ArrayUnion operations with raw data for logging
        if (hasAdds)
        {
            var addValues = ConvertJsonElementsToFirestore(added, arrayType, entityType, propertyName);
            if (addValues.Length > 0)
            {
                result.ArrayUnionOperations[propertyName] = FieldValue.ArrayUnion(addValues);
                result.ArrayUnionData[propertyName] = addValues;
            }
        }
    }

    private List<JsonElement> DeserializeJsonArray(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return new List<JsonElement>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return new List<JsonElement>();

            return doc.RootElement.EnumerateArray()
                .Select(e => e.Clone())
                .ToList();
        }
        catch
        {
            return new List<JsonElement>();
        }
    }

    private List<JsonElement> SerializeCurrentArray(
        IEnumerable? array,
        string arrayType,
        IEntityType entityType,
        string propertyName)
    {
        if (array == null)
            return new List<JsonElement>();

        var elements = new List<object>();
        foreach (var item in array)
        {
            if (item == null) continue;
            elements.Add(item);
        }

        // Serialize to JSON and parse back for comparison
        var json = JsonSerializer.Serialize(elements, JsonOptions);
        return DeserializeJsonArray(json);
    }

    private bool JsonElementsEqual(JsonElement a, JsonElement b)
    {
        return a.GetRawText() == b.GetRawText();
    }

    private object[] ConvertJsonElementsToFirestore(
        List<JsonElement> elements,
        string arrayType,
        IEntityType entityType,
        string propertyName)
    {
        var result = new List<object>();

        var elementClrType = entityType.GetArrayOfElementClrType(propertyName);

        foreach (var element in elements)
        {
            var value = ConvertJsonElementToFirestore(element, arrayType, elementClrType);
            if (value != null)
            {
                result.Add(value);
            }
        }

        return result.ToArray();
    }

    private object? ConvertJsonElementToFirestore(
        JsonElement element,
        string arrayType,
        Type? elementClrType)
    {
        switch (arrayType)
        {
            case ArrayOfAnnotations.ArrayType.GeoPoint:
                // Parse GeoPoint from JSON object with Latitude/Longitude
                if (element.ValueKind == JsonValueKind.Object)
                {
                    var lat = GetDoubleProperty(element, "Latitude", "Latitud");
                    var lon = GetDoubleProperty(element, "Longitude", "Longitud");
                    if (lat.HasValue && lon.HasValue)
                    {
                        return new GeoPoint(lat.Value, lon.Value);
                    }
                }
                return null;

            case ArrayOfAnnotations.ArrayType.Reference:
                // Parse reference - need to get ID and create DocumentReference
                if (elementClrType != null)
                {
                    var refEntityType = _model.FindEntityType(elementClrType);
                    if (refEntityType != null)
                    {
                        var idProp = refEntityType.FindPrimaryKey()?.Properties.FirstOrDefault();
                        var idPropName = idProp?.Name ?? "Id";

                        if (element.TryGetProperty(idPropName, out var idElement))
                        {
                            var idValue = idElement.GetString();
                            if (!string.IsNullOrEmpty(idValue))
                            {
                                var collectionName = _collectionManager.GetCollectionName(elementClrType);
                                return _firestoreClient.Database.Collection(collectionName).Document(idValue);
                            }
                        }
                    }
                }
                return null;

            case ArrayOfAnnotations.ArrayType.Embedded:
            default:
                // Convert embedded object to Dictionary
                if (element.ValueKind == JsonValueKind.Object)
                {
                    return JsonElementToDict(element);
                }
                return null;
        }
    }

    private double? GetDoubleProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var prop))
            {
                if (prop.TryGetDouble(out var value))
                    return value;
            }
        }
        return null;
    }

    private Dictionary<string, object> JsonElementToDict(JsonElement element)
    {
        var dict = new Dictionary<string, object>();

        foreach (var prop in element.EnumerateObject())
        {
            var value = JsonElementToValue(prop.Value);
            if (value != null)
            {
                dict[prop.Name] = value;
            }
        }

        return dict;
    }

    private object? JsonElementToValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number when element.TryGetDouble(out var d) => d,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToValue).Where(v => v != null).ToList(),
            JsonValueKind.Object => JsonElementToDict(element),
            _ => null
        };
    }

    private object ConvertCurrentArrayToFirestore(
        IEnumerable? array,
        string arrayType,
        IEntityType entityType,
        string propertyName)
    {
        if (array == null)
            return new List<object>();

        var elementClrType = entityType.GetArrayOfElementClrType(propertyName);

        switch (arrayType)
        {
            case ArrayOfAnnotations.ArrayType.GeoPoint:
                var geoList = new List<GeoPoint>();
                foreach (var item in array)
                {
                    if (item != null)
                        geoList.Add(ConvertToGeoPoint(item));
                }
                return geoList;

            case ArrayOfAnnotations.ArrayType.Reference:
                var refList = new List<DocumentReference>();
                if (elementClrType != null)
                {
                    var refEntityType = _model.FindEntityType(elementClrType);
                    var idProp = refEntityType?.FindPrimaryKey()?.Properties.FirstOrDefault();
                    var collectionName = _collectionManager.GetCollectionName(elementClrType);

                    foreach (var item in array)
                    {
                        if (item == null) continue;
                        var idValue = idProp?.PropertyInfo?.GetValue(item);
                        if (idValue != null)
                        {
                            refList.Add(_firestoreClient.Database.Collection(collectionName).Document(idValue.ToString()!));
                        }
                    }
                }
                return refList;

            case ArrayOfAnnotations.ArrayType.Embedded:
            default:
                var mapList = new List<Dictionary<string, object>>();
                foreach (var item in array)
                {
                    if (item != null)
                        mapList.Add(SerializeObjectToDict(item));
                }
                return mapList;
        }
    }

    private Dictionary<string, object> SerializeObjectToDict(object obj)
    {
        var dict = new Dictionary<string, object>();
        var type = obj.GetType();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var value = prop.GetValue(obj);
            if (value == null) continue;

            var convertedValue = _valueConverter.ToFirestore(value);
            if (convertedValue != null)
            {
                dict[prop.Name] = convertedValue;
            }
        }

        return dict;
    }

    #endregion
}

/// <summary>
/// Result of building a partial update, containing main updates and optional array operations.
/// Array modifications require separate operations because Firestore doesn't allow
/// ArrayRemove and ArrayUnion on the same field in a single UpdateAsync call.
/// </summary>
public class PartialUpdateResult
{
    /// <summary>
    /// Main updates dictionary with field paths and values.
    /// This is sent in the primary UpdateAsync call.
    /// </summary>
    public Dictionary<string, object> Updates { get; set; } = new();

    /// <summary>
    /// ArrayRemove operations to be executed before ArrayUnion operations.
    /// Key is the field name, value is the FieldValue.ArrayRemove operation.
    /// </summary>
    public Dictionary<string, object> ArrayRemoveOperations { get; } = new();

    /// <summary>
    /// Raw data for ArrayRemove operations (for logging purposes).
    /// Key is the field name, value is the array of elements being removed.
    /// </summary>
    public Dictionary<string, object[]> ArrayRemoveData { get; } = new();

    /// <summary>
    /// ArrayUnion operations to be executed after ArrayRemove operations.
    /// Key is the field name, value is the FieldValue.ArrayUnion operation.
    /// </summary>
    public Dictionary<string, object> ArrayUnionOperations { get; } = new();

    /// <summary>
    /// Raw data for ArrayUnion operations (for logging purposes).
    /// Key is the field name, value is the array of elements being added.
    /// </summary>
    public Dictionary<string, object[]> ArrayUnionData { get; } = new();

    /// <summary>
    /// Returns true if there are any actual data changes (excluding _updatedAt).
    /// </summary>
    public bool HasChanges =>
        Updates.Count > 1 || // More than just _updatedAt
        ArrayRemoveOperations.Count > 0 ||
        ArrayUnionOperations.Count > 0;

    /// <summary>
    /// Returns true if array modifications require multiple UpdateAsync calls.
    /// </summary>
    public bool RequiresMultipleOperations =>
        ArrayRemoveOperations.Count > 0 && ArrayUnionOperations.Count > 0;
}
