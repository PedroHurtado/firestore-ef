using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Firestore.EntityFrameworkCore.Infrastructure;

namespace Firestore.EntityFrameworkCore.Storage
{
    public class FirestoreDatabaseProvider(DatabaseProviderDependencies dependencies) : DatabaseProvider<FirestoreOptionsExtension>(dependencies)
    {
        public const string ProviderName = "Firestore.EntityFrameworkCore";

        public override string Name => ProviderName;

        public override bool IsConfigured(IDbContextOptions options)
        {
            return options?.FindExtension<FirestoreOptionsExtension>() != null;
        }
    }
}
