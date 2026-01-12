using Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;
using Microsoft.EntityFrameworkCore.Query;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Visitors
{
    public class FirestoreShapedQueryCompilingExpressionVisitorFactory
        : IShapedQueryCompilingExpressionVisitorFactory
    {
        private readonly ShapedQueryCompilingExpressionVisitorDependencies _dependencies;
        private readonly IQueryPipelineMediator _mediator;

        public FirestoreShapedQueryCompilingExpressionVisitorFactory(
            ShapedQueryCompilingExpressionVisitorDependencies dependencies,
            IQueryPipelineMediator mediator)
        {
            _dependencies = dependencies;
            _mediator = mediator;
        }

        public ShapedQueryCompilingExpressionVisitor Create(QueryCompilationContext queryCompilationContext)
        {
            return new FirestoreShapedQueryCompilingExpressionVisitor(
                _dependencies,
                queryCompilationContext,
                _mediator);
        }
    }
}
