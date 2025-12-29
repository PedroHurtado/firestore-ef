using Firestore.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;

namespace Firestore.EntityFrameworkCore.Query.Visitors
{
    public class FirestoreShapedQueryCompilingExpressionVisitorFactory
        : IShapedQueryCompilingExpressionVisitorFactory
    {
        private readonly ShapedQueryCompilingExpressionVisitorDependencies _dependencies;
        private readonly IFirestoreQueryExecutor _queryExecutor;
        private readonly IFirestoreCollectionManager _collectionManager;

        public FirestoreShapedQueryCompilingExpressionVisitorFactory(
            ShapedQueryCompilingExpressionVisitorDependencies dependencies,
            IFirestoreQueryExecutor queryExecutor,
            IFirestoreCollectionManager collectionManager)
        {
            _dependencies = dependencies;
            _queryExecutor = queryExecutor;
            _collectionManager = collectionManager;
        }

        public ShapedQueryCompilingExpressionVisitor Create(QueryCompilationContext queryCompilationContext)
        {
            return new FirestoreShapedQueryCompilingExpressionVisitor(
                _dependencies,
                queryCompilationContext,
                _queryExecutor,
                _collectionManager);
        }
    }
}
