using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

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
            DocumentSnapshot? startAfterDocument = null)
        {
            return new FirestoreQueryExpression(
                entityType ?? EntityType,
                collectionName ?? CollectionName)
            {
                Filters = filters ?? new List<FirestoreWhereClause>(Filters),
                OrderByClauses = orderByClauses ?? new List<FirestoreOrderByClause>(OrderByClauses),
                Limit = limit ?? Limit,
                StartAfterDocument = startAfterDocument ?? StartAfterDocument
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

    /// <summary>
    /// Representa una cláusula WHERE en una query de Firestore
    /// </summary>
    public class FirestoreWhereClause
    {
        /// <summary>
        /// Nombre de la propiedad/campo a filtrar (ej: "Precio", "Categoria")
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// Operador de comparación (==, !=, &gt;, &lt;, etc.)
        /// </summary>
        public FirestoreOperator Operator { get; set; }

        /// <summary>
        /// Expresión que representa el valor con el que comparar.
        /// Puede ser una ConstantExpression o una expresión que accede a QueryContext.ParameterValues
        /// </summary>
        public System.Linq.Expressions.Expression ValueExpression { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public FirestoreWhereClause(string propertyName, FirestoreOperator @operator, System.Linq.Expressions.Expression valueExpression)
        {
            PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            Operator = @operator;
            ValueExpression = valueExpression ?? throw new ArgumentNullException(nameof(valueExpression));
        }

        /// <summary>
        /// Evalúa la expresión del valor en runtime usando el QueryContext proporcionado
        /// </summary>
        public object? EvaluateValue(Microsoft.EntityFrameworkCore.Query.QueryContext queryContext)
        {
            System.Console.WriteLine($"[DEBUG EvaluateValue] Starting evaluation of expression: {ValueExpression}");
            System.Console.WriteLine($"[DEBUG EvaluateValue] Expression type: {ValueExpression.GetType().Name}");

            // Si es una ConstantExpression, retornar su valor directamente
            if (ValueExpression is System.Linq.Expressions.ConstantExpression constant)
            {
                System.Console.WriteLine($"[DEBUG EvaluateValue] Constant expression, value: {constant.Value}");
                return constant.Value;
            }

            // Para cualquier otra expresión (incluyendo accesos a QueryContext.ParameterValues),
            // compilarla y ejecutarla con el QueryContext como parámetro
            try
            {
                System.Console.WriteLine($"[DEBUG EvaluateValue] Attempting to replace queryContext parameter");

                // Reemplazar el parámetro queryContext en la expresión con el valor real
                var replacer = new QueryContextParameterReplacer(queryContext);
                var replacedExpression = replacer.Visit(ValueExpression);

                System.Console.WriteLine($"[DEBUG EvaluateValue] After replacement: {replacedExpression}");
                System.Console.WriteLine($"[DEBUG EvaluateValue] Replaced expression type: {replacedExpression.GetType().Name}");

                // Compilar y evaluar
                var lambda = System.Linq.Expressions.Expression.Lambda<Func<object>>(
                    System.Linq.Expressions.Expression.Convert(replacedExpression, typeof(object)));

                System.Console.WriteLine($"[DEBUG EvaluateValue] Lambda created, compiling...");
                var compiled = lambda.Compile();

                System.Console.WriteLine($"[DEBUG EvaluateValue] Compiled, executing...");
                var result = compiled();

                System.Console.WriteLine($"[DEBUG EvaluateValue] Result: {result ?? "NULL"}");
                System.Console.WriteLine($"[DEBUG EvaluateValue] Result type: {result?.GetType().Name ?? "NULL"}");

                return result;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[ERROR EvaluateValue] Exception: {ex.GetType().Name}");
                System.Console.WriteLine($"[ERROR EvaluateValue] Message: {ex.Message}");
                System.Console.WriteLine($"[ERROR EvaluateValue] StackTrace: {ex.StackTrace}");
                throw new InvalidOperationException($"Failed to evaluate filter value expression: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Visitor que reemplaza referencias al parámetro QueryContext con el valor real
        /// </summary>
        private class QueryContextParameterReplacer : System.Linq.Expressions.ExpressionVisitor
        {
            private readonly Microsoft.EntityFrameworkCore.Query.QueryContext _queryContext;

            public QueryContextParameterReplacer(Microsoft.EntityFrameworkCore.Query.QueryContext queryContext)
            {
                _queryContext = queryContext;
            }

            protected override System.Linq.Expressions.Expression VisitParameter(System.Linq.Expressions.ParameterExpression node)
            {
                // Si es el parámetro "queryContext", reemplazarlo con una constante que contiene el QueryContext real
                if (node.Name == "queryContext" && node.Type == typeof(Microsoft.EntityFrameworkCore.Query.QueryContext))
                {
                    return System.Linq.Expressions.Expression.Constant(_queryContext, typeof(Microsoft.EntityFrameworkCore.Query.QueryContext));
                }

                return base.VisitParameter(node);
            }
        }

        /// <summary>
        /// Representación en string para debugging
        /// </summary>
        public override string ToString()
        {
            var operatorSymbol = Operator switch
            {
                FirestoreOperator.EqualTo => "==",
                FirestoreOperator.NotEqualTo => "!=",
                FirestoreOperator.LessThan => "<",
                FirestoreOperator.LessThanOrEqualTo => "<=",
                FirestoreOperator.GreaterThan => ">",
                FirestoreOperator.GreaterThanOrEqualTo => ">=",
                FirestoreOperator.ArrayContains => "array-contains",
                FirestoreOperator.In => "in",
                FirestoreOperator.ArrayContainsAny => "array-contains-any",
                FirestoreOperator.NotIn => "not-in",
                _ => Operator.ToString()
            };

            return $"{PropertyName} {operatorSymbol} <Expression: {ValueExpression}>";
        }
    }

    /// <summary>
    /// Operadores de comparación soportados por Firestore
    /// </summary>
    public enum FirestoreOperator
    {
        /// <summary>
        /// Igual a (==) - WhereEqualTo
        /// </summary>
        EqualTo,

        /// <summary>
        /// No igual a (!=) - WhereNotEqualTo
        /// </summary>
        NotEqualTo,

        /// <summary>
        /// Menor que (&lt;) - WhereLessThan
        /// </summary>
        LessThan,

        /// <summary>
        /// Menor o igual que (&lt;=) - WhereLessThanOrEqualTo
        /// </summary>
        LessThanOrEqualTo,

        /// <summary>
        /// Mayor que (&gt;) - WhereGreaterThan
        /// </summary>
        GreaterThan,

        /// <summary>
        /// Mayor o igual que (&gt;=) - WhereGreaterThanOrEqualTo
        /// </summary>
        GreaterThanOrEqualTo,

        /// <summary>
        /// Array contiene (array-contains) - WhereArrayContains
        /// Ejemplo: p.Tags.Contains("nuevo")
        /// </summary>
        ArrayContains,

        /// <summary>
        /// Valor en lista (in) - WhereIn
        /// Ejemplo: ids.Contains(p.Id)
        /// NOTA: Firestore limita a 30 elementos máximo
        /// </summary>
        In,

        /// <summary>
        /// Array contiene cualquiera (array-contains-any) - WhereArrayContainsAny
        /// Ejemplo: p.Tags intersecta con ["nuevo", "destacado"]
        /// NOTA: Firestore limita a 30 elementos máximo
        /// </summary>
        ArrayContainsAny,

        /// <summary>
        /// Valor NO en lista (not-in) - WhereNotIn
        /// NOTA: Firestore limita a 10 elementos máximo
        /// </summary>
        NotIn
    }

    /// <summary>
    /// Representa una cláusula ORDER BY en una query de Firestore
    /// </summary>
    public class FirestoreOrderByClause
    {
        /// <summary>
        /// Nombre de la propiedad/campo por el cual ordenar (ej: "Nombre", "Precio")
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// Si es orden descendente (true) o ascendente (false)
        /// </summary>
        public bool Descending { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public FirestoreOrderByClause(string propertyName, bool descending = false)
        {
            PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            Descending = descending;
        }

        /// <summary>
        /// Representación en string para debugging
        /// </summary>
        public override string ToString()
        {
            return $"{PropertyName} {(Descending ? "DESC" : "ASC")}";
        }
    }
}
