using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Metadata;
using Firestore.EntityFrameworkCore.Metadata.Conventions;
using Firestore.EntityFrameworkCore.Query;
using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Query.Visitors
{
    public class FirestoreShapedQueryCompilingExpressionVisitor : ShapedQueryCompilingExpressionVisitor
    {
        private readonly FirestoreQueryCompilationContext _firestoreContext;

        public FirestoreShapedQueryCompilingExpressionVisitor(
            ShapedQueryCompilingExpressionVisitorDependencies dependencies,
            QueryCompilationContext queryCompilationContext)
            : base(dependencies, queryCompilationContext)
        {
            // Direct cast - same pattern as Cosmos DB and other official providers
            _firestoreContext = (FirestoreQueryCompilationContext)queryCompilationContext;
        }

        protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
        {
            var firestoreQueryExpression = (FirestoreQueryExpression)shapedQueryExpression.QueryExpression;

            // Copy ComplexType Includes from FirestoreQueryCompilationContext to FirestoreQueryExpression
            var complexTypeIncludes = _firestoreContext.ComplexTypeIncludes;
            if (complexTypeIncludes.Count > 0)
            {
                firestoreQueryExpression = firestoreQueryExpression.Update(
                    complexTypeIncludes: new List<LambdaExpression>(complexTypeIncludes));
            }

            var entityType = firestoreQueryExpression.EntityType.ClrType;

            // Determinar si debemos trackear las entidades
            var isTracking = QueryCompilationContext.QueryTrackingBehavior == QueryTrackingBehavior.TrackAll;

            var queryContextParameter = Expression.Parameter(typeof(QueryContext), "queryContext");
            var documentSnapshotParameter = Expression.Parameter(typeof(DocumentSnapshot), "documentSnapshot");
            var isTrackingParameter = Expression.Parameter(typeof(bool), "isTracking");

            var shaperExpression = CreateShaperExpression(
                queryContextParameter,
                documentSnapshotParameter,
                isTrackingParameter,
                firestoreQueryExpression);

            var shaperLambda = Expression.Lambda(
                shaperExpression,
                queryContextParameter,
                documentSnapshotParameter,
                isTrackingParameter);

            var enumerableType = typeof(FirestoreQueryingEnumerable<>).MakeGenericType(entityType);
            var constructor = enumerableType.GetConstructor(new[]
            {
                typeof(QueryContext),
                typeof(FirestoreQueryExpression),
                typeof(Func<,,,>).MakeGenericType(typeof(QueryContext), typeof(DocumentSnapshot), typeof(bool), entityType),
                typeof(Type),
                typeof(bool)
            })!;

            var newExpression = Expression.New(
                constructor,
                QueryCompilationContext.QueryContextParameter,
                Expression.Constant(firestoreQueryExpression),
                Expression.Constant(shaperLambda.Compile()),
                Expression.Constant(entityType),
                Expression.Constant(isTracking));

            return newExpression;
        }

        #region Shaper Creation

        private Expression CreateShaperExpression(
            ParameterExpression queryContextParameter,
            ParameterExpression documentSnapshotParameter,
            ParameterExpression isTrackingParameter,
            FirestoreQueryExpression queryExpression)
        {
            var entityType = queryExpression.EntityType.ClrType;
            var deserializeMethod = typeof(FirestoreShapedQueryCompilingExpressionVisitor)
                .GetMethod(nameof(DeserializeEntity), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(entityType);

            return Expression.Call(
                deserializeMethod,
                queryContextParameter,
                documentSnapshotParameter,
                isTrackingParameter,
                Expression.Constant(queryExpression));
        }

        private static T DeserializeEntity<T>(
            QueryContext queryContext,
            DocumentSnapshot documentSnapshot,
            bool isTracking,
            FirestoreQueryExpression queryExpression) where T : class, new()
        {
            var dbContext = queryContext.Context;
            var serviceProvider = ((IInfrastructure<IServiceProvider>)dbContext).Instance;

            var model = dbContext.Model;

            // Identity Resolution: verificar si la entidad ya está trackeada antes de deserializar
            if (isTracking)
            {
                var existingEntity = TryGetTrackedEntity<T>(dbContext, documentSnapshot.Id);
                if (existingEntity != null)
                {
                    return existingEntity;
                }
            }

            var typeMappingSource = (ITypeMappingSource)serviceProvider.GetService(typeof(ITypeMappingSource))!;
            var collectionManager = (IFirestoreCollectionManager)serviceProvider.GetService(typeof(IFirestoreCollectionManager))!;
            var loggerFactory = (Microsoft.Extensions.Logging.ILoggerFactory)serviceProvider.GetService(typeof(Microsoft.Extensions.Logging.ILoggerFactory))!;
            var clientWrapper = (IFirestoreClientWrapper)serviceProvider.GetService(typeof(IFirestoreClientWrapper))!;

            var deserializerLogger = Microsoft.Extensions.Logging.LoggerFactoryExtensions.CreateLogger<Storage.FirestoreDocumentDeserializer>(loggerFactory);
            var deserializer = new Storage.FirestoreDocumentDeserializer(
                model,
                typeMappingSource,
                collectionManager,
                deserializerLogger);

            // Intentar crear proxy si lazy loading está habilitado
            var entity = TryCreateLazyLoadingProxy<T>(dbContext, serviceProvider);
            if (entity != null)
            {
                // Poblar el proxy con los datos del documento
                deserializer.DeserializeIntoEntity(documentSnapshot, entity);
            }
            else
            {
                // Crear entidad normal
                entity = deserializer.DeserializeEntity<T>(documentSnapshot);
            }

            // Cargar includes de navegaciones normales
            if (queryExpression.PendingIncludes.Count > 0)
            {
                LoadIncludes(entity, documentSnapshot, queryExpression.PendingIncludes, clientWrapper, deserializer, model, isTracking, dbContext)
                    .GetAwaiter().GetResult();
            }

            // Cargar includes en ComplexTypes (ej: .Include(e => e.DireccionPrincipal.SucursalCercana))
            if (queryExpression.ComplexTypeIncludes.Count > 0)
            {
                LoadComplexTypeIncludes(entity, documentSnapshot, queryExpression.ComplexTypeIncludes, clientWrapper, deserializer, model, isTracking, dbContext)
                    .GetAwaiter().GetResult();
            }

            // Adjuntar al ChangeTracker como Unchanged para habilitar tracking de cambios
            // Solo si QueryTrackingBehavior es TrackAll (no NoTracking)
            if (isTracking)
            {
                dbContext.Attach(entity);

                // Establecer shadow FK properties para navegaciones con DocumentReference
                SetShadowForeignKeys(entity, documentSnapshot, model.FindEntityType(typeof(T))!, dbContext);
            }

            return entity;
        }

        /// <summary>
        /// Sets shadow foreign key properties for navigations that use DocumentReference.
        /// This enables EF Core's Explicit Loading and Lazy Loading to work.
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
                // Skip collections
                if (navigation.IsCollection)
                    continue;

                // Skip if it's a subcollection
                if (navigation.IsSubCollection())
                    continue;

                // Get the DocumentReference from data
                if (!data.TryGetValue(navigation.Name, out var value))
                    continue;

                if (value is not Google.Cloud.Firestore.DocumentReference docRef)
                    continue;

                // Find the FK property for this navigation
                var foreignKey = navigation.ForeignKey;
                foreach (var fkProperty in foreignKey.Properties)
                {
                    // If it's a shadow property, set it via the entry
                    if (fkProperty.IsShadowProperty())
                    {
                        // Extract the ID from the DocumentReference
                        var referencedId = docRef.Id;

                        // Convert to the FK property type if needed
                        var convertedValue = ConvertKeyValue(referencedId, fkProperty);
                        entry.Property(fkProperty.Name).CurrentValue = convertedValue;
                    }
                }
            }
        }

        /// <summary>
        /// Identity Resolution: busca si la entidad ya está siendo trackeada usando IStateManager.
        /// Usa O(1) lookup por clave primaria.
        /// </summary>
        private static T? TryGetTrackedEntity<T>(DbContext dbContext, string documentId) where T : class
        {
            var entityType = dbContext.Model.FindEntityType(typeof(T));
            if (entityType == null) return null;

            var key = entityType.FindPrimaryKey();
            if (key == null) return null;

            if (key.Properties.Count == 0) return null;
            var keyProperty = key.Properties[0];

            // Convertir el ID del documento al tipo de la PK
            var convertedKey = ConvertKeyValue(documentId, keyProperty);
            var keyValues = new object[] { convertedKey };

            // Usar IStateManager para lookup O(1)
            var stateManager = dbContext.GetService<IStateManager>();
            var entry = stateManager.TryGetEntry(key, keyValues);

            return entry?.Entity as T;
        }

        /// <summary>
        /// Convierte el ID de Firestore (siempre string) al tipo de la clave primaria.
        /// Soporta ValueConverters configurados en el modelo.
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

        /// <summary>
        /// Identity Resolution para entidades incluidas (versión no genérica).
        /// Busca si la entidad ya está siendo trackeada usando IStateManager.
        /// </summary>
        private static object? TryGetTrackedEntity(DbContext dbContext, IReadOnlyEntityType entityType, string documentId)
        {
            var key = entityType.FindPrimaryKey();
            if (key == null) return null;

            if (key.Properties.Count == 0) return null;
            var keyProperty = key.Properties[0];

            // Convertir el ID del documento al tipo de la PK
            var convertedKey = ConvertKeyValue(documentId, keyProperty);
            var keyValues = new object[] { convertedKey };

            // Usar IStateManager para lookup O(1)
            var stateManager = dbContext.GetService<IStateManager>();
            var entry = stateManager.TryGetEntry((IKey)key, keyValues);

            return entry?.Entity;
        }

        #endregion

        #region Include Loading

        private static async Task LoadIncludes<T>(
            T entity,
            DocumentSnapshot documentSnapshot,
            List<IReadOnlyNavigation> allIncludes,
            IFirestoreClientWrapper clientWrapper,
            Storage.FirestoreDocumentDeserializer deserializer,
            IModel model,
            bool isTracking,
            DbContext dbContext) where T : class
        {
            var rootNavigations = allIncludes
                .Where(n => n.DeclaringEntityType == model.FindEntityType(typeof(T)))
                .ToList();

            var tasks = rootNavigations.Select(navigation =>
                LoadNavigationAsync(entity, documentSnapshot, navigation, allIncludes, clientWrapper, deserializer, model, isTracking, dbContext));

            await Task.WhenAll(tasks);
        }

        private static async Task LoadNavigationAsync(
            object entity,
            DocumentSnapshot documentSnapshot,
            IReadOnlyNavigation navigation,
            List<IReadOnlyNavigation> allIncludes,
            IFirestoreClientWrapper clientWrapper,
            Storage.FirestoreDocumentDeserializer deserializer,
            IModel model,
            bool isTracking,
            DbContext dbContext)
        {
            if (navigation.IsCollection)
            {
                await LoadSubCollectionAsync(entity, documentSnapshot, navigation, allIncludes, clientWrapper, deserializer, model, isTracking, dbContext);
            }
            else
            {
                await LoadReferenceAsync(entity, documentSnapshot, navigation, allIncludes, clientWrapper, deserializer, model, isTracking, dbContext);
            }
        }

        private static async Task LoadSubCollectionAsync(
            object parentEntity,
            DocumentSnapshot parentDoc,
            IReadOnlyNavigation navigation,
            List<IReadOnlyNavigation> allIncludes,
            IFirestoreClientWrapper clientWrapper,
            Storage.FirestoreDocumentDeserializer deserializer,
            IModel model,
            bool isTracking,
            DbContext dbContext)
        {
            if (!navigation.IsSubCollection())
                return;

            var subCollectionName = GetSubCollectionName(navigation);
            var subCollectionRef = parentDoc.Reference.Collection(subCollectionName);

            var snapshot = await subCollectionRef.GetSnapshotAsync();

            var listType = typeof(List<>).MakeGenericType(navigation.TargetEntityType.ClrType);
            var list = (System.Collections.IList)Activator.CreateInstance(listType)!;

            var deserializeMethod = typeof(Storage.FirestoreDocumentDeserializer)
                .GetMethod(nameof(Storage.FirestoreDocumentDeserializer.DeserializeEntity))!
                .MakeGenericMethod(navigation.TargetEntityType.ClrType);

            foreach (var doc in snapshot.Documents)
            {
                if (!doc.Exists)
                    continue;

                // Identity Resolution: verificar si la entidad ya está trackeada
                object? childEntity = null;
                if (isTracking)
                {
                    childEntity = TryGetTrackedEntity(dbContext, navigation.TargetEntityType, doc.Id);
                }

                // Si no está trackeada, deserializar
                if (childEntity == null)
                {
                    childEntity = deserializeMethod.Invoke(deserializer, new object[] { doc });
                    if (childEntity == null)
                        continue;

                    var childIncludes = allIncludes
                        .Where(inc => inc.DeclaringEntityType == navigation.TargetEntityType)
                        .ToList();

                    if (childIncludes.Count > 0)
                    {
                        var loadIncludesMethod = typeof(FirestoreShapedQueryCompilingExpressionVisitor)
                            .GetMethod(nameof(LoadIncludes), BindingFlags.NonPublic | BindingFlags.Static)!
                            .MakeGenericMethod(navigation.TargetEntityType.ClrType);

                        await (Task)loadIncludesMethod.Invoke(null, new object[]
                        {
                            childEntity, doc, allIncludes, clientWrapper, deserializer, model, isTracking, dbContext
                        })!;
                    }

                    // Adjuntar al ChangeTracker como Unchanged
                    if (isTracking)
                    {
                        dbContext.Attach(childEntity);
                    }
                }

                ApplyFixup(parentEntity, childEntity, navigation);

                list.Add(childEntity);
            }

            navigation.PropertyInfo?.SetValue(parentEntity, list);
        }

        private static async Task LoadReferenceAsync(
            object entity,
            DocumentSnapshot documentSnapshot,
            IReadOnlyNavigation navigation,
            List<IReadOnlyNavigation> allIncludes,
            IFirestoreClientWrapper clientWrapper,
            Storage.FirestoreDocumentDeserializer deserializer,
            IModel model,
            bool isTracking,
            DbContext dbContext)
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

            // Obtener el ID de la referencia para identity resolution
            string? referencedId = null;
            DocumentSnapshot? referencedDoc = null;

            if (referenceValue is Google.Cloud.Firestore.DocumentReference docRef)
            {
                referencedId = docRef.Id;

                // Identity Resolution: verificar si la entidad ya está trackeada
                if (isTracking)
                {
                    var existingEntity = TryGetTrackedEntity(dbContext, navigation.TargetEntityType, referencedId);
                    if (existingEntity != null)
                    {
                        ApplyFixup(entity, existingEntity, navigation);
                        navigation.PropertyInfo?.SetValue(entity, existingEntity);
                        return;
                    }
                }

                referencedDoc = await docRef.GetSnapshotAsync();
            }
            else if (referenceValue is string id)
            {
                referencedId = id;

                // Identity Resolution: verificar si la entidad ya está trackeada
                if (isTracking)
                {
                    var existingEntity = TryGetTrackedEntity(dbContext, navigation.TargetEntityType, referencedId);
                    if (existingEntity != null)
                    {
                        ApplyFixup(entity, existingEntity, navigation);
                        navigation.PropertyInfo?.SetValue(entity, existingEntity);
                        return;
                    }
                }

                var targetEntityType = model.FindEntityType(navigation.TargetEntityType.ClrType);
                if (targetEntityType != null)
                {
                    var collectionName = GetCollectionNameForEntityType(targetEntityType);
                    var docRefFromId = clientWrapper.Database.Collection(collectionName).Document(id);
                    referencedDoc = await docRefFromId.GetSnapshotAsync();
                }
            }

            if (referencedDoc == null || !referencedDoc.Exists)
                return;

            var deserializeMethod = typeof(Storage.FirestoreDocumentDeserializer)
                .GetMethod(nameof(Storage.FirestoreDocumentDeserializer.DeserializeEntity))!
                .MakeGenericMethod(navigation.TargetEntityType.ClrType);

            var referencedEntity = deserializeMethod.Invoke(deserializer, new object[] { referencedDoc });

            if (referencedEntity != null)
            {
                var childIncludes = allIncludes
                    .Where(inc => inc.DeclaringEntityType == navigation.TargetEntityType)
                    .ToList();

                if (childIncludes.Count > 0)
                {
                    var loadIncludesMethod = typeof(FirestoreShapedQueryCompilingExpressionVisitor)
                        .GetMethod(nameof(LoadIncludes), BindingFlags.NonPublic | BindingFlags.Static)!
                        .MakeGenericMethod(navigation.TargetEntityType.ClrType);

                    await (Task)loadIncludesMethod.Invoke(null, new object[]
                    {
                        referencedEntity, referencedDoc, allIncludes, clientWrapper, deserializer, model, isTracking, dbContext
                    })!;
                }

                // Adjuntar al ChangeTracker como Unchanged
                if (isTracking)
                {
                    dbContext.Attach(referencedEntity);
                }

                ApplyFixup(entity, referencedEntity, navigation);

                navigation.PropertyInfo?.SetValue(entity, referencedEntity);
            }
        }

        private static void ApplyFixup(
            object parent,
            object child,
            IReadOnlyNavigation navigation)
        {
            if (navigation.Inverse != null)
            {
                var inverseProperty = navigation.Inverse.PropertyInfo;
                if (inverseProperty != null)
                {
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
            }
        }

        /// <summary>
        /// Loads references inside ComplexTypes based on extracted Include expressions.
        /// Example: .Include(e => e.DireccionPrincipal.SucursalCercana)
        /// </summary>
        private static async Task LoadComplexTypeIncludes<T>(
            T entity,
            DocumentSnapshot documentSnapshot,
            List<LambdaExpression> complexTypeIncludes,
            IFirestoreClientWrapper clientWrapper,
            Storage.FirestoreDocumentDeserializer deserializer,
            IModel model,
            bool isTracking,
            DbContext dbContext) where T : class
        {
            var data = documentSnapshot.ToDictionary();

            foreach (var includeExpr in complexTypeIncludes)
            {
                await LoadComplexTypeInclude(entity, data, includeExpr, clientWrapper, deserializer, model, isTracking, dbContext);
            }
        }

        /// <summary>
        /// Loads a single reference inside a ComplexType.
        /// Parses the expression to get: ComplexTypeProperty.ReferenceProperty
        /// </summary>
        private static async Task LoadComplexTypeInclude(
            object entity,
            Dictionary<string, object> data,
            LambdaExpression includeExpr,
            IFirestoreClientWrapper clientWrapper,
            Storage.FirestoreDocumentDeserializer deserializer,
            IModel model,
            bool isTracking,
            DbContext dbContext)
        {
            // Parse the expression: e => e.DireccionPrincipal.SucursalCercana
            // We need to extract: ComplexTypeProp = DireccionPrincipal, ReferenceProp = SucursalCercana
            if (includeExpr.Body is not MemberExpression refMemberExpr)
                return;

            var referenceProperty = refMemberExpr.Member as PropertyInfo;
            if (referenceProperty == null)
                return;

            if (refMemberExpr.Expression is not MemberExpression complexTypeMemberExpr)
                return;

            var complexTypeProperty = complexTypeMemberExpr.Member as PropertyInfo;
            if (complexTypeProperty == null)
                return;

            // Get the ComplexType instance from the entity
            var complexTypeInstance = complexTypeProperty.GetValue(entity);
            if (complexTypeInstance == null)
                return;

            // Get the raw data for the ComplexType from the document
            if (!data.TryGetValue(complexTypeProperty.Name, out var complexTypeData) ||
                complexTypeData is not Dictionary<string, object> complexTypeDict)
                return;

            // Get the DocumentReference from the ComplexType data
            if (!complexTypeDict.TryGetValue(referenceProperty.Name, out var referenceValue))
                return;

            if (referenceValue == null)
                return;

            // Load the referenced entity
            DocumentSnapshot? referencedDoc = null;
            string? referencedId = null;

            if (referenceValue is Google.Cloud.Firestore.DocumentReference docRef)
            {
                referencedId = docRef.Id;
                referencedDoc = await docRef.GetSnapshotAsync();
            }
            else if (referenceValue is string id)
            {
                referencedId = id;
                var targetType = referenceProperty.PropertyType;
                var targetEntityType = model.FindEntityType(targetType);
                if (targetEntityType != null)
                {
                    var collectionName = GetCollectionNameForEntityType(targetEntityType);
                    var docRefFromId = clientWrapper.Database.Collection(collectionName).Document(id);
                    referencedDoc = await docRefFromId.GetSnapshotAsync();
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
                    var existingEntity = TryGetTrackedEntity(dbContext, targetEntityType, referencedId);
                    if (existingEntity != null)
                    {
                        referenceProperty.SetValue(complexTypeInstance, existingEntity);
                        return;
                    }
                }
            }

            // Deserialize the referenced entity
            var deserializeMethod = typeof(Storage.FirestoreDocumentDeserializer)
                .GetMethod(nameof(Storage.FirestoreDocumentDeserializer.DeserializeEntity))!
                .MakeGenericMethod(referenceProperty.PropertyType);

            var referencedEntity = deserializeMethod.Invoke(deserializer, new object[] { referencedDoc });

            if (referencedEntity != null)
            {
                // Track the referenced entity
                if (isTracking)
                {
                    dbContext.Attach(referencedEntity);
                }

                // Set the reference property on the ComplexType instance
                referenceProperty.SetValue(complexTypeInstance, referencedEntity);
            }
        }

        #endregion

        #region Lazy Loading Proxy Support

        /// <summary>
        /// Attempts to create a lazy loading proxy for the entity type.
        /// Returns null if lazy loading proxies are not enabled.
        /// Uses reflection to avoid direct dependency on Microsoft.EntityFrameworkCore.Proxies.
        /// </summary>
        private static T? TryCreateLazyLoadingProxy<T>(DbContext dbContext, IServiceProvider serviceProvider) where T : class
        {
            try
            {
                // Find Proxies assembly (only loaded if UseLazyLoadingProxies() was called)
                var proxiesAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Microsoft.EntityFrameworkCore.Proxies");
                if (proxiesAssembly == null)
                    return null;

                // Get IProxyFactory type and service
                var proxyFactoryType = proxiesAssembly.GetTypes()
                    .FirstOrDefault(t => t.Name == "IProxyFactory");
                if (proxyFactoryType == null)
                    return null;

                var proxyFactory = serviceProvider.GetService(proxyFactoryType);
                if (proxyFactory == null)
                    return null;

                var entityType = dbContext.Model.FindEntityType(typeof(T));
                if (entityType == null)
                    return null;

                // Get ILazyLoader type from Abstractions assembly
                var lazyLoaderType = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.GetName().Name == "Microsoft.EntityFrameworkCore.Abstractions")
                    .SelectMany(a => a.GetTypes())
                    .FirstOrDefault(t => t.Name == "ILazyLoader");
                if (lazyLoaderType == null)
                    return null;

                // Get or create LazyLoader instance
                object? lazyLoader = serviceProvider.GetService(lazyLoaderType);
                if (lazyLoader == null)
                {
                    // LazyLoader is not a singleton - create via factory
                    var loaderFactoryType = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => a.GetTypes())
                        .FirstOrDefault(t => t.Name == "ILazyLoaderFactory");

                    if (loaderFactoryType != null)
                    {
                        var loaderFactory = serviceProvider.GetService(loaderFactoryType);
                        var createMethod = loaderFactoryType.GetMethod("Create");
                        if (loaderFactory != null && createMethod != null)
                        {
                            lazyLoader = createMethod.Invoke(loaderFactory, new object[] { dbContext });
                        }
                    }
                }

                if (lazyLoader == null)
                    return null;

                // Find and invoke CreateLazyLoadingProxy(DbContext, IEntityType, ILazyLoader, object[])
                var createProxyMethod = proxyFactoryType.GetMethods()
                    .FirstOrDefault(m => m.Name == "CreateLazyLoadingProxy" && m.GetParameters().Length == 4);
                if (createProxyMethod == null)
                    return null;

                var proxy = createProxyMethod.Invoke(proxyFactory, new object[]
                {
                    dbContext,
                    entityType,
                    lazyLoader,
                    Array.Empty<object>()
                });

                return proxy as T;
            }
            catch
            {
                // If proxy creation fails for any reason, fall back to normal entity
                return null;
            }
        }

        #endregion

        #region Helper Methods

        private static string GetSubCollectionName(IReadOnlyNavigation navigation)
        {
            var childEntityType = navigation.ForeignKey.DeclaringEntityType;

            // Pluralizar el nombre del tipo de entidad
            return Pluralize(childEntityType.ClrType.Name);
        }

        private static string GetCollectionNameForEntityType(IEntityType entityType)
        {
            var tableAttribute = entityType.ClrType
                .GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.TableAttribute>();

            if (tableAttribute != null && !string.IsNullOrEmpty(tableAttribute.Name))
                return tableAttribute.Name;

            return Pluralize(entityType.ClrType.Name);
        }

        private static string Pluralize(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            if (name.EndsWith("y", StringComparison.OrdinalIgnoreCase) &&
                name.Length > 1 &&
                !IsVowel(name[name.Length - 2]))
            {
                return name.Substring(0, name.Length - 1) + "ies";
            }

            if (name.EndsWith("s", StringComparison.OrdinalIgnoreCase))
                return name + "es";

            return name + "s";
        }

        private static bool IsVowel(char c)
        {
            c = char.ToLowerInvariant(c);
            return c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u';
        }

        #endregion
    }
}
