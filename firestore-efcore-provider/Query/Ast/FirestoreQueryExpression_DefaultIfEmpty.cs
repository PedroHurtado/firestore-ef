using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Ast
{
    /// <summary>
    /// Parámetros para la traducción de DefaultIfEmpty.
    /// </summary>
    public record TranslateDefaultIfEmptyRequest(
        ShapedQueryExpression Source,
        Expression? DefaultValue);

    /// <summary>
    /// Feature: DefaultIfEmpty translation.
    /// DefaultIfEmpty returns a collection with a single default value if the source is empty.
    /// </summary>
    public partial class FirestoreQueryExpression
    {
        #region DefaultIfEmpty Properties

        /// <summary>
        /// Indica si esta query tiene DefaultIfEmpty aplicado.
        /// </summary>
        public bool HasDefaultIfEmpty { get; protected set; }

        /// <summary>
        /// Expresión del valor por defecto para DefaultIfEmpty.
        /// null significa usar default(T).
        /// </summary>
        public Expression? DefaultValueExpression { get; protected set; }

        #endregion

        #region DefaultIfEmpty Commands

        /// <summary>
        /// Establece DefaultIfEmpty con el valor por defecto.
        /// </summary>
        public FirestoreQueryExpression WithDefaultIfEmpty(Expression? defaultValue)
        {
            HasDefaultIfEmpty = true;
            DefaultValueExpression = defaultValue;
            return this;
        }

        #endregion

        #region DefaultIfEmpty Translation

        /// <summary>
        /// Traduce DefaultIfEmpty.
        /// Almacena el valor por defecto en el AST para que el Executor lo use si no hay resultados.
        /// </summary>
        public static ShapedQueryExpression? TranslateDefaultIfEmpty(TranslateDefaultIfEmptyRequest request)
        {
            var (source, defaultValue) = request;
            var ast = (FirestoreQueryExpression)source.QueryExpression;

            ast.WithDefaultIfEmpty(defaultValue);

            return source.UpdateQueryExpression(ast);
        }

        #endregion
    }
}
