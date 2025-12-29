using Firestore.EntityFrameworkCore.Query.Translators;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Ast
{
    /// <summary>
    /// Parameters for Average translation.
    /// </summary>
    public record TranslateAverageRequest(
        ShapedQueryExpression Source,
        LambdaExpression? Selector,
        Type ResultType);

    /// <summary>
    /// Feature: Average aggregation translation.
    /// </summary>
    public partial class FirestoreQueryExpression
    {
        #region Average Translation

        /// <summary>
        /// Translates Average from the Visitor.
        /// </summary>
        public static ShapedQueryExpression? TranslateAverage(TranslateAverageRequest request)
        {
            var translator = new FirestoreAverageTranslator();
            var propertyName = translator.Translate(request.Selector);

            if (propertyName == null)
                return null;

            var ast = (FirestoreQueryExpression)request.Source.QueryExpression;
            var newAst = ast.WithAverage(propertyName, request.ResultType);

            return request.Source.UpdateQueryExpression(newAst);
        }

        #endregion
    }
}
