using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using Firestore.EntityFrameworkCore.Metadata.Conventions;

namespace Firestore.EntityFrameworkCore.Metadata
{
    public class FirestoreModelValidator(ModelValidatorDependencies dependencies) : ModelValidator(dependencies)
    {

        public override void Validate(IModel model, IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
        {
            base.Validate(model, logger);
            ValidateFirestoreConstraints(model, logger);
            ValidateNoUnsupportedRelationships(model);
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

        /// <summary>
        /// Valida que no existan relaciones N:M, 1:N o 1:1 no configuradas para Firestore.
        /// Las únicas relaciones permitidas son SubCollections y DocumentReferences.
        /// </summary>
        internal static void ValidateNoUnsupportedRelationships(IModel model)
        {
            foreach (var entityType in model.GetEntityTypes())
            {
                // Validar relaciones N:M (SkipNavigations)
                ValidateNoManyToManyRelationships(entityType);

                // Validar relaciones 1:N y 1:1 no configuradas
                ValidateNavigationsAreConfigured(entityType);
            }
        }

        /// <summary>
        /// Bloquea relaciones Many-to-Many (HasMany().WithMany())
        /// </summary>
        private static void ValidateNoManyToManyRelationships(IReadOnlyEntityType entityType)
        {
            var skipNavigations = entityType.GetSkipNavigations().ToList();

            foreach (var skipNav in skipNavigations)
            {
                throw new NotSupportedException(
                    $"Many-to-Many relationship detected: '{entityType.DisplayName()}.{skipNav.Name}' -> '{skipNav.TargetEntityType.DisplayName()}'. " +
                    $"Many-to-Many relationships (HasMany().WithMany()) are not supported in Firestore. " +
                    $"Consider using SubCollections or denormalization instead.");
            }
        }

        /// <summary>
        /// Valida que todas las navegaciones estén configuradas como SubCollection o DocumentReference
        /// </summary>
        private static void ValidateNavigationsAreConfigured(IReadOnlyEntityType entityType)
        {
            var navigations = entityType.GetNavigations().ToList();

            foreach (var navigation in navigations)
            {
                // Si está configurada para Firestore, está OK
                if (navigation.IsFirestoreConfigured())
                {
                    continue;
                }

                // Determinar el tipo de relación para dar un mensaje de error apropiado
                var foreignKey = navigation.ForeignKey;
                var isCollection = navigation.IsCollection;
                var isPrincipal = navigation.IsOnDependent == false;

                if (isCollection)
                {
                    // Es una colección (1:N desde el lado principal)
                    throw new NotSupportedException(
                        $"One-to-Many relationship detected: '{entityType.DisplayName()}.{navigation.Name}' -> '{navigation.TargetEntityType.DisplayName()}'. " +
                        $"One-to-Many relationships (HasMany().WithOne()) are not supported in Firestore. " +
                        $"Use SubCollections instead: entity.SubCollection(e => e.{navigation.Name})");
                }
                else
                {
                    // Es una navegación singular (1:1 o FK inversa)
                    if (isPrincipal)
                    {
                        // Es el lado principal de 1:1
                        throw new NotSupportedException(
                            $"One-to-One relationship detected: '{entityType.DisplayName()}.{navigation.Name}' -> '{navigation.TargetEntityType.DisplayName()}'. " +
                            $"One-to-One relationships (HasOne().WithOne()) are not supported in Firestore. " +
                            $"Use DocumentReferences instead: entity.Reference(e => e.{navigation.Name})");
                    }
                    else
                    {
                        // Es el lado dependiente (tiene la FK)
                        throw new NotSupportedException(
                            $"Foreign Key relationship detected: '{entityType.DisplayName()}.{navigation.Name}' -> '{navigation.TargetEntityType.DisplayName()}'. " +
                            $"Traditional FK relationships are not supported in Firestore. " +
                            $"Use DocumentReferences instead: entity.Reference(e => e.{navigation.Name})");
                    }
                }
            }
        }
    }
}
