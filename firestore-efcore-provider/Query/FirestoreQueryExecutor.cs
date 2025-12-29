using Firestore.EntityFrameworkCore.Query.Ast;
using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Extensions;
using Firestore.EntityFrameworkCore.Metadata.Conventions;

namespace Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Ejecuta queries de Firestore construyendo Google.Cloud.Firestore.Query
    /// desde FirestoreQueryExpression y retornando QuerySnapshot.
    /// </summary>
    public class FirestoreQueryExecutor : IFirestoreQueryExecutor
    {
        private readonly IFirestoreClientWrapper _client;
        private readonly IFirestoreDocumentDeserializer _deserializer;
        private readonly IFirestoreCollectionManager _collectionManager;
        private readonly ILogger<FirestoreQueryExecutor> _logger;

        public FirestoreQueryExecutor(
            IFirestoreClientWrapper client,
            IFirestoreDocumentDeserializer deserializer,
            IFirestoreCollectionManager collectionManager,
            ILogger<FirestoreQueryExecutor> logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _deserializer = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
            _collectionManager = collectionManager ?? throw new ArgumentNullException(nameof(collectionManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public IFirestoreDocumentDeserializer Deserializer => _deserializer;

        /// <summary>
        /// Ejecuta una query y retorna DocumentSnapshots para proyecciones.
        /// Usado por FirestoreProjectionQueryingEnumerable para aplicar shapers.
        /// </summary>
        public async IAsyncEnumerable<DocumentSnapshot> ExecuteQueryForDocumentsAsync(
            FirestoreQueryExpression queryExpression,
            QueryContext queryContext,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(queryExpression);

            _logger.LogInformation("=== Executing Firestore query for documents ===");
            _logger.LogInformation("Collection: {Collection}", queryExpression.CollectionName);

            // Manejar queries por ID
            if (queryExpression.IsIdOnlyQuery)
            {
                var doc = await ExecuteIdQueryForDocumentAsync(queryExpression, queryContext, cancellationToken);
                if (doc != null && doc.Exists)
                {
                    yield return doc;
                }
                yield break;
            }

            // Query normal (no es por ID)
            _logger.LogInformation("Filters count: {Count}", queryExpression.Filters.Count);

            // Construir Google.Cloud.Firestore.Query
            var query = BuildQuery(queryExpression, queryContext);

            // Ejecutar
            var snapshot = await _client.ExecuteQueryAsync(query, cancellationToken);

            _logger.LogInformation("Query returned {Count} documents", snapshot.Count);

            // Calcular Skip para aplicar en memoria
            int skipValue = 0;
            if (queryExpression.Skip.HasValue)
            {
                skipValue = queryExpression.Skip.Value;
            }
            else if (queryExpression.SkipExpression != null)
            {
                skipValue = EvaluateIntExpression(queryExpression.SkipExpression, queryContext);
            }

            int currentIndex = 0;
            foreach (var doc in snapshot.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Aplicar Skip en memoria
                if (currentIndex < skipValue)
                {
                    currentIndex++;
                    continue;
                }

                if (doc.Exists)
                {
                    yield return doc;
                }

                currentIndex++;
            }
        }

        /// <summary>
        /// Ejecuta una query por ID y retorna el DocumentSnapshot para proyecciones.
        /// </summary>
        public async Task<DocumentSnapshot?> ExecuteIdQueryForDocumentAsync(
            FirestoreQueryExpression queryExpression,
            QueryContext queryContext,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(queryExpression);

            if (!queryExpression.IsIdOnlyQuery)
            {
                throw new InvalidOperationException(
                    "ExecuteIdQueryForDocumentAsync can only be called for ID-only queries. " +
                    "Use ExecuteQueryForDocumentsAsync for regular queries.");
            }

            _logger.LogInformation("=== Executing Firestore ID query for document ===");
            _logger.LogInformation("Collection: {Collection}", queryExpression.CollectionName);

            // Evaluar la expresión del ID en runtime
            var idValueExpression = queryExpression.IdValueExpression!;
            var idValue = EvaluateIdExpression(idValueExpression, queryContext);

            if (idValue == null)
            {
                throw new InvalidOperationException("ID value cannot be null in an ID-only query");
            }

            var idString = idValue.ToString();
            _logger.LogInformation("Getting document by ID: {Id}", idString);

            // Usar GetDocumentAsync porque el ID es metadata del documento
            var documentSnapshot = await _client.GetDocumentAsync(
                queryExpression.CollectionName,
                idString!,
                cancellationToken);

            if (documentSnapshot != null && documentSnapshot.Exists)
            {
                _logger.LogInformation("Document found with ID: {Id}", idString);
                return documentSnapshot;
            }
            else
            {
                _logger.LogInformation("Document not found with ID: {Id}", idString);
                return null;
            }
        }

        /// <summary>
        /// Ejecuta una query y retorna entidades deserializadas con navegaciones cargadas.
        /// Este método encapsula toda la lógica de ejecución, deserialización y carga de includes.
        /// </summary>
        public async IAsyncEnumerable<T> ExecuteQueryAsync<T>(
            FirestoreQueryExpression queryExpression,
            Microsoft.EntityFrameworkCore.Query.QueryContext queryContext,
            DbContext dbContext,
            bool isTracking,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : class
        {
            ArgumentNullException.ThrowIfNull(queryExpression);
            ArgumentNullException.ThrowIfNull(dbContext);

            _logger.LogInformation("=== Executing Firestore query (generic) ===");
            _logger.LogInformation("Collection: {Collection}, EntityType: {EntityType}",
                queryExpression.CollectionName, typeof(T).Name);

            // Manejar queries por ID
            if (queryExpression.IsIdOnlyQuery)
            {
                await foreach (var entity in ExecuteIdQueryInternalAsync<T>(
                    queryExpression, queryContext, dbContext, isTracking, cancellationToken))
                {
                    yield return entity;
                }
                yield break;
            }

            // Construir y ejecutar la query normal
            var query = BuildQuery(queryExpression, queryContext);
            var snapshot = await _client.ExecuteQueryAsync(query, cancellationToken);

            _logger.LogInformation("Query returned {Count} documents", snapshot.Count);

            // Calcular Skip para aplicar en memoria
            int skipValue = 0;
            if (queryExpression.Skip.HasValue)
            {
                skipValue = queryExpression.Skip.Value;
            }
            else if (queryExpression.SkipExpression != null)
            {
                skipValue = EvaluateIntExpression(queryExpression.SkipExpression, queryContext);
            }

            var serviceProvider = ((IInfrastructure<IServiceProvider>)dbContext).Instance;
            var model = dbContext.Model;
            var stateManager = dbContext.GetService<IStateManager>();
            var entityType = model.FindEntityType(typeof(T));
            var key = entityType?.FindPrimaryKey();

            int currentIndex = 0;
            foreach (var doc in snapshot.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Aplicar Skip en memoria
                if (currentIndex < skipValue)
                {
                    currentIndex++;
                    continue;
                }

                if (!doc.Exists)
                    continue;

                // Identity Resolution: verificar si la entidad ya está trackeada
                T? entity = null;
                if (isTracking && key != null)
                {
                    entity = TryGetTrackedEntity<T>(stateManager, key, doc.Id);
                }

                // Si no está trackeada, deserializar
                if (entity == null)
                {
                    entity = _deserializer.DeserializeEntity<T>(doc, dbContext, serviceProvider);

                    // Cargar navegaciones (SubCollections y DocumentReferences)
                    if (queryExpression.PendingIncludes.Count > 0)
                    {
                        await LoadIncludesAsync(
                            entity,
                            doc,
                            queryExpression.PendingIncludes,
                            model,
                            isTracking,
                            dbContext,
                            queryContext,
                            cancellationToken);
                    }

                    // Cargar includes en ComplexTypes
                    if (queryExpression.ComplexTypeIncludes.Count > 0)
                    {
                        await LoadComplexTypeIncludesAsync(
                            entity,
                            doc,
                            queryExpression.ComplexTypeIncludes,
                            model,
                            isTracking,
                            dbContext,
                            cancellationToken);
                    }

                    // Adjuntar al ChangeTracker como Unchanged
                    if (isTracking)
                    {
                        dbContext.Attach(entity);

                        // Establecer shadow FK properties para navegaciones con DocumentReference
                        SetShadowForeignKeys(entity, doc, model.FindEntityType(typeof(T))!, dbContext);
                    }
                }

                currentIndex++;
                yield return entity;
            }
        }

        /// <summary>
        /// Ejecuta una query por ID y retorna la entidad deserializada con navegaciones cargadas.
        /// </summary>
        public async Task<T?> ExecuteIdQueryAsync<T>(
            FirestoreQueryExpression queryExpression,
            QueryContext queryContext,
            DbContext dbContext,
            bool isTracking,
            CancellationToken cancellationToken = default) where T : class
        {
            ArgumentNullException.ThrowIfNull(queryExpression);
            ArgumentNullException.ThrowIfNull(dbContext);

            if (!queryExpression.IsIdOnlyQuery)
            {
                throw new InvalidOperationException(
                    "ExecuteIdQueryAsync<T> can only be called for ID-only queries. " +
                    "Use ExecuteQueryAsync<T> for regular queries.");
            }

            _logger.LogInformation("Executing ID-only query (generic) for type {EntityType}", typeof(T).Name);

            // Obtener el documento por ID
            var doc = await ExecuteIdQueryForDocumentAsync(queryExpression, queryContext, cancellationToken);

            if (doc == null || !doc.Exists)
            {
                _logger.LogInformation("Document not found");
                return null;
            }

            var serviceProvider = ((IInfrastructure<IServiceProvider>)dbContext).Instance;
            var model = dbContext.Model;
            var stateManager = dbContext.GetService<IStateManager>();
            var entityType = model.FindEntityType(typeof(T));
            var key = entityType?.FindPrimaryKey();

            // Identity Resolution: verificar si la entidad ya está trackeada
            T? entity = null;
            if (isTracking && key != null)
            {
                entity = TryGetTrackedEntity<T>(stateManager, key, doc.Id);
            }

            // Si no está trackeada, deserializar
            if (entity == null)
            {
                entity = _deserializer.DeserializeEntity<T>(doc, dbContext, serviceProvider);

                // Cargar navegaciones (SubCollections y DocumentReferences)
                if (queryExpression.PendingIncludes.Count > 0)
                {
                    await LoadIncludesAsync(
                        entity,
                        doc,
                        queryExpression.PendingIncludes,
                        model,
                        isTracking,
                        dbContext,
                        queryContext,
                        cancellationToken);
                }

                // Cargar includes en ComplexTypes
                if (queryExpression.ComplexTypeIncludes.Count > 0)
                {
                    await LoadComplexTypeIncludesAsync(
                        entity,
                        doc,
                        queryExpression.ComplexTypeIncludes,
                        model,
                        isTracking,
                        dbContext,
                        cancellationToken);
                }

                // Adjuntar al ChangeTracker como Unchanged
                if (isTracking)
                {
                    dbContext.Attach(entity);

                    // Establecer shadow FK properties para navegaciones con DocumentReference
                    SetShadowForeignKeys(entity, doc, model.FindEntityType(typeof(T))!, dbContext);
                }
            }

            return entity;
        }

        /// <summary>
        /// Ejecuta una query por ID y retorna la entidad con navegaciones cargadas.
        /// Usado internamente para convertir a IAsyncEnumerable.
        /// </summary>
        private async IAsyncEnumerable<T> ExecuteIdQueryInternalAsync<T>(
            FirestoreQueryExpression queryExpression,
            QueryContext queryContext,
            DbContext dbContext,
            bool isTracking,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken) where T : class
        {
            var entity = await ExecuteIdQueryAsync<T>(queryExpression, queryContext, dbContext, isTracking, cancellationToken);
            if (entity != null)
            {
                yield return entity;
            }
        }

        /// <summary>
        /// Identity Resolution: busca si la entidad ya está siendo trackeada usando IStateManager.
        /// Usa O(1) lookup por clave primaria.
        /// </summary>
        private T? TryGetTrackedEntity<T>(IStateManager stateManager, IKey key, string documentId) where T : class
        {
            if (key.Properties.Count == 0) return null;

            var keyProperty = key.Properties[0];
            var convertedKey = ConvertKeyValue(documentId, keyProperty);
            var keyValues = new object[] { convertedKey };

            var entry = stateManager.TryGetEntry(key, keyValues);
            return entry?.Entity as T;
        }

        /// <summary>
        /// Convierte el ID de Firestore (siempre string) al tipo de la clave primaria.
        /// </summary>
        private static object ConvertKeyValue(string firestoreId, IReadOnlyProperty keyProperty)
        {
            var targetType = keyProperty.ClrType;

            // Usar ValueConverter si está configurado
            var converter = keyProperty.GetValueConverter();
            if (converter != null)
            {
                return converter.ConvertFromProvider(firestoreId)!;
            }

            // Conversión estándar por tipo
            if (targetType == typeof(string)) return firestoreId;
            if (targetType == typeof(int)) return int.Parse(firestoreId);
            if (targetType == typeof(long)) return long.Parse(firestoreId);
            if (targetType == typeof(Guid)) return Guid.Parse(firestoreId);

            return Convert.ChangeType(firestoreId, targetType);
        }

        #region Include Loading

        /// <summary>
        /// Carga las navegaciones (Includes) para una entidad.
        /// </summary>
        private async Task LoadIncludesAsync<T>(
            T entity,
            DocumentSnapshot documentSnapshot,
            IReadOnlyList<IncludeInfo> allIncludes,
            IModel model,
            bool isTracking,
            DbContext dbContext,
            QueryContext queryContext,
            CancellationToken cancellationToken) where T : class
        {
            var entityType = model.FindEntityType(typeof(T));
            if (entityType == null) return;

            // Resolver navegaciones desde el modelo usando NavigationName
            foreach (var includeInfo in allIncludes)
            {
                var navigation = entityType.FindNavigation(includeInfo.NavigationName);
                if (navigation == null)
                    continue;

                if (navigation.IsCollection)
                {
                    await LoadSubCollectionAsync(
                        entity, documentSnapshot, navigation, includeInfo,
                        allIncludes, model, isTracking, dbContext, queryContext, cancellationToken);
                }
                else
                {
                    await LoadReferenceAsync(
                        entity, documentSnapshot, navigation, includeInfo,
                        allIncludes, model, isTracking, dbContext, queryContext, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Carga una SubCollection para una entidad.
        /// Aplica filtros, ordenamiento y paginación a nivel de Firestore (server-side).
        /// </summary>
        private async Task LoadSubCollectionAsync(
            object parentEntity,
            DocumentSnapshot parentDoc,
            IReadOnlyNavigation navigation,
            IncludeInfo includeInfo,
            IReadOnlyList<IncludeInfo> allIncludes,
            IModel model,
            bool isTracking,
            DbContext dbContext,
            QueryContext queryContext,
            CancellationToken cancellationToken)
        {
            if (!navigation.IsSubCollection())
                return;

            var subCollectionName = _collectionManager.GetCollectionName(navigation.TargetEntityType.ClrType);
            var targetEntityType = navigation.TargetEntityType;

            // Construir query con filtros aplicados a nivel de Firestore
            var snapshot = await ExecuteSubCollectionQueryAsync(
                parentDoc.Reference.Path,
                subCollectionName,
                includeInfo,
                targetEntityType,
                queryContext,
                cancellationToken);

            // Crear colección vacía del tipo correcto
            var collection = _deserializer.CreateEmptyCollection(navigation);

            var stateManager = dbContext.GetService<IStateManager>();
            var key = targetEntityType.FindPrimaryKey();

            // Calcular Skip para aplicar en memoria (Firestore no soporta offset nativo)
            int skipValue = 0;
            if (includeInfo.Skip.HasValue)
            {
                skipValue = includeInfo.Skip.Value;
            }
            else if (includeInfo.SkipExpression != null)
            {
                skipValue = EvaluateIntExpression(includeInfo.SkipExpression, queryContext);
            }

            int currentIndex = 0;
            foreach (var doc in snapshot.Documents)
            {
                if (!doc.Exists)
                    continue;

                // Aplicar Skip en memoria
                if (currentIndex < skipValue)
                {
                    currentIndex++;
                    continue;
                }

                // Identity Resolution
                object? childEntity = null;
                if (isTracking && key != null)
                {
                    childEntity = TryGetTrackedEntityNonGeneric(stateManager, key, doc.Id);
                }

                // Deserializar si no está trackeada
                if (childEntity == null)
                {
                    childEntity = DeserializeEntityNonGeneric(doc, targetEntityType.ClrType);
                    if (childEntity == null)
                        continue;

                    // Cargar Includes recursivamente (ThenInclude)
                    // Filtrar IncludeInfo cuyos NavigationName existan en el targetEntityType
                    var childIncludes = allIncludes
                        .Where(inc => targetEntityType.FindNavigation(inc.NavigationName) != null)
                        .ToList();

                    if (childIncludes.Count > 0)
                    {
                        await LoadIncludesNonGenericAsync(
                            childEntity, doc, targetEntityType, childIncludes,
                            model, isTracking, dbContext, queryContext, cancellationToken);
                    }

                    // Adjuntar al ChangeTracker
                    if (isTracking)
                    {
                        dbContext.Attach(childEntity);
                    }
                }

                ApplyFixup(parentEntity, childEntity, navigation);
                _deserializer.AddToCollection(collection, childEntity);
                currentIndex++;
            }

            navigation.PropertyInfo?.SetValue(parentEntity, collection);
        }

        /// <summary>
        /// Carga una referencia (DocumentReference) para una entidad.
        /// </summary>
        private async Task LoadReferenceAsync(
            object entity,
            DocumentSnapshot documentSnapshot,
            IReadOnlyNavigation navigation,
            IncludeInfo includeInfo,
            IReadOnlyList<IncludeInfo> allIncludes,
            IModel model,
            bool isTracking,
            DbContext dbContext,
            QueryContext queryContext,
            CancellationToken cancellationToken)
        {
            var data = documentSnapshot.ToDictionary();

            object? referenceValue = null;
            if (data.TryGetValue(navigation.Name, out var directValue))
            {
                referenceValue = directValue;
            }
            else if (data.TryGetValue($"{navigation.Name}Id", out var idValue))
            {
                referenceValue = idValue;
            }

            if (referenceValue == null)
                return;

            var targetEntityType = navigation.TargetEntityType;
            var stateManager = dbContext.GetService<IStateManager>();
            var key = targetEntityType.FindPrimaryKey();

            string? referencedId = null;
            DocumentSnapshot? referencedDoc = null;

            if (referenceValue is Google.Cloud.Firestore.DocumentReference docRef)
            {
                referencedId = docRef.Id;

                // Identity Resolution
                if (isTracking && key != null)
                {
                    var existingEntity = TryGetTrackedEntityNonGeneric(stateManager, key, referencedId);
                    if (existingEntity != null)
                    {
                        ApplyFixup(entity, existingEntity, navigation);
                        navigation.PropertyInfo?.SetValue(entity, existingEntity);
                        return;
                    }
                }

                referencedDoc = await GetDocumentByReferenceAsync(docRef.Path, cancellationToken);
            }
            else if (referenceValue is string id)
            {
                referencedId = id;

                // Identity Resolution
                if (isTracking && key != null)
                {
                    var existingEntity = TryGetTrackedEntityNonGeneric(stateManager, key, referencedId);
                    if (existingEntity != null)
                    {
                        ApplyFixup(entity, existingEntity, navigation);
                        navigation.PropertyInfo?.SetValue(entity, existingEntity);
                        return;
                    }
                }

                var collectionName = _collectionManager.GetCollectionName(targetEntityType.ClrType);
                var docPath = $"{collectionName}/{id}";
                referencedDoc = await GetDocumentByReferenceAsync(docPath, cancellationToken);
            }

            if (referencedDoc == null || !referencedDoc.Exists)
                return;

            var referencedEntity = DeserializeEntityNonGeneric(referencedDoc, targetEntityType.ClrType);

            if (referencedEntity != null)
            {
                // Cargar Includes recursivamente (ThenInclude)
                // Filtrar IncludeInfo cuyos NavigationName existan en el targetEntityType
                var childIncludes = allIncludes
                    .Where(inc => targetEntityType.FindNavigation(inc.NavigationName) != null)
                    .ToList();

                if (childIncludes.Count > 0)
                {
                    await LoadIncludesNonGenericAsync(
                        referencedEntity, referencedDoc, targetEntityType, childIncludes,
                        model, isTracking, dbContext, queryContext, cancellationToken);
                }

                if (isTracking)
                {
                    dbContext.Attach(referencedEntity);
                }

                ApplyFixup(entity, referencedEntity, navigation);
                navigation.PropertyInfo?.SetValue(entity, referencedEntity);
            }
        }

        /// <summary>
        /// Carga Includes en ComplexTypes.
        /// </summary>
        private async Task LoadComplexTypeIncludesAsync<T>(
            T entity,
            DocumentSnapshot documentSnapshot,
            IReadOnlyList<System.Linq.Expressions.LambdaExpression> complexTypeIncludes,
            IModel model,
            bool isTracking,
            DbContext dbContext,
            CancellationToken cancellationToken) where T : class
        {
            var data = documentSnapshot.ToDictionary();

            foreach (var includeExpr in complexTypeIncludes)
            {
                await LoadComplexTypeIncludeAsync(entity, data, includeExpr, model, isTracking, dbContext, cancellationToken);
            }
        }

        /// <summary>
        /// Carga una referencia dentro de un ComplexType.
        /// </summary>
        private async Task LoadComplexTypeIncludeAsync(
            object entity,
            Dictionary<string, object> data,
            System.Linq.Expressions.LambdaExpression includeExpr,
            IModel model,
            bool isTracking,
            DbContext dbContext,
            CancellationToken cancellationToken)
        {
            if (includeExpr.Body is not System.Linq.Expressions.MemberExpression refMemberExpr)
                return;

            var referenceProperty = refMemberExpr.Member as PropertyInfo;
            if (referenceProperty == null)
                return;

            if (refMemberExpr.Expression is not System.Linq.Expressions.MemberExpression complexTypeMemberExpr)
                return;

            var complexTypeProperty = complexTypeMemberExpr.Member as PropertyInfo;
            if (complexTypeProperty == null)
                return;

            var complexTypeInstance = complexTypeProperty.GetValue(entity);
            if (complexTypeInstance == null)
                return;

            if (!data.TryGetValue(complexTypeProperty.Name, out var complexTypeData) ||
                complexTypeData is not Dictionary<string, object> complexTypeDict)
                return;

            if (!complexTypeDict.TryGetValue(referenceProperty.Name, out var referenceValue))
                return;

            if (referenceValue == null)
                return;

            DocumentSnapshot? referencedDoc = null;
            string? referencedId = null;

            if (referenceValue is Google.Cloud.Firestore.DocumentReference docRef)
            {
                referencedId = docRef.Id;
                referencedDoc = await GetDocumentByReferenceAsync(docRef.Path, cancellationToken);
            }
            else if (referenceValue is string id)
            {
                referencedId = id;
                var targetType = referenceProperty.PropertyType;
                var targetEntityType = model.FindEntityType(targetType);
                if (targetEntityType != null)
                {
                    var collectionName = _collectionManager.GetCollectionName(targetEntityType.ClrType);
                    var docPath = $"{collectionName}/{id}";
                    referencedDoc = await GetDocumentByReferenceAsync(docPath, cancellationToken);
                }
            }

            if (referencedDoc == null || !referencedDoc.Exists)
                return;

            // Identity Resolution
            if (isTracking && referencedId != null)
            {
                var targetEntityType = model.FindEntityType(referenceProperty.PropertyType);
                if (targetEntityType != null)
                {
                    var stateManager = dbContext.GetService<IStateManager>();
                    var key = targetEntityType.FindPrimaryKey();
                    if (key != null)
                    {
                        var existingEntity = TryGetTrackedEntityNonGeneric(stateManager, key, referencedId);
                        if (existingEntity != null)
                        {
                            referenceProperty.SetValue(complexTypeInstance, existingEntity);
                            return;
                        }
                    }
                }
            }

            var referencedEntity = DeserializeEntityNonGeneric(referencedDoc, referenceProperty.PropertyType);

            if (referencedEntity != null)
            {
                if (isTracking)
                {
                    dbContext.Attach(referencedEntity);
                }

                referenceProperty.SetValue(complexTypeInstance, referencedEntity);
            }
        }

        /// <summary>
        /// Versión no genérica de LoadIncludesAsync para llamadas recursivas.
        /// </summary>
        private async Task LoadIncludesNonGenericAsync(
            object entity,
            DocumentSnapshot documentSnapshot,
            IReadOnlyEntityType entityType,
            IReadOnlyList<IncludeInfo> allIncludes,
            IModel model,
            bool isTracking,
            DbContext dbContext,
            QueryContext queryContext,
            CancellationToken cancellationToken)
        {
            // Resolver navegaciones desde el modelo usando NavigationName
            foreach (var includeInfo in allIncludes)
            {
                var navigation = entityType.FindNavigation(includeInfo.NavigationName);
                if (navigation == null)
                    continue;

                if (navigation.IsCollection)
                {
                    await LoadSubCollectionAsync(
                        entity, documentSnapshot, navigation, includeInfo,
                        allIncludes, model, isTracking, dbContext, queryContext, cancellationToken);
                }
                else
                {
                    await LoadReferenceAsync(
                        entity, documentSnapshot, navigation, includeInfo,
                        allIncludes, model, isTracking, dbContext, queryContext, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Deserializa una entidad de forma no genérica.
        /// </summary>
        private object? DeserializeEntityNonGeneric(DocumentSnapshot doc, Type entityType)
        {
            var deserializeMethod = typeof(IFirestoreDocumentDeserializer)
                .GetMethods()
                .First(m => m.Name == nameof(IFirestoreDocumentDeserializer.DeserializeEntity) &&
                           m.GetParameters().Length == 1)
                .MakeGenericMethod(entityType);

            return deserializeMethod.Invoke(_deserializer, new object[] { doc });
        }

        /// <summary>
        /// Identity Resolution no genérica.
        /// </summary>
        private static object? TryGetTrackedEntityNonGeneric(IStateManager stateManager, IReadOnlyKey key, string documentId)
        {
            if (key.Properties.Count == 0) return null;

            var keyProperty = key.Properties[0];
            var convertedKey = ConvertKeyValue(documentId, keyProperty);
            var keyValues = new object[] { convertedKey };

            var entry = stateManager.TryGetEntry((IKey)key, keyValues);
            return entry?.Entity;
        }

        /// <summary>
        /// Aplica fixup bidireccional entre entidades.
        /// </summary>
        private static void ApplyFixup(object parent, object child, IReadOnlyNavigation navigation)
        {
            if (navigation.Inverse == null) return;

            var inverseProperty = navigation.Inverse.PropertyInfo;
            if (inverseProperty == null) return;

            if (navigation.IsCollection)
            {
                inverseProperty.SetValue(child, parent);
            }
            else
            {
                if (navigation.Inverse.IsCollection)
                {
                    var collection = inverseProperty.GetValue(parent) as System.Collections.IList;
                    if (collection != null && !collection.Contains(child))
                    {
                        collection.Add(child);
                    }
                }
                else
                {
                    inverseProperty.SetValue(parent, child);
                }
            }
        }

        /// <summary>
        /// Establece shadow FK properties para navegaciones con DocumentReference.
        /// </summary>
        private static void SetShadowForeignKeys(
            object entity,
            DocumentSnapshot documentSnapshot,
            IEntityType entityType,
            DbContext dbContext)
        {
            var data = documentSnapshot.ToDictionary();
            var entry = dbContext.Entry(entity);

            foreach (var navigation in entityType.GetNavigations())
            {
                if (navigation.IsCollection)
                    continue;

                if (navigation.IsSubCollection())
                    continue;

                if (!data.TryGetValue(navigation.Name, out var value))
                    continue;

                if (value is not Google.Cloud.Firestore.DocumentReference docRef)
                    continue;

                var foreignKey = navigation.ForeignKey;
                foreach (var fkProperty in foreignKey.Properties)
                {
                    if (fkProperty.IsShadowProperty())
                    {
                        var referencedId = docRef.Id;
                        var convertedValue = ConvertKeyValue(referencedId, fkProperty);
                        entry.Property(fkProperty.Name).CurrentValue = convertedValue;
                    }
                }
            }
        }

        #endregion

        /// <summary>
        /// Evalúa la expresión del ID en runtime usando el QueryContext
        /// </summary>
        private object? EvaluateIdExpression(
            System.Linq.Expressions.Expression idExpression,
            Microsoft.EntityFrameworkCore.Query.QueryContext queryContext)
        {
            // Si es una ConstantExpression, retornar su valor directamente
            if (idExpression is System.Linq.Expressions.ConstantExpression constant)
            {
                return constant.Value;
            }

            // Para cualquier otra expresión (incluyendo accesos a QueryContext.ParameterValues),
            // compilarla y ejecutarla con el QueryContext como parámetro
            try
            {
                // Reemplazar el parámetro queryContext en la expresión con el valor real
                var replacer = new ExpressionParameterReplacer(queryContext);
                var replacedExpression = replacer.Visit(idExpression);

                // Compilar y evaluar
                var lambda = System.Linq.Expressions.Expression.Lambda<Func<object>>(
                    System.Linq.Expressions.Expression.Convert(replacedExpression, typeof(object)));

                var compiled = lambda.Compile();
                var result = compiled();

                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to evaluate ID expression: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Evalúa una expresión entera en runtime usando el QueryContext.
        /// Usado para Limit (Take) y Skip cuando son expresiones parametrizadas.
        /// </summary>
        public int EvaluateIntExpression(
            System.Linq.Expressions.Expression expression,
            Microsoft.EntityFrameworkCore.Query.QueryContext queryContext)
        {
            // Si es una ConstantExpression, retornar su valor directamente
            if (expression is System.Linq.Expressions.ConstantExpression constant && constant.Value != null)
            {
                return (int)constant.Value;
            }

            // Para expresiones parametrizadas, compilar y evaluar
            try
            {
                var replacer = new ExpressionParameterReplacer(queryContext);
                var replacedExpression = replacer.Visit(expression);

                // Compilar y evaluar
                var lambda = System.Linq.Expressions.Expression.Lambda<Func<int>>(replacedExpression);
                var compiled = lambda.Compile();
                return compiled();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to evaluate int expression: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Visitor que reemplaza referencias al parámetro QueryContext con el valor real
        /// y resuelve parámetros desde QueryContext.ParameterValues
        /// </summary>
        private class ExpressionParameterReplacer : System.Linq.Expressions.ExpressionVisitor
        {
            private readonly Microsoft.EntityFrameworkCore.Query.QueryContext _queryContext;

            public ExpressionParameterReplacer(Microsoft.EntityFrameworkCore.Query.QueryContext queryContext)
            {
                _queryContext = queryContext;
            }

            protected override System.Linq.Expressions.Expression VisitParameter(System.Linq.Expressions.ParameterExpression node)
            {
                // Si es el parámetro "queryContext", reemplazarlo con una constante que contiene el QueryContext real
                if (node.Name == "queryContext" && node.Type == typeof(Microsoft.EntityFrameworkCore.Query.QueryContext))
                {
                    return System.Linq.Expressions.Expression.Constant(_queryContext, typeof(Microsoft.EntityFrameworkCore.Query.QueryContext));
                }

                // Si es un parámetro que existe en QueryContext.ParameterValues (variables capturadas),
                // reemplazarlo con su valor real
                if (node.Name != null && _queryContext.ParameterValues.TryGetValue(node.Name, out var parameterValue))
                {
                    return System.Linq.Expressions.Expression.Constant(parameterValue, node.Type);
                }

                return base.VisitParameter(node);
            }
        }

        /// <summary>
        /// Construye un Google.Cloud.Firestore.Query desde FirestoreQueryExpression.
        /// Public to allow aggregation queries to reuse query building logic.
        /// </summary>
        public Google.Cloud.Firestore.Query BuildQuery(
            FirestoreQueryExpression queryExpression,
            Microsoft.EntityFrameworkCore.Query.QueryContext queryContext)
        {
            // Obtener CollectionReference inicial
            Google.Cloud.Firestore.Query query = _client.GetCollection(queryExpression.CollectionName);

            // Aplicar filtros WHERE (AND implícito)
            foreach (var filter in queryExpression.Filters)
            {
                query = ApplyWhereClause(query, filter, queryContext, queryExpression.EntityType);
            }

            // Aplicar grupos OR
            foreach (var orGroup in queryExpression.OrFilterGroups)
            {
                query = ApplyOrFilterGroup(query, orGroup, queryContext, queryExpression.EntityType);
            }

            // Aplicar ordenamiento ORDER BY
            foreach (var orderBy in queryExpression.OrderByClauses)
            {
                query = ApplyOrderByClause(query, orderBy);
            }

            // Calcular Skip para ajustar el límite
            // Como Skip se aplica en memoria, necesitamos traer (Skip + Limit) documentos de Firestore
            int skipValue = 0;
            if (queryExpression.Skip.HasValue)
            {
                skipValue = queryExpression.Skip.Value;
            }
            else if (queryExpression.SkipExpression != null)
            {
                skipValue = EvaluateIntExpression(queryExpression.SkipExpression, queryContext);
            }

            // Aplicar límite LIMIT (Take) - ajustado por Skip
            if (queryExpression.Limit.HasValue)
            {
                var effectiveLimit = queryExpression.Limit.Value + skipValue;
                query = query.Limit(effectiveLimit);
                _logger.LogTrace("Applied Limit: {Limit} (adjusted for Skip: {Skip})", effectiveLimit, skipValue);
            }
            else if (queryExpression.LimitExpression != null)
            {
                // Evaluar expresión de límite en runtime (para parámetros de EF Core)
                var limitValue = EvaluateIntExpression(queryExpression.LimitExpression, queryContext);
                var effectiveLimit = limitValue + skipValue;
                query = query.Limit(effectiveLimit);
                _logger.LogTrace("Applied Limit from expression: {Limit} (adjusted for Skip: {Skip})", effectiveLimit, skipValue);
            }

            // Aplicar LimitToLast (TakeLast) - requiere OrderBy para funcionar
            if (queryExpression.LimitToLast.HasValue)
            {
                query = query.LimitToLast(queryExpression.LimitToLast.Value);
                _logger.LogTrace("Applied LimitToLast: {Limit}", queryExpression.LimitToLast.Value);
            }
            else if (queryExpression.LimitToLastExpression != null)
            {
                var limitToLastValue = EvaluateIntExpression(queryExpression.LimitToLastExpression, queryContext);
                query = query.LimitToLast(limitToLastValue);
                _logger.LogTrace("Applied LimitToLast from expression: {Limit}", limitToLastValue);
            }

            // Aplicar cursor START AFTER (Skip con paginación)
            if (queryExpression.StartAfterCursor != null)
            {
                query = ApplyStartAfterCursor(query, queryExpression.StartAfterCursor);
            }

            return query;
        }

        /// <summary>
        /// Aplica un FirestoreCursor al query usando StartAfter.
        /// Si el cursor tiene OrderByValues, los usa; de lo contrario, usa solo el DocumentId.
        /// </summary>
        private Google.Cloud.Firestore.Query ApplyStartAfterCursor(
            Google.Cloud.Firestore.Query query,
            FirestoreCursor cursor)
        {
            _logger.LogTrace("Applied StartAfter: {Cursor}", cursor);

            // Si hay valores de OrderBy, usarlos junto con el ID
            if (cursor.OrderByValues.Count > 0)
            {
                // Firestore StartAfter acepta los valores en el orden de los OrderBy clauses
                var values = new List<object?>(cursor.OrderByValues);

                // Agregar el Document ID al final (para desempatar si hay valores iguales)
                // Firestore SDK espera que el último valor sea el ID del documento cuando
                // se usa FieldPath.DocumentId en el OrderBy
                values.Add(cursor.DocumentId);

                return query.StartAfter(values.ToArray());
            }

            // Si no hay valores de OrderBy, usar solo el Document ID
            // Esto asume que la query está ordenada por __name__ (DocumentId)
            return query.StartAfter(cursor.DocumentId);
        }

        /// <summary>
        /// Aplica un grupo de filtros OR usando Filter.Or()
        /// </summary>
        private Google.Cloud.Firestore.Query ApplyOrFilterGroup(
            Google.Cloud.Firestore.Query query,
            FirestoreOrFilterGroup orGroup,
            Microsoft.EntityFrameworkCore.Query.QueryContext queryContext,
            IEntityType entityType)
        {
            if (orGroup.Clauses.Count == 0)
            {
                return query;
            }

            if (orGroup.Clauses.Count == 1)
            {
                // Single clause - no need for OR
                return ApplyWhereClause(query, orGroup.Clauses[0], queryContext, entityType);
            }

            // Build individual filters for OR
            var filters = new List<Filter>();
            foreach (var clause in orGroup.Clauses)
            {
                var filter = BuildFilter(clause, queryContext, entityType);
                if (filter != null)
                {
                    filters.Add(filter);
                }
            }

            if (filters.Count == 0)
            {
                return query;
            }

            if (filters.Count == 1)
            {
                return query.Where(filters[0]);
            }

            // Combine with OR
            var orFilter = Filter.Or(filters.ToArray());
            _logger.LogTrace("Applied OR filter with {Count} clauses", filters.Count);

            return query.Where(orFilter);
        }

        /// <summary>
        /// Builds a Firestore Filter from a FirestoreWhereClause
        /// </summary>
        private Filter? BuildFilter(
            FirestoreWhereClause clause,
            Microsoft.EntityFrameworkCore.Query.QueryContext queryContext,
            IEntityType entityType)
        {
            var value = clause.EvaluateValue(queryContext);

            // Validar filtro con null - requiere PersistNullValues configurado
            if (value == null)
            {
                ValidateNullFilter(clause.PropertyName, entityType);
            }

            if (clause.EnumType != null && value != null)
            {
                value = ConvertToEnumString(value, clause.EnumType);
            }

            var convertedValue = ConvertValueForFirestore(value);
            var fieldPath = GetFieldPath(clause.PropertyName);

            return clause.Operator switch
            {
                FirestoreOperator.EqualTo => Filter.EqualTo(fieldPath, convertedValue),
                FirestoreOperator.NotEqualTo => Filter.NotEqualTo(fieldPath, convertedValue),
                FirestoreOperator.LessThan => Filter.LessThan(fieldPath, convertedValue),
                FirestoreOperator.LessThanOrEqualTo => Filter.LessThanOrEqualTo(fieldPath, convertedValue),
                FirestoreOperator.GreaterThan => Filter.GreaterThan(fieldPath, convertedValue),
                FirestoreOperator.GreaterThanOrEqualTo => Filter.GreaterThanOrEqualTo(fieldPath, convertedValue),
                FirestoreOperator.ArrayContains => Filter.ArrayContains(fieldPath, convertedValue),
                FirestoreOperator.In => BuildInFilter(fieldPath, convertedValue),
                FirestoreOperator.ArrayContainsAny => BuildArrayContainsAnyFilter(fieldPath, convertedValue),
                FirestoreOperator.NotIn => BuildNotInFilter(fieldPath, convertedValue),
                _ => null
            };
        }

        private Filter BuildInFilter(FieldPath fieldPath, object? value)
        {
            if (value is not IEnumerable enumerable)
            {
                throw new InvalidOperationException(
                    $"WhereIn requires an IEnumerable value");
            }

            var values = ConvertEnumerableToArray(enumerable);
            return Filter.InArray(fieldPath, values);
        }

        private Filter BuildArrayContainsAnyFilter(FieldPath fieldPath, object? value)
        {
            if (value is not IEnumerable enumerable)
            {
                throw new InvalidOperationException(
                    $"WhereArrayContainsAny requires an IEnumerable value");
            }

            var values = ConvertEnumerableToArray(enumerable);
            return Filter.ArrayContainsAny(fieldPath, values);
        }

        private Filter BuildNotInFilter(FieldPath fieldPath, object? value)
        {
            if (value is not IEnumerable enumerable)
            {
                throw new InvalidOperationException(
                    $"WhereNotIn requires an IEnumerable value");
            }

            var values = ConvertEnumerableToArray(enumerable);
            return Filter.NotInArray(fieldPath, values);
        }

        /// <summary>
        /// Aplica una cláusula WHERE al query
        /// </summary>
        private Google.Cloud.Firestore.Query ApplyWhereClause(
            Google.Cloud.Firestore.Query query,
            FirestoreWhereClause clause,
            Microsoft.EntityFrameworkCore.Query.QueryContext queryContext,
            IEntityType entityType)
        {
            // Evaluar el valor en runtime usando el QueryContext
            var value = clause.EvaluateValue(queryContext);

            // Validar filtro con null - requiere PersistNullValues configurado
            if (value == null)
            {
                ValidateNullFilter(clause.PropertyName, entityType);
            }

            // Si hay un tipo de enum, convertir el valor numérico a string del enum
            if (clause.EnumType != null && value != null)
            {
                value = ConvertToEnumString(value, clause.EnumType);
            }

            // Convertir valor al tipo esperado por Firestore
            var convertedValue = ConvertValueForFirestore(value);

            // Determinar el campo a usar (FieldPath.DocumentId para "Id")
            var fieldPath = GetFieldPath(clause.PropertyName);

            _logger.LogTrace("Applying filter: {PropertyName} {Operator} {Value}",
                clause.PropertyName, clause.Operator, convertedValue);

            // Aplicar el operador correspondiente
            return clause.Operator switch
            {
                FirestoreOperator.EqualTo =>
                    query.WhereEqualTo(fieldPath, convertedValue),

                FirestoreOperator.NotEqualTo =>
                    query.WhereNotEqualTo(fieldPath, convertedValue),

                FirestoreOperator.LessThan =>
                    query.WhereLessThan(fieldPath, convertedValue),

                FirestoreOperator.LessThanOrEqualTo =>
                    query.WhereLessThanOrEqualTo(fieldPath, convertedValue),

                FirestoreOperator.GreaterThan =>
                    query.WhereGreaterThan(fieldPath, convertedValue),

                FirestoreOperator.GreaterThanOrEqualTo =>
                    query.WhereGreaterThanOrEqualTo(fieldPath, convertedValue),

                FirestoreOperator.ArrayContains =>
                    query.WhereArrayContains(fieldPath, convertedValue),

                FirestoreOperator.In =>
                    ApplyWhereIn(query, fieldPath, convertedValue),

                FirestoreOperator.ArrayContainsAny =>
                    ApplyWhereArrayContainsAny(query, fieldPath, convertedValue),

                FirestoreOperator.NotIn =>
                    ApplyWhereNotIn(query, fieldPath, convertedValue),

                _ => throw new NotSupportedException(
                    $"Firestore operator {clause.Operator} is not supported")
            };
        }

        /// <summary>
        /// Validates that a null filter is allowed for the property.
        /// Throws NotSupportedException if property doesn't have PersistNullValues configured.
        /// </summary>
        private void ValidateNullFilter(string propertyName, IEntityType entityType)
        {
            // Obtener la propiedad del modelo - soportar propiedades anidadas
            var propertyPath = propertyName.Split('.');
            var property = entityType.FindProperty(propertyPath[0]);

            if (property == null)
            {
                // Si no encontramos la propiedad, podría ser una propiedad anidada de ComplexType
                // En este caso, permitimos la query (no tenemos forma de validar ComplexTypes aún)
                return;
            }

            if (!property.IsPersistNullValuesEnabled())
            {
                throw new NotSupportedException(
                    $"Filtering by null on property '{propertyName}' is not supported. " +
                    "Firestore does not store null values by default. " +
                    "Configure the property with .PersistNullValues() in OnModelCreating if you need this functionality.");
            }
        }

        /// <summary>
        /// Aplica WhereIn validando límite de 30 elementos
        /// </summary>
        private Google.Cloud.Firestore.Query ApplyWhereIn(
            Google.Cloud.Firestore.Query query,
            FieldPath fieldPath,
            object? value)
        {
            if (value is not IEnumerable enumerable)
            {
                throw new InvalidOperationException(
                    $"WhereIn requires an IEnumerable value, got {value?.GetType().Name ?? "null"}");
            }

            // Convertir a array y validar límite
            var values = ConvertEnumerableToArray(enumerable);

            if (values.Length > 30)
            {
                throw new InvalidOperationException(
                    $"Firestore WhereIn supports a maximum of 30 elements. Got {values.Length} elements. " +
                    "Consider splitting into multiple queries or using a different approach.");
            }

            return query.WhereIn(fieldPath, values);
        }

        /// <summary>
        /// Aplica WhereArrayContainsAny validando límite de 30 elementos
        /// </summary>
        private Google.Cloud.Firestore.Query ApplyWhereArrayContainsAny(
            Google.Cloud.Firestore.Query query,
            FieldPath fieldPath,
            object? value)
        {
            if (value is not IEnumerable enumerable)
            {
                throw new InvalidOperationException(
                    $"WhereArrayContainsAny requires an IEnumerable value, got {value?.GetType().Name ?? "null"}");
            }

            var values = ConvertEnumerableToArray(enumerable);

            if (values.Length > 30)
            {
                throw new InvalidOperationException(
                    $"Firestore WhereArrayContainsAny supports a maximum of 30 elements. Got {values.Length} elements.");
            }

            return query.WhereArrayContainsAny(fieldPath, values);
        }

        /// <summary>
        /// Aplica WhereNotIn validando límite de 10 elementos
        /// </summary>
        private Google.Cloud.Firestore.Query ApplyWhereNotIn(
            Google.Cloud.Firestore.Query query,
            FieldPath fieldPath,
            object? value)
        {
            if (value is not IEnumerable enumerable)
            {
                throw new InvalidOperationException(
                    $"WhereNotIn requires an IEnumerable value, got {value?.GetType().Name ?? "null"}");
            }

            var values = ConvertEnumerableToArray(enumerable);

            if (values.Length > 10)
            {
                throw new InvalidOperationException(
                    $"Firestore WhereNotIn supports a maximum of 10 elements. Got {values.Length} elements.");
            }

            return query.WhereNotIn(fieldPath, values);
        }

        /// <summary>
        /// Gets the appropriate FieldPath for a property name.
        /// Returns FieldPath.DocumentId for "Id" property, otherwise a regular FieldPath.
        /// Supports nested properties like "Direccion.Ciudad" → FieldPath("Direccion", "Ciudad")
        /// </summary>
        private FieldPath GetFieldPath(string propertyName)
        {
            if (propertyName == "Id")
                return FieldPath.DocumentId;

            // Split nested property paths: "Direccion.Ciudad" → ["Direccion", "Ciudad"]
            var segments = propertyName.Split('.');
            return new FieldPath(segments);
        }

        /// <summary>
        /// Aplica una cláusula ORDER BY al query
        /// </summary>
        private Google.Cloud.Firestore.Query ApplyOrderByClause(
            Google.Cloud.Firestore.Query query,
            FirestoreOrderByClause orderBy)
        {
            _logger.LogTrace("Applying order by: {PropertyName} {Direction}",
                orderBy.PropertyName, orderBy.Descending ? "DESC" : "ASC");

            return orderBy.Descending
                ? query.OrderByDescending(orderBy.PropertyName)
                : query.OrderBy(orderBy.PropertyName);
        }

        /// <summary>
        /// Convierte un valor numérico al nombre string del enum correspondiente.
        /// Se usa cuando la query tiene un cast de enum a int.
        /// </summary>
        private object ConvertToEnumString(object value, Type enumType)
        {
            // Si ya es el tipo enum, convertir a string
            if (value.GetType() == enumType)
            {
                return value.ToString()!;
            }

            // Si es un valor numérico, convertir a enum y luego a string
            try
            {
                var enumValue = Enum.ToObject(enumType, value);
                return enumValue.ToString()!;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to convert value '{value}' to enum type '{enumType.Name}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Convierte un valor de C# al tipo esperado por Firestore.
        /// Aplica conversiones necesarias: decimal → double, enum → string
        /// </summary>
        private object? ConvertValueForFirestore(object? value)
        {
            if (value == null)
                return null;

            // Conversión: decimal → double
            if (value is decimal d)
            {
                return (double)d;
            }

            // Conversión: enum → string
            if (value is Enum e)
            {
                return e.ToString();
            }

            // Conversión: DateTime → UTC
            if (value is DateTime dt)
            {
                return dt.ToUniversalTime();
            }

            // Conversión: List<decimal> → double[]
            if (value is IEnumerable enumerable && value is not string && value is not byte[])
            {
                return ConvertEnumerableForFirestore(enumerable);
            }

            // Para otros tipos, retornar tal cual
            return value;
        }

        /// <summary>
        /// Convierte una colección aplicando conversiones de elementos
        /// </summary>
        private object ConvertEnumerableForFirestore(IEnumerable enumerable)
        {
            var list = new List<object>();

            foreach (var item in enumerable)
            {
                if (item != null)
                {
                    // Aplicar conversiones recursivamente a cada elemento
                    var convertedItem = ConvertValueForFirestore(item);
                    if (convertedItem != null)
                    {
                        list.Add(convertedItem);
                    }
                }
            }

            return list.ToArray();
        }

        /// <summary>
        /// Convierte IEnumerable a array aplicando conversiones
        /// </summary>
        private object[] ConvertEnumerableToArray(IEnumerable enumerable)
        {
            var list = new List<object>();

            foreach (var item in enumerable)
            {
                if (item != null)
                {
                    var convertedItem = ConvertValueForFirestore(item);
                    if (convertedItem != null)
                    {
                        list.Add(convertedItem);
                    }
                }
            }

            return list.ToArray();
        }

        #region Aggregation Execution

        /// <summary>
        /// Ejecuta una agregación de Firestore (Count, Sum, Average, Min, Max, Any)
        /// </summary>
        public async Task<T> ExecuteAggregationAsync<T>(
            FirestoreQueryExpression queryExpression,
            Microsoft.EntityFrameworkCore.Query.QueryContext queryContext,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(queryExpression);

            _logger.LogInformation("=== Executing Firestore aggregation ===");
            _logger.LogInformation("Collection: {Collection}, Type: {AggregationType}",
                queryExpression.CollectionName, queryExpression.AggregationType);

            // Build base query with filters
            var query = BuildQuery(queryExpression, queryContext);

            return queryExpression.AggregationType switch
            {
                FirestoreAggregationType.Count => await ExecuteCountAsync<T>(query, cancellationToken),
                FirestoreAggregationType.Any => await ExecuteAnyAsync<T>(query, cancellationToken),
                FirestoreAggregationType.Sum => await ExecuteSumAsync<T>(query, queryExpression, cancellationToken),
                FirestoreAggregationType.Average => await ExecuteAverageAsync<T>(query, queryExpression, cancellationToken),
                FirestoreAggregationType.Min => await ExecuteMinAsync<T>(query, queryExpression, cancellationToken),
                FirestoreAggregationType.Max => await ExecuteMaxAsync<T>(query, queryExpression, cancellationToken),
                _ => throw new NotSupportedException($"Aggregation type {queryExpression.AggregationType} is not supported")
            };
        }

        private async Task<T> ExecuteCountAsync<T>(Google.Cloud.Firestore.Query query, CancellationToken cancellationToken)
        {
            var aggregateQuery = query.Count();
            // Ciclo 11: Usar wrapper en lugar de llamada directa al SDK
            var snapshot = await _client.ExecuteAggregateQueryAsync(aggregateQuery, cancellationToken);
            var count = snapshot.Count ?? 0;

            _logger.LogInformation("Count result: {Count}", count);

            // Convert to requested type (int, long, etc)
            return (T)Convert.ChangeType(count, typeof(T));
        }

        private async Task<T> ExecuteAnyAsync<T>(Google.Cloud.Firestore.Query query, CancellationToken cancellationToken)
        {
            // Use Limit(1) and check if any documents exist
            var limitedQuery = query.Limit(1);
            // Ciclo 11: Usar wrapper en lugar de llamada directa al SDK
            var snapshot = await _client.ExecuteQueryAsync(limitedQuery, cancellationToken);
            var exists = snapshot.Count > 0;

            _logger.LogInformation("Any result: {Exists}", exists);

            return (T)(object)exists;
        }

        private async Task<T> ExecuteSumAsync<T>(
            Google.Cloud.Firestore.Query query,
            FirestoreQueryExpression queryExpression,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(queryExpression.AggregationPropertyName))
            {
                throw new InvalidOperationException("Sum requires a property name");
            }

            var aggregateQuery = query.Aggregate(AggregateField.Sum(queryExpression.AggregationPropertyName));
            // Ciclo 11: Usar wrapper en lugar de llamada directa al SDK
            var snapshot = await _client.ExecuteAggregateQueryAsync(aggregateQuery, cancellationToken);

            // Get the sum value - Firestore returns it as the first (and only) aggregate field
            var sumValue = snapshot.GetValue<double?>(AggregateField.Sum(queryExpression.AggregationPropertyName));
            var result = sumValue ?? 0;

            _logger.LogInformation("Sum result for {Property}: {Result}",
                queryExpression.AggregationPropertyName, result);

            return ConvertAggregationResult<T>(result, queryExpression.AggregationResultType);
        }

        private async Task<T> ExecuteAverageAsync<T>(
            Google.Cloud.Firestore.Query query,
            FirestoreQueryExpression queryExpression,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(queryExpression.AggregationPropertyName))
            {
                throw new InvalidOperationException("Average requires a property name");
            }

            var aggregateQuery = query.Aggregate(AggregateField.Average(queryExpression.AggregationPropertyName));
            // Ciclo 11: Usar wrapper en lugar de llamada directa al SDK
            var snapshot = await _client.ExecuteAggregateQueryAsync(aggregateQuery, cancellationToken);

            var avgValue = snapshot.GetValue<double?>(AggregateField.Average(queryExpression.AggregationPropertyName));

            // Average on empty set returns null - throw InvalidOperationException like LINQ does
            if (avgValue == null)
            {
                throw new InvalidOperationException("Sequence contains no elements");
            }

            _logger.LogInformation("Average result for {Property}: {Result}",
                queryExpression.AggregationPropertyName, avgValue);

            return ConvertAggregationResult<T>(avgValue.Value, queryExpression.AggregationResultType);
        }

        private async Task<T> ExecuteMinAsync<T>(
            Google.Cloud.Firestore.Query query,
            FirestoreQueryExpression queryExpression,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(queryExpression.AggregationPropertyName))
            {
                throw new InvalidOperationException("Min requires a property name");
            }

            // Firestore doesn't support Min natively - use OrderBy + Limit(1)
            var minQuery = query.OrderBy(queryExpression.AggregationPropertyName).Limit(1);
            // Ciclo 11: Usar wrapper en lugar de llamada directa al SDK
            var snapshot = await _client.ExecuteQueryAsync(minQuery, cancellationToken);

            if (snapshot.Count == 0)
            {
                throw new InvalidOperationException("Sequence contains no elements");
            }

            var document = snapshot.Documents[0];
            var value = document.GetValue<object>(queryExpression.AggregationPropertyName);

            _logger.LogInformation("Min result for {Property}: {Result}",
                queryExpression.AggregationPropertyName, value);

            return ConvertAggregationResult<T>(value, queryExpression.AggregationResultType);
        }

        private async Task<T> ExecuteMaxAsync<T>(
            Google.Cloud.Firestore.Query query,
            FirestoreQueryExpression queryExpression,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(queryExpression.AggregationPropertyName))
            {
                throw new InvalidOperationException("Max requires a property name");
            }

            // Firestore doesn't support Max natively - use OrderByDescending + Limit(1)
            var maxQuery = query.OrderByDescending(queryExpression.AggregationPropertyName).Limit(1);
            // Ciclo 11: Usar wrapper en lugar de llamada directa al SDK
            var snapshot = await _client.ExecuteQueryAsync(maxQuery, cancellationToken);

            if (snapshot.Count == 0)
            {
                throw new InvalidOperationException("Sequence contains no elements");
            }

            var document = snapshot.Documents[0];
            var value = document.GetValue<object>(queryExpression.AggregationPropertyName);

            _logger.LogInformation("Max result for {Property}: {Result}",
                queryExpression.AggregationPropertyName, value);

            return ConvertAggregationResult<T>(value, queryExpression.AggregationResultType);
        }

        /// <summary>
        /// Converts aggregation result to the expected return type.
        /// Handles decimal ↔ double conversions and other numeric conversions.
        /// </summary>
        private T ConvertAggregationResult<T>(object value, Type? targetType)
        {
            targetType ??= typeof(T);

            // Handle null
            if (value == null)
            {
                return default!;
            }

            // Firestore returns numbers as double or long
            // Convert to the expected type (int, decimal, etc)
            try
            {
                // Special case: Firestore double → decimal
                if (targetType == typeof(decimal) && value is double d)
                {
                    return (T)(object)(decimal)d;
                }

                // Special case: Firestore long → int
                if (targetType == typeof(int) && value is long l)
                {
                    return (T)(object)(int)l;
                }

                // General conversion
                return (T)Convert.ChangeType(value, targetType);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to convert aggregation result '{value}' (type: {value.GetType().Name}) to {targetType.Name}: {ex.Message}", ex);
            }
        }

        #endregion

        #region Navigation Loading

        /// <inheritdoc />
        public async Task<QuerySnapshot> GetSubCollectionAsync(
            string parentDocPath,
            string subCollectionName,
            CancellationToken cancellationToken = default)
        {
            var relativePath = ExtractRelativePath(parentDocPath);
            var parentDoc = _client.Database.Document(relativePath);
            return await _client.GetSubCollectionAsync(parentDoc, subCollectionName, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<DocumentSnapshot> GetDocumentByReferenceAsync(
            string docPath,
            CancellationToken cancellationToken = default)
        {
            var relativePath = ExtractRelativePath(docPath);
            var docRef = _client.Database.Document(relativePath);
            return await _client.GetDocumentByReferenceAsync(docRef, cancellationToken);
        }

        /// <summary>
        /// Extrae el path relativo de un path completo de Firestore.
        /// Convierte "projects/{project}/databases/{db}/documents/Collection/DocId" a "Collection/DocId"
        /// Si ya es un path relativo, lo devuelve tal cual.
        /// </summary>
        private static string ExtractRelativePath(string fullPath)
        {
            const string documentsMarker = "/documents/";
            var index = fullPath.IndexOf(documentsMarker, StringComparison.Ordinal);
            return index >= 0 ? fullPath.Substring(index + documentsMarker.Length) : fullPath;
        }

        /// <summary>
        /// Ejecuta una query en una subcollection aplicando filtros, ordenamiento y paginación.
        /// Los filtros se aplican a nivel de Firestore (server-side) para mayor eficiencia.
        /// </summary>
        private async Task<QuerySnapshot> ExecuteSubCollectionQueryAsync(
            string parentDocPath,
            string subCollectionName,
            IncludeInfo includeInfo,
            IReadOnlyEntityType targetEntityType,
            QueryContext queryContext,
            CancellationToken cancellationToken)
        {
            var relativePath = ExtractRelativePath(parentDocPath);
            var parentDoc = _client.Database.Document(relativePath);
            var subCollection = parentDoc.Collection(subCollectionName);

            // Si no hay operaciones, retornar todos los documentos
            if (!includeInfo.HasOperations)
            {
                return await _client.GetSubCollectionAsync(parentDoc, subCollectionName, cancellationToken);
            }

            // Construir query con filtros
            Google.Cloud.Firestore.Query query = subCollection;

            // Aplicar filtros WHERE (AND implícito)
            foreach (var filter in includeInfo.Filters)
            {
                query = ApplyWhereClauseForInclude(query, filter, queryContext, targetEntityType);
            }

            // Aplicar grupos OR
            foreach (var orGroup in includeInfo.OrFilterGroups)
            {
                query = ApplyOrFilterGroupForInclude(query, orGroup, queryContext, targetEntityType);
            }

            // Aplicar ordenamiento ORDER BY
            foreach (var orderBy in includeInfo.OrderByClauses)
            {
                query = ApplyOrderByClause(query, orderBy);
            }

            // Calcular Skip para ajustar el límite
            int skipValue = 0;
            if (includeInfo.Skip.HasValue)
            {
                skipValue = includeInfo.Skip.Value;
            }
            else if (includeInfo.SkipExpression != null)
            {
                skipValue = EvaluateIntExpression(includeInfo.SkipExpression, queryContext);
            }

            // Aplicar límite LIMIT (Take) - ajustado por Skip
            if (includeInfo.Take.HasValue)
            {
                var effectiveLimit = includeInfo.Take.Value + skipValue;
                query = query.Limit(effectiveLimit);
            }
            else if (includeInfo.TakeExpression != null)
            {
                var limitValue = EvaluateIntExpression(includeInfo.TakeExpression, queryContext);
                var effectiveLimit = limitValue + skipValue;
                query = query.Limit(effectiveLimit);
            }

            // Ejecutar query
            return await _client.ExecuteQueryAsync(query, cancellationToken);
        }

        /// <summary>
        /// Aplica una cláusula WHERE para Includes.
        /// Similar a ApplyWhereClause pero usando IReadOnlyEntityType.
        /// </summary>
        private Google.Cloud.Firestore.Query ApplyWhereClauseForInclude(
            Google.Cloud.Firestore.Query query,
            FirestoreWhereClause clause,
            QueryContext queryContext,
            IReadOnlyEntityType entityType)
        {
            var value = clause.EvaluateValue(queryContext);

            // Si hay un tipo de enum, convertir el valor numérico a string del enum
            if (clause.EnumType != null && value != null)
            {
                value = ConvertToEnumString(value, clause.EnumType);
            }

            // Convertir valor al tipo esperado por Firestore
            var convertedValue = ConvertValueForFirestore(value);
            var fieldPath = GetFieldPath(clause.PropertyName);

            _logger.LogTrace("Applying Include filter: {PropertyName} {Operator} {Value}",
                clause.PropertyName, clause.Operator, convertedValue);

            return clause.Operator switch
            {
                FirestoreOperator.EqualTo => query.WhereEqualTo(fieldPath, convertedValue),
                FirestoreOperator.NotEqualTo => query.WhereNotEqualTo(fieldPath, convertedValue),
                FirestoreOperator.LessThan => query.WhereLessThan(fieldPath, convertedValue),
                FirestoreOperator.LessThanOrEqualTo => query.WhereLessThanOrEqualTo(fieldPath, convertedValue),
                FirestoreOperator.GreaterThan => query.WhereGreaterThan(fieldPath, convertedValue),
                FirestoreOperator.GreaterThanOrEqualTo => query.WhereGreaterThanOrEqualTo(fieldPath, convertedValue),
                FirestoreOperator.ArrayContains => query.WhereArrayContains(fieldPath, convertedValue),
                FirestoreOperator.In => ApplyWhereIn(query, fieldPath, convertedValue),
                FirestoreOperator.ArrayContainsAny => ApplyWhereArrayContainsAny(query, fieldPath, convertedValue),
                FirestoreOperator.NotIn => ApplyWhereNotIn(query, fieldPath, convertedValue),
                _ => throw new NotSupportedException($"Firestore operator {clause.Operator} is not supported in Include filters")
            };
        }

        /// <summary>
        /// Aplica un grupo de filtros OR para Includes.
        /// </summary>
        private Google.Cloud.Firestore.Query ApplyOrFilterGroupForInclude(
            Google.Cloud.Firestore.Query query,
            FirestoreOrFilterGroup orGroup,
            QueryContext queryContext,
            IReadOnlyEntityType entityType)
        {
            if (orGroup.Clauses.Count == 0)
                return query;

            if (orGroup.Clauses.Count == 1)
            {
                return ApplyWhereClauseForInclude(query, orGroup.Clauses[0], queryContext, entityType);
            }

            // Build individual filters for OR
            var filters = new List<Filter>();
            foreach (var clause in orGroup.Clauses)
            {
                var filter = BuildFilterForInclude(clause, queryContext, entityType);
                if (filter != null)
                    filters.Add(filter);
            }

            if (filters.Count == 0)
                return query;

            if (filters.Count == 1)
                return query.Where(filters[0]);

            // Combine with OR
            var orFilter = Filter.Or(filters.ToArray());
            _logger.LogTrace("Applied Include OR filter with {Count} clauses", filters.Count);

            return query.Where(orFilter);
        }

        /// <summary>
        /// Builds a Firestore Filter for Includes.
        /// </summary>
        private Filter? BuildFilterForInclude(
            FirestoreWhereClause clause,
            QueryContext queryContext,
            IReadOnlyEntityType entityType)
        {
            var value = clause.EvaluateValue(queryContext);

            if (clause.EnumType != null && value != null)
            {
                value = ConvertToEnumString(value, clause.EnumType);
            }

            var convertedValue = ConvertValueForFirestore(value);
            var fieldPath = GetFieldPath(clause.PropertyName);

            return clause.Operator switch
            {
                FirestoreOperator.EqualTo => Filter.EqualTo(fieldPath, convertedValue),
                FirestoreOperator.NotEqualTo => Filter.NotEqualTo(fieldPath, convertedValue),
                FirestoreOperator.LessThan => Filter.LessThan(fieldPath, convertedValue),
                FirestoreOperator.LessThanOrEqualTo => Filter.LessThanOrEqualTo(fieldPath, convertedValue),
                FirestoreOperator.GreaterThan => Filter.GreaterThan(fieldPath, convertedValue),
                FirestoreOperator.GreaterThanOrEqualTo => Filter.GreaterThanOrEqualTo(fieldPath, convertedValue),
                FirestoreOperator.ArrayContains => Filter.ArrayContains(fieldPath, convertedValue),
                FirestoreOperator.In => BuildInFilter(fieldPath, convertedValue),
                FirestoreOperator.ArrayContainsAny => BuildArrayContainsAnyFilter(fieldPath, convertedValue),
                FirestoreOperator.NotIn => BuildNotInFilter(fieldPath, convertedValue),
                _ => null
            };
        }

        #endregion

        #region Filter Compilation (Legacy)

        /// <summary>
        /// Compiles IncludeInfo filters (FirestoreWhereClause) to an in-memory predicate.
        ///
        /// NOTE: This method is kept for backward compatibility but is no longer used
        /// since filters are now applied at the Firestore level (server-side).
        ///
        /// TODO [FASE 2/3]: This method is a temporary bridge. The correct architecture is:
        /// 1. AstResolver resolves Expression values in FirestoreWhereClause
        /// 2. Filters are applied in the Firestore query to the subcollection
        /// 3. Only matching documents are fetched (server-side filtering)
        ///
        /// Currently, we fetch ALL documents and filter in memory (inefficient).
        /// This method will be removed when Phase 2/3 is implemented.
        /// </summary>
        private Func<object, bool>? CompileIncludeFilters(
            IncludeInfo includeInfo,
            Type entityType,
            QueryContext queryContext)
        {
            if (includeInfo.Filters.Count == 0 && includeInfo.OrFilterGroups.Count == 0)
                return null;

            // Build predicates for each filter clause
            var predicates = new List<Func<object, bool>>();

            foreach (var clause in includeInfo.Filters)
            {
                var predicate = CompileWhereClauseToPredicate(clause, entityType, queryContext);
                if (predicate != null)
                    predicates.Add(predicate);
            }

            // For OR groups, any clause matching is sufficient
            foreach (var orGroup in includeInfo.OrFilterGroups)
            {
                var orPredicates = orGroup.Clauses
                    .Select(c => CompileWhereClauseToPredicate(c, entityType, queryContext))
                    .Where(p => p != null)
                    .ToList();

                if (orPredicates.Count > 0)
                {
                    predicates.Add(obj => orPredicates.Any(p => p!(obj)));
                }
            }

            if (predicates.Count == 0)
                return null;

            // All predicates must match (AND logic)
            return obj => predicates.All(p => p(obj));
        }

        /// <summary>
        /// Compiles a single FirestoreWhereClause to an in-memory predicate.
        /// </summary>
        private Func<object, bool>? CompileWhereClauseToPredicate(
            FirestoreWhereClause clause,
            Type entityType,
            QueryContext queryContext)
        {
            try
            {
                var property = entityType.GetProperty(clause.PropertyName);
                if (property == null)
                    return null;

                // Resolve the value from the Expression
                object? value = clause.EvaluateValue(queryContext);

                return obj =>
                {
                    var propertyValue = property.GetValue(obj);
                    return CompareValues(propertyValue, value, clause.Operator);
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Compares two values using the specified Firestore operator.
        /// </summary>
        private static bool CompareValues(object? left, object? right, FirestoreOperator op)
        {
            if (left == null && right == null)
                return op == FirestoreOperator.EqualTo;
            if (left == null || right == null)
                return op == FirestoreOperator.NotEqualTo;

            try
            {
                var comparable = left as IComparable;
                if (comparable == null)
                    return op == FirestoreOperator.EqualTo ? Equals(left, right) : !Equals(left, right);

                // Convert right to same type as left for comparison
                var rightConverted = Convert.ChangeType(right, left.GetType());
                var comparison = comparable.CompareTo(rightConverted);

                return op switch
                {
                    FirestoreOperator.EqualTo => comparison == 0,
                    FirestoreOperator.NotEqualTo => comparison != 0,
                    FirestoreOperator.LessThan => comparison < 0,
                    FirestoreOperator.LessThanOrEqualTo => comparison <= 0,
                    FirestoreOperator.GreaterThan => comparison > 0,
                    FirestoreOperator.GreaterThanOrEqualTo => comparison >= 0,
                    _ => false
                };
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Visitor that resolves EF Core parameter expressions (e.g., __variableName_0)
        /// using values from QueryContext.ParameterValues.
        /// </summary>
        private class EfCoreParameterResolvingVisitor : ExpressionVisitor
        {
            private readonly IReadOnlyDictionary<string, object?> _parameterValues;

            public EfCoreParameterResolvingVisitor(IReadOnlyDictionary<string, object?> parameterValues)
            {
                _parameterValues = parameterValues;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                // Check if this is an EF Core parameter (starts with __)
                if (node.Name != null && node.Name.StartsWith("__"))
                {
                    if (_parameterValues.TryGetValue(node.Name, out var value))
                    {
                        return Expression.Constant(value, node.Type);
                    }
                }

                return base.VisitParameter(node);
            }
        }

        /// <summary>
        /// Visitor that evaluates closure references (captured variables) to their constant values.
        /// </summary>
        private class ClosureEvaluatingVisitor : ExpressionVisitor
        {
            protected override Expression VisitMember(MemberExpression node)
            {
                // Check if this is a closure reference (accessing a field on a constant object)
                if (node.Expression is ConstantExpression constantExpr && constantExpr.Value != null)
                {
                    var member = node.Member;
                    var value = member switch
                    {
                        System.Reflection.FieldInfo field => field.GetValue(constantExpr.Value),
                        System.Reflection.PropertyInfo prop => prop.GetValue(constantExpr.Value),
                        _ => throw new NotSupportedException($"Unsupported member type: {member.GetType()}")
                    };

                    return Expression.Constant(value, node.Type);
                }

                return base.VisitMember(node);
            }
        }

        #endregion
    }
}