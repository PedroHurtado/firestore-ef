using Microsoft.EntityFrameworkCore.Query;

namespace Firestore.EntityFrameworkCore.Query
{
    public class FirestoreQueryContextFactory(QueryContextDependencies dependencies) : IQueryContextFactory
    {
        private readonly QueryContextDependencies _dependencies = dependencies;

        public QueryContext Create()
        {
            return new FirestoreQueryContext(_dependencies);
        }
    }
}
