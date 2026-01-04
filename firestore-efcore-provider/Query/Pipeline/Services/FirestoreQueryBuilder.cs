using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Resolved;
using Google.Cloud.Firestore;
using System;
using System.Collections;
using System.Collections.Generic;
using FirestoreQuery = Google.Cloud.Firestore.Query;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Builds Firestore SDK Query and AggregateQuery objects from resolved queries.
/// </summary>
public class FirestoreQueryBuilder : IQueryBuilder
{
    private readonly IFirestoreClientWrapper _clientWrapper;

    public FirestoreQueryBuilder(IFirestoreClientWrapper clientWrapper)
    {
        _clientWrapper = clientWrapper;
    }

    /// <inheritdoc />
    public FirestoreQuery Build(ResolvedFirestoreQuery resolvedQuery)
    {
        // Start with collection reference (which is a Query)
        FirestoreQuery query = _clientWrapper.GetCollection(resolvedQuery.CollectionPath);

        // Apply filters
        query = ApplyFilters(query, resolvedQuery.FilterResults);

        // Apply ordering
        query = ApplyOrderBy(query, resolvedQuery.OrderByClauses);

        // For Min/Max, apply Select + OrderBy + Limit(1)
        if (resolvedQuery.AggregationType == FirestoreAggregationType.Min)
        {
            query = query
                .Select(resolvedQuery.AggregationPropertyName!)
                .OrderBy(resolvedQuery.AggregationPropertyName!)
                .Limit(1);
            return query;
        }

        if (resolvedQuery.AggregationType == FirestoreAggregationType.Max)
        {
            query = query
                .Select(resolvedQuery.AggregationPropertyName!)
                .OrderByDescending(resolvedQuery.AggregationPropertyName!)
                .Limit(1);
            return query;
        }

        // Apply pagination
        query = ApplyPagination(query, resolvedQuery.Pagination);

        // Apply cursor
        if (resolvedQuery.StartAfterCursor != null)
        {
            query = ApplyCursor(query, resolvedQuery.StartAfterCursor);
        }

        return query;
    }

    /// <inheritdoc />
    public AggregateQuery BuildAggregate(ResolvedFirestoreQuery resolvedQuery)
    {
        // Build base query first (without Min/Max special handling)
        FirestoreQuery query = _clientWrapper.GetCollection(resolvedQuery.CollectionPath);
        query = ApplyFilters(query, resolvedQuery.FilterResults);

        // Build aggregate query based on type
        return resolvedQuery.AggregationType switch
        {
            FirestoreAggregationType.Count => query.Count(),
            FirestoreAggregationType.Any => query.Count(), // Any uses Count > 0
            FirestoreAggregationType.Sum => query.Aggregate(AggregateField.Sum(resolvedQuery.AggregationPropertyName!)),
            FirestoreAggregationType.Average => query.Aggregate(AggregateField.Average(resolvedQuery.AggregationPropertyName!)),
            FirestoreAggregationType.Min or FirestoreAggregationType.Max =>
                throw new NotSupportedException($"Min/Max are not native Firestore aggregations. Use Build() instead."),
            _ => throw new NotSupportedException($"Aggregation type {resolvedQuery.AggregationType} is not supported")
        };
    }

    private static FirestoreQuery ApplyFilters(FirestoreQuery query, IReadOnlyList<ResolvedFilterResult> filterResults)
    {
        foreach (var filterResult in filterResults)
        {
            // Apply AND clauses
            foreach (var clause in filterResult.AndClauses)
            {
                query = ApplyWhereClause(query, clause);
            }

            // Apply OR group if present
            if (filterResult.OrGroup != null)
            {
                query = ApplyOrGroup(query, filterResult.OrGroup);
            }

            // Apply nested OR groups
            if (filterResult.NestedOrGroups != null)
            {
                foreach (var nestedOrGroup in filterResult.NestedOrGroups)
                {
                    query = ApplyOrGroup(query, nestedOrGroup);
                }
            }
        }

        return query;
    }

    private static FirestoreQuery ApplyWhereClause(FirestoreQuery query, ResolvedWhereClause clause)
    {
        return query.Where(CreateFilter(clause));
    }

    private static FirestoreQuery ApplyOrGroup(FirestoreQuery query, ResolvedOrFilterGroup orGroup)
    {
        if (orGroup.Clauses.Count == 0)
            return query;

        // Build Filter objects for each clause
        var filters = new Filter[orGroup.Clauses.Count];
        for (int i = 0; i < orGroup.Clauses.Count; i++)
        {
            filters[i] = CreateFilter(orGroup.Clauses[i]);
        }

        // Combine with OR
        var orFilter = Filter.Or(filters);
        return query.Where(orFilter);
    }

