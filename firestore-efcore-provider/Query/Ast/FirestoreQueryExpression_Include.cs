using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Query.Translators;
using Microsoft.EntityFrameworkCore.Query;

namespace Firestore.EntityFrameworkCore.Query.Ast
{
    /// <summary>
    /// Request record for TranslateInclude.
    /// </summary>
    public record TranslateIncludeRequest(
        ShapedQueryExpression Source,
        IncludeExpression IncludeExpression,
        IFirestoreCollectionManager CollectionManager);

    /// <summary>
    /// Partial class for Include feature (MicroDomain pattern).
    /// Coordinator only - all logic is in FirestoreIncludeTranslator.
    /// </summary>
    public partial class FirestoreQueryExpression
    {
        /// <summary>
        /// Translates an Include expression and adds it to the AST.
        /// Called from FirestoreQueryableMethodTranslatingExpressionVisitor.TranslateSelect.
        /// </summary>
        public static ShapedQueryExpression TranslateInclude(TranslateIncludeRequest request)
        {
            var (source, includeExpression, collectionManager) = request;
            var ast = (FirestoreQueryExpression)source.QueryExpression;

            // Create visitor and translator
            var visitor = new IncludeExtractionVisitor(collectionManager);
            var translator = new FirestoreIncludeTranslator(visitor);

            var includes = translator.Translate(includeExpression);
            foreach (var include in includes)
            {
                ast.AddInclude(include);
            }

            return source.UpdateQueryExpression(ast);
        }

        /// <summary>
        /// Adds ComplexType Includes from the compilation context to the AST.
        /// Called from FirestoreShapedQueryCompilingExpressionVisitor.VisitShapedQuery.
        /// </summary>
        public static void AddComplexTypeIncludes(
            FirestoreQueryExpression ast,
            QueryCompilationContext queryCompilationContext)
        {
            var firestoreContext = (FirestoreQueryCompilationContext)queryCompilationContext;
            foreach (var complexTypeInclude in firestoreContext.ComplexTypeIncludes)
            {
                ast.AddInclude(complexTypeInclude);
            }
        }

        /// <summary>
        /// Adds ArrayOf Reference Includes from the compilation context to the AST.
        /// Called from FirestoreShapedQueryCompilingExpressionVisitor.VisitShapedQuery.
        /// </summary>
        public static void AddArrayOfIncludes(
            FirestoreQueryExpression ast,
            QueryCompilationContext queryCompilationContext)
        {
            var firestoreContext = (FirestoreQueryCompilationContext)queryCompilationContext;
            foreach (var arrayOfInclude in firestoreContext.ArrayOfIncludes)
            {
                ast.AddInclude(arrayOfInclude);
            }
        }
    }
}
