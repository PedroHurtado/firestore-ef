using Microsoft.EntityFrameworkCore.Update;

namespace Fudie.Firestore.EntityFrameworkCore.Update
{
    public class FirestoreModificationCommandBatch : SingularModificationCommandBatch
    {
        public FirestoreModificationCommandBatch(
            ModificationCommandBatchFactoryDependencies dependencies)
            : base(dependencies)
        {
        }
    }
}
