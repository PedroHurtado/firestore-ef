using Microsoft.EntityFrameworkCore.Query;

namespace Firestore.EntityFrameworkCore.Query
{
    public class FirestoreQueryCompilationContext(
        QueryCompilationContextDependencies dependencies,
        bool async) : QueryCompilationContext(dependencies, async)
    {
    }
}
