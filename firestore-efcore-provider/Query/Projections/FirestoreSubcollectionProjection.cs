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
        /// Whether this is a collection navigation (subcollection) or a reference navigation (document reference).
        /// True for subcollections (List&lt;T&gt;), false for document references (single entity).
        /// </summary>
        public bool IsCollection { get; }

        /// <summary>
        /// The CLR type of the target entity.
        /// </summary>
        public Type TargetClrType { get; }

        /// <summary>
        /// The name of the primary key property for the target entity.
        /// Used by the Resolver to detect ID optimization.
        /// </summary>
        public string? PrimaryKeyPropertyName { get; }

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
        /// Pagination information (Limit, LimitToLast, Skip) with support for
        /// both constant values and parameterized expressions.
        /// </summary>
        public FirestorePaginationInfo Pagination { get; } = new();

        // Backward compatibility properties - delegate to Pagination
        /// <summary>
        /// Maximum number of documents to return (Take).
        /// </summary>
        public int? Limit => Pagination.Limit;

        /// <summary>
        /// Number of documents to skip (Skip/Offset).
        /// </summary>
        public int? Offset => Pagination.Skip;

        /// <summary>
        /// Indicates if this is an Any() operation (returns bool).
        /// </summary>
        public bool IsAny { get; set; }

        /// <summary>
        /// Indicates if this returns a single element (First, FirstOrDefault, Single, SingleOrDefault).
        /// </summary>
        public bool IsSingleElement { get; set; }

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
        /// Lista de resultados de filtros traducidos.
        /// Cada FirestoreFilterResult corresponde a un .Where() en la subcollection.
        /// Se almacena para procesamiento posterior sin afectar la funcionalidad existente.
        /// </summary>
        public List<FirestoreFilterResult> FilterResults { get; }

        /// <summary>
        /// Includes for FK navigations within the subcollection.
        /// Populated when EF Core generates LeftJoin for FK references in subcollection projections.
        /// </summary>
        public List<IncludeInfo> Includes { get; }

        /// <summary>
        /// Creates a new subcollection projection definition.
        /// </summary>
        /// <param name="navigationName">Name of the navigation property in the entity.</param>
        /// <param name="resultName">Name of the field in the projection result.</param>
        /// <param name="collectionName">Name of the collection in Firestore.</param>
        /// <param name="isCollection">Whether this is a collection navigation (true) or reference navigation (false).</param>
        /// <param name="targetClrType">The CLR type of the target entity.</param>
        /// <param name="primaryKeyPropertyName">The name of the primary key property for the target entity.</param>
        public FirestoreSubcollectionProjection(
            string navigationName,
            string resultName,
            string collectionName,
            bool isCollection,
            Type targetClrType,
            string? primaryKeyPropertyName = null)
        {
            NavigationName = navigationName ?? throw new ArgumentNullException(nameof(navigationName));
            ResultName = resultName ?? throw new ArgumentNullException(nameof(resultName));
            CollectionName = collectionName ?? throw new ArgumentNullException(nameof(collectionName));
            IsCollection = isCollection;
            TargetClrType = targetClrType ?? throw new ArgumentNullException(nameof(targetClrType));
            PrimaryKeyPropertyName = primaryKeyPropertyName;
            Filters = new List<FirestoreWhereClause>();
            OrderByClauses = new List<FirestoreOrderByClause>();
            NestedSubcollections = new List<FirestoreSubcollectionProjection>();
            FilterResults = new List<FirestoreFilterResult>();
            Includes = new List<IncludeInfo>();
        }
    }
}
