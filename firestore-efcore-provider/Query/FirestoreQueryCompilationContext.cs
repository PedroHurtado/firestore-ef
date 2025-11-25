using Microsoft.EntityFrameworkCore.Query;

namespace Firestore.EntityFrameworkCore.Query
{
    public class FirestoreQueryCompilationContextFactory : IQueryCompilationContextFactory
    {
        private readonly QueryCompilationContextDependencies _dependencies;

        public FirestoreQueryCompilationContextFactory(
            QueryCompilationContextDependencies dependencies)
        {
            _dependencies = dependencies;
        }

        public QueryCompilationContext Create(bool async)
        {
            return new FirestoreQueryCompilationContext(_dependencies, async);
        }
    }

    public class FirestoreQueryCompilationContext(
        QueryCompilationContextDependencies dependencies,
        bool async) : QueryCompilationContext(dependencies, async)
    {
    }
}
