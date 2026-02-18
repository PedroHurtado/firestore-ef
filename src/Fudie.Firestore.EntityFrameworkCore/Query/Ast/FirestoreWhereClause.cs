using System;
using System.Linq.Expressions;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Ast
{
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
        public Expression ValueExpression { get; set; }

        /// <summary>
        /// Tipo del enum original cuando la comparación involucra un enum.
        /// Se usa para convertir el valor numérico a string.
        /// Null si no es una comparación de enum.
        /// </summary>
        public Type? EnumType { get; set; }

        /// <summary>
        /// Nombre de la colección Firestore cuando el filtro compara contra un DocumentReference.
        /// Cuando no es null, indica que PropertyName es una navegación Reference y el valor
        /// debe convertirse a DocumentReference("{CollectionName}/{value}") al ejecutar.
        /// </summary>
        public string? ReferenceCollectionName { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public FirestoreWhereClause(string propertyName, FirestoreOperator @operator, Expression valueExpression)
        {
            PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            Operator = @operator;
            ValueExpression = valueExpression ?? throw new ArgumentNullException(nameof(valueExpression));
        }

        /// <summary>
        /// Constructor con tipo de enum
        /// </summary>
        public FirestoreWhereClause(string propertyName, FirestoreOperator @operator, Expression valueExpression, Type? enumType)
            : this(propertyName, @operator, valueExpression)
        {
            EnumType = enumType;
        }

        /// <summary>
        /// Evalúa la expresión del valor en runtime usando el QueryContext proporcionado
        /// </summary>
        public object? EvaluateValue(Microsoft.EntityFrameworkCore.Query.QueryContext queryContext)
        {
            // Handle StartsWithUpperBoundExpression - compute prefix + \uffff
            if (ValueExpression is StartsWithUpperBoundExpression startsWithUpperBound)
            {
                // First evaluate the prefix expression
                var prefixClause = new FirestoreWhereClause(PropertyName, Operator, startsWithUpperBound.PrefixExpression);
                var prefix = prefixClause.EvaluateValue(queryContext) as string;
                return StartsWithUpperBoundExpression.ComputeUpperBound(prefix ?? "");
            }

            // Si es una ConstantExpression, retornar su valor directamente
            if (ValueExpression is ConstantExpression constant)
            {
                return constant.Value;
            }

            // Para cualquier otra expresión (incluyendo accesos a QueryContext.ParameterValues),
            // compilarla y ejecutarla con el QueryContext como parámetro
            try
            {
                // Reemplazar el parámetro queryContext en la expresión con el valor real
                var replacer = new QueryContextParameterReplacer(queryContext);
                var replacedExpression = replacer.Visit(ValueExpression);

                // Compilar y evaluar
                var lambda = Expression.Lambda<Func<object>>(
                    Expression.Convert(replacedExpression, typeof(object)));

                var compiled = lambda.Compile();
                var result = compiled();

                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to evaluate filter value expression: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Visitor que reemplaza referencias al parámetro QueryContext con el valor real
        /// y resuelve parámetros desde QueryContext.ParameterValues
        /// </summary>
        private class QueryContextParameterReplacer : ExpressionVisitor
        {
            private readonly Microsoft.EntityFrameworkCore.Query.QueryContext _queryContext;

            public QueryContextParameterReplacer(Microsoft.EntityFrameworkCore.Query.QueryContext queryContext)
            {
                _queryContext = queryContext;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                // Si es el parámetro "queryContext", reemplazarlo con una constante que contiene el QueryContext real
                if (node.Name == "queryContext" && node.Type == typeof(Microsoft.EntityFrameworkCore.Query.QueryContext))
                {
                    return Expression.Constant(_queryContext, typeof(Microsoft.EntityFrameworkCore.Query.QueryContext));
                }

                // Si es un parámetro que existe en QueryContext.ParameterValues (variables capturadas),
                // reemplazarlo con su valor real
                if (node.Name != null && _queryContext.ParameterValues.TryGetValue(node.Name, out var parameterValue))
                {
                    return Expression.Constant(parameterValue, node.Type);
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
}
