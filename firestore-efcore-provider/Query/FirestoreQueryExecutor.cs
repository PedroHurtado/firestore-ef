using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Extensions;

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
        private readonly ILogger<FirestoreQueryExecutor> _logger;

        public FirestoreQueryExecutor(
            IFirestoreClientWrapper client,
            IFirestoreDocumentDeserializer deserializer,
            ILogger<FirestoreQueryExecutor> logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _deserializer = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public IFirestoreDocumentDeserializer Deserializer => _deserializer;

        /// <summary>
        /// Ejecuta una FirestoreQueryExpression y retorna los documentos resultantes
        /// </summary>
        public async Task<QuerySnapshot> ExecuteQueryAsync(
            FirestoreQueryExpression queryExpression,
            Microsoft.EntityFrameworkCore.Query.QueryContext queryContext,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(queryExpression);

            _logger.LogInformation("=== Executing Firestore query ===");
            _logger.LogInformation("Collection: {Collection}", queryExpression.CollectionName);

            // 🔥 Las queries por ID no deben llegar aquí - deben usar ExecuteIdQueryAsync
            if (queryExpression.IsIdOnlyQuery)
            {
                throw new InvalidOperationException(
                    "ID-only queries should use ExecuteIdQueryAsync instead of ExecuteQueryAsync. " +
                    "This is an internal error in the query execution pipeline.");
            }

            // Query normal (no es por ID)
            _logger.LogInformation("Filters count: {Count}", queryExpression.Filters.Count);

            // Construir Google.Cloud.Firestore.Query
            var query = BuildQuery(queryExpression, queryContext);

            // Ejecutar
            var snapshot = await _client.ExecuteQueryAsync(query, cancellationToken);

            _logger.LogInformation("Query returned {Count} documents", snapshot.Count);

            return snapshot;
        }

        /// <summary>
        /// Ejecuta una query por ID usando GetDocumentAsync (el ID es metadata, no está en el documento)
        /// </summary>
        public async Task<DocumentSnapshot?> ExecuteIdQueryAsync(
            FirestoreQueryExpression queryExpression,
            Microsoft.EntityFrameworkCore.Query.QueryContext queryContext,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(queryExpression);

            if (!queryExpression.IsIdOnlyQuery)
            {
                throw new InvalidOperationException(
                    "ExecuteIdQueryAsync can only be called for ID-only queries. " +
                    "Use ExecuteQueryAsync for regular queries.");
            }

            _logger.LogInformation("=== Executing Firestore ID query ===");
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

            // 🔥 Usar GetDocumentAsync porque el ID es metadata del documento
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
            if (queryExpression.StartAfterDocument != null)
            {
                query = query.StartAfter(queryExpression.StartAfterDocument);
                _logger.LogTrace("Applied StartAfter: {DocumentId}",
                    queryExpression.StartAfterDocument.Id);
            }

            return query;
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

        #endregion
    }
}