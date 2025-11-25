using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using System;

namespace Firestore.EntityFrameworkCore.Metadata
{
    public class FirestoreModelValidator(ModelValidatorDependencies dependencies) : ModelValidator(dependencies)
    {

        public override void Validate(IModel model, IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
        {
            base.Validate(model, logger);
            ValidateFirestoreConstraints(model, logger);
        }

        private static void ValidateFirestoreConstraints(
            IModel model, 
            IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
        {
            foreach (var entityType in model.GetEntityTypes())
            {
                var primaryKey = entityType.FindPrimaryKey();
                if (primaryKey == null)
                {
                    throw new InvalidOperationException(
                        $"La entidad '{entityType.DisplayName()}' no tiene clave primaria.");
                }

                if (primaryKey.Properties.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"La entidad '{entityType.DisplayName()}' tiene clave compuesta. Firestore no las soporta.");
                }

                var keyType = primaryKey.Properties[0].ClrType;
                if (keyType != typeof(string) && keyType != typeof(Guid) && 
                    keyType != typeof(int) && keyType != typeof(long))
                {
                    logger.Logger.LogWarning(
                        $"La entidad '{entityType.DisplayName()}' usa '{keyType.Name}' como clave. " +
                        "Tipos recomendados: string, Guid, int, long.");
                }
            }
        }
    }
}
