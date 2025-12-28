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
        private static readonly FirestoreIncludeTranslator IncludeTranslator = new();

        /// <summary>
        /// Translates an Include expression and adds it to the AST.
        /// Called from FirestoreQueryableMethodTranslatingExpressionVisitor.TranslateSelect.
        /// </summary>
        public static ShapedQueryExpression TranslateInclude(TranslateIncludeRequest request)
        {
            var (source, includeExpression) = request;
            var ast = (FirestoreQueryExpression)source.QueryExpression;

            // Translator does ALL the work
            var includes = IncludeTranslator.Translate(includeExpression);
            foreach (var include in includes)
            {
                ast.AddInclude(include);
            }

            return source.UpdateQueryExpression(ast);
        }
    }
}
