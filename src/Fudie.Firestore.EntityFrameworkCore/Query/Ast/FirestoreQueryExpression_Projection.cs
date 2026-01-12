using Fudie.Firestore.EntityFrameworkCore.Infrastructure;
using Fudie.Firestore.EntityFrameworkCore.Query.Translators;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Ast
{
    /// <summary>
    /// Parameters for Select translation.
    /// </summary>
    public record TranslateSelectRequest(
        ShapedQueryExpression Source,
        LambdaExpression Selector,
        IFirestoreCollectionManager CollectionManager);

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
            var ast = (FirestoreQueryExpression)request.Source.QueryExpression;
            var translator = new FirestoreProjectionTranslator(request.CollectionManager, ast.EntityType, ast.PendingIncludes);
            var projection = translator.Translate(request.Selector);

            if (projection != null)
            {
                ast.WithProjection(projection);
            }

            return request.Source.UpdateQueryExpression(ast);
        }

        #endregion
    }
}
