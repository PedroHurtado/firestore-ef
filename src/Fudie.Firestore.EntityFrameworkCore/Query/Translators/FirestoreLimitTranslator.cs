using System;
using System.Linq.Expressions;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Translators
{
    /// <summary>
    /// Traduce expresiones de límite (Take/TakeLast) a valores enteros.
    /// </summary>
    internal class FirestoreLimitTranslator
    {
        /// <summary>
        /// Traduce una expresión de conteo a un valor entero.
        /// </summary>
        /// <param name="countExpression">Expresión que representa el límite</param>
        /// <returns>Valor entero o null si no se puede evaluar en tiempo de compilación</returns>
        public int? Translate(Expression countExpression)
        {
            return ExtractIntConstant(countExpression);
        }

        /// <summary>
        /// Extracts an integer constant value from an expression.
        /// Handles literal constants, closures, captured variables, and arithmetic expressions.
        /// </summary>
        private static int? ExtractIntConstant(Expression expression)
        {
            // Direct compile and evaluate - works for most cases including
            // closures, captured variables, and parametrized expressions
            try
            {
                var converted = expression.Type == typeof(int)
                    ? expression
                    : Expression.Convert(expression, typeof(int));
                var lambda = Expression.Lambda<Func<int>>(converted);
                var compiled = lambda.Compile();
                return compiled();
            }
            catch
            {
                // If compile fails, return null to indicate we can't translate
                return null;
            }
        }
    }
}
