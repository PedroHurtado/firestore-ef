using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace Firestore.EntityFrameworkCore.Metadata.Conventions
{
    public class FirestoreConventionSetBuilder : ProviderConventionSetBuilder
    {
        public FirestoreConventionSetBuilder(ProviderConventionSetBuilderDependencies dependencies)
            : base(dependencies)
        {
        }

        public override ConventionSet CreateConventionSet()
        {
            var conventionSet = base.CreateConventionSet();
            return conventionSet;
        }
    }
}
