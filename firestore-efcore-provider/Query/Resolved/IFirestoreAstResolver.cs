using Firestore.EntityFrameworkCore.Query.Ast;

namespace Firestore.EntityFrameworkCore.Query.Resolved
{
    /// <summary>
    /// Resolves a FirestoreQueryExpression (AST) into a ResolvedFirestoreQuery.
    /// Evaluates all expressions and resolves navigations using IModel.
    /// Registered as Scoped because it depends on IFirestoreQueryContext.
    /// </summary>
    public interface IFirestoreAstResolver
    {
        /// <summary>
        /// Resolves the AST into a fully resolved query ready for execution.
        /// </summary>
        /// <param name="ast">The AST to resolve</param>
        /// <returns>A resolved query with all expressions evaluated</returns>
        ResolvedFirestoreQuery Resolve(FirestoreQueryExpression ast);
    }
}
