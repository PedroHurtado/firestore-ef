using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Tipo de agregación para queries de Firestore.
    /// </summary>
    public enum FirestoreAggregationType
    {
        None,
        Count,
        Any,
        Sum,
        Average,
        Min,
        Max
    }

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
        /// Lista de filtros WHERE aplicados a la query (AND implícito)
        /// </summary>
        public List<FirestoreWhereClause> Filters { get; set; }

        /// <summary>
        /// Lista de grupos OR aplicados a la query.
        /// Each OR group is combined with AND with other filters.
        /// </summary>
        public List<FirestoreOrFilterGroup> OrFilterGroups { get; set; }

        /// <summary>
        /// Lista de ordenamientos aplicados a la query
        /// </summary>
        public List<FirestoreOrderByClause> OrderByClauses { get; set; }

        /// <summary>
        /// Límite de documentos a retornar (equivalente a LINQ Take).
        /// Puede ser un valor constante (int?) o una expresión parametrizada.
        /// </summary>
        public int? Limit { get; set; }

        /// <summary>
        /// Expresión para el límite (para parámetros de EF Core).
        /// Se evalúa en tiempo de ejecución.
        /// </summary>
        public Expression? LimitExpression { get; set; }

        /// <summary>
        /// Número de documentos a saltar (equivalente a LINQ Skip).
        /// NOTA: Firestore no soporta offset nativo. Este skip se aplica
        /// en memoria después de obtener los resultados, lo cual es ineficiente
        /// para grandes conjuntos de datos. Para paginación eficiente, usar
        /// cursores con StartAfterDocument.
        /// </summary>
        public int? Skip { get; set; }

        /// <summary>
        /// Expresión para el skip (para parámetros de EF Core).
        /// Se evalúa en tiempo de ejecución.
        /// </summary>
        public Expression? SkipExpression { get; set; }

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
        /// Lista de Includes en propiedades de ComplexTypes.
        /// Ej: .Include(e => e.DireccionPrincipal.SucursalCercana)
        /// Estas se extraen antes de que EF Core las procese (ya que EF Core no las soporta)
        /// y se cargan durante la deserialización.
        /// </summary>
        public List<LambdaExpression> ComplexTypeIncludes { get; set; }

        /// <summary>
        /// Tipo de agregación a ejecutar (Count, Sum, Average, Min, Max).
        /// None indica una query normal que retorna entidades.
        /// </summary>
        public FirestoreAggregationType AggregationType { get; set; }

        /// <summary>
        /// Nombre de la propiedad para agregaciones Sum, Average, Min, Max.
        /// </summary>
        public string? AggregationPropertyName { get; set; }

        /// <summary>
        /// Tipo de resultado para agregaciones (int, long, decimal, double, etc).
        /// </summary>
        public Type? AggregationResultType { get; set; }

        /// <summary>
        /// Indica si esta query es solo por ID (sin otros filtros)
        /// </summary>
        public bool IsIdOnlyQuery => IdValueExpression != null;

        /// <summary>
        /// Indica si esta query es una agregación
        /// </summary>
        public bool IsAggregation => AggregationType != FirestoreAggregationType.None;

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
            OrFilterGroups = new List<FirestoreOrFilterGroup>();
            OrderByClauses = new List<FirestoreOrderByClause>();
            PendingIncludes = new List<IReadOnlyNavigation>();
            ComplexTypeIncludes = new List<LambdaExpression>();
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
            List<FirestoreOrFilterGroup>? orFilterGroups = null,
            List<FirestoreOrderByClause>? orderByClauses = null,
            int? limit = null,
            Expression? limitExpression = null,
            int? skip = null,
            Expression? skipExpression = null,
            DocumentSnapshot? startAfterDocument = null,
            Expression? idValueExpression = null,
            List<IReadOnlyNavigation>? pendingIncludes = null,
            List<LambdaExpression>? complexTypeIncludes = null,
            FirestoreAggregationType? aggregationType = null,
            string? aggregationPropertyName = null,
            Type? aggregationResultType = null)
        {
            return new FirestoreQueryExpression(
                entityType ?? EntityType,
                collectionName ?? CollectionName)
            {
                Filters = filters ?? new List<FirestoreWhereClause>(Filters),
                OrFilterGroups = orFilterGroups ?? new List<FirestoreOrFilterGroup>(OrFilterGroups),
                OrderByClauses = orderByClauses ?? new List<FirestoreOrderByClause>(OrderByClauses),
                Limit = limit ?? Limit,
                LimitExpression = limitExpression ?? LimitExpression,
                Skip = skip ?? Skip,
                SkipExpression = skipExpression ?? SkipExpression,
                StartAfterDocument = startAfterDocument ?? StartAfterDocument,
                IdValueExpression = idValueExpression ?? IdValueExpression,
                PendingIncludes = pendingIncludes ?? new List<IReadOnlyNavigation>(PendingIncludes),
                ComplexTypeIncludes = complexTypeIncludes ?? new List<LambdaExpression>(ComplexTypeIncludes),
                AggregationType = aggregationType ?? AggregationType,
                AggregationPropertyName = aggregationPropertyName ?? AggregationPropertyName,
                AggregationResultType = aggregationResultType ?? AggregationResultType
            };
        }

        /// <summary>
        /// Configura la query para una agregación Count
        /// </summary>
        public FirestoreQueryExpression WithCount()
        {
            return Update(aggregationType: FirestoreAggregationType.Count, aggregationResultType: typeof(int));
        }

        /// <summary>
        /// Configura la query para una agregación Any
        /// </summary>
        public FirestoreQueryExpression WithAny()
        {
            return Update(aggregationType: FirestoreAggregationType.Any, aggregationResultType: typeof(bool));
        }

        /// <summary>
        /// Configura la query para una agregación Sum
        /// </summary>
        public FirestoreQueryExpression WithSum(string propertyName, Type resultType)
        {
            return Update(
                aggregationType: FirestoreAggregationType.Sum,
                aggregationPropertyName: propertyName,
                aggregationResultType: resultType);
        }

        /// <summary>
        /// Configura la query para una agregación Average
        /// </summary>
        public FirestoreQueryExpression WithAverage(string propertyName, Type resultType)
        {
            return Update(
                aggregationType: FirestoreAggregationType.Average,
                aggregationPropertyName: propertyName,
                aggregationResultType: resultType);
        }

        /// <summary>
        /// Configura la query para una agregación Min
        /// </summary>
        public FirestoreQueryExpression WithMin(string propertyName, Type resultType)
        {
            return Update(
                aggregationType: FirestoreAggregationType.Min,
                aggregationPropertyName: propertyName,
                aggregationResultType: resultType);
        }

        /// <summary>
        /// Configura la query para una agregación Max
        /// </summary>
        public FirestoreQueryExpression WithMax(string propertyName, Type resultType)
        {
            return Update(
                aggregationType: FirestoreAggregationType.Max,
                aggregationPropertyName: propertyName,
                aggregationResultType: resultType);
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
        /// Agrega múltiples filtros WHERE a la query (para AND)
        /// </summary>
        public FirestoreQueryExpression AddFilters(IEnumerable<FirestoreWhereClause> filters)
        {
            var newFilters = new List<FirestoreWhereClause>(Filters);
            newFilters.AddRange(filters);
            return Update(filters: newFilters);
        }

        /// <summary>
        /// Agrega un grupo OR a la query
        /// </summary>
        public FirestoreQueryExpression AddOrFilterGroup(FirestoreOrFilterGroup orGroup)
        {
            var newOrGroups = new List<FirestoreOrFilterGroup>(OrFilterGroups) { orGroup };
            return Update(orFilterGroups: newOrGroups);
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
        /// Establece la expresión del límite (para parámetros de EF Core).
        /// Se evalúa en tiempo de ejecución.
        /// </summary>
        public FirestoreQueryExpression WithLimitExpression(Expression limitExpression)
        {
            return Update(limitExpression: limitExpression);
        }

        /// <summary>
        /// Establece el número de documentos a saltar.
        /// NOTA: Firestore no soporta offset nativo. Este skip se aplica
        /// en memoria, lo cual es ineficiente para grandes conjuntos de datos.
        /// </summary>
        public FirestoreQueryExpression WithSkip(int skip)
        {
            return Update(skip: skip);
        }

        /// <summary>
        /// Establece la expresión del skip (para parámetros de EF Core).
        /// Se evalúa en tiempo de ejecución.
        /// </summary>
        public FirestoreQueryExpression WithSkipExpression(Expression skipExpression)
        {
            return Update(skipExpression: skipExpression);
        }

        /// <summary>
        /// Establece el documento desde el cual empezar (para paginación)
        /// </summary>
        public FirestoreQueryExpression WithStartAfter(DocumentSnapshot document)
        {
            return Update(startAfterDocument: document);
        }

        /// <summary>
        /// Agrega una navegación a cargar con Include (evita duplicados)
        /// </summary>
        public FirestoreQueryExpression AddInclude(IReadOnlyNavigation navigation)
        {
            // Evitar duplicados - verificar si ya existe la misma navegación
            if (PendingIncludes.Any(n => n.Name == navigation.Name &&
                                         n.DeclaringEntityType == navigation.DeclaringEntityType))
            {
                return this; // Ya existe, no agregar duplicado
            }

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
