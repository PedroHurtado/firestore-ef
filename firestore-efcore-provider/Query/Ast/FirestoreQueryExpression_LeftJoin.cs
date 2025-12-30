using Firestore.EntityFrameworkCore.Query.Translators;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Ast
{
    /// <summary>
    /// Parameters for LeftJoin translation.
    /// </summary>
    public record TranslateLeftJoinRequest(
        ShapedQueryExpression Outer,
        ShapedQueryExpression Inner,
        LambdaExpression OuterKeySelector,
        LambdaExpression InnerKeySelector,
        LambdaExpression ResultSelector);

    /// <summary>
    /// Feature: LeftJoin translation.
    /// In Firestore we don't support real JOINs. EF Core uses LeftJoin internally
    /// when processing Include() for navigations. This slice extracts the navigation
    /// and adds it as a pending include.
    /// </summary>
    public partial class FirestoreQueryExpression
    {
        #region LeftJoin Translation

        /// <summary>
        /// Translates a LeftJoin operation from the Visitor.
        /// Converts the LeftJoin to a pending Include since Firestore doesn't support real JOINs.
        /// </summary>
        public static ShapedQueryExpression TranslateLeftJoin(TranslateLeftJoinRequest request)
        {
            var outerQueryExpression = (FirestoreQueryExpression)request.Outer.QueryExpression;
            var innerQueryExpression = (FirestoreQueryExpression)request.Inner.QueryExpression;

            var translator = new FirestoreLeftJoinTranslator();
            var includeInfo = translator.Translate(
                request.OuterKeySelector,
                outerQueryExpression.EntityType,
                innerQueryExpression.EntityType);

            if (includeInfo != null)
            {
                var newQueryExpression = outerQueryExpression.AddInclude(
                    includeInfo.NavigationName,
                    includeInfo.IsCollection);
                return request.Outer.UpdateQueryExpression(newQueryExpression);
            }

            // No navigation found - throw descriptive error
            throw new NotSupportedException(
                $"Firestore does not support real joins. " +
                $"Could not identify navigation for LeftJoin between " +
                $"'{outerQueryExpression.EntityType.ClrType.Name}' and '{innerQueryExpression.EntityType.ClrType.Name}'. " +
                $"Use .Reference() to configure DocumentReference navigations.");
        }

        #endregion
    }
}
