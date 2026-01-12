using Microsoft.EntityFrameworkCore.Storage;
using System.Threading;
using System.Threading.Tasks;
using Fudie.Firestore.EntityFrameworkCore.Infrastructure;

namespace Fudie.Firestore.EntityFrameworkCore.Storage
{
    public class FirestoreDatabaseCreator(IFirestoreClientWrapper firestoreClient) : IDatabaseCreator
    {
        private readonly IFirestoreClientWrapper _firestoreClient = firestoreClient;

        public bool CanConnect() => CanConnectAsync().GetAwaiter().GetResult();

        public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var testCollection = _firestoreClient.GetCollection("_connection_test");
                await testCollection.Limit(1).GetSnapshotAsync(cancellationToken);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool EnsureCreated() => true;
        public Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = default) 
            => Task.FromResult(true);

        public bool EnsureDeleted() => false;
        public Task<bool> EnsureDeletedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }
}
