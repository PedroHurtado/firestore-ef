using Microsoft.EntityFrameworkCore.Query;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Custom QueryCompilationContext for Firestore.
    ///
    /// NOTE: FilteredIncludes storage was removed. Filtered Includes are now translated
    /// directly in FirestoreQueryableMethodTranslatingExpressionVisitor.TranslateInclude
    /// using FirestoreIncludeTranslator, which produces IncludeInfo with:
    /// - FirestoreWhereClause (translated by FirestoreWhereTranslator)
    /// - FirestoreOrderByClause (translated by FirestoreOrderByTranslator)
    /// - Take/Skip values or expressions (translated by FirestoreLimitTranslator)
    ///
    /// This ensures consistency with the main query translation pipeline.
    /// </summary>
    public class FirestoreQueryCompilationContext : QueryCompilationContext
    {
        private readonly List<LambdaExpression> _complexTypeIncludes = new();

        public FirestoreQueryCompilationContext(
            QueryCompilationContextDependencies dependencies,
            bool async) : base(dependencies, async)
        {
        }

        /// <summary>
        /// Include expressions that target properties inside ComplexTypes.
        /// These are extracted before EF Core processes them and used during deserialization.
        /// </summary>
        public IReadOnlyList<LambdaExpression> ComplexTypeIncludes => _complexTypeIncludes;

        /// <summary>
        /// Adds a ComplexType include expression for later processing.
        /// Called by ComplexTypeIncludeExtractorVisitor.
        /// </summary>
        internal void AddComplexTypeInclude(LambdaExpression includeExpression)
        {
            _complexTypeIncludes.Add(includeExpression);
        }
    }
}
