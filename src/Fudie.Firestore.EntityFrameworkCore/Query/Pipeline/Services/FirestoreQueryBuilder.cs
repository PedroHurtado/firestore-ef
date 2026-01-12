using Fudie.Firestore.EntityFrameworkCore.Infrastructure;
using Fudie.Firestore.EntityFrameworkCore.Query.Ast;
using Fudie.Firestore.EntityFrameworkCore.Query.Resolved;
using Google.Cloud.Firestore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FirestoreQuery = Google.Cloud.Firestore.Query;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

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
        FirestoreQuery query = _clientWrapper.GetCollection(resolvedQuery.CollectionPath);

        query = ApplyFilters(query, resolvedQuery.FilterResults);
        query = ApplyOrderBy(query, resolvedQuery.OrderByClauses);

        // Min/Max: Select + OrderBy + Limit(1)
        if (resolvedQuery.AggregationType == FirestoreAggregationType.Min)
        {
            return query
                .Select(resolvedQuery.AggregationPropertyName!)
                .OrderBy(resolvedQuery.AggregationPropertyName!)
                .Limit(1);
        }

        if (resolvedQuery.AggregationType == FirestoreAggregationType.Max)
        {
            return query
                .Select(resolvedQuery.AggregationPropertyName!)
                .OrderByDescending(resolvedQuery.AggregationPropertyName!)
                .Limit(1);
        }

        query = ApplyProjection(query, resolvedQuery.Projection, resolvedQuery.Includes, resolvedQuery.CollectionPath);
        query = ApplyPagination(query, resolvedQuery.Pagination);

        if (resolvedQuery.StartAfterCursor != null)
            query = ApplyCursor(query, resolvedQuery.StartAfterCursor);

        return query;
    }

    /// <inheritdoc />
    public AggregateQuery BuildAggregate(ResolvedFirestoreQuery resolvedQuery)
    {
        FirestoreQuery query = _clientWrapper.GetCollection(resolvedQuery.CollectionPath);
        query = ApplyFilters(query, resolvedQuery.FilterResults);

        return BuildAggregateQuery(query, resolvedQuery.AggregationType, resolvedQuery.AggregationPropertyName);
    }

    /// <inheritdoc />
    public FirestoreQuery BuildInclude(string parentDocPath, ResolvedInclude include)
        => BuildSubcollectionBase(parentDocPath, include.CollectionPath,
            include.FilterResults, include.OrderByClauses, include.Pagination);

    /// <inheritdoc />
    public FirestoreQuery BuildSubcollectionQuery(string parentDocPath, ResolvedSubcollectionProjection subcollection)
    {
        var query = BuildSubcollectionBase(parentDocPath, subcollection.CollectionPath,
            subcollection.FilterResults, subcollection.OrderByClauses, subcollection.Pagination);

        if (subcollection.Fields?.Count > 0)
        {
            // Exclude fields that belong to includes (e.g., "Libros.Titulo" when "Libro" is an include)
            // Use CollectionPath (e.g., "Libros") to match field paths, not NavigationName
            var includeCollectionPaths = GetAllIncludeCollectionPaths(subcollection.Includes);
            var fields = subcollection.Fields
                .Select(f => f.FieldPath)
                .Where(path => !includeCollectionPaths.Any(cp => path.StartsWith(cp + ".")))
                .ToList();

            // Add FK navigation fields needed for includes (e.g., "Libro" for Libro.Titulo)
            foreach (var include in subcollection.Includes)
            {
                if (!fields.Contains(include.NavigationName))
                    fields.Add(include.NavigationName);
            }

            query = query.Select(fields.ToArray());
        }

        return query;
    }

    /// <inheritdoc />
    public AggregateQuery BuildSubcollectionAggregate(string parentDocPath, ResolvedSubcollectionProjection subcollection)
    {
        var query = BuildSubcollectionBase(parentDocPath, subcollection.CollectionPath,
            subcollection.FilterResults, subcollection.OrderByClauses, subcollection.Pagination);

        return BuildAggregateQuery(query, subcollection.Aggregation ?? FirestoreAggregationType.Count, subcollection.AggregationPropertyName);
    }

    /// <summary>
    /// Builds a base query for any subcollection (includes and projections).
    /// </summary>
    private FirestoreQuery BuildSubcollectionBase(
        string parentDocPath,
        string collectionPath,
        IReadOnlyList<ResolvedFilterResult> filters,
        IReadOnlyList<ResolvedOrderByClause> orderBy,
        ResolvedPaginationInfo pagination)
    {
        var relativePath = ExtractRelativePath(parentDocPath);
        var docRef = _clientWrapper.Database.Document(relativePath);
        FirestoreQuery query = docRef.Collection(collectionPath);

        query = ApplyFilters(query, filters);
        query = ApplyOrderBy(query, orderBy);
        return ApplyPagination(query, pagination);
    }

    /// <summary>
    /// Builds an AggregateQuery from a base query and aggregation type.
    /// </summary>
    private static AggregateQuery BuildAggregateQuery(
        FirestoreQuery query,
        FirestoreAggregationType aggregationType,
        string? propertyName)
    {
        return aggregationType switch
        {
            FirestoreAggregationType.Count or FirestoreAggregationType.Any => query.Count(),
            FirestoreAggregationType.Sum => query.Aggregate(AggregateField.Sum(propertyName!)),
            FirestoreAggregationType.Average => query.Aggregate(AggregateField.Average(propertyName!)),
            FirestoreAggregationType.Min or FirestoreAggregationType.Max =>
                throw new NotSupportedException("Min/Max are not native Firestore aggregations. Use Build() instead."),
            _ => throw new NotSupportedException($"Aggregation {aggregationType} not supported")
        };
    }

    private static FirestoreQuery ApplyFilters(FirestoreQuery query, IReadOnlyList<ResolvedFilterResult> filterResults)
    {
        foreach (var filterResult in filterResults)
        {
            foreach (var clause in filterResult.AndClauses)
                query = query.Where(CreateFilter(clause));

            if (filterResult.OrGroup != null)
                query = ApplyOrGroup(query, filterResult.OrGroup);

            if (filterResult.NestedOrGroups != null)
            {
                foreach (var nestedOrGroup in filterResult.NestedOrGroups)
                    query = ApplyOrGroup(query, nestedOrGroup);
            }
        }

        return query;
    }

    private static FirestoreQuery ApplyOrGroup(FirestoreQuery query, ResolvedOrFilterGroup orGroup)
    {
        if (orGroup.Clauses.Count == 0)
            return query;

        var filters = orGroup.Clauses.Select(CreateFilter).ToArray();
        return query.Where(Filter.Or(filters));
    }

    private static Filter CreateFilter(ResolvedWhereClause clause)
    {
        var value = clause.Value;
        // Use IsPrimaryKey flag to determine if this is a document ID filter
        // The flag is set by FirestoreAstResolver using EF Core metadata
        var fieldPath = clause.IsPrimaryKey
            ? FieldPath.DocumentId
            : new FieldPath(clause.PropertyName.Split('.'));

        // Firestore document IDs must be strings - convert non-string PK values
        if (clause.IsPrimaryKey && value != null && value is not string)
        {
            value = value.ToString();
        }

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

    private static FirestoreQuery ApplyProjection(
        FirestoreQuery query,
        ResolvedProjectionDefinition? projection,
        IReadOnlyList<ResolvedInclude>? includes = null,
        string? rootCollectionPath = null)
    {
        if (projection == null || !projection.HasFields)
            return query;

        // If no Reference includes, use original behavior exactly as before
        var referenceNavigations = BuildReferenceNavigationMap(includes);
        if (referenceNavigations.Count == 0)
        {
            return query.Select(projection.Fields!.Select(f => f.FieldPath).ToArray());
        }

        // With References:
        // - Replace reference collection fields with navigation name (e.g., "Autors.Nombre" → "Autor")
        // - Strip root collection prefix from root fields (e.g., "Libros.Titulo" → "Titulo")
        // - Keep ComplexType paths as-is (e.g., "Direccion.Ciudad")
        var fieldPaths = new HashSet<string>();

        foreach (var field in projection.Fields!)
        {
            var fieldPath = field.FieldPath;

            // Check if this field belongs to a Reference navigation
            // e.g., "Autors.Nombre" → should project "Autor" (the DocumentReference field)
            var referenceField = GetReferenceFieldName(fieldPath, referenceNavigations);
            if (referenceField != null)
            {
                fieldPaths.Add(referenceField);
            }
            else
            {
                // Check if it starts with root collection prefix (e.g., "Libros.Titulo" → "Titulo")
                var cleanedPath = StripRootCollectionPrefix(fieldPath, rootCollectionPath);
                fieldPaths.Add(cleanedPath);
            }
        }

        return query.Select(fieldPaths.ToArray());
    }

    /// <summary>
    /// Strips the root collection prefix from a field path if present.
    /// "Libros.Titulo" with rootCollectionPath="Libros" → "Titulo"
    /// "Direccion.Ciudad" with rootCollectionPath="Libros" → "Direccion.Ciudad" (no change)
    /// </summary>
    private static string StripRootCollectionPrefix(string fieldPath, string? rootCollectionPath)
    {
        if (string.IsNullOrEmpty(rootCollectionPath))
            return fieldPath;

        var prefix = rootCollectionPath + ".";
        if (fieldPath.StartsWith(prefix, StringComparison.Ordinal))
        {
            return fieldPath[prefix.Length..];
        }

        return fieldPath;
    }

    /// <summary>
    /// Builds a map of Reference collection paths to navigation names.
    /// </summary>
    private static Dictionary<string, string> BuildReferenceNavigationMap(IReadOnlyList<ResolvedInclude>? includes)
    {
        var map = new Dictionary<string, string>();
        if (includes == null)
            return map;

        // Detectar colecciones con múltiples referencias
        var collectionCounts = includes
            .Where(i => i.IsReference)
            .GroupBy(i => i.CollectionPath)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var include in includes)
        {
            if (include.IsReference)
            {
                var hasDuplicates = collectionCounts[include.CollectionPath] > 1;
                var key = hasDuplicates ? include.NavigationName : include.CollectionPath;
                map[key] = include.NavigationName;
            }
        }

        return map;
    }

    /// <summary>
    /// Checks if a field path belongs to a Reference navigation and returns the navigation name.
    /// Handles both partial projections (e.g., "Autors.Nombre" → "Autor") and
    /// full entity projections (e.g., "Autors" → "Autor").
    /// </summary>
    private static string? GetReferenceFieldName(string fieldPath, Dictionary<string, string> referenceNavigations)
    {
        // Check for full entity projection (e.g., "Autors" → "Autor")
        // This happens when projecting the complete FK entity: Select(l => l.Autor)
        if (referenceNavigations.TryGetValue(fieldPath, out var fullEntityNavigation))
        {
            return fullEntityNavigation;
        }

        // Check for partial projection (e.g., "Autors.Nombre" → "Autor")
        var dotIndex = fieldPath.IndexOf('.');
        if (dotIndex <= 0)
            return null;

        var collectionPrefix = fieldPath[..dotIndex];

        if (referenceNavigations.TryGetValue(collectionPrefix, out var navigationName))
        {
            return navigationName;
        }

        return null;
    }

    private static FirestoreQuery ApplyPagination(FirestoreQuery query, ResolvedPaginationInfo pagination)
    {
        if (pagination.Skip.HasValue)
            query = query.Offset(pagination.Skip.Value);

        if (pagination.Limit.HasValue)
            query = query.Limit(pagination.Limit.Value);

        if (pagination.LimitToLast.HasValue)
            query = query.LimitToLast(pagination.LimitToLast.Value);

        return query;
    }

    private static FirestoreQuery ApplyCursor(FirestoreQuery query, ResolvedCursor cursor)
    {
        if (cursor.OrderByValues?.Count > 0)
        {
            var values = new object?[cursor.OrderByValues.Count + 1];
            values[0] = cursor.DocumentId;
            for (int i = 0; i < cursor.OrderByValues.Count; i++)
                values[i + 1] = cursor.OrderByValues[i];
            return query.StartAfter(values);
        }

        return query.StartAfter(cursor.DocumentId);
    }

    /// <summary>
    /// Extracts relative path from full Firestore document path.
    /// "projects/{p}/databases/{d}/documents/Collection/DocId" → "Collection/DocId"
    /// </summary>
    private static string ExtractRelativePath(string fullPath)
    {
        const string marker = "/documents/";
        var index = fullPath.IndexOf(marker, StringComparison.Ordinal);
        return index >= 0 ? fullPath.Substring(index + marker.Length) : fullPath;
    }

    /// <summary>
    /// Gets all collection paths from includes recursively (for filtering FK fields from subcollection projections).
    /// </summary>
    private static HashSet<string> GetAllIncludeCollectionPaths(IReadOnlyList<ResolvedInclude> includes)
    {
        var paths = new HashSet<string>();
        CollectIncludeCollectionPaths(includes, paths);
        return paths;
    }

    private static void CollectIncludeCollectionPaths(IReadOnlyList<ResolvedInclude> includes, HashSet<string> paths)
    {
        foreach (var include in includes)
        {
            paths.Add(include.CollectionPath);
            CollectIncludeCollectionPaths(include.NestedIncludes, paths);
        }
    }
}