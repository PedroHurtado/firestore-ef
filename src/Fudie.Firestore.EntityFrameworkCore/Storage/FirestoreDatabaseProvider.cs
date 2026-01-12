using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Fudie.Firestore.EntityFrameworkCore.Infrastructure;

namespace Fudie.Firestore.EntityFrameworkCore.Storage
{
    public class FirestoreDatabaseProvider(DatabaseProviderDependencies dependencies) : DatabaseProvider<FirestoreOptionsExtension>(dependencies)
    {
        public const string ProviderName = "Fudie.Firestore.EntityFrameworkCore";

        public override string Name => ProviderName;

        public override bool IsConfigured(IDbContextOptions options)
        {
            return options?.FindExtension<FirestoreOptionsExtension>() != null;
        }
    }
}
