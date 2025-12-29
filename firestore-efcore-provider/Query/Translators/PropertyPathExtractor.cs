using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Firestore.EntityFrameworkCore.Query.Translators
{
    /// <summary>
    /// Helper to extract property paths from expressions.
    /// Used by OrderByTranslator, AggregationTranslator, etc.
    /// </summary>
    internal static class PropertyPathExtractor
    {
        /// <summary>
        /// Extracts property path from a lambda expression.
        /// Handles simple properties (e.Name) and nested properties (e.Address.City).
        /// Unwraps Convert expressions for value types.
        /// </summary>
        /// <param name="lambda">Lambda expression (e.g., e => e.Name)</param>
        /// <returns>Property path (e.g., "Name" or "Address.City") or null if not extractable</returns>
        public static string? ExtractFromLambda(LambdaExpression? lambda)
        {
            if (lambda == null)
                return null;

            var body = lambda.Body;

            // Unwrap Convert expressions (common for value types)
            if (body is UnaryExpression unary &&
                (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
            {
                body = unary.Operand;
            }

            // Handle MemberExpression (e.g., e.Name, e.Address.City)
            if (body is MemberExpression memberExpr && memberExpr.Member is PropertyInfo)
            {
                return BuildPropertyPath(memberExpr);
            }

            return null;
        }

        /// <summary>
        /// Extracts property path from a MemberExpression directly.
        /// </summary>
        /// <param name="memberExpr">Member expression (e.g., e.Address.City)</param>
        /// <returns>Property path (e.g., "Address.City") or null if null input</returns>
        public static string? ExtractFromMemberExpression(MemberExpression? memberExpr)
        {
            if (memberExpr == null)
                return null;

            return BuildPropertyPath(memberExpr);
        }

        /// <summary>
        /// Builds the property path for nested properties.
        /// For e.Address.City returns "Address.City".
        /// For e.Name returns "Name".
        /// </summary>
        private static string BuildPropertyPath(MemberExpression memberExpr)
        {
            var parts = new List<string>();
            Expression? current = memberExpr;

            while (current is MemberExpression member)
            {
                parts.Add(member.Member.Name);
                current = member.Expression;
            }

            // Reverse to get correct order (parent to child)
            parts.Reverse();
            return string.Join(".", parts);
        }
    }
}
