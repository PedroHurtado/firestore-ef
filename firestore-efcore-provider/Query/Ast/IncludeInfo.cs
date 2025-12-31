using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Ast
{
    /// <summary>
    /// Represents an Include with optional filter for Filtered Includes.
    /// Supports .Include(c => c.Pedidos.Where(p => p.Estado == EstadoPedido.Confirmado))
    ///
    /// Uses only AST types (no EF Core types like IReadOnlyNavigation) for:
    /// - Cache compatibility
    /// - Clean separation between translation and execution
    /// - Executor only needs NavigationName and IsCollection
    ///
    /// Structure:
    /// - NavigationName (string) - the property name
    /// - IsCollection (bool) - SubCollection vs DocumentReference
    /// - CollectionName (string) - Firestore collection name
    /// - TargetClrType (Type) - CLR type of the target entity
    /// - FirestoreWhereClause for filters
    /// - FirestoreOrderByClause for ordering
    /// - int?/Expression? for Take/Skip (Expression only for parameterized queries)
    /// </summary>
    public class IncludeInfo
    {
        #region Navigation

        /// <summary>
        /// The navigation property name.
        /// </summary>
        public string NavigationName { get; }

        /// <summary>
        /// Whether this is a collection navigation (SubCollection) or single (DocumentReference).
        /// </summary>
        public bool IsCollection { get; }

        /// <summary>
        /// The Firestore collection name for this navigation.
        /// </summary>
        public string CollectionName { get; }

        /// <summary>
        /// The CLR type of the target entity.
        /// </summary>
        public Type TargetClrType { get; }

        /// <summary>
        /// The name of the primary key property for the target entity.
        /// Used by the Resolver to detect ID optimization.
        /// </summary>
        public string? PrimaryKeyPropertyName { get; }

        #endregion

        #region Filters (translated by FirestoreWhereTranslator)

        /// <summary>
        /// Filters for the subcollection (AND logic).
        /// Translated from .Where(p => ...) using FirestoreWhereTranslator.
        /// </summary>
        private readonly List<FirestoreWhereClause> _filters = new();
        public IReadOnlyList<FirestoreWhereClause> Filters => _filters;

        /// <summary>
        /// OR filter groups for the subcollection.
        /// </summary>
        private readonly List<FirestoreOrFilterGroup> _orFilterGroups = new();
        public IReadOnlyList<FirestoreOrFilterGroup> OrFilterGroups => _orFilterGroups;

        /// <summary>
        /// Lista de resultados de filtros traducidos.
        /// Cada FirestoreFilterResult corresponde a un .Where() en el Include filtrado.
        /// Se almacena para procesamiento posterior sin afectar la funcionalidad existente.
        /// </summary>
        private readonly List<FirestoreFilterResult> _filterResults = new();
        public IReadOnlyList<FirestoreFilterResult> FilterResults => _filterResults;

        #endregion

        #region OrderBy (translated by FirestoreOrderByTranslator)

        /// <summary>
        /// Ordering clauses for the subcollection.
        /// Translated from .OrderBy/.ThenBy using FirestoreOrderByTranslator.
        /// </summary>
        private readonly List<FirestoreOrderByClause> _orderByClauses = new();
        public IReadOnlyList<FirestoreOrderByClause> OrderByClauses => _orderByClauses;

        #endregion

        #region Limit/Skip (translated by FirestoreLimitTranslator)

        /// <summary>
        /// Pagination information (Limit, LimitToLast, Skip) with support for
        /// both constant values and parameterized expressions.
        /// </summary>
        public FirestorePaginationInfo Pagination { get; } = new();

        // Backward compatibility properties - delegate to Pagination
        // Note: Uses "Take" name for backward compatibility with existing code
        /// <summary>
        /// Take limit for the subcollection (constant value).
        /// </summary>
        public int? Take => Pagination.Limit;

        /// <summary>
        /// Take limit expression for parameterized queries.
        /// </summary>
        public Expression? TakeExpression => Pagination.LimitExpression;

        /// <summary>
        /// Skip count for the subcollection (constant value).
        /// </summary>
        public int? Skip => Pagination.Skip;

        /// <summary>
        /// Skip count expression for parameterized queries.
        /// </summary>
        public Expression? SkipExpression => Pagination.SkipExpression;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an IncludeInfo with full navigation information.
        /// </summary>
        public IncludeInfo(string navigationName, bool isCollection, string collectionName, Type targetClrType, string? primaryKeyPropertyName = null)
        {
            NavigationName = navigationName ?? throw new ArgumentNullException(nameof(navigationName));
            IsCollection = isCollection;
            CollectionName = collectionName ?? throw new ArgumentNullException(nameof(collectionName));
            TargetClrType = targetClrType ?? throw new ArgumentNullException(nameof(targetClrType));
            PrimaryKeyPropertyName = primaryKeyPropertyName;
        }

        #endregion

        #region Commands

        /// <summary>
        /// Adds a filter clause to the include.
        /// </summary>
        public IncludeInfo AddFilter(FirestoreWhereClause filter)
        {
            _filters.Add(filter);
            return this;
        }

        /// <summary>
        /// Adds multiple filter clauses to the include.
        /// </summary>
        public IncludeInfo AddFilters(IEnumerable<FirestoreWhereClause> filters)
        {
            _filters.AddRange(filters);
            return this;
        }

        /// <summary>
        /// Adds an OR filter group to the include.
        /// </summary>
        public IncludeInfo AddOrFilterGroup(FirestoreOrFilterGroup orGroup)
        {
            _orFilterGroups.Add(orGroup);
            return this;
        }

        /// <summary>
        /// Adds a filter result to the include.
        /// Se almacena para procesamiento posterior.
        /// </summary>
        public IncludeInfo AddFilterResult(FirestoreFilterResult filterResult)
        {
            _filterResults.Add(filterResult);
            return this;
        }

        /// <summary>
        /// Sets the ordering (replaces existing - for OrderBy).
        /// </summary>
        public IncludeInfo SetOrderBy(FirestoreOrderByClause orderBy)
        {
            _orderByClauses.Clear();
            _orderByClauses.Add(orderBy);
            return this;
        }

        /// <summary>
        /// Adds an ordering clause (for ThenBy).
        /// </summary>
        public IncludeInfo AddOrderBy(FirestoreOrderByClause orderBy)
        {
            _orderByClauses.Add(orderBy);
            return this;
        }

        /// <summary>
        /// Sets the Take limit (constant value).
        /// </summary>
        public IncludeInfo WithTake(int take)
        {
            Pagination.WithLimit(take);
            return this;
        }

        /// <summary>
        /// Sets the Take limit (expression for parameterized queries).
        /// </summary>
        public IncludeInfo WithTakeExpression(Expression takeExpression)
        {
            Pagination.WithLimitExpression(takeExpression);
            return this;
        }

        /// <summary>
        /// Sets the Skip count (constant value).
        /// </summary>
        public IncludeInfo WithSkip(int skip)
        {
            Pagination.WithSkip(skip);
            return this;
        }

        /// <summary>
        /// Sets the Skip count (expression for parameterized queries).
        /// </summary>
        public IncludeInfo WithSkipExpression(Expression skipExpression)
        {
            Pagination.WithSkipExpression(skipExpression);
            return this;
        }

        #endregion

        #region Computed Properties

        /// <summary>
        /// Whether this include has any filter/ordering/limit operations.
        /// </summary>
        public bool HasOperations => _filters.Count > 0 ||
                                     _orFilterGroups.Count > 0 ||
                                     _orderByClauses.Count > 0 ||
                                     Pagination.HasPagination;

        #endregion

        #region ToString

        public override string ToString()
        {
            var parts = new List<string> { NavigationName };
            if (IsCollection) parts.Add("[Collection]");
            if (_filters.Count > 0 || _orFilterGroups.Count > 0)
                parts.Add($"Where({_filters.Count + _orFilterGroups.Count})");
            if (_orderByClauses.Count > 0)
                parts.Add($"OrderBy({_orderByClauses.Count})");
            if (Take.HasValue || TakeExpression != null)
                parts.Add($"Take({Take?.ToString() ?? "expr"})");
            if (Skip.HasValue || SkipExpression != null)
                parts.Add($"Skip({Skip?.ToString() ?? "expr"})");
            return string.Join(".", parts);
        }

        #endregion
    }
}
