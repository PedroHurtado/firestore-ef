using Microsoft.EntityFrameworkCore.Update;

namespace Firestore.EntityFrameworkCore.Update
{
    public class FirestoreModificationCommandBatchFactory : IModificationCommandBatchFactory
    {
        private readonly ModificationCommandBatchFactoryDependencies _dependencies;

        public FirestoreModificationCommandBatchFactory(
            ModificationCommandBatchFactoryDependencies dependencies)
        {
            _dependencies = dependencies;
        }

        public ModificationCommandBatch Create()
        {
            return new FirestoreModificationCommandBatch(_dependencies);
        }
    }
}
