using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;
using Firestore.EntityFrameworkCore.Storage;
using Firestore.EntityFrameworkCore.Update;
using Firestore.EntityFrameworkCore.Query;
using Firestore.EntityFrameworkCore.Metadata.Conventions;
using Firestore.EntityFrameworkCore.Metadata;
using Firestore.EntityFrameworkCore.Infrastructure.Internal;
using Google.Cloud.Firestore;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Linq;

namespace Firestore.EntityFrameworkCore.Infrastructure
{
    public static class FirestoreServiceCollectionExtensions
    {
        public static IServiceCollection AddEntityFrameworkFirestore(
            this IServiceCollection serviceCollection)
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);

            serviceCollection.AddSingleton<LoggingDefinitions, FirestoreLoggingDefinitions>();
            serviceCollection.AddLogging();

            var builder = new EntityFrameworkServicesBuilder(serviceCollection);

            builder.TryAddCoreServices();

            serviceCollection.AddScoped<IProviderConventionSetBuilder, FirestoreConventionSetBuilder>();

            builder
                .TryAdd<IDatabaseProvider, DatabaseProvider<FirestoreOptionsExtension>>()
                .TryAdd<IDatabase, FirestoreDatabase>()
                .TryAdd<IDbContextTransactionManager, FirestoreTransactionManager>()
                .TryAdd<IQueryContextFactory, FirestoreQueryContextFactory>()
                .TryAdd<IQueryCompilationContextFactory, FirestoreQueryCompilationContextFactory>()
                .TryAdd<IQueryableMethodTranslatingExpressionVisitorFactory, FirestoreQueryableMethodTranslatingExpressionVisitorFactory>()
                .TryAdd<IShapedQueryCompilingExpressionVisitorFactory, FirestoreShapedQueryCompilingExpressionVisitorFactory>()
                .TryAdd<ITypeMappingSource, FirestoreTypeMappingSource>()
                .TryAdd<IModelValidator, FirestoreModelValidator>()
                .TryAdd<IDatabaseCreator, FirestoreDatabaseCreator>()
                .TryAdd<IExecutionStrategyFactory, FirestoreExecutionStrategyFactory>()
                .TryAddProviderSpecificServices(b => b
                    .TryAddScoped<IUpdateSqlGenerator, FirestoreUpdateSqlGenerator>()
                    .TryAddScoped<IModificationCommandBatchFactory, FirestoreModificationCommandBatchFactory>()
                    .TryAddScoped<IFirestoreClientWrapper, FirestoreClientWrapper>()
                    .TryAddSingleton<IFirestoreIdGenerator, FirestoreIdGenerator>()
                    .TryAddSingleton<IFirestoreDocumentSerializer, FirestoreDocumentSerializer>()
                    .TryAddScoped<IFirestoreCollectionManager, FirestoreCollectionManager>());

