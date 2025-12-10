using Microsoft.EntityFrameworkCore.Storage;

namespace Firestore.EntityFrameworkCore.Storage
{
    public class FirestoreExecutionStrategyFactory(ExecutionStrategyDependencies dependencies) : IExecutionStrategyFactory
    {
        private readonly ExecutionStrategyDependencies _dependencies = dependencies;

        public IExecutionStrategy Create()
        {
            return new FirestoreExecutionStrategy(_dependencies);
        }
    }
}
