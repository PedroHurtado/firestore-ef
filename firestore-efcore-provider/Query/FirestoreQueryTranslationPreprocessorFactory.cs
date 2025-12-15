using Microsoft.EntityFrameworkCore.Query;

namespace Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Factory that creates FirestoreQueryTranslationPreprocessor instances.
    /// </summary>
    public class FirestoreQueryTranslationPreprocessorFactory : IQueryTranslationPreprocessorFactory
    {
        private readonly QueryTranslationPreprocessorDependencies _dependencies;

        public FirestoreQueryTranslationPreprocessorFactory(
            QueryTranslationPreprocessorDependencies dependencies)
        {
            _dependencies = dependencies;
        }

        public QueryTranslationPreprocessor Create(QueryCompilationContext queryCompilationContext)
        {
            return new FirestoreQueryTranslationPreprocessor(_dependencies, queryCompilationContext);
        }
    }
}