using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Represents an Include with optional filter for Filtered Includes.
    /// Supports .Include(c => c.Pedidos.Where(p => p.Estado == EstadoPedido.Confirmado))
    /// </summary>
    public class IncludeInfo
    {
        /// <summary>
        /// The navigation to include. Can be null during early extraction phase.
        /// </summary>
        public IReadOnlyNavigation? Navigation { get; }

        /// <summary>
        /// The navigation property name. Used when Navigation is not yet available.
        /// </summary>
        public string? NavigationName { get; }

        /// <summary>
        /// Optional filter expression for Filtered Includes.
        /// For .Include(c => c.Pedidos.Where(p => ...)), this is the Where predicate.
        /// Null for unfiltered includes.
        /// </summary>
        public LambdaExpression? FilterExpression { get; set; }

        /// <summary>
        /// Optional ordering expressions for the subcollection.
        /// </summary>
        public List<(LambdaExpression KeySelector, bool Descending)> OrderByExpressions { get; } = new();

        /// <summary>
        /// Optional Take limit for the subcollection.
        /// </summary>
        public int? Take { get; set; }

        /// <summary>
        /// Optional Skip count for the subcollection.
        /// </summary>
        public int? Skip { get; set; }

        /// <summary>
        /// Creates an IncludeInfo with a navigation reference.
        /// </summary>
        public IncludeInfo(IReadOnlyNavigation navigation)
        {
            Navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            NavigationName = navigation.Name;
        }

        /// <summary>
        /// Creates an IncludeInfo with just a navigation name (for early extraction phase).
        /// </summary>
        public IncludeInfo(string navigationName)
        {
            NavigationName = navigationName ?? throw new ArgumentNullException(nameof(navigationName));
            Navigation = null;
        }

        /// <summary>
        /// Whether this include has any filter/ordering/limit operations.
        /// </summary>
        public bool HasOperations => FilterExpression != null ||
                                     OrderByExpressions.Count > 0 ||
                                     Take.HasValue ||
                                     Skip.HasValue;

        /// <summary>
        /// Gets the effective navigation name.
        /// </summary>
        public string EffectiveNavigationName => Navigation?.Name ?? NavigationName ?? string.Empty;

        public override string ToString()
        {
            var parts = new List<string> { EffectiveNavigationName };
            if (FilterExpression != null) parts.Add("Where");
            if (OrderByExpressions.Count > 0) parts.Add($"OrderBy({OrderByExpressions.Count})");
            if (Take.HasValue) parts.Add($"Take({Take})");
            if (Skip.HasValue) parts.Add($"Skip({Skip})");
            return string.Join(".", parts);
        }
    }
}
