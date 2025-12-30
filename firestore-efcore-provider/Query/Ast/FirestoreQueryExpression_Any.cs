using Firestore.EntityFrameworkCore.Query.Translators;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Ast
{
    /// <summary>
    /// Parámetros para la traducción de Any.
    /// </summary>
    public record TranslateAnyRequest(
        ShapedQueryExpression Source,
        LambdaExpression? Predicate);

    /// <summary>
    /// Feature: Any translation.
    /// Any checks if there are any elements matching the predicate.
    /// </summary>
    public partial class FirestoreQueryExpression
    {
        #region Any Properties

        /// <summary>
        /// Indica si esta query es una query Any.
        /// </summary>
        public bool IsAnyQuery => AggregationType == FirestoreAggregationType.Any;

        #endregion

        #region Any Translation

        /// <summary>
        /// Traduce Any.
        /// Si tiene predicate, primero aplica el filtro usando FirestoreWhereTranslator.
        /// Luego marca la query como Any.
        /// </summary>
        public static ShapedQueryExpression? TranslateAny(TranslateAnyRequest request)
        {
            var (source, predicate) = request;
            var ast = (FirestoreQueryExpression)source.QueryExpression;

            // Si hay predicate, traducir y aplicar filtros
            if (predicate != null)
            {
                var translator = new FirestoreWhereTranslator();
                var filterResult = translator.Translate(predicate.Body);

                if (filterResult == null)
                    return null;

                // Store the filter result for later processing
                ast.AddFilterResult(filterResult);

                if (filterResult.IsOrGroup)
                {
                    ast.AddOrFilterGroup(filterResult.OrGroup!);
                }
                else
                {
                    foreach (var clause in filterResult.AndClauses)
                    {
                        ast.AddFilter(clause);
                    }
                }
            }

            // Marcar como Any query
            ast.WithAny();

            return source.UpdateQueryExpression(ast);
        }

        #endregion
    }
}
