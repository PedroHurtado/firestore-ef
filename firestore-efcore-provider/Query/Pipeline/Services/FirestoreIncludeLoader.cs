using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Resolved;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Executes resolved includes for eager loading (Include/ThenInclude).
/// Uses ResolvedInclude (already resolved by Resolver) - converts to ResolvedFirestoreQuery
/// and executes via sub-pipeline for tracking and proxying.
/// </summary>
public class FirestoreIncludeLoader : IIncludeLoader
{
    private readonly IQueryPipelineMediator _mediator;
    private readonly IFirestoreQueryContext _queryContext;

    public FirestoreIncludeLoader(IQueryPipelineMediator mediator, IFirestoreQueryContext queryContext)
    {
        _mediator = mediator;
        _queryContext = queryContext;
    }

    public async Task LoadIncludeAsync(
        object entity,
        IEntityType entityType,
        ResolvedInclude resolvedInclude,
        CancellationToken cancellationToken)
    {
        var navigation = entityType.FindNavigation(resolvedInclude.NavigationName);
        if (navigation == null)
            return;

        // Convert ResolvedInclude → ResolvedFirestoreQuery with FK filter
        var resolvedQuery = ToResolvedQuery(entity, navigation, resolvedInclude);

        // Execute sub-pipeline
        var context = new PipelineContext
        {
            Ast = null!,
            QueryContext = _queryContext,
            IsTracking = true,
            ResultType = resolvedInclude.TargetEntityType,
            Kind = QueryKind.Entity,
            EntityType = resolvedInclude.TargetEntityType,
            ResolvedQuery = resolvedQuery
        };

        var results = new List<object>();
        await foreach (var item in _mediator.ExecuteAsync<object>(context, cancellationToken))
        {
            results.Add(item);
        }

        AssignNavigation(entity, navigation, results);

        // Nested includes
        if (resolvedInclude.NestedIncludes.Count > 0)
        {
            foreach (var item in results)
            {
                foreach (var nested in resolvedInclude.NestedIncludes)
                {
                    await LoadIncludeAsync(item, navigation.TargetEntityType, nested, cancellationToken);
                }
            }
        }
    }

    /// <summary>
    /// Converts ResolvedInclude to ResolvedFirestoreQuery, adding FK filter.
    /// </summary>
    private static ResolvedFirestoreQuery ToResolvedQuery(
        object entity,
        INavigation navigation,
        ResolvedInclude include)
    {
        var fkFilter = BuildFkFilter(entity, navigation, include);
        var filters = new List<ResolvedFilterResult>();
        if (fkFilter != null)
            filters.Add(fkFilter);
        filters.AddRange(include.FilterResults);

        return new ResolvedFirestoreQuery(
            CollectionPath: include.CollectionPath,
            EntityClrType: include.TargetEntityType,
            DocumentId: include.IsCollection ? null : GetDocumentIdForReference(entity, navigation),
            FilterResults: filters,
            OrderByClauses: include.OrderByClauses,
            Pagination: include.Pagination,
            StartAfterCursor: null,
            Includes: [],
            AggregationType: FirestoreAggregationType.None,
            AggregationPropertyName: null,
            AggregationResultType: null,
            Projection: null,
            ReturnDefault: false,
            ReturnType: null);
    }

    private static ResolvedFilterResult? BuildFkFilter(object entity, INavigation navigation, ResolvedInclude include)
    {
        if (!include.IsCollection)
            return null; // Reference uses DocumentId

        var fk = navigation.ForeignKey;
        var pkValue = fk.PrincipalKey.Properties[0].GetGetter().GetClrValue(entity);
        var fkPropertyName = fk.Properties[0].Name;

        return ResolvedFilterResult.FromClause(
            new ResolvedWhereClause(fkPropertyName, FirestoreOperator.EqualTo, pkValue));
    }

    private static string? GetDocumentIdForReference(object entity, INavigation navigation)
    {
        var fkValue = navigation.ForeignKey.Properties[0].GetGetter().GetClrValue(entity);
        return fkValue?.ToString();
    }

    private static void AssignNavigation(object entity, INavigation navigation, IReadOnlyList<object> items)
    {
        var prop = navigation.PropertyInfo;
        if (prop == null) return;

        if (navigation.IsCollection)
        {
            var elementType = navigation.ClrType.IsGenericType
                ? navigation.ClrType.GetGenericArguments()[0]
                : typeof(object);
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
            foreach (var item in items) list.Add(item);
            prop.SetValue(entity, list);
        }
        else
        {
            prop.SetValue(entity, items.Count > 0 ? items[0] : null);
        }
    }
}
