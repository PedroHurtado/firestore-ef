using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Custom QueryTranslationPreprocessor that intercepts Include expressions
    /// targeting properties inside ComplexTypes before EF Core's NavigationExpandingExpressionVisitor
    /// rejects them.
    /// </summary>
    public class FirestoreQueryTranslationPreprocessor : QueryTranslationPreprocessor
    {
        private readonly QueryCompilationContext _queryCompilationContext;

        public FirestoreQueryTranslationPreprocessor(
            QueryTranslationPreprocessorDependencies dependencies,
            QueryCompilationContext queryCompilationContext)
            : base(dependencies, queryCompilationContext)
        {
            _queryCompilationContext = queryCompilationContext;
        }

        public override Expression Process(Expression query)
        {
            // First, extract and remove ComplexType Includes before EF Core processes them
            var complexTypeIncludeVisitor = new ComplexTypeIncludeExtractorVisitor(_queryCompilationContext);
            query = complexTypeIncludeVisitor.Visit(query);

            // Then let EF Core process the remaining (valid) Includes
            return base.Process(query);
        }
    }
}