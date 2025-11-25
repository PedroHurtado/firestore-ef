using System.Text;
using Microsoft.EntityFrameworkCore.Update;

namespace Firestore.EntityFrameworkCore.Update
{
    public class FirestoreUpdateSqlGenerator(UpdateSqlGeneratorDependencies dependencies) : UpdateSqlGenerator(dependencies)
    {

        public override ResultSetMapping AppendInsertOperation(
            StringBuilder commandStringBuilder,
            IReadOnlyModificationCommand command,
            int commandPosition)
        {
            return ResultSetMapping.NoResults;
        }

        public override ResultSetMapping AppendUpdateOperation(
            StringBuilder commandStringBuilder,
            IReadOnlyModificationCommand command,
            int commandPosition)
        {
            return ResultSetMapping.NoResults;
        }

        public override ResultSetMapping AppendDeleteOperation(
            StringBuilder commandStringBuilder,
            IReadOnlyModificationCommand command,
            int commandPosition)
        {
            return ResultSetMapping.NoResults;
        }

       
    }
}
