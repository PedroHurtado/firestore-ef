using Fudie.Firestore.EntityFrameworkCore.Query.Translators;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Linq.Expressions;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Ast
{
    /// <summary>
    /// Parameters for Min translation.
    /// </summary>
    public record TranslateMinRequest(
        ShapedQueryExpression Source,
        LambdaExpression? Selector,
        Type ResultType);

    /// <summary>
    /// Feature: Min aggregation translation.
    /// </summary>
    public partial class FirestoreQueryExpression
    {
        #region Min Translation

        /// <summary>
        /// Translates Min from the Visitor.
        /// </summary>
        public static ShapedQueryExpression? TranslateMin(TranslateMinRequest request)
        {
            var translator = new FirestoreMinTranslator();
            var propertyName = translator.Translate(request.Selector);

            if (propertyName == null)
                return null;

            var ast = (FirestoreQueryExpression)request.Source.QueryExpression;
            var newAst = ast.WithMin(propertyName, request.ResultType);

            return request.Source.UpdateQueryExpression(newAst);
        }

        #endregion
    }
}
