using Firestore.EntityFrameworkCore.Query.Translators;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Ast
{
    /// <summary>
    /// Parameters for Select translation.
    /// </summary>
    public record TranslateSelectRequest(
        ShapedQueryExpression Source,
        LambdaExpression Selector);

    /// <summary>
    /// Feature: Select/Projection translation and commands.
    /// </summary>
    public partial class FirestoreQueryExpression
    {
        #region Projection Translation

        /// <summary>
        /// Translates a Select clause from the Visitor.
        /// </summary>
        public static ShapedQueryExpression TranslateSelect(TranslateSelectRequest request)
        {
            var translator = new FirestoreProjectionTranslator();
            var projection = translator.Translate(request.Selector);

            var ast = (FirestoreQueryExpression)request.Source.QueryExpression;

            if (projection != null)
            {
                ast.WithProjection(projection);
            }

            return request.Source.UpdateQueryExpression(ast);
        }

        #endregion
    }
}
