using Microsoft.EntityFrameworkCore.Query;

namespace Firestore.EntityFrameworkCore.Query.Visitors
{
    public class FirestoreQueryableMethodTranslatingExpressionVisitorFactory
        : IQueryableMethodTranslatingExpressionVisitorFactory
    {
        private readonly QueryableMethodTranslatingExpressionVisitorDependencies _dependencies;

        public FirestoreQueryableMethodTranslatingExpressionVisitorFactory(
            QueryableMethodTranslatingExpressionVisitorDependencies dependencies)
        {
            _dependencies = dependencies;
        }

        public QueryableMethodTranslatingExpressionVisitor Create(QueryCompilationContext queryCompilationContext)
        {
            return new FirestoreQueryableMethodTranslatingExpressionVisitor(_dependencies, queryCompilationContext);
        }
    }
}
