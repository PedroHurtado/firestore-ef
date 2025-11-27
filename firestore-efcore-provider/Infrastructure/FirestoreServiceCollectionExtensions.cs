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

            // ✅ CRÍTICO: Reemplazar el IProviderConventionSetBuilder por defecto con el nuestro
            // AddScoped reemplaza el servicio anterior registrado por TryAddCoreServices
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
            // Obtener el nombre de la colección en Firestore
            var collectionName = GetCollectionName(entityType);

            // Crear la expresión de query inicial (sin filtros ni ordenamientos)
            var queryExpression = new Query.FirestoreQueryExpression(entityType, collectionName);

            // Crear el shaper que define cómo materializar las entidades
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
            // Buscar atributo [Table] en el tipo
            var tableAttribute = entityType.ClrType
                .GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.TableAttribute>();

            if (tableAttribute != null && !string.IsNullOrEmpty(tableAttribute.Name))
                return tableAttribute.Name;

            // Fallback: usar el nombre del tipo pluralizado
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

        protected override ShapedQueryExpression? TranslateAll(ShapedQueryExpression source, LambdaExpression predicate)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateAny(ShapedQueryExpression source, LambdaExpression? predicate)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateAverage(ShapedQueryExpression source, LambdaExpression? selector, Type resultType)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateCast(ShapedQueryExpression source, Type castType)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateConcat(ShapedQueryExpression source1, ShapedQueryExpression source2)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateContains(ShapedQueryExpression source, Expression item)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateCount(ShapedQueryExpression source, LambdaExpression? predicate)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateDefaultIfEmpty(ShapedQueryExpression source, Expression? defaultValue)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateDistinct(ShapedQueryExpression source)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateElementAtOrDefault(ShapedQueryExpression source, Expression index, bool returnDefault)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateExcept(ShapedQueryExpression source1, ShapedQueryExpression source2)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateFirstOrDefault(ShapedQueryExpression source, LambdaExpression? predicate, Type returnType, bool returnDefault)
        {
            // Si hay un predicado, primero aplicar el Where
            if (predicate != null)
            {
                source = TranslateWhere(source, predicate) ?? source;
            }

            // Obtener el FirestoreQueryExpression actual
            var firestoreQueryExpression = (Query.FirestoreQueryExpression)source.QueryExpression;

            // Aplicar límite de 1 documento (FirstOrDefault solo necesita el primero)
            var newQueryExpression = firestoreQueryExpression.WithLimit(1);

            // Retornar el ShapedQueryExpression actualizado
            return source.UpdateQueryExpression(newQueryExpression);
        }

        protected override ShapedQueryExpression? TranslateGroupBy(ShapedQueryExpression source, LambdaExpression keySelector, LambdaExpression? elementSelector, LambdaExpression? resultSelector)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateGroupJoin(ShapedQueryExpression outer, ShapedQueryExpression inner, LambdaExpression outerKeySelector, LambdaExpression innerKeySelector, LambdaExpression resultSelector)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateIntersect(ShapedQueryExpression source1, ShapedQueryExpression source2)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateJoin(ShapedQueryExpression outer, ShapedQueryExpression inner, LambdaExpression outerKeySelector, LambdaExpression innerKeySelector, LambdaExpression resultSelector)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateLastOrDefault(ShapedQueryExpression source, LambdaExpression? predicate, Type returnType, bool returnDefault)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateLeftJoin(ShapedQueryExpression outer, ShapedQueryExpression inner, LambdaExpression outerKeySelector, LambdaExpression innerKeySelector, LambdaExpression resultSelector)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateLongCount(ShapedQueryExpression source, LambdaExpression? predicate)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateMax(ShapedQueryExpression source, LambdaExpression? selector, Type resultType)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateMin(ShapedQueryExpression source, LambdaExpression? selector, Type resultType)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateOfType(ShapedQueryExpression source, Type resultType)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateOrderBy(ShapedQueryExpression source, LambdaExpression keySelector, bool ascending)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateReverse(ShapedQueryExpression source)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression TranslateSelect(ShapedQueryExpression source, LambdaExpression selector)
        {
            // Para la Fase 1, soportamos proyecciones simples de entidad completa
            // Proyecciones complejas (campos específicos) se implementarán en Fase 3

            // Caso 1: Proyección de identidad (x => x)
            // Simplemente retornamos el source original sin cambios
            if (selector.Body == selector.Parameters[0])
            {
                return source;
            }

            // Caso 2: Proyección a la misma entidad con conversión de tipo
            // Ejemplo: x => (Product)x
            if (selector.Body is UnaryExpression unary &&
                unary.NodeType == ExpressionType.Convert &&
                unary.Operand == selector.Parameters[0])
            {
                return source;
            }

            // Caso 3: Proyecciones complejas (select new { ... } o x => x.Property)
            // Por ahora, para permitir que funcione ToList() y queries básicas,
            // retornamos el source sin cambios
            // TODO: Implementar en Fase 3 - Proyecciones y transformaciones

            // Nota: Las proyecciones complejas se implementarán en una fase posterior
            // Por ahora retornamos la entidad completa
            return source;
        }

        protected override ShapedQueryExpression? TranslateSelectMany(ShapedQueryExpression source, LambdaExpression collectionSelector, LambdaExpression resultSelector)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateSelectMany(ShapedQueryExpression source, LambdaExpression selector)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateSingleOrDefault(ShapedQueryExpression source, LambdaExpression? predicate, Type returnType, bool returnDefault)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateSkip(ShapedQueryExpression source, Expression count)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateSkipWhile(ShapedQueryExpression source, LambdaExpression predicate)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateSum(ShapedQueryExpression source, LambdaExpression? selector, Type resultType)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateTake(ShapedQueryExpression source, Expression count)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateTakeWhile(ShapedQueryExpression source, LambdaExpression predicate)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateThenBy(ShapedQueryExpression source, LambdaExpression keySelector, bool ascending)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateUnion(ShapedQueryExpression source1, ShapedQueryExpression source2)
        {
            throw new NotImplementedException();
        }

        protected override ShapedQueryExpression? TranslateWhere(ShapedQueryExpression source, LambdaExpression predicate)
        {
            System.Console.WriteLine($"[DEBUG] TranslateWhere called with predicate: {predicate}");

            // Obtener el FirestoreQueryExpression actual
            var firestoreQueryExpression = (Query.FirestoreQueryExpression)source.QueryExpression;

            // 🔥 SOLUCIÓN: Detectar si hay parámetros __p_X en la expresión
            // Si los hay, significa que el valor vendrá en runtime y debemos prepararnos
            var parameterReplacer = new RuntimeParameterReplacer(QueryCompilationContext);
            var evaluatedBody = parameterReplacer.Visit(predicate.Body);

            System.Console.WriteLine($"[DEBUG] After parameter replacement: {evaluatedBody}");

            // Traducir el predicado a filtros de Firestore
            var translator = new FirestoreWhereTranslator();
            var whereClause = translator.Translate(evaluatedBody);

            if (whereClause == null)
            {
                System.Console.WriteLine("[DEBUG] TranslateWhere: Could not translate predicate, returning null");
                return null;
            }

            System.Console.WriteLine($"[DEBUG] TranslateWhere: Created filter - {whereClause.PropertyName} {whereClause.Operator} <Expression>");

            // Crear una nueva expresión con el filtro agregado
            var newQueryExpression = firestoreQueryExpression.AddFilter(whereClause);

            return source.UpdateQueryExpression(newQueryExpression);
        }

        protected override Expression VisitExtension(Expression extensionExpression)
        {
            return base.VisitExtension(extensionExpression);
        }
    }

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
            // Obtener el FirestoreQueryExpression
            var firestoreQueryExpression = (Query.FirestoreQueryExpression)shapedQueryExpression.QueryExpression;

            // Obtener el tipo de entidad
            var entityType = firestoreQueryExpression.EntityType.ClrType;

            // Crear parámetros para el shaper
            var queryContextParameter = Expression.Parameter(typeof(QueryContext), "queryContext");
            var documentSnapshotParameter = Expression.Parameter(typeof(DocumentSnapshot), "documentSnapshot");

            // Crear el shaper: (queryContext, documentSnapshot) => entity
            var shaperExpression = CreateShaperExpression(
                queryContextParameter,
                documentSnapshotParameter,
                firestoreQueryExpression);

            var shaperLambda = Expression.Lambda(
                shaperExpression,
                queryContextParameter,
                documentSnapshotParameter);

            // Crear la expresión que instancia FirestoreQueryingEnumerable<T>
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

        /// <summary>
        /// Crea la expresión del shaper que convierte DocumentSnapshot en una entidad.
        /// </summary>
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

        /// <summary>
        /// Deserializa un DocumentSnapshot a una entidad.
        /// </summary>
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

            var deserializerLogger = Microsoft.Extensions.Logging.LoggerFactoryExtensions.CreateLogger<Storage.FirestoreDocumentDeserializer>(loggerFactory);
            var deserializer = new Storage.FirestoreDocumentDeserializer(
                model,
                typeMappingSource,
                collectionManager,
                deserializerLogger);

            return deserializer.DeserializeEntity<T>(documentSnapshot);
        }

    }

    /// <summary>
    /// Visitor que reemplaza ParameterExpression de runtime (__p_0) con acceso a QueryContext.ParameterValues
    /// </summary>

