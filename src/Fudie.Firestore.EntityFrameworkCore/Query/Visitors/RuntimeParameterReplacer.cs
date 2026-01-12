using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Visitors
{
    internal class RuntimeParameterReplacer : ExpressionVisitor
    {
        private readonly QueryCompilationContext _queryCompilationContext;

        public RuntimeParameterReplacer(QueryCompilationContext queryCompilationContext)
        {
            _queryCompilationContext = queryCompilationContext;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node.Name != null && node.Name.StartsWith("__p_"))
            {
                var queryContextParam = QueryCompilationContext.QueryContextParameter;
                var parameterValuesProperty = Expression.Property(queryContextParam, "ParameterValues");
                var indexer = Expression.Property(parameterValuesProperty, "Item", Expression.Constant(node.Name));
                var converted = Expression.Convert(indexer, node.Type);

                return converted;
            }

            return base.VisitParameter(node);
        }
    }
}
