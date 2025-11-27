using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Firestore.EntityFrameworkCore.Infrastructure;

namespace Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Ejecuta queries de Firestore construyendo Google.Cloud.Firestore.Query
    /// desde FirestoreQueryExpression y retornando QuerySnapshot.
    /// </summary>
    public class FirestoreQueryExecutor
    {
        private readonly IFirestoreClientWrapper _client;
        private readonly ILogger<FirestoreQueryExecutor> _logger;

        public FirestoreQueryExecutor(
            IFirestoreClientWrapper client,
            ILogger<FirestoreQueryExecutor> logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

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
            _logger.LogInformation("Filters count: {Count}", queryExpression.Filters.Count);

            /*foreach (var filter in queryExpression.Filters)
            {
                // Evaluar el valor en runtime
                var evaluatedValue = filter.EvaluateValue(queryContext);
                
                _logger.LogInformation("  Filter: {PropertyName} {Operator} {Value} (Type: {ValueType})",
                    filter.PropertyName,
                    filter.Operator,
                    evaluatedValue ?? "NULL",
                    evaluatedValue?.GetType().Name ?? "NULL");
            }*/

            // Construir Google.Cloud.Firestore.Query
            var query = BuildFirestoreQuery(queryExpression, queryContext);

            // Ejecutar
            var snapshot = await _client.ExecuteQueryAsync(query, cancellationToken);

            _logger.LogInformation("Query returned {Count} documents", snapshot.Count);

            return snapshot;
        }

        /// <summary>
        /// Construye un Google.Cloud.Firestore.Query desde FirestoreQueryExpression
        /// </summary>
        private Google.Cloud.Firestore.Query BuildFirestoreQuery(
            FirestoreQueryExpression queryExpression,
            Microsoft.EntityFrameworkCore.Query.QueryContext queryContext)
        {
            // Obtener CollectionReference inicial
            Google.Cloud.Firestore.Query query = _client.GetCollection(queryExpression.CollectionName);

            // Aplicar filtros WHERE
            foreach (var filter in queryExpression.Filters)
            {
                query = ApplyWhereClause(query, filter, queryContext);
            }

            // Aplicar ordenamiento ORDER BY
            foreach (var orderBy in queryExpression.OrderByClauses)
            {
                query = ApplyOrderByClause(query, orderBy);
            }

            // Aplicar límite LIMIT (Take)
            if (queryExpression.Limit.HasValue)
            {
                query = query.Limit(queryExpression.Limit.Value);
                _logger.LogTrace("Applied Limit: {Limit}", queryExpression.Limit.Value);
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
        /// Aplica una cláusula WHERE al query
        /// </summary>
        private Google.Cloud.Firestore.Query ApplyWhereClause(
            Google.Cloud.Firestore.Query query,
            FirestoreWhereClause clause,
            Microsoft.EntityFrameworkCore.Query.QueryContext queryContext)
        {
            // Evaluar el valor en runtime usando el QueryContext
            var value = clause.EvaluateValue(queryContext);
            
            // Convertir valor al tipo esperado por Firestore
            var convertedValue = ConvertValueForFirestore(value);

            _logger.LogTrace("Applying filter: {PropertyName} {Operator} {Value}",
                clause.PropertyName, clause.Operator, convertedValue);

            // Aplicar el operador correspondiente
            return clause.Operator switch
            {
                FirestoreOperator.EqualTo =>
                    query.WhereEqualTo(clause.PropertyName, convertedValue),

                FirestoreOperator.NotEqualTo =>
                    query.WhereNotEqualTo(clause.PropertyName, convertedValue),

                FirestoreOperator.LessThan =>
                    query.WhereLessThan(clause.PropertyName, convertedValue),

                FirestoreOperator.LessThanOrEqualTo =>
                    query.WhereLessThanOrEqualTo(clause.PropertyName, convertedValue),

                FirestoreOperator.GreaterThan =>
                    query.WhereGreaterThan(clause.PropertyName, convertedValue),

                FirestoreOperator.GreaterThanOrEqualTo =>
                    query.WhereGreaterThanOrEqualTo(clause.PropertyName, convertedValue),

                FirestoreOperator.ArrayContains =>
                    query.WhereArrayContains(clause.PropertyName, convertedValue),

                FirestoreOperator.In =>
                    ApplyWhereIn(query, clause.PropertyName, convertedValue),

                FirestoreOperator.ArrayContainsAny =>
                    ApplyWhereArrayContainsAny(query, clause.PropertyName, convertedValue),

                FirestoreOperator.NotIn =>
                    ApplyWhereNotIn(query, clause.PropertyName, convertedValue),

                _ => throw new NotSupportedException(
                    $"Firestore operator {clause.Operator} is not supported")
            };
        }

        /// <summary>
        /// Aplica WhereIn validando límite de 30 elementos
        /// </summary>
        private Google.Cloud.Firestore.Query ApplyWhereIn(
            Google.Cloud.Firestore.Query query,
            string propertyName,
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

            return query.WhereIn(propertyName, values);
        }

        /// <summary>
        /// Aplica WhereArrayContainsAny validando límite de 30 elementos
        /// </summary>
        private Google.Cloud.Firestore.Query ApplyWhereArrayContainsAny(
            Google.Cloud.Firestore.Query query,
            string propertyName,
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

            return query.WhereArrayContainsAny(propertyName, values);
        }

        /// <summary>
        /// Aplica WhereNotIn validando límite de 10 elementos
        /// </summary>
        private Google.Cloud.Firestore.Query ApplyWhereNotIn(
            Google.Cloud.Firestore.Query query,
            string propertyName,
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

            return query.WhereNotIn(propertyName, values);
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
    }
}