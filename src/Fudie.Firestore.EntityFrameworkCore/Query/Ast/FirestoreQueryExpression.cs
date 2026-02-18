using Fudie.Firestore.EntityFrameworkCore.Query.Projections;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Ast
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
        /// Nombre de la propiedad que es la clave primaria (ej: "Id").
        /// Se usa en el Resolver para detectar optimización de ID.
        /// </summary>
        public string? PrimaryKeyPropertyName { get; protected set; }

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
        /// Lista de resultados de filtros traducidos.
        /// Cada FirestoreFilterResult corresponde a un .Where() o predicado de operador terminal.
        /// Se almacena para procesamiento posterior sin afectar la funcionalidad existente.
        /// </summary>
        private readonly List<FirestoreFilterResult> _filterResults = new();
        public IReadOnlyList<FirestoreFilterResult> FilterResults => _filterResults;

        /// <summary>
        /// Lista de ordenamientos aplicados a la query
        /// </summary>
        private readonly List<FirestoreOrderByClause> _orderByClauses = new();
        public IReadOnlyList<FirestoreOrderByClause> OrderByClauses => _orderByClauses;

        /// <summary>
        /// Pagination information (Limit, LimitToLast, Skip) with support for
        /// both constant values and parameterized expressions.
        /// </summary>
        public FirestorePaginationInfo Pagination { get; } = new();

        // Backward compatibility properties - delegate to Pagination
        /// <summary>
        /// Límite de documentos a retornar (equivalente a LINQ Take).
        /// </summary>
        public int? Limit => Pagination.Limit;

        /// <summary>
        /// Expresión para el límite (para parámetros de EF Core).
        /// </summary>
        public Expression? LimitExpression => Pagination.LimitExpression;

        /// <summary>
        /// Límite de documentos a retornar desde el final (equivalente a LINQ TakeLast).
        /// </summary>
        public int? LimitToLast => Pagination.LimitToLast;

        /// <summary>
        /// Expresión para LimitToLast (para parámetros de EF Core).
        /// </summary>
        public Expression? LimitToLastExpression => Pagination.LimitToLastExpression;

        /// <summary>
        /// Número de documentos a saltar (equivalente a LINQ Skip).
        /// </summary>
        public int? Skip => Pagination.Skip;

        /// <summary>
        /// Expresión para el skip (para parámetros de EF Core).
        /// </summary>
        public Expression? SkipExpression => Pagination.SkipExpression;

        /// <summary>
        /// Cursor desde el cual empezar (para paginación/Skip).
        /// </summary>
        public FirestoreCursor? StartAfterCursor { get; protected set; }

        // IdValueExpression, ReturnDefault, ReturnType: see FirestoreQueryExpression_FirstOrDefault.cs

        /// <summary>
        /// Lista de Includes a cargar (Include/ThenInclude).
        /// Uses IncludeInfo with only primitive types (no EF Core types) for cache compatibility.
        /// </summary>
        private readonly List<IncludeInfo> _pendingIncludes = new();
        public IReadOnlyList<IncludeInfo> PendingIncludes => _pendingIncludes;

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

        // IsIdOnlyQuery: see FirestoreQueryExpression_FirstOrDefault.cs

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
        public FirestoreQueryExpression(IEntityType entityType, string collectionName, string? primaryKeyPropertyName = null)
        {
            EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
            CollectionName = collectionName ?? throw new ArgumentNullException(nameof(collectionName));
            PrimaryKeyPropertyName = primaryKeyPropertyName;
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

        /// <summary>
        /// Agrega un resultado de filtro traducido a la query.
        /// Se almacena para procesamiento posterior.
        /// </summary>
        public FirestoreQueryExpression AddFilterResult(FirestoreFilterResult filterResult)
        {
            _filterResults.Add(filterResult);
            return this;
        }

        #endregion

        // OrderBy Commands: see FirestoreQueryExpression_OrderBy.cs
        // Limit Commands: see FirestoreQueryExpression_Limit.cs
        // Skip Commands: see FirestoreQueryExpression_Skip.cs

        #region Cursor Commands

        /// <summary>
        /// Establece el cursor desde el cual empezar (para paginación)
        /// </summary>
        public FirestoreQueryExpression WithStartAfter(FirestoreCursor cursor)
        {
            StartAfterCursor = cursor;
            return this;
        }

        #endregion

        // FirstOrDefault Commands: see FirestoreQueryExpression_FirstOrDefault.cs

        #region Include Commands

        /// <summary>
        /// Adds an Include to the query (avoids duplicates by NavigationName).
        /// </summary>
        public FirestoreQueryExpression AddInclude(IncludeInfo includeInfo)
        {
            // Avoid duplicates by name
            if (_pendingIncludes.Any(i => i.NavigationName == includeInfo.NavigationName))
            {
                return this;
            }

            _pendingIncludes.Add(includeInfo);
            return this;
        }

        /// <summary>
        /// Adds an Include with full navigation information.
        /// Convenience overload that creates IncludeInfo internally.
        /// </summary>
        public FirestoreQueryExpression AddInclude(string navigationName, bool isCollection, string collectionName, Type targetClrType)
        {
            return AddInclude(new IncludeInfo(navigationName, isCollection, collectionName, targetClrType));
        }

        /// <summary>
        /// Removes a pending Include by navigation name.
        /// Used when a Reference navigation in a Where clause is detected and transformed
        /// into a DocumentReference comparison, making the auto-generated Include unnecessary.
        /// </summary>
        public FirestoreQueryExpression RemoveInclude(string navigationName)
        {
            _pendingIncludes.RemoveAll(i => i.NavigationName == navigationName);
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