            return serviceCollection;
        }
    }

    #region QueryableMethodTranslatingExpressionVisitor

    public class FirestoreQueryableMethodTranslatingExpressionVisitorFactory
        : IQueryableMethodTranslatingExpressionVisitorFactory
    {
        private readonly QueryableMethodTranslatingExpressionVisitorDependencies _dependencies;

        public FirestoreQueryableMethodTranslatingExpressionVisitorFactory(
            QueryableMethodTranslatingExpressionVisitorDependencies dependencies)
        {
            _dependencies = dependencies;
        }

        public QueryableMethodTranslatingExpressionVisitor Create(QueryCompilationContext queryCompilationContext)
        {
            return new FirestoreQueryableMethodTranslatingExpressionVisitor(_dependencies, queryCompilationContext);
        }
    }

    public class FirestoreQueryableMethodTranslatingExpressionVisitor
        : QueryableMethodTranslatingExpressionVisitor
    {
        public FirestoreQueryableMethodTranslatingExpressionVisitor(
            QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
            QueryCompilationContext queryCompilationContext)
            : base(dependencies, queryCompilationContext, subquery: false)
        {
        }

        protected FirestoreQueryableMethodTranslatingExpressionVisitor(
            FirestoreQueryableMethodTranslatingExpressionVisitor parentVisitor)
            : base(parentVisitor.Dependencies, parentVisitor.QueryCompilationContext, subquery: true)
        {
        }

        protected override ShapedQueryExpression CreateShapedQueryExpression(IEntityType entityType)
        {
            var collectionName = GetCollectionName(entityType);
            var queryExpression = new Query.FirestoreQueryExpression(entityType, collectionName);

            var entityShaperExpression = new StructuralTypeShaperExpression(
                entityType,
                new ProjectionBindingExpression(
                    queryExpression,
                    new ProjectionMember(),
                    typeof(ValueBuffer)),
                nullable: false);

            return new ShapedQueryExpression(queryExpression, entityShaperExpression);
        }

        private string GetCollectionName(IEntityType entityType)
        {
            var tableAttribute = entityType.ClrType
                .GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.TableAttribute>();

            if (tableAttribute != null && !string.IsNullOrEmpty(tableAttribute.Name))
                return tableAttribute.Name;

            var entityName = entityType.ClrType.Name;
            return Pluralize(entityName);
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

        protected override QueryableMethodTranslatingExpressionVisitor CreateSubqueryVisitor()
        {
            return new FirestoreQueryableMethodTranslatingExpressionVisitor(this);
        }

        #region Translate Methods

        protected override ShapedQueryExpression? TranslateFirstOrDefault(
            ShapedQueryExpression source,
            LambdaExpression? predicate,
            Type returnType,
            bool returnDefault)
        {
            if (predicate != null)
            {
                source = TranslateWhere(source, predicate) ?? source;
            }

            var firestoreQueryExpression = (Query.FirestoreQueryExpression)source.QueryExpression;
            var newQueryExpression = firestoreQueryExpression.WithLimit(1);

            return source.UpdateQueryExpression(newQueryExpression);
        }

        protected override ShapedQueryExpression TranslateSelect(
            ShapedQueryExpression source,
            LambdaExpression selector)
        {
            Console.WriteLine($"🔍 TranslateSelect - selector.Body type: {selector.Body.GetType().Name}");

            // ✅ CRÍTICO: Procesar includes aquí donde SÍ llegan
            if (selector.Body is Microsoft.EntityFrameworkCore.Query.IncludeExpression includeExpression)
            {
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine("✓ Detected IncludeExpression in TranslateSelect");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

                // Usar el visitor especializado para extraer TODOS los includes
                var includeVisitor = new IncludeExtractionVisitor();
                includeVisitor.Visit(includeExpression);

                // Agregar todos los includes detectados al query
                var firestoreQueryExpression = (Query.FirestoreQueryExpression)source.QueryExpression;

                Console.WriteLine($"\n📋 Includes detectados por el visitor:");
                foreach (var navigation in includeVisitor.DetectedNavigations)
                {
                    Console.WriteLine($"  → {navigation.DeclaringEntityType.ClrType.Name}.{navigation.Name} " +
                                    $"(IsCollection: {navigation.IsCollection}, Target: {navigation.TargetEntityType.ClrType.Name})");
                    firestoreQueryExpression.PendingIncludes.Add(navigation);
                }

                Console.WriteLine($"\n✅ Total includes capturados: {includeVisitor.DetectedNavigations.Count}");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
            }

            // Proyección de identidad (x => x)
            if (selector.Body == selector.Parameters[0])
            {
                return source;
            }

            // Proyección con conversión de tipo
            if (selector.Body is UnaryExpression unary &&
                unary.NodeType == ExpressionType.Convert &&
                unary.Operand == selector.Parameters[0])
            {
                return source;
            }

            return source;
        }

        protected override ShapedQueryExpression? TranslateWhere(
            ShapedQueryExpression source,
            LambdaExpression predicate)
        {
            var firestoreQueryExpression = (Query.FirestoreQueryExpression)source.QueryExpression;

            var parameterReplacer = new RuntimeParameterReplacer(QueryCompilationContext);
            var evaluatedBody = parameterReplacer.Visit(predicate.Body);

            var translator = new FirestoreWhereTranslator();
            var whereClause = translator.Translate(evaluatedBody);

            if (whereClause == null)
            {
                return null;
            }

            // Detección de queries por ID
            if (whereClause.PropertyName == "Id")
            {
                if (whereClause.Operator != Query.FirestoreOperator.EqualTo)
                {
                    throw new InvalidOperationException(
                        $"Firestore ID queries only support the '==' operator.");
                }

                if (firestoreQueryExpression.Filters.Count > 0)
                {
                    throw new InvalidOperationException(
                        "Firestore ID queries cannot be combined with other filters.");
                }

                if (firestoreQueryExpression.IsIdOnlyQuery)
                {
                    throw new InvalidOperationException(
                        "Cannot apply multiple ID filters.");
                }

                var newQueryExpression = new Query.FirestoreQueryExpression(
                    firestoreQueryExpression.EntityType,
                    firestoreQueryExpression.CollectionName)
                {
                    IdValueExpression = whereClause.ValueExpression,
                    Filters = new List<Query.FirestoreWhereClause>(firestoreQueryExpression.Filters),
                    OrderByClauses = new List<Query.FirestoreOrderByClause>(firestoreQueryExpression.OrderByClauses),
                    Limit = firestoreQueryExpression.Limit,
                    StartAfterDocument = firestoreQueryExpression.StartAfterDocument,
                    PendingIncludes = firestoreQueryExpression.PendingIncludes // 🔥 MANTENER INCLUDES
                };

                return source.UpdateQueryExpression(newQueryExpression);
            }

            if (firestoreQueryExpression.IsIdOnlyQuery)
            {
                throw new InvalidOperationException(
                    "Cannot add filters to an ID-only query.");
            }

            var normalQueryExpression = firestoreQueryExpression.AddFilter(whereClause);
            return source.UpdateQueryExpression(normalQueryExpression);
        }

        #endregion

        #region Not Implemented Methods

        protected override ShapedQueryExpression? TranslateAll(ShapedQueryExpression source, LambdaExpression predicate)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateAny(ShapedQueryExpression source, LambdaExpression? predicate)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateAverage(ShapedQueryExpression source, LambdaExpression? selector, Type resultType)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateCast(ShapedQueryExpression source, Type castType)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateConcat(ShapedQueryExpression source1, ShapedQueryExpression source2)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateContains(ShapedQueryExpression source, Expression item)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateCount(ShapedQueryExpression source, LambdaExpression? predicate)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateDefaultIfEmpty(ShapedQueryExpression source, Expression? defaultValue)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateDistinct(ShapedQueryExpression source)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateElementAtOrDefault(ShapedQueryExpression source, Expression index, bool returnDefault)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateExcept(ShapedQueryExpression source1, ShapedQueryExpression source2)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateGroupBy(ShapedQueryExpression source, LambdaExpression keySelector, LambdaExpression? elementSelector, LambdaExpression? resultSelector)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateGroupJoin(ShapedQueryExpression outer, ShapedQueryExpression inner, LambdaExpression outerKeySelector, LambdaExpression innerKeySelector, LambdaExpression resultSelector)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateIntersect(ShapedQueryExpression source1, ShapedQueryExpression source2)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateJoin(ShapedQueryExpression outer, ShapedQueryExpression inner, LambdaExpression outerKeySelector, LambdaExpression innerKeySelector, LambdaExpression resultSelector)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateLastOrDefault(ShapedQueryExpression source, LambdaExpression? predicate, Type returnType, bool returnDefault)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateLeftJoin(ShapedQueryExpression outer, ShapedQueryExpression inner, LambdaExpression outerKeySelector, LambdaExpression innerKeySelector, LambdaExpression resultSelector)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateLongCount(ShapedQueryExpression source, LambdaExpression? predicate)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateMax(ShapedQueryExpression source, LambdaExpression? selector, Type resultType)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateMin(ShapedQueryExpression source, LambdaExpression? selector, Type resultType)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateOfType(ShapedQueryExpression source, Type resultType)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateOrderBy(ShapedQueryExpression source, LambdaExpression keySelector, bool ascending)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateReverse(ShapedQueryExpression source)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateSelectMany(ShapedQueryExpression source, LambdaExpression collectionSelector, LambdaExpression resultSelector)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateSelectMany(ShapedQueryExpression source, LambdaExpression selector)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateSingleOrDefault(ShapedQueryExpression source, LambdaExpression? predicate, Type returnType, bool returnDefault)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateSkip(ShapedQueryExpression source, Expression count)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateSkipWhile(ShapedQueryExpression source, LambdaExpression predicate)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateSum(ShapedQueryExpression source, LambdaExpression? selector, Type resultType)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateTake(ShapedQueryExpression source, Expression count)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateTakeWhile(ShapedQueryExpression source, LambdaExpression predicate)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateThenBy(ShapedQueryExpression source, LambdaExpression keySelector, bool ascending)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateUnion(ShapedQueryExpression source1, ShapedQueryExpression source2)
            => throw new NotImplementedException();

        #endregion
    }

    /// <summary>
    /// ✅ Visitor especializado para extraer TODOS los includes del árbol de expresiones
    /// </summary>
    internal class IncludeExtractionVisitor : ExpressionVisitor
    {
        public List<IReadOnlyNavigation> DetectedNavigations { get; } = new();
        private int _depth = 0;

        protected override Expression VisitExtension(Expression node)
        {
            if (node is Microsoft.EntityFrameworkCore.Query.IncludeExpression includeExpression)
            {
                // Capturar esta navegación
                if (includeExpression.Navigation is IReadOnlyNavigation navigation)
                {
                    Console.WriteLine($"{GetIndent()}✓ Captured Include: {navigation.Name}");
                    DetectedNavigations.Add(navigation);
                }

                // 🔥 CRÍTICO: Visitar EntityExpression y NavigationExpression
                // para encontrar ThenInclude anidados
                _depth++;
                Visit(includeExpression.EntityExpression);
                Visit(includeExpression.NavigationExpression);
                _depth--;

                return node; // No llamar a base, ya visitamos manualmente
            }

            // Para otras expresiones, dejar que el visitor base maneje la recursión
            return base.VisitExtension(node);
        }

        private string GetIndent() => new string(' ', _depth * 2);
    }

    #endregion

    #region ShapedQueryCompilingExpressionVisitor

    public class FirestoreShapedQueryCompilingExpressionVisitorFactory
        : IShapedQueryCompilingExpressionVisitorFactory
    {
        private readonly ShapedQueryCompilingExpressionVisitorDependencies _dependencies;

        public FirestoreShapedQueryCompilingExpressionVisitorFactory(
            ShapedQueryCompilingExpressionVisitorDependencies dependencies)
        {
            _dependencies = dependencies;
        }

        public ShapedQueryCompilingExpressionVisitor Create(QueryCompilationContext queryCompilationContext)
        {
            return new FirestoreShapedQueryCompilingExpressionVisitor(_dependencies, queryCompilationContext);
        }
    }

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
            var firestoreQueryExpression = (Query.FirestoreQueryExpression)shapedQueryExpression.QueryExpression;

            // ✅ Los includes ya fueron capturados en TranslateSelect
            PrintIncludesSummary(firestoreQueryExpression);

            var entityType = firestoreQueryExpression.EntityType.ClrType;

            var queryContextParameter = Expression.Parameter(typeof(QueryContext), "queryContext");
            var documentSnapshotParameter = Expression.Parameter(typeof(DocumentSnapshot), "documentSnapshot");

            var shaperExpression = CreateShaperExpression(
                queryContextParameter,
                documentSnapshotParameter,
                firestoreQueryExpression);

            var shaperLambda = Expression.Lambda(
                shaperExpression,
                queryContextParameter,
                documentSnapshotParameter);

            var enumerableType = typeof(Query.FirestoreQueryingEnumerable<>).MakeGenericType(entityType);
            var constructor = enumerableType.GetConstructor(new[]
            {
                typeof(QueryContext),
                typeof(Query.FirestoreQueryExpression),
                typeof(Func<,,>).MakeGenericType(typeof(QueryContext), typeof(DocumentSnapshot), entityType),
                typeof(Type)
            })!;

            var newExpression = Expression.New(
                constructor,
                QueryCompilationContext.QueryContextParameter,
                Expression.Constant(firestoreQueryExpression),
                Expression.Constant(shaperLambda.Compile()),
                Expression.Constant(entityType));

            return newExpression;
        }

        #region Shaper Creation

        private Expression CreateShaperExpression(
            ParameterExpression queryContextParameter,
            ParameterExpression documentSnapshotParameter,
            Query.FirestoreQueryExpression queryExpression)
        {
            var entityType = queryExpression.EntityType.ClrType;
            var deserializeMethod = typeof(FirestoreShapedQueryCompilingExpressionVisitor)
                .GetMethod(nameof(DeserializeEntity), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(entityType);

            return Expression.Call(
                deserializeMethod,
                queryContextParameter,
                documentSnapshotParameter,
                Expression.Constant(queryExpression));
        }

        private static T DeserializeEntity<T>(
            QueryContext queryContext,
            DocumentSnapshot documentSnapshot,
            Query.FirestoreQueryExpression queryExpression) where T : class, new()
        {
            var dbContext = queryContext.Context;
            var serviceProvider = ((IInfrastructure<IServiceProvider>)dbContext).Instance;

            var model = dbContext.Model;
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

            // 🔥 Cargar includes
            if (queryExpression.PendingIncludes.Count > 0)
            {
                LoadIncludes(entity, documentSnapshot, queryExpression.PendingIncludes, clientWrapper, deserializer, model)
                    .GetAwaiter().GetResult();
            }

            return entity;
        }

        #endregion

        #region Include Loading (El resto del código permanece igual)

        private static async Task LoadIncludes<T>(
            T entity,
            DocumentSnapshot documentSnapshot,
            List<IReadOnlyNavigation> allIncludes,
            IFirestoreClientWrapper clientWrapper,
            Storage.FirestoreDocumentDeserializer deserializer,
            IModel model) where T : class
        {
            var rootNavigations = allIncludes
                .Where(n => n.DeclaringEntityType == model.FindEntityType(typeof(T)))
                .ToList();

            var tasks = rootNavigations.Select(navigation =>
                LoadNavigationAsync(entity, documentSnapshot, navigation, allIncludes, clientWrapper, deserializer, model));

            await Task.WhenAll(tasks);
        }

        private static async Task LoadNavigationAsync(
            object entity,
            DocumentSnapshot documentSnapshot,
            IReadOnlyNavigation navigation,
            List<IReadOnlyNavigation> allIncludes,
            IFirestoreClientWrapper clientWrapper,
            Storage.FirestoreDocumentDeserializer deserializer,
            IModel model)
        {
            if (navigation.IsCollection)
            {
                await LoadSubCollectionAsync(entity, documentSnapshot, navigation, allIncludes, clientWrapper, deserializer, model);
            }
            else
            {
                await LoadReferenceAsync(entity, documentSnapshot, navigation, allIncludes, clientWrapper, deserializer, model);
            }
        }

        private static async Task LoadSubCollectionAsync(
            object parentEntity,
            DocumentSnapshot parentDoc,
            IReadOnlyNavigation navigation,
            List<IReadOnlyNavigation> allIncludes,
            IFirestoreClientWrapper clientWrapper,
            Storage.FirestoreDocumentDeserializer deserializer,
            IModel model)
        {
            if (!navigation.IsSubCollection())
            {
                Console.WriteLine($"⚠ Navigation '{navigation.Name}' is not a subcollection, skipping");
                return;
            }

            var subCollectionName = GetSubCollectionName(navigation);
            var subCollectionRef = parentDoc.Reference.Collection(subCollectionName);

            Console.WriteLine($"📂 Loading subcollection: {parentDoc.Reference.Path}/{subCollectionName}");

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

                var childEntity = deserializeMethod.Invoke(deserializer, new object[] { doc });
                if (childEntity == null)
                    continue;

                var childIncludes = allIncludes
                    .Where(inc => inc.DeclaringEntityType == navigation.TargetEntityType)
                    .ToList();

                if (childIncludes.Count > 0)
                {
                    Console.WriteLine($"  🔁 Loading {childIncludes.Count} nested include(s) for {navigation.TargetEntityType.ClrType.Name}");

                    var loadIncludesMethod = typeof(FirestoreShapedQueryCompilingExpressionVisitor)
                        .GetMethod(nameof(LoadIncludes), BindingFlags.NonPublic | BindingFlags.Static)!
                        .MakeGenericMethod(navigation.TargetEntityType.ClrType);

                    await (Task)loadIncludesMethod.Invoke(null, new object[]
                    {
                        childEntity, doc, allIncludes, clientWrapper, deserializer, model
                    })!;
                }

                ApplyFixup(parentEntity, childEntity, navigation);

                list.Add(childEntity);
            }

            navigation.PropertyInfo?.SetValue(parentEntity, list);
            Console.WriteLine($"✅ Loaded {list.Count} item(s) for {navigation.Name}");
        }

        private static async Task LoadReferenceAsync(
            object entity,
            DocumentSnapshot documentSnapshot,
            IReadOnlyNavigation navigation,
            List<IReadOnlyNavigation> allIncludes,
            IFirestoreClientWrapper clientWrapper,
            Storage.FirestoreDocumentDeserializer deserializer,
            IModel model)
        {
            Console.WriteLine($"🔗 Loading reference: {navigation.Name}");

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
            {
                Console.WriteLine($"⚠ Reference field not found for {navigation.Name}");
                return;
            }

            DocumentSnapshot? referencedDoc = null;

            if (referenceValue is Google.Cloud.Firestore.DocumentReference docRef)
            {
                Console.WriteLine($"  → Found DocumentReference: {docRef.Path}");
                referencedDoc = await docRef.GetSnapshotAsync();
            }
            else if (referenceValue is string id)
            {
                var targetEntityType = model.FindEntityType(navigation.TargetEntityType.ClrType);
                if (targetEntityType != null)
                {
                    var collectionName = GetCollectionNameForEntityType(targetEntityType);
                    var docRefFromId = clientWrapper.Database.Collection(collectionName).Document(id);
                    Console.WriteLine($"  → Constructed reference from ID: {docRefFromId.Path}");
                    referencedDoc = await docRefFromId.GetSnapshotAsync();
                }
            }

            if (referencedDoc == null || !referencedDoc.Exists)
            {
                Console.WriteLine($"⚠ Referenced document not found");
                return;
            }

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
                    Console.WriteLine($"  🔁 Loading {childIncludes.Count} nested include(s) for reference {navigation.Name}");

                    var loadIncludesMethod = typeof(FirestoreShapedQueryCompilingExpressionVisitor)
                        .GetMethod(nameof(LoadIncludes), BindingFlags.NonPublic | BindingFlags.Static)!
                        .MakeGenericMethod(navigation.TargetEntityType.ClrType);

                    await (Task)loadIncludesMethod.Invoke(null, new object[]
                    {
                        referencedEntity, referencedDoc, allIncludes, clientWrapper, deserializer, model
                    })!;
                }

                ApplyFixup(entity, referencedEntity, navigation);

                navigation.PropertyInfo?.SetValue(entity, referencedEntity);
                Console.WriteLine($"✅ Loaded reference {navigation.Name}");
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
                        Console.WriteLine($"  🔗 Fixup: {child.GetType().Name}.{inverseProperty.Name} → {parent.GetType().Name}");
                    }
                    else
                    {
                        if (navigation.Inverse.IsCollection)
                        {
                            var collection = inverseProperty.GetValue(parent) as System.Collections.IList;
                            if (collection != null && !collection.Contains(child))
                            {
                                collection.Add(child);
                                Console.WriteLine($"  🔗 Fixup: Added to {parent.GetType().Name}.{inverseProperty.Name}");
                            }
                        }
                        else
                        {
                            inverseProperty.SetValue(parent, child);
                            Console.WriteLine($"  🔗 Fixup: {parent.GetType().Name}.{inverseProperty.Name} → {child.GetType().Name}");
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

        private void PrintIncludesSummary(Query.FirestoreQueryExpression queryExpression)
        {
            Console.WriteLine("\n╔═══════════════════════════════════════════════════════╗");
            Console.WriteLine("║         RESUMEN DE INCLUDES DETECTADOS                ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════╝");
            Console.WriteLine($"Total PendingIncludes: {queryExpression.PendingIncludes.Count}\n");

            if (queryExpression.PendingIncludes.Count == 0)
            {
                Console.WriteLine("⚠ ⚠ ⚠  NO SE DETECTÓ NINGÚN INCLUDE  ⚠ ⚠ ⚠\n");
            }
            else
            {
                var grouped = queryExpression.PendingIncludes
                    .GroupBy(n => n.DeclaringEntityType.ClrType.Name)
                    .OrderBy(g => g.Key);

                foreach (var group in grouped)
                {
                    Console.WriteLine($"  📁 {group.Key}:");
                    foreach (var nav in group)
                    {
                        var typeIndicator = nav.IsCollection ? "[Collection]" : "[Reference]";
                        var isSubColl = nav.IsSubCollection() ? "✓ SubCollection" : "⚠ NOT SubCollection";
                        Console.WriteLine($"    └─{typeIndicator} {nav.Name} → {nav.TargetEntityType.ClrType.Name} ({isSubColl})");
                    }
                }

                Console.WriteLine($"\n  📊 Árbol de carga esperado:");
                PrintLoadingTree(queryExpression.PendingIncludes);
            }

            Console.WriteLine($"\n═══════════════════════════════════════════════════════\n");
        }

        private void PrintLoadingTree(List<IReadOnlyNavigation> navigations)
        {
            var allTargetTypes = new HashSet<IReadOnlyEntityType>(
                navigations.Select(n => n.TargetEntityType));

            var rootTypes = navigations
                .Select(n => n.DeclaringEntityType)
                .Distinct()
                .Where(t => !allTargetTypes.Contains(t))
                .ToList();

            foreach (var rootType in rootTypes)
            {
                Console.WriteLine($"  {rootType.ClrType.Name}");
                PrintNavigationChildren(rootType, navigations, indent: "    ");
            }
        }

        private void PrintNavigationChildren(
            IReadOnlyEntityType entityType,
            List<IReadOnlyNavigation> allNavigations,
            string indent)
        {
            var children = allNavigations
                .Where(n => n.DeclaringEntityType == entityType)
                .ToList();

            foreach (var child in children)
            {
                var indicator = child.IsCollection ? "└─[1:N]" : "└─[N:1]";
                Console.WriteLine($"{indent}{indicator} {child.Name} → {child.TargetEntityType.ClrType.Name}");

                PrintNavigationChildren(child.TargetEntityType, allNavigations, indent + "    ");
            }
        }

        #endregion
    }

    #endregion

    #region Support Classes

    internal class RuntimeParameterReplacer : ExpressionVisitor
    {
        private readonly QueryCompilationContext _queryCompilationContext;

        public RuntimeParameterReplacer(QueryCompilationContext queryCompilationContext)
        {
            _queryCompilationContext = queryCompilationContext;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node.Name != null && node.Name.StartsWith("__p_"))
            {
                var queryContextParam = QueryCompilationContext.QueryContextParameter;
                var parameterValuesProperty = Expression.Property(queryContextParam, "ParameterValues");
                var indexer = Expression.Property(parameterValuesProperty, "Item", Expression.Constant(node.Name));
                var converted = Expression.Convert(indexer, node.Type);

                return converted;
            }

            return base.VisitParameter(node);
        }
    }

    internal class FirestoreWhereTranslator
    {
        public Query.FirestoreWhereClause? Translate(Expression expression)
        {
            if (expression is BinaryExpression binaryExpression)
            {
                return TranslateBinaryExpression(binaryExpression);
            }

            if (expression is MethodCallExpression methodCallExpression)
            {
                return TranslateMethodCallExpression(methodCallExpression);
            }

            return null;
        }

        private Query.FirestoreWhereClause? TranslateBinaryExpression(BinaryExpression binary)
        {
            string? propertyName = null;
            Expression? valueExpression = null;

            if (binary.Left is MemberExpression leftMember && leftMember.Member is PropertyInfo leftProp)
            {
                propertyName = leftProp.Name;
                valueExpression = binary.Right;
            }
            else if (binary.Right is MemberExpression rightMember && rightMember.Member is PropertyInfo rightProp)
            {
                propertyName = rightProp.Name;
                valueExpression = binary.Left;
            }
            else if (binary.Left is MethodCallExpression leftMethod &&
                     leftMethod.Method.Name == "Property" &&
                     leftMethod.Method.DeclaringType?.Name == "EF")
            {
                propertyName = GetPropertyNameFromEFProperty(leftMethod);
                valueExpression = binary.Right;
            }
            else if (binary.Right is MethodCallExpression rightMethod &&
                     rightMethod.Method.Name == "Property" &&
                     rightMethod.Method.DeclaringType?.Name == "EF")
            {
                propertyName = GetPropertyNameFromEFProperty(rightMethod);
                valueExpression = binary.Left;
            }

            if (propertyName == null || valueExpression == null)
                return null;

            var firestoreOperator = binary.NodeType switch
            {
                ExpressionType.Equal => Query.FirestoreOperator.EqualTo,
                ExpressionType.NotEqual => Query.FirestoreOperator.NotEqualTo,
                ExpressionType.LessThan => Query.FirestoreOperator.LessThan,
                ExpressionType.LessThanOrEqual => Query.FirestoreOperator.LessThanOrEqualTo,
                ExpressionType.GreaterThan => Query.FirestoreOperator.GreaterThan,
                ExpressionType.GreaterThanOrEqual => Query.FirestoreOperator.GreaterThanOrEqualTo,
                _ => (Query.FirestoreOperator?)null
            };

            if (!firestoreOperator.HasValue)
                return null;

            return new Query.FirestoreWhereClause(propertyName, firestoreOperator.Value, valueExpression);
        }

        private Query.FirestoreWhereClause? TranslateMethodCallExpression(MethodCallExpression methodCall)
        {
            if (methodCall.Method.Name == "Contains")
            {
                if (methodCall.Object != null && methodCall.Arguments.Count == 1)
                {
                    if (methodCall.Arguments[0] is MemberExpression member && member.Member is PropertyInfo prop)
                    {
                        var propertyName = prop.Name;
                        return new Query.FirestoreWhereClause(propertyName, Query.FirestoreOperator.In, methodCall.Object);
                    }
                }

                if (methodCall.Object is MemberExpression objMember &&
                    objMember.Member is PropertyInfo objProp &&
                    methodCall.Arguments.Count == 1)
                {
                    var propertyName = objProp.Name;
                    return new Query.FirestoreWhereClause(propertyName, Query.FirestoreOperator.ArrayContains, methodCall.Arguments[0]);
                }
            }

            return null;
        }

        private string? GetPropertyNameFromEFProperty(MethodCallExpression methodCall)
        {
            if (methodCall.Arguments.Count >= 2 && methodCall.Arguments[1] is ConstantExpression constant)
            {
                return constant.Value as string;
            }
            return null;
        }
    }

    public class FirestoreLoggingDefinitions : LoggingDefinitions
    {
    }

    #endregion

    #region Interfaces

    public interface IFirestoreClientWrapper
    {
        FirestoreDb Database { get; }
        Task<DocumentSnapshot> GetDocumentAsync(string collection, string documentId, CancellationToken cancellationToken = default);
        Task<bool> DocumentExistsAsync(string collection, string documentId, CancellationToken cancellationToken = default);
        Task<QuerySnapshot> GetCollectionAsync(string collection, CancellationToken cancellationToken = default);
        Task<WriteResult> SetDocumentAsync(string collection, string documentId, Dictionary<string, object> data, CancellationToken cancellationToken = default);
        Task<WriteResult> UpdateDocumentAsync(string collection, string documentId, Dictionary<string, object> data, CancellationToken cancellationToken = default);
        Task<WriteResult> DeleteDocumentAsync(string collection, string documentId, CancellationToken cancellationToken = default);
        Task<QuerySnapshot> ExecuteQueryAsync(Google.Cloud.Firestore.Query query, CancellationToken cancellationToken = default);
        Task<T> RunTransactionAsync<T>(Func<Transaction, Task<T>> callback, CancellationToken cancellationToken = default);
        WriteBatch CreateBatch();
        CollectionReference GetCollection(string collection);
        DocumentReference GetDocument(string collection, string documentId);
    }

    public interface IFirestoreIdGenerator { string GenerateId(); }
    public interface IFirestoreDocumentSerializer { }
    public interface IFirestoreCollectionManager { string GetCollectionName(Type entityType); }

    #endregion
}