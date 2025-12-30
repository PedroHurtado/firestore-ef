using Firestore.EntityFrameworkCore.Query.Translators;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Ast
{
    /// <summary>
    /// Parámetros para la traducción de Take/TakeLast.
    /// </summary>
    public record TranslateLimitRequest(
        ShapedQueryExpression Source,
        Expression Count,
        bool IsLimitToLast);

    /// <summary>
    /// Feature: Limit/LimitToLast translation and commands.
    /// </summary>
    public partial class FirestoreQueryExpression
    {
        #region Limit Commands

        /// <summary>
        /// Establece el límite de documentos a retornar
        /// </summary>
        public FirestoreQueryExpression WithLimit(int limit)
        {
            Pagination.WithLimit(limit);
            return this;
        }

        /// <summary>
        /// Establece la expresión del límite (para parámetros de EF Core).
        /// </summary>
        public FirestoreQueryExpression WithLimitExpression(Expression limitExpression)
        {
            Pagination.WithLimitExpression(limitExpression);
            return this;
        }

        /// <summary>
        /// Establece el límite de documentos desde el final (TakeLast).
        /// </summary>
        public FirestoreQueryExpression WithLimitToLast(int limitToLast)
        {
            Pagination.WithLimitToLast(limitToLast);
            return this;
        }

        /// <summary>
        /// Establece la expresión del límite desde el final (TakeLast parametrizado).
        /// </summary>
        public FirestoreQueryExpression WithLimitToLastExpression(Expression limitToLastExpression)
        {
            Pagination.WithLimitToLastExpression(limitToLastExpression);
            return this;
        }

        #endregion

        #region Limit Translation

        /// <summary>
        /// Traduce Take/TakeLast desde el Visitor.
        /// </summary>
        public static ShapedQueryExpression TranslateLimit(TranslateLimitRequest request)
        {
            var translator = new FirestoreLimitTranslator();
            var limitValue = translator.Translate(request.Count);

            var ast = (FirestoreQueryExpression)request.Source.QueryExpression;

            if (request.IsLimitToLast)
            {
                if (limitValue != null)
                    ast.WithLimitToLast(limitValue.Value);
                else
                    ast.WithLimitToLastExpression(request.Count);
            }
            else
            {
                if (limitValue != null)
                    ast.WithLimit(limitValue.Value);
                else
                    ast.WithLimitExpression(request.Count);
            }

            return request.Source.UpdateQueryExpression(ast);
        }

        #endregion
    }
}
