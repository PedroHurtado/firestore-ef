using Microsoft.EntityFrameworkCore.Query;

namespace Firestore.EntityFrameworkCore.Query
{
    public class FirestoreQueryContext(QueryContextDependencies dependencies) : QueryContext(dependencies)
    {
    }
}