internal class RuntimeParameterReplacer : ExpressionVisitor
{
    private readonly QueryCompilationContext _queryCompilationContext;

    public RuntimeParameterReplacer(QueryCompilationContext queryCompilationContext)
    {
        _queryCompilationContext = queryCompilationContext;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        // Detectar parámetros de runtime como __p_0, __p_1, etc.
        if (node.Name != null && node.Name.StartsWith("__p_"))
        {
            System.Console.WriteLine($"[DEBUG RuntimeParameterReplacer] Found runtime parameter: {node.Name}");

            // Crear una expresión que accede a QueryContext.ParameterValues["{node.Name}"]
            // En tiempo de compilación: QueryContext.ParameterValues["__p_0"]
            // En tiempo de ejecución: esto se evaluará al valor real
            var queryContextParam = QueryCompilationContext.QueryContextParameter; // 🔥 CORREGIDO: Es estático
            var parameterValuesProperty = Expression.Property(queryContextParam, "ParameterValues");
            var indexer = Expression.Property(parameterValuesProperty, "Item", Expression.Constant(node.Name));
            
            // Convertir al tipo correcto
            var converted = Expression.Convert(indexer, node.Type);

            System.Console.WriteLine($"[DEBUG RuntimeParameterReplacer] Replaced with QueryContext access");
            
            return converted;
        }

        return base.VisitParameter(node);
    }
}

    /// <summary>
    /// Traductor de expresiones LINQ a filtros de Firestore (WhereClause).
    /// </summary>
    internal class FirestoreWhereTranslator
    {
        public Query.FirestoreWhereClause? Translate(Expression expression)
        {
            // Manejar expresiones binarias (==, !=, <, >, <=, >=)
            if (expression is BinaryExpression binaryExpression)
            {
                return TranslateBinaryExpression(binaryExpression);
            }

            // Manejar llamadas a métodos (Contains, StartsWith, etc.)
            if (expression is MethodCallExpression methodCallExpression)
            {
                return TranslateMethodCallExpression(methodCallExpression);
            }

            // No podemos traducir esta expresión
            return null;
        }

        private Query.FirestoreWhereClause? TranslateBinaryExpression(BinaryExpression binary)
        {
            // Extraer el nombre de la propiedad y el valor
            string? propertyName = null;
            Expression? valueExpression = null;

            // Determinar qué lado es la propiedad y qué lado es el valor
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
            // Manejar EF.Property<T>(entity, "PropertyName")
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
            else
            {
                // No podemos traducir esta expresión
                return null;
            }

            if (propertyName == null || valueExpression == null)
                return null;

            // Mapear el operador de C# a operador de Firestore
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

            // 🔥 NUEVO: La valueExpression puede ser un acceso a QueryContext.ParameterValues
            // Lo dejamos como una expresión que se evaluará en runtime
            return new Query.FirestoreWhereClause(propertyName, firestoreOperator.Value, valueExpression);
        }

        private Query.FirestoreWhereClause? TranslateMethodCallExpression(MethodCallExpression methodCall)
        {
            // Soportar Contains para operador "In"
            if (methodCall.Method.Name == "Contains")
            {
                // Caso 1: list.Contains(property) -> WhereIn
                if (methodCall.Object != null && methodCall.Arguments.Count == 1)
                {
                    if (methodCall.Arguments[0] is MemberExpression member && member.Member is PropertyInfo prop)
                    {
                        var propertyName = prop.Name;
                        return new Query.FirestoreWhereClause(propertyName, Query.FirestoreOperator.In, methodCall.Object);
                    }
                }

                // Caso 2: property.Contains(value) -> Array-Contains
                if (methodCall.Object is MemberExpression objMember &&
                    objMember.Member is PropertyInfo objProp &&
                    methodCall.Arguments.Count == 1)
                {
                    var propertyName = objProp.Name;
                    return new Query.FirestoreWhereClause(propertyName, Query.FirestoreOperator.ArrayContains, methodCall.Arguments[0]);
                }
            }

            // No podemos traducir este método
            return null;
        }

        /// <summary>
        /// Extrae el nombre de la propiedad de una llamada a EF.Property (entity, "PropertyName")
        /// </summary>
        private string? GetPropertyNameFromEFProperty(MethodCallExpression methodCall)
        {
            // EF.Property tiene 2 argumentos: entity y propertyName (string constante)
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

    public interface IFirestoreClientWrapper
    {
        FirestoreDb Database { get; }

        Task<DocumentSnapshot> GetDocumentAsync(
            string collection, string documentId, CancellationToken cancellationToken = default);

        Task<bool> DocumentExistsAsync(
            string collection, string documentId, CancellationToken cancellationToken = default);

        Task<QuerySnapshot> GetCollectionAsync(
            string collection, CancellationToken cancellationToken = default);

        Task<WriteResult> SetDocumentAsync(
            string collection, string documentId, Dictionary<string, object> data,
            CancellationToken cancellationToken = default);

        Task<WriteResult> UpdateDocumentAsync(
            string collection, string documentId, Dictionary<string, object> data,
            CancellationToken cancellationToken = default);

        Task<WriteResult> DeleteDocumentAsync(
            string collection, string documentId, CancellationToken cancellationToken = default);

        Task<QuerySnapshot> ExecuteQueryAsync(
            Google.Cloud.Firestore.Query query, CancellationToken cancellationToken = default);

        Task<T> RunTransactionAsync<T>(
            Func<Transaction, Task<T>> callback,
            CancellationToken cancellationToken = default);

        WriteBatch CreateBatch();

        CollectionReference GetCollection(string collection);

        DocumentReference GetDocument(string collection, string documentId);
    }

    public interface IFirestoreIdGenerator { string GenerateId(); }
    public interface IFirestoreDocumentSerializer { }
    public interface IFirestoreCollectionManager { string GetCollectionName(Type entityType); }
}