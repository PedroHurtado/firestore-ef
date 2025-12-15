using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Metadata;
using Firestore.EntityFrameworkCore.Metadata.Conventions;
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
        public FirestoreShapedQueryCompilingExpressionVisitor(
            ShapedQueryCompilingExpressionVisitorDependencies dependencies,
            QueryCompilationContext queryCompilationContext)
            : base(dependencies, queryCompilationContext)
        {
        }

        protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
        {
            var firestoreQueryExpression = (FirestoreQueryExpression)shapedQueryExpression.QueryExpression;

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

            var entity = deserializer.DeserializeEntity<T>(documentSnapshot);

            // Cargar includes
            if (queryExpression.PendingIncludes.Count > 0)
            {
                LoadIncludes(entity, documentSnapshot, queryExpression.PendingIncludes, clientWrapper, deserializer, model, isTracking, dbContext)
                    .GetAwaiter().GetResult();
            }

            // Adjuntar al ChangeTracker como Unchanged para habilitar tracking de cambios
            // Solo si QueryTrackingBehavior es TrackAll (no NoTracking)
            if (isTracking)
            {
                dbContext.Attach(entity);
            }

            return entity;
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
