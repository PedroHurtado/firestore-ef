using Firestore.EntityFrameworkCore.Query.Ast;
using System;
using System.Collections.Generic;

namespace Firestore.EntityFrameworkCore.Query.Projections
{
    /// <summary>
    /// Represents a subcollection projection with optional filters, ordering, and limits.
    /// Reuses FirestoreWhereClause and FirestoreOrderByClause for consistency.
    /// All filters and ordering are applied in Firestore, not in memory.
    /// </summary>
    public class FirestoreSubcollectionProjection
    {
        /// <summary>
        /// Name of the navigation property in the entity (e.g., "Pedidos").
        /// </summary>
        public string NavigationName { get; }

        /// <summary>
        /// Name of the field in the projection result.
        /// May differ from NavigationName when using aliases (e.g., "TopPedidos").
        /// </summary>
        public string ResultName { get; }

        /// <summary>
        /// Name of the collection in Firestore (e.g., "orders").
        /// </summary>
        public string CollectionName { get; }

        /// <summary>
        /// Filters to apply to the subcollection query.
        /// Reuses FirestoreWhereClause for consistency with main query filters.
        /// </summary>
        public List<FirestoreWhereClause> Filters { get; }

        /// <summary>
        /// Ordering clauses for the subcollection query.
        /// Reuses FirestoreOrderByClause for consistency with main query ordering.
        /// </summary>
        public List<FirestoreOrderByClause> OrderByClauses { get; }

        /// <summary>
        /// Maximum number of documents to return (Take).
        /// Null means no limit.
        /// </summary>
        public int? Limit { get; set; }

        /// <summary>
        /// Fields to project from the subcollection.
        /// Null means return the entire entity.
        /// </summary>
        public List<FirestoreProjectedField>? Fields { get; set; }

        /// <summary>
        /// Aggregation type if this is an aggregation projection (Count, Sum, etc.).
        /// Null means no aggregation.
        /// </summary>
        public FirestoreAggregationType? Aggregation { get; set; }

        /// <summary>
        /// Property name for aggregations that require it (Sum, Average, Min, Max).
        /// </summary>
        public string? AggregationPropertyName { get; set; }

        /// <summary>
        /// Nested subcollections (e.g., Pedidos.Lineas).
        /// </summary>
        public List<FirestoreSubcollectionProjection> NestedSubcollections { get; }

        /// <summary>
        /// Creates a new subcollection projection definition.
        /// </summary>
        /// <param name="navigationName">Name of the navigation property in the entity.</param>
        /// <param name="resultName">Name of the field in the projection result.</param>
        /// <param name="collectionName">Name of the collection in Firestore.</param>
        public FirestoreSubcollectionProjection(
            string navigationName,
            string resultName,
            string collectionName)
        {
            NavigationName = navigationName ?? throw new ArgumentNullException(nameof(navigationName));
            ResultName = resultName ?? throw new ArgumentNullException(nameof(resultName));
            CollectionName = collectionName ?? throw new ArgumentNullException(nameof(collectionName));
            Filters = new List<FirestoreWhereClause>();
            OrderByClauses = new List<FirestoreOrderByClause>();
            NestedSubcollections = new List<FirestoreSubcollectionProjection>();
        }
    }
}
