using Fudie.Firestore.EntityFrameworkCore.Query.Ast;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Resolved
{
    /// <summary>
    /// Resolves a FirestoreQueryExpression (AST) into a ResolvedFirestoreQuery.
    /// Evaluates all expressions and resolves navigations using IModel.
    /// Registered as Singleton because context is passed per-request via Resolve method.
    /// </summary>
    public interface IFirestoreAstResolver
    {
        /// <summary>
        /// Resolves the AST into a fully resolved query ready for execution.
        /// </summary>
        /// <param name="ast">The AST to resolve</param>
        /// <param name="queryContext">The query context containing parameter values</param>
        /// <returns>A resolved query with all expressions evaluated</returns>
        ResolvedFirestoreQuery Resolve(FirestoreQueryExpression ast, IFirestoreQueryContext queryContext);
    }
}
