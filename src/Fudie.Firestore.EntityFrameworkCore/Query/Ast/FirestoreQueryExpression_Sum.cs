using Fudie.Firestore.EntityFrameworkCore.Query.Translators;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Linq.Expressions;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Ast
{
    /// <summary>
    /// Parameters for Sum translation.
    /// </summary>
    public record TranslateSumRequest(
        ShapedQueryExpression Source,
        LambdaExpression? Selector,
        Type ResultType);

    /// <summary>
    /// Feature: Sum aggregation translation.
    /// </summary>
    public partial class FirestoreQueryExpression
    {
        #region Sum Translation

        /// <summary>
        /// Translates Sum from the Visitor.
        /// </summary>
        public static ShapedQueryExpression? TranslateSum(TranslateSumRequest request)
        {
            var translator = new FirestoreSumTranslator();
            var propertyName = translator.Translate(request.Selector);

            if (propertyName == null)
                return null;

            var ast = (FirestoreQueryExpression)request.Source.QueryExpression;
            var newAst = ast.WithSum(propertyName, request.ResultType);

            return request.Source.UpdateQueryExpression(newAst);
        }

        #endregion
    }
}
