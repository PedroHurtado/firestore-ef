using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Representación interna de una query de Firestore.
    /// Esta clase encapsula toda la información necesaria para construir
    /// una Google.Cloud.Firestore.Query y ejecutarla.
    /// </summary>
    public class FirestoreQueryExpression : Expression
    {
        /// <summary>
        /// Tipo de entidad que se está consultando
        /// </summary>
        public IEntityType EntityType { get; set; }

        /// <summary>
        /// Nombre de la colección en Firestore (ej: "productos", "clientes")
        /// </summary>
        public string CollectionName { get; set; }

        /// <summary>
        /// Lista de filtros WHERE aplicados a la query
        /// </summary>
        public List<FirestoreWhereClause> Filters { get; set; }

        /// <summary>
        /// Lista de ordenamientos aplicados a la query
        /// </summary>
        public List<FirestoreOrderByClause> OrderByClauses { get; set; }

        /// <summary>
        /// Límite de documentos a retornar (equivalente a LINQ Take)
        /// </summary>
        public int? Limit { get; set; }

        /// <summary>
        /// Documento desde el cual empezar (para paginación/Skip)
        /// </summary>
        public DocumentSnapshot? StartAfterDocument { get; set; }

        /// <summary>
        /// Si la query es solo por ID, contiene la expresión del ID.
        /// En este caso, se usará GetDocumentAsync en lugar de ExecuteQueryAsync.
        /// </summary>
        public Expression? IdValueExpression { get; set; }

        /// <summary>
        /// Lista de navegaciones a cargar (Include/ThenInclude)
        /// </summary>
        public List<IReadOnlyNavigation> PendingIncludes { get; set; }

        /// <summary>
        /// Indica si esta query es solo por ID (sin otros filtros)
        /// </summary>
        public bool IsIdOnlyQuery => IdValueExpression != null;

        /// <summary>
        /// Constructor
        /// </summary>
        public FirestoreQueryExpression(
            IEntityType entityType,
            string collectionName)
        {
            EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
            CollectionName = collectionName ?? throw new ArgumentNullException(nameof(collectionName));
            Filters = new List<FirestoreWhereClause>();
            OrderByClauses = new List<FirestoreOrderByClause>();
            PendingIncludes = new List<IReadOnlyNavigation>();
        }

        /// <summary>
        /// Tipo de retorno de la expresión (IAsyncEnumerable del tipo de entidad)
        /// </summary>
        public override Type Type => typeof(IAsyncEnumerable<>).MakeGenericType(EntityType.ClrType);

        /// <summary>
        /// Tipo de nodo de expresión (Extension para expresiones personalizadas)
        /// </summary>
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <summary>
        /// Crea una copia de esta expresión con los cambios especificados
        /// </summary>
        public FirestoreQueryExpression Update(
            IEntityType? entityType = null,
            string? collectionName = null,
            List<FirestoreWhereClause>? filters = null,
            List<FirestoreOrderByClause>? orderByClauses = null,
            int? limit = null,
            DocumentSnapshot? startAfterDocument = null,
            Expression? idValueExpression = null,
            List<IReadOnlyNavigation>? pendingIncludes = null)
        {
            return new FirestoreQueryExpression(
                entityType ?? EntityType,
                collectionName ?? CollectionName)
            {
                Filters = filters ?? new List<FirestoreWhereClause>(Filters),
                OrderByClauses = orderByClauses ?? new List<FirestoreOrderByClause>(OrderByClauses),
                Limit = limit ?? Limit,
                StartAfterDocument = startAfterDocument ?? StartAfterDocument,
                IdValueExpression = idValueExpression ?? IdValueExpression,
                PendingIncludes = pendingIncludes ?? new List<IReadOnlyNavigation>(PendingIncludes)
            };
        }

        /// <summary>
        /// Agrega un filtro WHERE a la query
        /// </summary>
        public FirestoreQueryExpression AddFilter(FirestoreWhereClause filter)
        {
            var newFilters = new List<FirestoreWhereClause>(Filters) { filter };
            return Update(filters: newFilters);
        }

        /// <summary>
        /// Agrega un ordenamiento a la query
        /// </summary>
        public FirestoreQueryExpression AddOrderBy(FirestoreOrderByClause orderBy)
        {
            var newOrderBys = new List<FirestoreOrderByClause>(OrderByClauses) { orderBy };
            return Update(orderByClauses: newOrderBys);
        }

        /// <summary>
        /// Establece el límite de documentos a retornar
        /// </summary>
        public FirestoreQueryExpression WithLimit(int limit)
        {
            return Update(limit: limit);
        }

        /// <summary>
        /// Establece el documento desde el cual empezar (para paginación)
        /// </summary>
        public FirestoreQueryExpression WithStartAfter(DocumentSnapshot document)
        {
            return Update(startAfterDocument: document);
        }

        /// <summary>
        /// Agrega una navegación a cargar con Include
        /// </summary>
        public FirestoreQueryExpression AddInclude(IReadOnlyNavigation navigation)
        {
            var newIncludes = new List<IReadOnlyNavigation>(PendingIncludes) { navigation };
            return Update(pendingIncludes: newIncludes);
        }

        /// <summary>
        /// Representación en string para debugging
        /// </summary>
        public override string ToString()
        {
            var parts = new List<string>();
            parts.Add($"Collection: {CollectionName}");

            if (Filters.Count > 0)
            {
                var filters = string.Join(", ", Filters);
                parts.Add($"Filters: [{filters}]");
            }

            if (OrderByClauses.Count > 0)
            {
                var orderBys = string.Join(", ", OrderByClauses);
                parts.Add($"OrderBy: [{orderBys}]");
            }

            if (Limit.HasValue)
            {
                parts.Add($"Limit: {Limit.Value}");
            }

            if (StartAfterDocument != null)
            {
                parts.Add($"StartAfter: {StartAfterDocument.Id}");
            }

            return string.Join(" | ", parts);
        }
    }
}
