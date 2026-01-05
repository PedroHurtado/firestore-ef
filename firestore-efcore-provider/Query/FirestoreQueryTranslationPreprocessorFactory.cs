using Firestore.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;

namespace Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Factory that creates FirestoreQueryTranslationPreprocessor instances.
    /// </summary>
    public class FirestoreQueryTranslationPreprocessorFactory : IQueryTranslationPreprocessorFactory
    {
        private readonly QueryTranslationPreprocessorDependencies _dependencies;
        private readonly IFirestoreCollectionManager _collectionManager;

        public FirestoreQueryTranslationPreprocessorFactory(
            QueryTranslationPreprocessorDependencies dependencies,
            IFirestoreCollectionManager collectionManager)
        {
            _dependencies = dependencies;
            _collectionManager = collectionManager;
        }

        public QueryTranslationPreprocessor Create(QueryCompilationContext queryCompilationContext)
        {
            return new FirestoreQueryTranslationPreprocessor(_dependencies, queryCompilationContext, _collectionManager);
        }
    }
}