using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fudie.Firestore.EntityFrameworkCore.Infrastructure;
using Fudie.Firestore.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections;
using System.Reflection;
using System.ComponentModel.DataAnnotations.Schema;
using Google.Cloud.Firestore;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

namespace Fudie.Firestore.EntityFrameworkCore.Storage
{
    public class FirestoreDatabase(
        DatabaseDependencies dependencies,
        IFirestoreClientWrapper firestoreClient,
        IFirestoreDocumentSerializer documentSerializer,
        IFirestoreIdGenerator idGenerator,
        IFirestoreCollectionManager collectionManager,
        IDiagnosticsLogger<Microsoft.EntityFrameworkCore.DbLoggerCategory.Database.Command> commandLogger,
        ITypeMappingSource typeMappingSource,
        IModel model,
        IFirestoreValueConverter valueConverter,
        IFirestoreCommandLogger firestoreCommandLogger
        ) : Database(dependencies)

    {
        private readonly IFirestoreClientWrapper _firestoreClient = firestoreClient;
        private readonly IFirestoreDocumentSerializer _documentSerializer = documentSerializer;
        private readonly IFirestoreIdGenerator _idGenerator = idGenerator;
        private readonly IFirestoreCollectionManager _collectionManager = collectionManager;
        private readonly IDiagnosticsLogger<Microsoft.EntityFrameworkCore.DbLoggerCategory.Database.Command> _commandLogger = commandLogger;
        private readonly ITypeMappingSource _typeMappingSource = typeMappingSource;
        private readonly IModel _model = model;
        private readonly IFirestoreValueConverter _valueConverter = valueConverter;
        private readonly IFirestoreCommandLogger _firestoreCommandLogger = firestoreCommandLogger;

        public override int SaveChanges(IList<IUpdateEntry> entries)
        {
            return SaveChangesAsync(entries).GetAwaiter().GetResult();
        }

        public override async Task<int> SaveChangesAsync(
            IList<IUpdateEntry> entries,
            CancellationToken cancellationToken = default)
        {
            if (entries == null || entries.Count == 0)
                return 0;

            var processedCount = 0;

            // 1. Ordenar entradas para Firestore:
            //    - INSERTs: padres primero (para que existan antes de crear subcollections)
            //    - DELETEs: hijos primero (para evitar documentos huérfanos)
            var orderedEntries = OrderEntriesForFirestore(entries);

            // 2. Procesar entidades normales (CRUD)
            foreach (var entry in orderedEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entityType = entry.EntityType;

                // ✅ Saltar entidades intermedias generadas automáticamente
                if (IsJoinEntity(entityType))
                    continue;

                // ✅ NUEVO: Obtener el document path (puede ser jerárquico para subcollections)
                var documentRef = GetDocumentPath(entry, entries);

                switch (entry.EntityState)
                {
                    case EntityState.Added:
                        await AddDocumentAsync(documentRef, entry, cancellationToken);
                        processedCount++;
                        break;

                    case EntityState.Modified:
                        await UpdateDocumentAsync(documentRef, entry, cancellationToken);
                        processedCount++;
                        break;

                    case EntityState.Deleted:
                        await DeleteDocumentAsync(documentRef, entry, cancellationToken);
                        processedCount++;
                        break;
                }
            }

            // 3. Gestionar tablas intermedias para skip navigations (N:M)
            await ProcessSkipNavigationChanges(entries, cancellationToken);

            return processedCount;
        }

        // ========================================================================
        // ✅ NUEVAS FUNCIONALIDADES PARA SUBCOLLECTIONS
        // ========================================================================

        /// <summary>
        /// Obtiene el DocumentReference completo para una entidad, incluyendo el path jerárquico si es subcollection
        /// </summary>
        private DocumentReference GetDocumentPath(IUpdateEntry entry, IList<IUpdateEntry> allEntries)
        {
            var entityType = entry.EntityType;
            var documentId = GetOrGenerateDocumentId(entry);

            // ✅ Buscar si esta entidad es una subcollection de otra
            var parentNavigation = FindParentNavigationForSubCollection(entityType);

            if (parentNavigation == null)
            {
                // Es una entidad raíz - path simple
                var collectionName = _collectionManager.GetCollectionName(entityType.ClrType);
                return _firestoreClient.Database
                    .Collection(collectionName)
                    .Document(documentId);
            }

            // Es una subcollection - construir path jerárquico
            return BuildSubCollectionPath(entry, parentNavigation, allEntries, documentId);
        }

        /// <summary>
        /// Encuentra la navigation que marca esta entidad como subcollection
        /// </summary>
        private INavigation? FindParentNavigationForSubCollection(IEntityType entityType)
        {
            // Buscar en todas las entidades del modelo
            foreach (var parentEntityType in _model.GetEntityTypes())
            {
                foreach (var navigation in parentEntityType.GetNavigations())
                {
                    // Si esta navigation apunta a nuestro entityType y está marcada como subcollection
                    if (navigation.TargetEntityType == entityType && navigation.IsSubCollection())
                    {
                        return navigation;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Calcula la profundidad de una entidad en el árbol de documentos.
        /// Root collections tienen profundidad 0, subcollections tienen profundidad 1+.
        /// </summary>
        private int GetEntityDepth(IEntityType entityType)
        {
            var parentNavigation = FindParentNavigationForSubCollection(entityType);

            if (parentNavigation == null)
            {
                // Es una entidad raíz
                return 0;
            }

            // Es una subcollection - profundidad = 1 + profundidad del padre
            return 1 + GetEntityDepth(parentNavigation.DeclaringEntityType);
        }

        /// <summary>
        /// Ordena las entradas según el tipo de operación y profundidad en el árbol de documentos.
        /// - INSERTs: padres primero (menor profundidad)
        /// - DELETEs: hijos primero (mayor profundidad)
        /// - UPDATEs: sin orden específico
        /// </summary>
        private IEnumerable<IUpdateEntry> OrderEntriesForFirestore(IList<IUpdateEntry> entries)
        {
            var inserts = entries
                .Where(e => e.EntityState == EntityState.Added)
                .OrderBy(e => GetEntityDepth(e.EntityType))
                .ToList();

            var updates = entries
                .Where(e => e.EntityState == EntityState.Modified)
                .ToList();

            var deletes = entries
                .Where(e => e.EntityState == EntityState.Deleted)
                .OrderByDescending(e => GetEntityDepth(e.EntityType))
                .ToList();

            // Orden: INSERTs, UPDATEs, DELETEs
            return inserts.Concat(updates).Concat(deletes);
        }

        /// <summary>
        /// Construye el path completo para una subcollection, incluyendo todos los niveles padres
        /// </summary>
        private DocumentReference BuildSubCollectionPath(
            IUpdateEntry entry,
            INavigation parentNavigation,
            IList<IUpdateEntry> allEntries,
            string documentId)
        {
            var entityType = entry.EntityType;
            var parentEntityType = parentNavigation.DeclaringEntityType;

            // Buscar la entidad padre en el ChangeTracker
            var parentEntry = FindParentEntry(entry, parentNavigation, allEntries);

            if (parentEntry == null)
            {
                throw new InvalidOperationException(
                    $"Cannot save subcollection entity '{entityType.ClrType.Name}' (ID: {documentId}) " +
                    $"because parent entity '{parentEntityType.ClrType.Name}' is not being tracked. " +
                    $"Make sure to add the parent entity to the context first.");
            }

            // Obtener el path del padre (recursivo para subcollections anidadas)
            var parentPath = GetDocumentPath(parentEntry, allEntries);

            // Construir el path de la subcollection
            var subCollectionName = _collectionManager.GetCollectionName(entityType.ClrType);

            return parentPath
                .Collection(subCollectionName)
                .Document(documentId);
        }

        /// <summary>
        /// Encuentra la entidad padre en el ChangeTracker
        /// </summary>
        private IUpdateEntry? FindParentEntry(
            IUpdateEntry childEntry,
            INavigation parentNavigation,
            IList<IUpdateEntry> allEntries)
        {
            var childEntity = childEntry.ToEntityEntry().Entity;
            var parentClrType = parentNavigation.DeclaringEntityType.ClrType;

            // 1. Buscar primero en allEntries (entidades con cambios pendientes)
            foreach (var entry in allEntries)
            {
                // Usar IsAssignableTo para soportar herencia
                if (!entry.EntityType.ClrType.IsAssignableTo(parentClrType))
                    continue;

                if (IsChildInParentCollection(childEntity, entry.ToEntityEntry().Entity, parentNavigation))
                    return entry;
            }

            // 2. Si no encontró, buscar en el ChangeTracker completo (incluye Unchanged)
            var dbContext = childEntry.ToEntityEntry().Context;
            foreach (var trackedEntry in dbContext.ChangeTracker.Entries())
            {
                // Usar IsAssignableTo para soportar herencia
                if (!trackedEntry.Metadata.ClrType.IsAssignableTo(parentClrType))
                    continue;

                if (IsChildInParentCollection(childEntity, trackedEntry.Entity, parentNavigation))
                    return trackedEntry.GetInfrastructure();
            }

            return null;
        }

        /// <summary>
        /// Verifica si una entidad hijo está contenida en la colección de navegación del padre
        /// </summary>
        private static bool IsChildInParentCollection(
            object childEntity,
            object parentEntity,
            INavigation parentNavigation)
        {
            var childrenCollection = parentNavigation.PropertyInfo?.GetValue(parentEntity) as IEnumerable;
            if (childrenCollection == null)
                return false;

            return childrenCollection
                .Cast<object>()
                .Any(item => ReferenceEquals(item, childEntity));
        }

        // ========================================================================
        // MÉTODOS CRUD MODIFICADOS PARA SOPORTAR SUBCOLLECTIONS
        // ========================================================================

        private string GetOrGenerateDocumentId(IUpdateEntry entry)
        {
            var keyProperties = entry.EntityType.FindPrimaryKey()?.Properties;
            if (keyProperties == null || keyProperties.Count == 0)
                throw new InvalidOperationException("La entidad no tiene clave primaria.");

            if (keyProperties.Count > 1)
                throw new NotSupportedException("Firestore no soporta claves compuestas.");

            var keyProperty = keyProperties[0];
            var keyValue = entry.GetCurrentValue(keyProperty);

            if (keyValue == null || IsDefaultValue(keyValue, keyProperty.ClrType))
            {
                if (entry.EntityState == EntityState.Added)
                    return _idGenerator.GenerateId();
                throw new InvalidOperationException("No se puede modificar/eliminar sin ID.");
            }

            return keyValue.ToString() ?? throw new InvalidOperationException("El ID no puede ser null.");
        }

        private async Task AddDocumentAsync(
            DocumentReference documentRef,
            IUpdateEntry entry,
            CancellationToken cancellationToken)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var document = SerializeEntityFromEntry(entry);
            document["_createdAt"] = DateTime.UtcNow;
            document["_updatedAt"] = DateTime.UtcNow;

            await documentRef.SetAsync(document, cancellationToken: cancellationToken);

            stopwatch.Stop();

            // Log the insert operation
            _firestoreCommandLogger.LogInsert(
                GetRelativeCollectionPath(documentRef.Parent.Path),
                documentRef.Id,
                entry.EntityType.ClrType,
                stopwatch.Elapsed,
                document);

            // Actualizar el ID si fue generado
            var documentId = documentRef.Id;
            UpdateEntityIdIfNeeded(entry, documentId);
        }

        private async Task UpdateDocumentAsync(
            DocumentReference documentRef,
            IUpdateEntry entry,
            CancellationToken cancellationToken)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var document = SerializeEntityFromEntry(entry);
            document["_updatedAt"] = DateTime.UtcNow;

            await documentRef.SetAsync(document, SetOptions.MergeAll, cancellationToken);

            stopwatch.Stop();

            // Log the update operation
            _firestoreCommandLogger.LogUpdate(
                GetRelativeCollectionPath(documentRef.Parent.Path),
                documentRef.Id,
                entry.EntityType.ClrType,
                stopwatch.Elapsed,
                document);
        }

        private async Task DeleteDocumentAsync(
            DocumentReference documentRef,
            IUpdateEntry entry,
            CancellationToken cancellationToken)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            await documentRef.DeleteAsync(cancellationToken: cancellationToken);

            stopwatch.Stop();

            // Log the delete operation
            _firestoreCommandLogger.LogDelete(
                GetRelativeCollectionPath(documentRef.Parent.Path),
                documentRef.Id,
                entry.EntityType.ClrType,
                stopwatch.Elapsed);
        }

        // ========================================================================
        // SERIALIZACIÓN (SIN CAMBIOS)
        // ========================================================================

        private Dictionary<string, object> SerializeEntityFromEntry(IUpdateEntry entry)
        {
            var dict = new Dictionary<string, object>();

            // Serializar propiedades simples
            SerializeProperties(entry.EntityType, prop => entry.GetCurrentValue(prop), dict);

            // Serializar Complex Properties (Value Objects)
            SerializeComplexProperties(entry.EntityType, entry.ToEntityEntry().Entity, dict);

            // ✅ Serializar propiedades ArrayOf (List<ValueObject>, List<GeoPoint>)
            SerializeArrayOfProperties(entry.EntityType, entry.ToEntityEntry().Entity, dict);

            // ✅ Serializar referencias de entidades individuales
            SerializeEntityReferences(entry.EntityType, entry.ToEntityEntry().Entity, dict);

            // ✅ Serializar colecciones de referencias de entidades
            SerializeEntityReferenceCollections(entry.EntityType, entry.ToEntityEntry().Entity, dict);

            // ✅ Convertir shadow FKs en DocumentReferences
            SerializeShadowForeignKeyReferences(entry, dict);

            // ✅ Serializar referencias a documentos de tablas intermedias (N:M)
            SerializeJoinEntityReferences(entry.EntityType, entry.ToEntityEntry().Entity, dict);

            return dict;
        }

        // ✅ Serializar referencias a documentos de tablas intermedias (N:M)
        private void SerializeJoinEntityReferences(
            IEntityType entityType,
            object entity,
            Dictionary<string, object> dict)
        {
            var skipNavigations = entityType.GetSkipNavigations();

            foreach (var skipNavigation in skipNavigations)
            {
                var collection = skipNavigation.PropertyInfo?.GetValue(entity) as IEnumerable;
                if (collection == null) continue;

                var joinReferences = CreateJoinEntityReferenceArray(skipNavigation, collection, entity);
                if (joinReferences.Length > 0)
                {
                    // Nombre del array: "PizzasIngredients", "ProductosCategorias", etc.
                    var joinArrayName = GetJoinCollectionName(skipNavigation);
                    dict[joinArrayName] = joinReferences;
                }
            }
        }

        // ✅ Crear array de referencias a documentos de la tabla intermedia
        private Google.Cloud.Firestore.DocumentReference[] CreateJoinEntityReferenceArray(
            ISkipNavigation skipNavigation,
            IEnumerable collection,
            object principalEntity)
        {
            var references = new List<Google.Cloud.Firestore.DocumentReference>();

            // Obtener ID de la entidad principal
            var principalEntityType = skipNavigation.DeclaringEntityType;
            var principalIdProperty = principalEntityType.FindPrimaryKey()?.Properties.FirstOrDefault();
            if (principalIdProperty == null) return Array.Empty<Google.Cloud.Firestore.DocumentReference>();

            var principalId = principalIdProperty.PropertyInfo?.GetValue(principalEntity);
            if (principalId == null || IsDefaultValue(principalId, principalIdProperty.ClrType))
                return Array.Empty<Google.Cloud.Firestore.DocumentReference>();

            // Obtener información para generar IDs determinísticos
            var targetEntityType = skipNavigation.TargetEntityType;
            var targetIdProperty = targetEntityType.FindPrimaryKey()?.Properties.FirstOrDefault();
            if (targetIdProperty == null) return Array.Empty<Google.Cloud.Firestore.DocumentReference>();

            var joinCollectionName = GetJoinCollectionName(skipNavigation);

            foreach (var item in collection)
            {
                if (item == null) continue;

                var targetId = targetIdProperty.PropertyInfo?.GetValue(item);
                if (targetId == null || IsDefaultValue(targetId, targetIdProperty.ClrType))
                    continue;

                // ✅ ID determinístico: principal_target (sin ordenar alfabéticamente)
                var documentId = $"{principalId}_{targetId}";

                var docRef = _firestoreClient.Database
                    .Collection(joinCollectionName)
                    .Document(documentId);

                references.Add(docRef);
            }

            return references.ToArray();
        }

        private void SerializeProperties(
            ITypeBase typeBase,
            Func<IProperty, object?> valueGetter,
            Dictionary<string, object> dict)
        {
            foreach (var property in typeBase.GetProperties())
            {
                if (property.IsPrimaryKey()) continue;

                // ✅ Omitir TODAS las FKs (las convertiremos en referencias)
                if (property.IsForeignKey()) continue;

                var value = valueGetter(property);

                // Solo persistir null si está explícitamente configurado con PersistNullValues()
                if (value == null)
                {
                    if (property.IsPersistNullValuesEnabled())
                    {
                        dict[property.Name] = null!;
                    }
                    continue;
                }

                value = ApplyConverter(property, value);
                if (value != null)
                {
                    dict[property.Name] = value;
                }
            }
        }

        // ✅ Convertir shadow FKs en DocumentReferences
        private void SerializeShadowForeignKeyReferences(IUpdateEntry entry, Dictionary<string, object> dict)
        {
            var entityType = entry.EntityType;

            // ✅ NUEVO: Si esta entidad es subcollection, no guardar referencia al padre
            // El path ya indica la jerarquía: /clientes/cli-001/pedidos/ped-001
            var isSubCollection = FindParentNavigationForSubCollection(entityType) != null;
            if (isSubCollection)
                return; // No serializar referencias al padre en subcollections

            // Buscar todas las FKs (incluyendo shadow properties)
            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                // Solo si la navegación inversa es una colección (relación 1:N)
                var principalToDependent = foreignKey.PrincipalToDependent;
                if (principalToDependent == null || !principalToDependent.IsCollection)
                    continue;

                // Obtener el valor de la shadow FK
                var fkProperty = foreignKey.Properties.First();
                var fkValue = entry.GetCurrentValue(fkProperty);

                if (fkValue == null || IsDefaultValue(fkValue, fkProperty.ClrType))
                    continue;

                // Obtener el tipo de la entidad principal (Pedido)
                var principalEntityType = foreignKey.PrincipalEntityType;
                var collectionName = _collectionManager.GetCollectionName(principalEntityType.ClrType);

                // Crear DocumentReference
                var docRef = _firestoreClient.Database
                    .Collection(collectionName)
                    .Document(fkValue.ToString()!);

                // Usar el nombre de la entidad principal (sin "Id")
                var referenceName = principalEntityType.ClrType.Name;
                dict[referenceName] = docRef;
            }
        }

        private void SerializeComplexProperties(
            ITypeBase typeBase,
            object entity,
            Dictionary<string, object> dict)
        {
            foreach (var complexProperty in typeBase.GetComplexProperties())
            {
                var complexValue = complexProperty.PropertyInfo?.GetValue(entity);
                if (complexValue == null) continue;

                // ✅ 1. Verificar si está marcado como Reference (ComplexProperty)
                if (complexProperty.FindAnnotation("Firestore:IsReference")?.Value is true)
                {
                    var refPropertyName = complexProperty.FindAnnotation("Firestore:ReferenceProperty")?.Value as string;
                    dict[complexProperty.Name] = ConvertToFirestoreReference(complexValue, refPropertyName);
                    continue;
                }

                // ✅ 2. Verificar si está marcado como GeoPoint
                if (complexProperty.FindAnnotation("Firestore:IsGeoPoint")?.Value is true)
                {
                    dict[complexProperty.Name] = ConvertToFirestoreGeoPoint(complexValue);
                    continue;
                }

                // ✅ 3. Verificar si es una colección de complex types
                if (complexValue is IEnumerable enumerable &&
                    complexValue is not string &&
                    complexValue is not byte[])
                {
                    var list = new List<Dictionary<string, object>>();
                    foreach (var item in enumerable)
                    {
                        list.Add(SerializeComplexTypeFromObject(item));
                    }
                    dict[complexProperty.Name] = list;
                    continue;
                }

                // ✅ 4. Complex type simple (no colección, no GeoPoint, no Reference)
                dict[complexProperty.Name] = SerializeComplexType(complexValue, complexProperty.ComplexType);
            }
        }

        // ✅ Serializar propiedades marcadas con ArrayOf (List<ValueObject>, List<GeoPoint>)
        private void SerializeArrayOfProperties(
            IEntityType entityType,
            object entity,
            Dictionary<string, object> dict)
        {
            var clrType = entityType.ClrType;

            foreach (var prop in clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // Verificar si esta propiedad está marcada como ArrayOf
                var arrayType = entityType.GetArrayOfType(prop.Name);
                if (arrayType == null)
                    continue;

                var value = prop.GetValue(entity);
                if (value == null)
                    continue;

                if (value is not IEnumerable enumerable)
                    continue;

                // Serializar según el tipo de ArrayOf
                if (arrayType == ArrayOfAnnotations.ArrayType.Embedded)
                {
                    // List<ValueObject> → List<Dictionary<string, object>>
                    var list = new List<Dictionary<string, object>>();
                    foreach (var item in enumerable)
                    {
                        if (item != null)
                        {
                            list.Add(SerializeComplexTypeFromObject(item));
                        }
                    }
                    dict[prop.Name] = list;
                }
                else if (arrayType == ArrayOfAnnotations.ArrayType.GeoPoint)
                {
                    // List<GeoLocation> → List<GeoPoint>
                    var list = new List<Google.Cloud.Firestore.GeoPoint>();
                    foreach (var item in enumerable)
                    {
                        if (item != null)
                        {
                            list.Add(ConvertToFirestoreGeoPoint(item));
                        }
                    }
                    dict[prop.Name] = list;
                }
                else if (arrayType == ArrayOfAnnotations.ArrayType.Reference)
                {
                    // List<Entity> → List<DocumentReference>
                    var elementType = entityType.GetArrayOfElementClrType(prop.Name);
                    if (elementType == null)
                        continue;

                    var referencedEntityType = _model.FindEntityType(elementType);
                    if (referencedEntityType == null)
                        continue;

                    var idProperty = referencedEntityType.FindPrimaryKey()?.Properties.FirstOrDefault();
                    if (idProperty == null)
                        continue;

                    var collectionName = _collectionManager.GetCollectionName(elementType);
                    var list = new List<Google.Cloud.Firestore.DocumentReference>();

                    foreach (var item in enumerable)
                    {
                        if (item == null)
                            continue;

                        var idValue = idProperty.PropertyInfo?.GetValue(item);
                        if (idValue == null || IsDefaultValue(idValue, idProperty.ClrType))
                            continue;

                        var docRef = _firestoreClient.Database
                            .Collection(collectionName)
                            .Document(idValue.ToString()!);
                        list.Add(docRef);
                    }
                    dict[prop.Name] = list;
                }
            }
        }

        // ✅ Serializar referencias de entidades individuales - DETECCIÓN AUTOMÁTICA
        private void SerializeEntityReferences(
            IEntityType entityType,
            object entity,
            Dictionary<string, object> dict)
        {
            foreach (var navigation in entityType.GetNavigations())
            {
                // Omitir colecciones (se manejan en SerializeEntityReferenceCollections)
                if (navigation.IsCollection) continue;

                // ⚠️ Omitir navegaciones inversas de ArrayOf References
                // Si la entidad destino tiene un ArrayOf Reference que apunta a esta entidad,
                // esta navegación es una FK inversa automática de EF Core que no debe serializarse
                if (IsInverseOfArrayOfReference(navigation))
                    continue;

                var relatedEntity = navigation.PropertyInfo?.GetValue(entity);
                if (relatedEntity == null) continue;

                // ✅ SIEMPRE usa la clave primaria (Id)
                var relatedEntityType = navigation.TargetEntityType;
                var primaryKey = relatedEntityType.FindPrimaryKey()?.Properties.FirstOrDefault();

                if (primaryKey == null)
                    throw new InvalidOperationException(
                        $"Entity type '{relatedEntityType.Name}' has no primary key defined.");

                var relatedId = primaryKey.PropertyInfo?.GetValue(relatedEntity);
                if (relatedId == null || IsDefaultValue(relatedId, primaryKey.ClrType))
                    continue; // Omitir si no tiene ID

                // Obtener nombre de la colección
                var collectionName = _collectionManager.GetCollectionName(relatedEntityType.ClrType);

                // Crear DocumentReference
                dict[navigation.Name] = _firestoreClient.Database
                    .Collection(collectionName)
                    .Document(relatedId.ToString()!);
            }
        }

        /// <summary>
        /// Verifica si una navegación es la inversa de un ArrayOf Reference.
        /// Busca en la entidad destino si hay alguna propiedad de colección
        /// del tipo de la entidad actual configurada como ArrayOf Reference.
        /// </summary>
        private static bool IsInverseOfArrayOfReference(INavigation navigation)
        {
            // Solo aplica a navegaciones de referencia (no colecciones)
            if (navigation.IsCollection)
                return false;

            var targetEntityType = navigation.TargetEntityType;
            var sourceClrType = navigation.DeclaringEntityType.ClrType;

            // Buscar en targetEntityType todas las propiedades ArrayOf Reference
            // que apunten al tipo de la entidad actual
            foreach (var prop in targetEntityType.ClrType.GetProperties())
            {
                // Verificar si es una colección
                if (!Metadata.Conventions.ConventionHelpers.IsGenericCollection(prop.PropertyType))
                    continue;

                // Verificar si el elemento de la colección es del tipo de la entidad actual
                var elementType = Metadata.Conventions.ConventionHelpers.GetCollectionElementType(prop.PropertyType);
                if (elementType != sourceClrType)
                    continue;

                // Verificar si está configurada como ArrayOf Reference
                var arrayType = targetEntityType.GetArrayOfType(prop.Name);
                if (arrayType == Metadata.Conventions.ArrayOfAnnotations.ArrayType.Reference)
                {
                    return true;
                }
            }

            return false;
        }

        // ✅ Serializar colecciones de referencias de entidades
        private void SerializeEntityReferenceCollections(
            IEntityType entityType,
            object entity,
            Dictionary<string, object> dict)
        {
            // Obtener todas las navegaciones de colección de EF Core
            foreach (var navigation in entityType.GetNavigations())
            {
                if (!navigation.IsCollection) continue;

                // ✅ NUEVO: Omitir subcollections (se guardan en paths separados)
                if (navigation.IsSubCollection())
                    continue;

                var collectionValue = navigation.PropertyInfo?.GetValue(entity);
                if (collectionValue == null) continue;

                var references = CreateReferenceArray(navigation, collectionValue);
                if (references.Length > 0)
                {
                    dict[navigation.Name] = references;
                }
            }
        }

        // ✅ Crear array de DocumentReferences desde una colección de entidades
        private Google.Cloud.Firestore.DocumentReference[] CreateReferenceArray(
            INavigation navigation,
            object collection)
        {
            var targetEntityType = navigation.TargetEntityType;
            var collectionName = _collectionManager.GetCollectionName(targetEntityType.ClrType);
            var references = new List<Google.Cloud.Firestore.DocumentReference>();

            // Obtener la clave primaria de la entidad objetivo
            var idProperty = targetEntityType.FindPrimaryKey()?.Properties.FirstOrDefault();
            if (idProperty == null)
                throw new InvalidOperationException(
                    $"Entity type '{targetEntityType.Name}' has no primary key defined.");

            foreach (var item in (IEnumerable)collection)
            {
                if (item == null) continue;

                // Obtener el ID de la entidad
                var idValue = idProperty.PropertyInfo?.GetValue(item);
                if (idValue == null || IsDefaultValue(idValue, idProperty.ClrType))
                    continue; // Omitir entidades sin ID

                var documentId = idValue.ToString()!;
                var docRef = _firestoreClient.Database
                    .Collection(collectionName)
                    .Document(documentId);

                references.Add(docRef);
            }

            return references.ToArray();
        }

        private Dictionary<string, object> SerializeComplexTypeFromObject(object complexObject)
        {
            var dict = new Dictionary<string, object>();
            var type = complexObject.GetType();

            foreach (var prop in type.GetProperties())
            {
                var value = prop.GetValue(complexObject);
                if (value == null) continue;

                // ✅ Manejar listas anidadas recursivamente
                if (value is IEnumerable enumerable && value is not string)
                {
                    var serializedList = SerializeNestedList(enumerable, prop.PropertyType);
                    if (serializedList != null)
                        dict[prop.Name] = serializedList;
                }
                else if (value.GetType().IsClass && value is not string)
                {
                    // ✅ Verificar si es una entidad (referencia)
                    var entityType = _model.FindEntityType(value.GetType());
                    if (entityType != null)
                    {
                        var docRef = CreateDocumentReference(value, entityType);
                        if (docRef != null)
                            dict[prop.Name] = docRef;
                    }
                    else
                    {
                        dict[prop.Name] = SerializeComplexTypeFromObject(value);
                    }
                }
                else
                {
                    // ✅ Aplicar conversión de tipos (DateTime→UTC, decimal→double, enum→string)
                    var convertedValue = _valueConverter.ToFirestore(value);
                    if (convertedValue != null)
                        dict[prop.Name] = convertedValue;
                }
            }

            return dict;
        }

        /// <summary>
        /// Serializa una lista anidada dentro de un ComplexType.
        /// Detecta automáticamente si es List de ComplexTypes, GeoPoints o References.
        /// </summary>
        private object? SerializeNestedList(IEnumerable enumerable, Type propertyType)
        {
            // Obtener el tipo de elemento de la lista
            var elementType = GetEnumerableElementType(propertyType);
            if (elementType == null)
                return null;

            // ✅ Caso 1: Es una entidad registrada → List<DocumentReference>
            var entityType = _model.FindEntityType(elementType);
            if (entityType != null)
            {
                var refList = new List<Google.Cloud.Firestore.DocumentReference>();
                foreach (var item in enumerable)
                {
                    if (item == null) continue;
                    var docRef = CreateDocumentReference(item, entityType);
                    if (docRef != null)
                        refList.Add(docRef);
                }
                return refList;
            }

            // ✅ Caso 2: Tiene propiedades Latitude/Longitude → List<GeoPoint>
            if (HasGeoPointProperties(elementType))
            {
                var geoList = new List<Google.Cloud.Firestore.GeoPoint>();
                foreach (var item in enumerable)
                {
                    if (item != null)
                        geoList.Add(ConvertToFirestoreGeoPoint(item));
                }
                return geoList;
            }

            // ✅ Caso 3: Es un ComplexType → List<Dictionary<string, object>>
            if (elementType.IsClass && elementType != typeof(string))
            {
                var mapList = new List<Dictionary<string, object>>();
                foreach (var item in enumerable)
                {
                    if (item != null)
                        mapList.Add(SerializeComplexTypeFromObject(item));
                }
                return mapList;
            }

            // Caso 4: Tipos primitivos - devolver tal cual
            return enumerable;
        }

        /// <summary>
        /// Obtiene el tipo de elemento de un IEnumerable genérico.
        /// </summary>
        private static Type? GetEnumerableElementType(Type type)
        {
            if (type.IsGenericType)
            {
                var genericArgs = type.GetGenericArguments();
                if (genericArgs.Length == 1)
                    return genericArgs[0];
            }

            // Buscar en interfaces
            foreach (var iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return iface.GetGenericArguments()[0];
            }

            return null;
        }

        /// <summary>
        /// Verifica si un tipo tiene propiedades Latitude y Longitude.
        /// </summary>
        private static bool HasGeoPointProperties(Type type)
        {
            var latProp = type.GetProperty("Latitude");
            var lngProp = type.GetProperty("Longitude");
            return latProp != null && lngProp != null &&
                   (latProp.PropertyType == typeof(double) || latProp.PropertyType == typeof(float)) &&
                   (lngProp.PropertyType == typeof(double) || lngProp.PropertyType == typeof(float));
        }

        /// <summary>
        /// Crea un DocumentReference a partir de una entidad.
        /// </summary>
        private Google.Cloud.Firestore.DocumentReference? CreateDocumentReference(object entity, IEntityType entityType)
        {
            var primaryKey = entityType.FindPrimaryKey()?.Properties.FirstOrDefault();
            if (primaryKey == null) return null;

            var idValue = primaryKey.PropertyInfo?.GetValue(entity);
            if (idValue == null || IsDefaultValue(idValue, primaryKey.ClrType)) return null;

            var collectionName = _collectionManager.GetCollectionName(entityType.ClrType);
            return _firestoreClient.Database
                .Collection(collectionName)
                .Document(idValue.ToString()!);
        }

        private Dictionary<string, object> SerializeComplexType(object complexObject, IComplexType complexType)
        {
            var dict = new Dictionary<string, object>();

            // Serializar propiedades simples del complex type
            SerializeProperties(complexType, prop => prop.PropertyInfo?.GetValue(complexObject), dict);

            // Serializar Complex Properties anidados (recursivo)
            SerializeComplexProperties(complexType, complexObject, dict);

            // ✅ Detectar y serializar referencias a entidades dentro del ComplexType
            SerializeNestedEntityReferences(complexObject, dict);

            return dict;
        }

        // ✅ Serializar referencias a entidades dentro de ComplexTypes
        private void SerializeNestedEntityReferences(object complexObject, Dictionary<string, object> dict)
        {
            var type = complexObject.GetType();

            foreach (var prop in type.GetProperties())
            {
                var propValue = prop.GetValue(complexObject);
                if (propValue == null) continue;

                var propType = prop.PropertyType;

                // ✅ Detectar si la propiedad es una entidad (tiene DbSet registrado)
                var entityType = _model.FindEntityType(propType);
                if (entityType != null)
                {
                    // Es una referencia a entidad - convertir a DocumentReference
                    var primaryKey = entityType.FindPrimaryKey()?.Properties.FirstOrDefault();
                    if (primaryKey == null)
                        throw new InvalidOperationException(
                            $"Entity type '{entityType.Name}' has no primary key defined.");

                    var relatedId = primaryKey.PropertyInfo?.GetValue(propValue);
                    if (relatedId == null || IsDefaultValue(relatedId, primaryKey.ClrType))
                        continue;

                    var collectionName = _collectionManager.GetCollectionName(propType);
                    dict[prop.Name] = _firestoreClient.Database
                        .Collection(collectionName)
                        .Document(relatedId.ToString()!);
                }
            }
        }

        private object? ApplyConverter(IProperty property, object value)
        {
            // ✅ Si es colección, ignorar el converter de EF Core
            if (value is IEnumerable enumerable && value is not string && value is not byte[])
            {
                return ConvertCollection(property, enumerable);
            }

            // ✅ Para otros tipos, buscar converter en property O en typeMapping
            var converter = property.GetValueConverter() ?? property.GetTypeMapping()?.Converter;

            if (converter != null)
            {
                return converter.ConvertToProvider(value);
            }

            // ✅ Fallback: usar _valueConverter para DateTime→UTC, decimal→double, enum→string
            return _valueConverter.ToFirestore(value);
        }

        private Google.Cloud.Firestore.DocumentReference ConvertToFirestoreReference(
            object value,
            string? propertyName)
        {
            var type = value.GetType();

            // Buscar la propiedad especificada o la PK del modelo
            PropertyInfo? property;
            if (propertyName != null)
            {
                property = type.GetProperty(propertyName);
                if (property == null)
                    throw new InvalidOperationException(
                        $"El tipo '{type.Name}' no tiene una propiedad '{propertyName}'");
            }
            else
            {
                // Use EF Core metadata to find the primary key - the authoritative source
                var entityType = _model.FindEntityType(type);
                if (entityType != null)
                {
                    var pkProperties = entityType.FindPrimaryKey()?.Properties;
                    var pkPropertyName = pkProperties is { Count: > 0 } ? pkProperties[0].Name : null;
                    if (pkPropertyName != null)
                    {
                        property = type.GetProperty(pkPropertyName);
                    }
                    else
                    {
                        property = null;
                    }
                }
                else
                {
                    // Fallback for types not registered in model (ComplexTypes)
                    // Try convention: "Id" or "{TypeName}Id"
                    property = type.GetProperty("Id") ?? type.GetProperty($"{type.Name}Id");
                }

                if (property == null)
                    throw new InvalidOperationException(
                        $"El tipo '{type.Name}' debe tener una clave primaria configurada o especificar una con HasReference(x => x.Property)");
            }

            var propertyValue = property.GetValue(value)?.ToString();
            if (string.IsNullOrEmpty(propertyValue))
                throw new InvalidOperationException($"La propiedad '{property.Name}' no puede ser null o vacía para una referencia");

            // Obtener nombre de la colección
            var collectionName = GetCollectionName(type);

            // Crear la referencia
            return _firestoreClient.Database.Collection(collectionName).Document(propertyValue);
        }

        private string GetCollectionName(Type type)
        {
            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            return tableAttr?.Name ?? type.Name.ToLowerInvariant() + "s";
        }

        private Google.Cloud.Firestore.GeoPoint ConvertToFirestoreGeoPoint(object value)
        {
            var type = value.GetType();

            var latProp = FindLatitudeProperty(type);
            var lonProp = FindLongitudeProperty(type);

            var lat = (double)latProp.GetValue(value)!;
            var lon = (double)lonProp.GetValue(value)!;

            return new Google.Cloud.Firestore.GeoPoint(lat, lon);
        }

        private PropertyInfo FindLatitudeProperty(Type type)
        {
            return type.GetProperty("Latitude")
                ?? type.GetProperty("Latitud")
                ?? throw new InvalidOperationException(
                    $"El tipo '{type.Name}' debe tener una propiedad 'Latitude' o 'Latitud' para usar HasGeoPoint()");
        }

        private PropertyInfo FindLongitudeProperty(Type type)
        {
            return type.GetProperty("Longitude")
                ?? type.GetProperty("Longitud")
                ?? throw new InvalidOperationException(
                    $"El tipo '{type.Name}' debe tener una propiedad 'Longitude' o 'Longitud' para usar HasGeoPoint()");
        }

        private object ConvertCollection(IProperty property, IEnumerable collection)
        {
            var elementType = property.ClrType.GetGenericArguments().FirstOrDefault()
                              ?? property.ClrType.GetElementType();

            if (elementType == null)
                return collection;

            // ✅ Para decimal → double
            if (elementType == typeof(decimal))
            {
                return collection.Cast<decimal>().Select(d => (double)d).ToList();
            }

            // ✅ Para enum → string
            if (elementType.IsEnum)
            {
                return collection.Cast<object>().Select(e => e.ToString()!).ToList();
            }

            return collection;
        }

        private CoreTypeMapping? FindMappingForType(Type type)
        {
            return _typeMappingSource.FindMapping(type);
        }

        private void UpdateEntityIdIfNeeded(IUpdateEntry entry, string documentId)
        {
            var keyProperty = entry.EntityType.FindPrimaryKey()?.Properties.FirstOrDefault();
            if (keyProperty != null)
            {
                var currentValue = entry.GetCurrentValue(keyProperty);
                if (currentValue == null || IsDefaultValue(currentValue, keyProperty.ClrType))
                {
                    entry.SetStoreGeneratedValue(keyProperty, documentId);
                }
            }
        }

        private static bool IsDefaultValue(object value, Type type)
        {
            if (value == null) return true;
            if (type.IsValueType)
            {
                var defaultValue = Activator.CreateInstance(type);
                return value.Equals(defaultValue);
            }
            return false;
        }

        /// <summary>
        /// Extrae la ruta relativa del documento desde el path completo de Firestore.
        /// Convierte "projects/{project}/databases/(default)/documents/Categories" en "Categories".
        /// </summary>
        private static string GetRelativeCollectionPath(string fullPath)
        {
            const string documentsMarker = "/documents/";
            var index = fullPath.IndexOf(documentsMarker, StringComparison.Ordinal);
            if (index >= 0)
            {
                return fullPath.Substring(index + documentsMarker.Length);
            }
            return fullPath;
        }

        // ========================================================================
        // ✅ FUNCIONALIDADES PARA RELACIONES N:M (SKIP NAVIGATIONS)
        // ========================================================================

        // ✅ Detectar si es una entidad intermedia generada automáticamente
        private bool IsJoinEntity(IEntityType entityType)
        {
            // Las entidades intermedias tienen 2+ foreign keys y no tienen propiedades propias
            var foreignKeys = entityType.GetForeignKeys().ToList();

            if (foreignKeys.Count < 2)
                return false;

            // Verificar que tiene solo FK properties (y la PK)
            var properties = entityType.GetProperties().ToList();
            var fkProperties = foreignKeys.SelectMany(fk => fk.Properties).ToHashSet();
            var pkProperties = entityType.FindPrimaryKey()?.Properties.ToHashSet() ?? new HashSet<IProperty>();

            // Si todas las propiedades son FKs o PKs, es una join entity
            return properties.All(p => fkProperties.Contains(p) || pkProperties.Contains(p));
        }

        // ✅ Gestionar tablas intermedias para relaciones N:M
        private async Task ProcessSkipNavigationChanges(
            IList<IUpdateEntry> entries,
            CancellationToken cancellationToken)
        {
            foreach (var entry in entries)
            {
                // Solo procesar entidades Added o Modified
                if (entry.EntityState != EntityState.Added &&
                    entry.EntityState != EntityState.Modified)
                    continue;

                var entityType = entry.EntityType;
                var skipNavigations = GetSkipNavigations(entityType);

                foreach (var skipNavigation in skipNavigations)
                {
                    await ProcessSkipNavigationForEntity(
                        entry,
                        skipNavigation,
                        cancellationToken);
                }
            }
        }

        // ✅ Obtener skip navigations (relaciones N:M) de una entidad
        private IEnumerable<ISkipNavigation> GetSkipNavigations(IEntityType entityType)
        {
            return entityType.GetSkipNavigations();
        }

        // ✅ Procesar cambios en una skip navigation específica
        private async Task ProcessSkipNavigationForEntity(
            IUpdateEntry entry,
            ISkipNavigation skipNavigation,
            CancellationToken cancellationToken)
        {
            var entity = entry.ToEntityEntry().Entity;
            var currentCollection = skipNavigation.PropertyInfo?.GetValue(entity) as IEnumerable;

            if (currentCollection == null)
                return;

            // Obtener colección original (antes de cambios)
            var originalCollection = entry.EntityState == EntityState.Added
                ? Enumerable.Empty<object>()
                : GetOriginalCollection(entry, skipNavigation);

            var currentIds = GetEntityIds(currentCollection, skipNavigation.TargetEntityType).ToHashSet();
            var originalIds = GetEntityIds(originalCollection, skipNavigation.TargetEntityType).ToHashSet();

            // Detectar añadidos y eliminados
            var addedIds = currentIds.Except(originalIds).ToList();
            var removedIds = originalIds.Except(currentIds).ToList();

            if (addedIds.Count == 0 && removedIds.Count == 0)
                return;

            // Obtener ID de la entidad principal
            var principalId = GetEntityId(entry);

            // Obtener nombre de la colección intermedia
            var joinCollectionName = GetJoinCollectionName(skipNavigation);

            // Añadir nuevas relaciones
            foreach (var targetId in addedIds)
            {
                await AddJoinDocument(
                    joinCollectionName,
                    principalId,
                    targetId,
                    skipNavigation,
                    cancellationToken);
            }

            // Eliminar relaciones
            foreach (var targetId in removedIds)
            {
                await RemoveJoinDocument(
                    joinCollectionName,
                    principalId,
                    targetId,
                    skipNavigation,
                    cancellationToken);
            }
        }

        // ✅ Obtener colección original del ChangeTracker
        private IEnumerable<object> GetOriginalCollection(
            IUpdateEntry entry,
            ISkipNavigation skipNavigation)
        {
            try
            {
                var entityEntry = entry.ToEntityEntry();
                var collectionEntry = entityEntry.Collection(skipNavigation.Name);

                if (!collectionEntry.IsModified)
                    return Enumerable.Empty<object>();

                var currentValue = collectionEntry.CurrentValue as IEnumerable<object>;
                return currentValue ?? Enumerable.Empty<object>();
            }
            catch
            {
                return Enumerable.Empty<object>();
            }
        }

        // ✅ Obtener IDs de una colección de entidades
        private IEnumerable<string> GetEntityIds(
            IEnumerable collection,
            IEntityType entityType)
        {
            var idProperty = entityType.FindPrimaryKey()?.Properties.FirstOrDefault();
            if (idProperty == null)
                yield break;

            foreach (var item in collection)
            {
                if (item == null) continue;

                var id = idProperty.PropertyInfo?.GetValue(item);
                if (id != null && !IsDefaultValue(id, idProperty.ClrType))
                {
                    yield return id.ToString()!;
                }
            }
        }

        // ✅ Obtener ID de una entidad
        private string GetEntityId(IUpdateEntry entry)
        {
            var keyProperty = entry.EntityType.FindPrimaryKey()?.Properties.FirstOrDefault();
            if (keyProperty == null)
                throw new InvalidOperationException("Entity has no primary key");

            var id = entry.GetCurrentValue(keyProperty);
            if (id == null || IsDefaultValue(id, keyProperty.ClrType))
                throw new InvalidOperationException("Entity ID is not set");

            return id.ToString()!;
        }

        // ✅ Generar nombre de colección intermedia (pluralizado y en orden)
        private string GetJoinCollectionName(ISkipNavigation skipNavigation)
        {
            // Obtener nombres pluralizados de las colecciones (ya vienen procesados por _collectionManager)
            var leftCollection = _collectionManager.GetCollectionName(skipNavigation.DeclaringEntityType.ClrType);
            var rightCollection = _collectionManager.GetCollectionName(skipNavigation.TargetEntityType.ClrType);

            // Mantener orden: Principal (DeclaringEntityType) primero
            return $"{leftCollection}{rightCollection}";
        }

        // ✅ Añadir documento en tabla intermedia
        private async Task AddJoinDocument(
            string joinCollectionName,
            string principalId,
            string targetId,
            ISkipNavigation skipNavigation,
            CancellationToken cancellationToken)
        {
            var leftEntity = skipNavigation.DeclaringEntityType.ClrType.Name;
            var rightEntity = skipNavigation.TargetEntityType.ClrType.Name;

            var leftCollection = _collectionManager.GetCollectionName(skipNavigation.DeclaringEntityType.ClrType);
            var rightCollection = _collectionManager.GetCollectionName(skipNavigation.TargetEntityType.ClrType);

            // ✅ ID determinístico: principal_target (sin ordenar)
            var documentId = $"{principalId}_{targetId}";

            var document = new Dictionary<string, object>
            {
                [$"{leftEntity}Id"] = _firestoreClient.Database
                    .Collection(leftCollection)
                    .Document(principalId),
                [$"{rightEntity}Id"] = _firestoreClient.Database
                    .Collection(rightCollection)
                    .Document(targetId),
                ["_createdAt"] = DateTime.UtcNow,
                ["_updatedAt"] = DateTime.UtcNow
            };

            await _firestoreClient.SetDocumentAsync(
                joinCollectionName,
                documentId,
                document,
                cancellationToken);
        }

        // ✅ Eliminar documento de tabla intermedia
        private async Task RemoveJoinDocument(
            string joinCollectionName,
            string principalId,
            string targetId,
            ISkipNavigation skipNavigation,
            CancellationToken cancellationToken)
        {
            // ✅ ID determinístico: principal_target (igual que AddJoinDocument)
            var documentId = $"{principalId}_{targetId}";

            await _firestoreClient.DeleteDocumentAsync(
                joinCollectionName,
                documentId,
                cancellationToken);
        }
    }
}