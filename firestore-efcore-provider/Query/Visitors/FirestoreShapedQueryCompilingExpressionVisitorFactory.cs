using Firestore.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;

namespace Firestore.EntityFrameworkCore.Query.Visitors
{
    public class FirestoreShapedQueryCompilingExpressionVisitorFactory
        : IShapedQueryCompilingExpressionVisitorFactory
    {
        private readonly ShapedQueryCompilingExpressionVisitorDependencies _dependencies;
        private readonly IFirestoreClientWrapper _clientWrapper;
        private readonly ILoggerFactory _loggerFactory;

        public FirestoreShapedQueryCompilingExpressionVisitorFactory(
            ShapedQueryCompilingExpressionVisitorDependencies dependencies,
            IFirestoreClientWrapper clientWrapper,
            ILoggerFactory loggerFactory)
        {
            _dependencies = dependencies;
            _clientWrapper = clientWrapper;
            _loggerFactory = loggerFactory;
        }

        public ShapedQueryCompilingExpressionVisitor Create(QueryCompilationContext queryCompilationContext)
        {
            var executor = FirestoreQueryExecutor.Create(
                _clientWrapper,
                _loggerFactory.CreateLogger<FirestoreQueryExecutor>());

            return new FirestoreShapedQueryCompilingExpressionVisitor(
                _dependencies,
                queryCompilationContext,
                executor);
        }
    }
}
