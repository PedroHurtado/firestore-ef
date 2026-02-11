using Fudie.Firestore.EntityFrameworkCore.Infrastructure;
using Fudie.Firestore.EntityFrameworkCore.Query.Translators;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Linq.Expressions;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Ast
{
    /// <summary>
    /// Parameters for Join translation.
    /// </summary>
    public record TranslateJoinRequest(
        ShapedQueryExpression Outer,
        ShapedQueryExpression Inner,
        LambdaExpression OuterKeySelector,
        LambdaExpression InnerKeySelector,
        LambdaExpression ResultSelector,
        IFirestoreCollectionManager CollectionManager);

    /// <summary>
    /// Feature: Join translation.
    /// In Firestore we don't support real JOINs. EF Core uses Join (INNER JOIN) internally
    /// when processing Include() for non-nullable navigations. This slice extracts the navigation
    /// and adds it as a pending include, identical to LeftJoin handling.
    /// </summary>
    public partial class FirestoreQueryExpression
    {
        #region Join Translation

        /// <summary>
        /// Translates a Join operation from the Visitor.
        /// Converts the Join to a pending Include since Firestore doesn't support real JOINs.
        /// </summary>
        public static ShapedQueryExpression TranslateJoin(TranslateJoinRequest request)
        {
            var outerQueryExpression = (FirestoreQueryExpression)request.Outer.QueryExpression;
            var innerQueryExpression = (FirestoreQueryExpression)request.Inner.QueryExpression;

            var translator = new FirestoreLeftJoinTranslator(request.CollectionManager);
            var includeInfo = translator.Translate(
                request.OuterKeySelector,
                outerQueryExpression.EntityType,
                innerQueryExpression.EntityType);

            if (includeInfo != null)
            {
                outerQueryExpression.AddInclude(includeInfo);
                return request.Outer.UpdateQueryExpression(outerQueryExpression);
            }

            throw new NotSupportedException(
                $"Firestore does not support real joins. " +
                $"Could not identify navigation for Join between " +
                $"'{outerQueryExpression.EntityType.ClrType.Name}' and '{innerQueryExpression.EntityType.ClrType.Name}'. " +
                $"Use .Reference() to configure DocumentReference navigations.");
        }

        #endregion
    }
}