using Firestore.EntityFrameworkCore.Query.Translators;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Ast
{
    /// <summary>
    /// Parámetros para la traducción de Count.
    /// </summary>
    public record TranslateCountRequest(
        ShapedQueryExpression Source,
        LambdaExpression? Predicate);

    /// <summary>
    /// Feature: Count translation.
    /// Count returns the number of elements matching the predicate.
    /// </summary>
    public partial class FirestoreQueryExpression
    {
        #region Count Properties

        /// <summary>
        /// Indica si esta query es una query Count.
        /// </summary>
        public bool IsCountQuery => AggregationType == FirestoreAggregationType.Count;

        #endregion

        #region Count Translation

        /// <summary>
        /// Traduce Count.
        /// Si tiene predicate, primero aplica el filtro usando FirestoreWhereTranslator.
        /// Luego marca la query como Count.
        /// </summary>
        public static ShapedQueryExpression? TranslateCount(TranslateCountRequest request)
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

            // Marcar como Count query
            ast.WithCount();

            return source.UpdateQueryExpression(ast);
        }

        #endregion
    }
}
