using Microsoft.EntityFrameworkCore.Query;

namespace Firestore.EntityFrameworkCore.Query.Visitors
{
    public class FirestoreShapedQueryCompilingExpressionVisitorFactory
        : IShapedQueryCompilingExpressionVisitorFactory
    {
        private readonly ShapedQueryCompilingExpressionVisitorDependencies _dependencies;
        private readonly IFirestoreQueryExecutor _queryExecutor;

        public FirestoreShapedQueryCompilingExpressionVisitorFactory(
            ShapedQueryCompilingExpressionVisitorDependencies dependencies,
            IFirestoreQueryExecutor queryExecutor)
        {
            _dependencies = dependencies;
            _queryExecutor = queryExecutor;
        }

        public ShapedQueryCompilingExpressionVisitor Create(QueryCompilationContext queryCompilationContext)
        {
            return new FirestoreShapedQueryCompilingExpressionVisitor(
                _dependencies,
                queryCompilationContext,
                _queryExecutor);
        }
    }
}
