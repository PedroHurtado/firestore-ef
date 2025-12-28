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
        /// Take limit for the subcollection (constant value).
        /// </summary>
        public int? Take { get; private set; }

        /// <summary>
        /// Take limit expression for parameterized queries.
        /// Resolved by AstResolver at execution time.
        /// </summary>
        public Expression? TakeExpression { get; private set; }

        /// <summary>
        /// Skip count for the subcollection (constant value).
        /// </summary>
        public int? Skip { get; private set; }

        /// <summary>
        /// Skip count expression for parameterized queries.
        /// Resolved by AstResolver at execution time.
        /// </summary>
        public Expression? SkipExpression { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an IncludeInfo with navigation name and collection flag.
        /// </summary>
        public IncludeInfo(string navigationName, bool isCollection)
        {
            NavigationName = navigationName ?? throw new ArgumentNullException(nameof(navigationName));
            IsCollection = isCollection;
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
            Take = take;
            TakeExpression = null;
            return this;
        }

        /// <summary>
        /// Sets the Take limit (expression for parameterized queries).
        /// </summary>
        public IncludeInfo WithTakeExpression(Expression takeExpression)
        {
            Take = null;
            TakeExpression = takeExpression;
            return this;
        }

        /// <summary>
        /// Sets the Skip count (constant value).
        /// </summary>
        public IncludeInfo WithSkip(int skip)
        {
            Skip = skip;
            SkipExpression = null;
            return this;
        }

        /// <summary>
        /// Sets the Skip count (expression for parameterized queries).
        /// </summary>
        public IncludeInfo WithSkipExpression(Expression skipExpression)
        {
            Skip = null;
            SkipExpression = skipExpression;
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
                                     Take.HasValue ||
                                     TakeExpression != null ||
                                     Skip.HasValue ||
                                     SkipExpression != null;

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
