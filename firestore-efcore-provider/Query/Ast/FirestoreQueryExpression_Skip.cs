using Firestore.EntityFrameworkCore.Query.Translators;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Ast
{
    /// <summary>
    /// Parámetros para la traducción de Skip.
    /// </summary>
    public record TranslateSkipRequest(
        ShapedQueryExpression Source,
        Expression Count);

    /// <summary>
    /// Feature: Skip translation and commands.
    /// </summary>
    public partial class FirestoreQueryExpression
    {
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

        #endregion

        #region Skip Translation

        /// <summary>
        /// Traduce Skip desde el Visitor.
        /// </summary>
        public static ShapedQueryExpression TranslateSkip(TranslateSkipRequest request)
        {
            var translator = new FirestoreSkipTranslator();
            var skipValue = translator.Translate(request.Count);

            var ast = (FirestoreQueryExpression)request.Source.QueryExpression;

            if (skipValue != null)
                ast.WithSkip(skipValue.Value);
            else
                ast.WithSkipExpression(request.Count);

            return request.Source.UpdateQueryExpression(ast);
        }

        #endregion
    }
}
