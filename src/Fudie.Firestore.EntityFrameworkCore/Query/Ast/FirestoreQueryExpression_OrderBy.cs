using Fudie.Firestore.EntityFrameworkCore.Query.Translators;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Ast
{
    /// <summary>
    /// Parámetros para la traducción de OrderBy/ThenBy.
    /// </summary>
    public record TranslateOrderByRequest(
        ShapedQueryExpression Source,
        LambdaExpression KeySelector,
        bool Ascending,
        bool IsFirst);

    /// <summary>
    /// Feature: OrderBy/ThenBy translation and commands.
    /// </summary>
    public partial class FirestoreQueryExpression
    {
        #region OrderBy Commands

        /// <summary>
        /// Reemplaza todos los ordenamientos con uno nuevo (para OrderBy/OrderByDescending)
        /// </summary>
        public FirestoreQueryExpression SetOrderBy(FirestoreOrderByClause orderBy)
        {
            _orderByClauses.Clear();
            _orderByClauses.Add(orderBy);
            return this;
        }

        /// <summary>
        /// Agrega un ordenamiento a los existentes (para ThenBy/ThenByDescending)
        /// </summary>
        public FirestoreQueryExpression AddOrderBy(FirestoreOrderByClause orderBy)
        {
            _orderByClauses.Add(orderBy);
            return this;
        }

        #endregion

        #region OrderBy Translation

        /// <summary>
        /// Traduce OrderBy/ThenBy desde el Visitor.
        /// </summary>
        public static ShapedQueryExpression? TranslateOrderBy(TranslateOrderByRequest request)
        {
            var translator = new FirestoreOrderByTranslator();
            var clause = translator.Translate(request.KeySelector, request.Ascending);
            if (clause == null)
            {
                return null;
            }

            var ast = (FirestoreQueryExpression)request.Source.QueryExpression;

            if (request.IsFirst)
                ast.SetOrderBy(clause);
            else
                ast.AddOrderBy(clause);

            return request.Source.UpdateQueryExpression(ast);
        }

        #endregion
    }
}