    private static Filter CreateFilter(ResolvedWhereClause clause)
    {
        // Value is already converted by the Resolver (IFirestoreValueConverter)
        var value = clause.Value;

        // Get field path - "Id" is special and maps to FieldPath.DocumentId
        var fieldPath = GetFieldPath(clause.PropertyName);

        return clause.Operator switch
        {
            FirestoreOperator.EqualTo => Filter.EqualTo(fieldPath, value),
            FirestoreOperator.NotEqualTo => Filter.NotEqualTo(fieldPath, value),
            FirestoreOperator.LessThan => Filter.LessThan(fieldPath, value),
            FirestoreOperator.LessThanOrEqualTo => Filter.LessThanOrEqualTo(fieldPath, value),
            FirestoreOperator.GreaterThan => Filter.GreaterThan(fieldPath, value),
            FirestoreOperator.GreaterThanOrEqualTo => Filter.GreaterThanOrEqualTo(fieldPath, value),
            FirestoreOperator.ArrayContains => Filter.ArrayContains(fieldPath, value),
            FirestoreOperator.ArrayContainsAny => Filter.ArrayContainsAny(fieldPath, (IEnumerable)value!),
            FirestoreOperator.In => Filter.InArray(fieldPath, (IEnumerable)value!),
            FirestoreOperator.NotIn => Filter.NotInArray(fieldPath, (IEnumerable)value!),
            _ => throw new NotSupportedException($"Operator {clause.Operator} is not supported")
        };
    }

    /// <summary>
    /// Gets the appropriate FieldPath for a property name.
    /// Returns FieldPath.DocumentId for "Id" property, otherwise a regular FieldPath.
    /// Supports nested properties like "Direccion.Ciudad" → FieldPath("Direccion", "Ciudad")
    /// </summary>
    private static FieldPath GetFieldPath(string propertyName)
    {
        if (propertyName == "Id")
            return FieldPath.DocumentId;

        // Split nested property paths: "Direccion.Ciudad" → ["Direccion", "Ciudad"]
        var segments = propertyName.Split('.');
        return new FieldPath(segments);
    }

    private static FirestoreQuery ApplyOrderBy(FirestoreQuery query, IReadOnlyList<ResolvedOrderByClause> orderByClauses)
    {
        foreach (var orderBy in orderByClauses)
        {
            query = orderBy.Descending
                ? query.OrderByDescending(orderBy.PropertyName)
                : query.OrderBy(orderBy.PropertyName);
        }

        return query;
    }

    private static FirestoreQuery ApplyPagination(FirestoreQuery query, ResolvedPaginationInfo pagination)
    {
        if (pagination.Skip.HasValue)
        {
            query = query.Offset(pagination.Skip.Value);
        }

        if (pagination.Limit.HasValue)
        {
            query = query.Limit(pagination.Limit.Value);
        }

        if (pagination.LimitToLast.HasValue)
        {
            query = query.LimitToLast(pagination.LimitToLast.Value);
        }

        return query;
    }

    private static FirestoreQuery ApplyCursor(FirestoreQuery query, ResolvedCursor cursor)
    {
        if (cursor.OrderByValues != null && cursor.OrderByValues.Count > 0)
        {
            // StartAfter with multiple values (document ID + order by values)
            var values = new object?[cursor.OrderByValues.Count + 1];
            values[0] = cursor.DocumentId;
            for (int i = 0; i < cursor.OrderByValues.Count; i++)
            {
                values[i + 1] = cursor.OrderByValues[i];
            }
            query = query.StartAfter(values);
        }
        else
        {
            // StartAfter with just document ID
            query = query.StartAfter(cursor.DocumentId);
        }

        return query;
    }

    /// <inheritdoc />
    public FirestoreQuery BuildInclude(string parentDocPath, ResolvedInclude include)
    {
        // Build the subcollection reference from parent document path
        // parentDocPath: "Clientes/cli-001" → subcollection: "Clientes/cli-001/Pedidos"
        var docRef = _clientWrapper.Database.Document(parentDocPath);
        var subCollectionRef = docRef.Collection(include.CollectionPath);

        FirestoreQuery query = subCollectionRef;

        // Apply filters
        query = ApplyFilters(query, include.FilterResults);

        // Apply ordering
        query = ApplyOrderBy(query, include.OrderByClauses);

        // Apply pagination
        query = ApplyPagination(query, include.Pagination);

        return query;
    }
}
