using Firestore.EntityFrameworkCore.Query.Translators;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Ast
{
    /// <summary>
    /// Parámetros para la traducción de Where.
    /// El PredicateBody ya debe tener los parámetros de runtime reemplazados por el Visitor.
    /// </summary>
    public record TranslateWhereRequest(
        ShapedQueryExpression Source,
        Expression PredicateBody);

    /// <summary>
    /// Feature: Where translation.
    /// Where applies filter clauses to the query.
    /// </summary>
    public partial class FirestoreQueryExpression
    {
        #region Where Translation

        /// <summary>
        /// Traduce Where.
        /// Preprocesa patrones de array, traduce el predicado y aplica los filtros.
        /// Maneja optimización de Id-only queries y grupos OR.
        /// </summary>
        public static ShapedQueryExpression? TranslateWhere(TranslateWhereRequest request)
        {
            var (source, predicateBody) = request;
            var ast = (FirestoreQueryExpression)source.QueryExpression;

            // Note: PreprocessArrayContainsPatterns is already called by the Visitor's Visit() method
            // before TranslateWhere is invoked, so predicateBody is already preprocessed.

            // Translate using FirestoreWhereTranslator
            var translator = new FirestoreWhereTranslator();
            var filterResult = translator.Translate(predicateBody);

            if (filterResult == null)
            {
                return null;
            }

            // Handle OR groups
            if (filterResult.IsOrGroup)
            {
                if (ast.IsIdOnlyQuery)
                {
                    throw new InvalidOperationException(
                        "Cannot add OR filters to an ID-only query.");
                }

                ast.AddOrFilterGroup(filterResult.OrGroup!);
                return source.UpdateQueryExpression(ast);
            }

            // Handle AND clauses (single or multiple) with possible nested OR groups
            var clauses = filterResult.AndClauses;
            var nestedOrGroups = filterResult.NestedOrGroups;

            if (clauses.Count == 0 && nestedOrGroups.Count == 0)
            {
                return null;
            }

            // Check for ID-only queries (optimization: use GetDocumentAsync instead of query)
            // Only valid when there's a SINGLE Id == clause with NO other filters
            if (clauses.Count == 1 && clauses[0].PropertyName == "Id")
            {
                var whereClause = clauses[0];
                if (whereClause.Operator != FirestoreOperator.EqualTo)
                {
                    throw new InvalidOperationException(
                        "Firestore ID queries only support the '==' operator.");
                }

                // If there are already other filters, treat Id as a normal filter
                // (executor will use FieldPath.DocumentId)
                if (ast.Filters.Count > 0 || ast.OrFilterGroups.Count > 0)
                {
                    ast.AddFilter(whereClause);
                    return source.UpdateQueryExpression(ast);
                }

                if (ast.IsIdOnlyQuery)
                {
                    throw new InvalidOperationException(
                        "Cannot apply multiple ID filters.");
                }

                // Create IdOnlyQuery (optimization for single document fetch)
                ast.WithIdValueExpression(whereClause.ValueExpression);
                return source.UpdateQueryExpression(ast);
            }

            // If we already have an IdOnlyQuery and need to add more filters,
            // convert it to a normal query with FieldPath.DocumentId
            if (ast.IsIdOnlyQuery)
            {
                // Create Id clause from the existing IdValueExpression
                var idClause = new FirestoreWhereClause(
                    "Id", FirestoreOperator.EqualTo, ast.IdValueExpression!, null);

                // Create new query without IdValueExpression (will use FieldPath.DocumentId)
                // Clear IdValueExpression by setting filters with the id clause
                ast.ClearIdValueExpressionWithFilters(new[] { idClause });

                // Add the new clauses
                ast.AddFilters(clauses);
                return source.UpdateQueryExpression(ast);
            }

            // Add all AND clauses
            ast.AddFilters(clauses);

            // Add nested OR groups (for patterns like A && (B || C))
            foreach (var orGroup in nestedOrGroups)
            {
                ast.AddOrFilterGroup(orGroup);
            }

            return source.UpdateQueryExpression(ast);
        }

        #endregion
    }
}
