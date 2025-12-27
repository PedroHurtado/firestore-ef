using Firestore.EntityFrameworkCore.Query.Projections;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Firestore.EntityFrameworkCore.Query.Ast
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
    ///
    /// Principios DDD: El AST solo se modifica mediante comandos específicos.
    /// Cada comando representa una operación de negocio clara.
    ///
    /// Patrón MicroDomain: Cada feature (OrderBy, Limit, Filter, etc.) tiene
    /// su propio partial file con: Record de parámetros + Commands + TranslateX estático.
    /// </summary>
    public partial class FirestoreQueryExpression : Expression
    {
        #region Properties

        /// <summary>
        /// Tipo de entidad que se está consultando
        /// </summary>
        public IEntityType EntityType { get; protected set; }

        /// <summary>
        /// Nombre de la colección en Firestore (ej: "productos", "clientes")
        /// </summary>
        public string CollectionName { get; protected set; }

        /// <summary>
        /// Lista de filtros WHERE aplicados a la query (AND implícito)
        /// </summary>
        private readonly List<FirestoreWhereClause> _filters = new();
        public IReadOnlyList<FirestoreWhereClause> Filters => _filters;

        /// <summary>
        /// Lista de grupos OR aplicados a la query.
        /// Each OR group is combined with AND with other filters.
        /// </summary>
        private readonly List<FirestoreOrFilterGroup> _orFilterGroups = new();
        public IReadOnlyList<FirestoreOrFilterGroup> OrFilterGroups => _orFilterGroups;

        /// <summary>
        /// Lista de ordenamientos aplicados a la query
        /// </summary>
        private readonly List<FirestoreOrderByClause> _orderByClauses = new();
        public IReadOnlyList<FirestoreOrderByClause> OrderByClauses => _orderByClauses;

        /// <summary>
        /// Límite de documentos a retornar (equivalente a LINQ Take).
        /// </summary>
        public int? Limit { get; protected set; }

        /// <summary>
        /// Expresión para el límite (para parámetros de EF Core).
        /// Se evalúa en tiempo de ejecución.
        /// </summary>
        public Expression? LimitExpression { get; protected set; }

        /// <summary>
        /// Límite de documentos a retornar desde el final (equivalente a LINQ TakeLast).
        /// Firestore usa LimitToLast() que requiere un OrderBy previo.
        /// </summary>
        public int? LimitToLast { get; protected set; }

        /// <summary>
        /// Expresión para LimitToLast (para parámetros de EF Core).
        /// Se evalúa en tiempo de ejecución.
        /// </summary>
        public Expression? LimitToLastExpression { get; protected set; }

        /// <summary>
        /// Número de documentos a saltar (equivalente a LINQ Skip).
        /// NOTA: Firestore no soporta offset nativo. Este skip se aplica
        /// en memoria después de obtener los resultados.
        /// </summary>
        public int? Skip { get; protected set; }

        /// <summary>
        /// Expresión para el skip (para parámetros de EF Core).
        /// Se evalúa en tiempo de ejecución.
        /// </summary>
        public Expression? SkipExpression { get; protected set; }

        /// <summary>
        /// Cursor desde el cual empezar (para paginación/Skip).
        /// </summary>
        public FirestoreCursor? StartAfterCursor { get; protected set; }

        /// <summary>
        /// Si la query es solo por ID, contiene la expresión del ID.
        /// En este caso, se usará GetDocumentAsync en lugar de ExecuteQueryAsync.
        /// </summary>
        public Expression? IdValueExpression { get; protected set; }

        /// <summary>
        /// Lista de navegaciones a cargar (Include/ThenInclude)
        /// </summary>
        private readonly List<IReadOnlyNavigation> _pendingIncludes = new();
        public IReadOnlyList<IReadOnlyNavigation> PendingIncludes => _pendingIncludes;

        /// <summary>
        /// Lista de Includes con información de filtros para Filtered Includes.
        /// </summary>
        private readonly List<IncludeInfo> _pendingIncludesWithFilters = new();
        public IReadOnlyList<IncludeInfo> PendingIncludesWithFilters => _pendingIncludesWithFilters;

        /// <summary>
        /// Lista de Includes en propiedades de ComplexTypes.
        /// </summary>
        private readonly List<LambdaExpression> _complexTypeIncludes = new();
        public IReadOnlyList<LambdaExpression> ComplexTypeIncludes => _complexTypeIncludes;

        /// <summary>
        /// Tipo de agregación a ejecutar (Count, Sum, Average, Min, Max).
        /// None indica una query normal que retorna entidades.
        /// </summary>
        public FirestoreAggregationType AggregationType { get; protected set; }

        /// <summary>
        /// Nombre de la propiedad para agregaciones Sum, Average, Min, Max.
        /// </summary>
        public string? AggregationPropertyName { get; protected set; }

        /// <summary>
        /// Tipo de resultado para agregaciones (int, long, decimal, double, etc).
        /// </summary>
        public Type? AggregationResultType { get; protected set; }

        /// <summary>
        /// Definición estructurada de la proyección Select.
        /// </summary>
        public FirestoreProjectionDefinition? Projection { get; protected set; }

        #endregion

        #region Computed Properties

        /// <summary>
        /// Indica si esta query tiene una proyección Select.
        /// </summary>
        public bool HasProjection => Projection != null;

        /// <summary>
        /// Indica si esta query es solo por ID (sin otros filtros)
        /// </summary>
        public bool IsIdOnlyQuery => IdValueExpression != null;

        /// <summary>
        /// Indica si esta query es una agregación
        /// </summary>
        public bool IsAggregation => AggregationType != FirestoreAggregationType.None;

        #endregion

        #region Expression Overrides

        /// <summary>
        /// Tipo de retorno de la expresión (IAsyncEnumerable del tipo de entidad)
        /// </summary>
        public override Type Type => typeof(IAsyncEnumerable<>).MakeGenericType(EntityType.ClrType);

        /// <summary>
        /// Tipo de nodo de expresión (Extension para expresiones personalizadas)
        /// </summary>
        public override ExpressionType NodeType => ExpressionType.Extension;

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor con solo los parámetros obligatorios
        /// </summary>
        public FirestoreQueryExpression(IEntityType entityType, string collectionName)
        {
            EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
            CollectionName = collectionName ?? throw new ArgumentNullException(nameof(collectionName));
        }

        #endregion

        #region Filter Commands

        /// <summary>
        /// Agrega un filtro WHERE a la query
        /// </summary>
        public FirestoreQueryExpression AddFilter(FirestoreWhereClause filter)
        {
            _filters.Add(filter);
            return this;
        }

        /// <summary>
        /// Agrega múltiples filtros WHERE a la query (para AND)
        /// </summary>
        public FirestoreQueryExpression AddFilters(IEnumerable<FirestoreWhereClause> filters)
        {
            _filters.AddRange(filters);
            return this;
        }

        /// <summary>
        /// Agrega un grupo OR a la query
        /// </summary>
        public FirestoreQueryExpression AddOrFilterGroup(FirestoreOrFilterGroup orGroup)
        {
            _orFilterGroups.Add(orGroup);
            return this;
        }

        #endregion

        // OrderBy Commands: see FirestoreQueryExpression_OrderBy.cs
        // Limit Commands: see FirestoreQueryExpression_Limit.cs

        #region Skip Commands

        /// <summary>
        /// Establece el número de documentos a saltar.
        /// </summary>
        public FirestoreQueryExpression WithSkip(int skip)
        {
            Skip = skip;
            return this;
        }

        /// <summary>
        /// Establece la expresión del skip (para parámetros de EF Core).
        /// </summary>
        public FirestoreQueryExpression WithSkipExpression(Expression skipExpression)
        {
            SkipExpression = skipExpression;
            return this;
        }

        /// <summary>
        /// Establece el cursor desde el cual empezar (para paginación)
        /// </summary>
        public FirestoreQueryExpression WithStartAfter(FirestoreCursor cursor)
        {
            StartAfterCursor = cursor;
            return this;
        }

        #endregion

        #region Id Query Commands

        /// <summary>
        /// Establece la expresión del ID para queries por documento único.
        /// </summary>
        public FirestoreQueryExpression WithIdValueExpression(Expression idExpression)
        {
            IdValueExpression = idExpression;
            return this;
        }

        /// <summary>
        /// Limpia la expresión del ID y establece filtros iniciales.
        /// Usado para convertir una IdOnlyQuery a una query normal con filtros.
        /// </summary>
        public FirestoreQueryExpression ClearIdValueExpressionWithFilters(IEnumerable<FirestoreWhereClause> initialFilters)
        {
            IdValueExpression = null;
            _filters.Clear();
            _filters.AddRange(initialFilters);
            return this;
        }

        #endregion

        #region Include Commands

        /// <summary>
        /// Agrega una navegación a cargar con Include (evita duplicados)
        /// </summary>
        public FirestoreQueryExpression AddInclude(IReadOnlyNavigation navigation)
        {
            // Evitar duplicados
            if (_pendingIncludes.Any(n => n.Name == navigation.Name &&
                                          n.DeclaringEntityType == navigation.DeclaringEntityType))
            {
                return this;
            }

            _pendingIncludes.Add(navigation);
            return this;
        }

        /// <summary>
        /// Agrega un Include con información de filtros
        /// </summary>
        public FirestoreQueryExpression AddIncludeWithFilters(IncludeInfo includeInfo)
        {
            _pendingIncludesWithFilters.Add(includeInfo);
            return this;
        }

        /// <summary>
        /// Establece los Includes de ComplexTypes.
        /// </summary>
        public FirestoreQueryExpression WithComplexTypeIncludes(IEnumerable<LambdaExpression> includes)
        {
            _complexTypeIncludes.Clear();
            _complexTypeIncludes.AddRange(includes);
            return this;
        }

        #endregion

        #region Aggregation Commands

        /// <summary>
        /// Configura la query para una agregación Count
        /// </summary>
        public FirestoreQueryExpression WithCount()
        {
            AggregationType = FirestoreAggregationType.Count;
            AggregationResultType = typeof(int);
            return this;
        }

        /// <summary>
        /// Configura la query para una agregación Any
        /// </summary>
        public FirestoreQueryExpression WithAny()
        {
            AggregationType = FirestoreAggregationType.Any;
            AggregationResultType = typeof(bool);
            return this;
        }

        /// <summary>
        /// Configura la query para una agregación Sum
        /// </summary>
        public FirestoreQueryExpression WithSum(string propertyName, Type resultType)
        {
            AggregationType = FirestoreAggregationType.Sum;
            AggregationPropertyName = propertyName;
            AggregationResultType = resultType;
            return this;
        }

        /// <summary>
        /// Configura la query para una agregación Average
        /// </summary>
        public FirestoreQueryExpression WithAverage(string propertyName, Type resultType)
        {
            AggregationType = FirestoreAggregationType.Average;
            AggregationPropertyName = propertyName;
            AggregationResultType = resultType;
            return this;
        }

        /// <summary>
        /// Configura la query para una agregación Min
        /// </summary>
        public FirestoreQueryExpression WithMin(string propertyName, Type resultType)
        {
            AggregationType = FirestoreAggregationType.Min;
            AggregationPropertyName = propertyName;
            AggregationResultType = resultType;
            return this;
        }

        /// <summary>
        /// Configura la query para una agregación Max
        /// </summary>
        public FirestoreQueryExpression WithMax(string propertyName, Type resultType)
        {
            AggregationType = FirestoreAggregationType.Max;
            AggregationPropertyName = propertyName;
            AggregationResultType = resultType;
            return this;
        }

        #endregion

        #region Projection Commands

        /// <summary>
        /// Configura la proyección Select de la query.
        /// </summary>
        public FirestoreQueryExpression WithProjection(FirestoreProjectionDefinition projection)
        {
            Projection = projection;
            return this;
        }

        #endregion

        #region ToString

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

            if (StartAfterCursor != null)
            {
                parts.Add($"StartAfter: {StartAfterCursor}");
            }

            return string.Join(" | ", parts);
        }

        #endregion
    }

    /// <summary>
    /// Special expression that represents the upper bound for a StartsWith query.
    /// </summary>
    public class StartsWithUpperBoundExpression : Expression
    {
        /// <summary>
        /// The prefix expression (the argument to StartsWith)
        /// </summary>
        public Expression PrefixExpression { get; }

        public StartsWithUpperBoundExpression(Expression prefixExpression)
        {
            PrefixExpression = prefixExpression ?? throw new ArgumentNullException(nameof(prefixExpression));
        }

        public override Type Type => typeof(string);
        public override ExpressionType NodeType => ExpressionType.Extension;

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var newPrefix = visitor.Visit(PrefixExpression);
            if (newPrefix != PrefixExpression)
            {
                return new StartsWithUpperBoundExpression(newPrefix);
            }
            return this;
        }

        /// <summary>
        /// Computes the upper bound string from a prefix.
        /// For "Alpha" returns "Alpha\uffff" which is greater than any string starting with "Alpha".
        /// </summary>
        public static string ComputeUpperBound(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                return "\uffff";
            }
            return prefix + '\uffff';
        }

        public override string ToString()
        {
            return $"StartsWithUpperBound({PrefixExpression})";
        }
    }
}
