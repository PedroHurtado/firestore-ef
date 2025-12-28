using Firestore.EntityFrameworkCore.Query.Translators;
using Microsoft.EntityFrameworkCore.Query;

namespace Firestore.EntityFrameworkCore.Query.Ast
{
    /// <summary>
    /// Request record for TranslateInclude.
    /// </summary>
    public record TranslateIncludeRequest(
        ShapedQueryExpression Source,
        IncludeExpression IncludeExpression);

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
            var (source, includeExpression) = request;
            var ast = (FirestoreQueryExpression)source.QueryExpression;

            // Create visitor and translator
            var visitor = new IncludeExtractionVisitor();
            var translator = new FirestoreIncludeTranslator(visitor);

            var includes = translator.Translate(includeExpression);
            foreach (var include in includes)
            {
                ast.AddInclude(include);
            }

            return source.UpdateQueryExpression(ast);
        }
    }
}
