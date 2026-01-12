using Fudie.Firestore.EntityFrameworkCore.Query.Translators;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Linq.Expressions;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Ast
{
    /// <summary>
    /// Parameters for Max translation.
    /// </summary>
    public record TranslateMaxRequest(
        ShapedQueryExpression Source,
        LambdaExpression? Selector,
        Type ResultType);

    /// <summary>
    /// Feature: Max aggregation translation.
    /// </summary>
    public partial class FirestoreQueryExpression
    {
        #region Max Translation

        /// <summary>
        /// Translates Max from the Visitor.
        /// </summary>
        public static ShapedQueryExpression? TranslateMax(TranslateMaxRequest request)
        {
            var translator = new FirestoreMaxTranslator();
            var propertyName = translator.Translate(request.Selector);

            if (propertyName == null)
                return null;

            var ast = (FirestoreQueryExpression)request.Source.QueryExpression;
            var newAst = ast.WithMax(propertyName, request.ResultType);

            return request.Source.UpdateQueryExpression(newAst);
        }

        #endregion
    }
}
