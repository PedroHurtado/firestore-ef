using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata
{
    /// <summary>
    /// Validador de modelo personalizado para Firestore.
    ///
    /// WHY: EF Core's ModelValidator está diseñado para bases de datos relacionales y tiene
    /// validaciones que no aplican o son demasiado restrictivas para Firestore:
    ///
    /// 1. ValidatePropertyMapping - Bloquea ComplexProperty nullable, pero Firestore
    ///    soporta campos opcionales naturalmente (el campo simplemente no existe).
    ///
    /// 2. ValidateRelationships/ValidateForeignKeys - Validan FKs tradicionales, pero
    ///    Firestore usa DocumentReferences y SubCollections en su lugar.
    ///
    /// 3. ValidateNoShadowKeys - Las shadow properties para FKs no aplican a documentos.
    ///
    /// 4. ValidateFieldMapping - EF Core requiere binding específico para materialización,
    ///    pero Firestore tiene su propio deserializador (FirestoreDocumentDeserializer).
    ///
    /// 5. ValidateData - No usamos HasData() para seeding en Firestore.
    ///
    /// WHAT: Implementamos validaciones selectivas:
    /// - MANTENEMOS: Validaciones de configuración del modelo (herencia, ciclos, keys, ownership)
    /// - SALTAMOS: Validaciones relacionales (FKs, shadow keys, field mapping)
    /// - AGREGAMOS: Validaciones específicas de Firestore (claves, relaciones no soportadas)
    ///
    /// REFERENCE: Similar al enfoque de CosmosModelValidator en EF Core.
    /// Fuente: https://github.com/dotnet/efcore/blob/main/src/EFCore.Cosmos/Infrastructure/Internal/CosmosModelValidator.cs
    /// </summary>
    public class FirestoreModelValidator(ModelValidatorDependencies dependencies) : ModelValidator(dependencies)
    {
        /// <summary>
        /// Override de ValidatePropertyMapping para permitir ComplexProperty nullable.
        ///
        /// WHY: EF Core bloquea ComplexProperty nullable porque en SQL Server los ComplexTypes
        /// se mapean a columnas y no pueden ser opcionales de forma nativa. Sin embargo,
        /// Firestore es una base de datos documental donde los campos opcionales son naturales:
        /// un campo simplemente puede no existir en el documento.
        ///
        /// WHAT: Solo validamos las restricciones de ComplexProperty EXCEPTO IsNullable.
        /// Esto permite que Value Objects DDD como `DepositPolicy?` sean opcionales.
        ///
        /// Issue relacionado: https://github.com/dotnet/efcore/issues/31376
        /// </summary>
        protected override void ValidatePropertyMapping(
            IModel model,
            IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
        {
            if (model is not IConventionModel conventionModel)
            {
                return;
            }

            // Solo validamos las restricciones de ComplexProperty (excepto IsNullable).
            // Las demás validaciones de propiedades no mapeadas las omitimos porque
            // Firestore tiene su propio sistema de serialización/deserialización.
            foreach (var entityType in conventionModel.GetEntityTypes())
            {
                ValidateComplexProperties(entityType);
            }
        }

        /// <summary>
        /// Valida un tipo base (entidad o ComplexType) y sus ComplexProperties.
        /// Solo valida las restricciones de ComplexProperty, omitiendo IsNullable.
        /// </summary>
        private void ValidateComplexProperties(IConventionTypeBase typeBase)
        {
            foreach (var complexProperty in typeBase.GetDeclaredComplexProperties())
            {
                if (complexProperty.IsShadowProperty())
                {
                    throw new InvalidOperationException(
                        CoreStrings.ComplexPropertyShadow(typeBase.DisplayName(), complexProperty.Name));
                }

                if (complexProperty.IsIndexerProperty())
                {
                    throw new InvalidOperationException(
                        CoreStrings.ComplexPropertyIndexer(typeBase.DisplayName(), complexProperty.Name));
                }

                if (complexProperty.IsCollection)
                {
                    throw new InvalidOperationException(
                        CoreStrings.ComplexPropertyCollection(typeBase.DisplayName(), complexProperty.Name));
                }

                // FIRESTORE: Permitimos ComplexProperty nullable.
                // EF Core bloquea esto para SQL Server, pero Firestore soporta campos opcionales.
                // El check original que OMITIMOS intencionalmente:
                // if (complexProperty.IsNullable)
                // {
                //     throw new InvalidOperationException(
                //         CoreStrings.ComplexPropertyOptional(typeBase.DisplayName(), complexProperty.Name));
                // }

                if (!complexProperty.ComplexType.GetMembers().Any())
                {
                    throw new InvalidOperationException(
                        CoreStrings.EmptyComplexType(complexProperty.ComplexType.DisplayName()));
                }

                // Recursión para ComplexTypes anidados
                ValidateComplexProperties(complexProperty.ComplexType);
            }
        }

        /// <summary>
        /// Override de Validate para llamar solo a las validaciones relevantes para Firestore.
        ///
        /// WHY: No podemos llamar a base.Validate() porque:
        /// 1. Internamente llama a ModelValidator.ValidatePropertyMapping (no nuestro override)
        /// 2. Incluye validaciones relacionales que no aplican a bases de datos documentales
        ///
        /// WHAT: Llamamos selectivamente a las validaciones que SÍ son relevantes:
        ///
        /// MANTENEMOS (útiles para cualquier proveedor):
        /// - ValidateIgnoredMembers: Previene errores de configuración
        /// - ValidatePropertyMapping: Nuestra versión que permite ComplexProperty nullable
        /// - ValidateOwnership: Mapea a documentos embebidos
        /// - ValidateNonNullPrimaryKeys: Firestore requiere IDs de documento
        /// - ValidateNoMutableKeys: Los IDs de documento son inmutables
        /// - ValidateNoCycles: Previene profundidad infinita de serialización
        /// - ValidateClrInheritance: La herencia aplica a documentos
        /// - ValidateInheritanceMapping: Discriminadores para polimorfismo
        /// - ValidateChangeTrackingStrategy: El change tracking aún se usa
        /// - ValidateTypeMappings: Útil para value converters
        /// - ValidatePrimitiveCollections: ArrayOf se beneficia de esto
        /// - LogShadowProperties: Diagnóstico sin overhead
        /// - ValidateRelationships: Necesario para que ValidateNoUnsupportedRelationships funcione
        ///
        /// SALTAMOS (no aplican a documentos):
        /// - ValidateNoShadowKeys: Las FKs shadow no aplican
        /// - ValidateForeignKeys: Usamos DocumentReferences
        /// - ValidateFieldMapping: FirestoreDocumentDeserializer lo maneja
        /// - ValidateQueryFilters: Implementación diferente en documentos
        /// - ValidateData: No usamos HasData() seeding
        /// - ValidateTriggers: Firestore Cloud Functions son externas
        ///
        /// MAINTENANCE: Si EF Core añade nuevas validaciones, evaluar si aplican a Firestore.
        /// </summary>
        public override void Validate(IModel model, IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
        {
            // ================================================================
            // VALIDACIONES MANTENIDAS (relevantes para cualquier proveedor)
            // ================================================================

            // Previene errores al ignorar propiedades heredadas
            ValidateIgnoredMembers(model, logger);

            // Nuestra versión que permite ComplexProperty nullable
            ValidatePropertyMapping(model, logger);

            // Ownership mapea a documentos embebidos (ComplexTypes)
            ValidateOwnership(model, logger);

            // Firestore requiere IDs de documento
            ValidateNonNullPrimaryKeys(model, logger);

            // Los IDs de documento son inmutables
            ValidateNoMutableKeys(model, logger);

            // Previene ciclos que causarían serialización infinita
            ValidateNoCycles(model, logger);

            // La herencia CLR aplica a documentos (discriminadores)
            ValidateClrInheritance(model, logger);
            ValidateInheritanceMapping(model, logger);

            // El change tracking de EF Core aún se utiliza
            ValidateChangeTrackingStrategy(model, logger);

            // Los value converters son útiles para tipos personalizados
            ValidateTypeMappings(model, logger);

            // ArrayOf se beneficia de la validación de colecciones primitivas
            ValidatePrimitiveCollections(model, logger);

            // Diagnóstico útil sin overhead
            LogShadowProperties(model, logger);

            // Valida relaciones - necesario para que ValidateNoUnsupportedRelationships funcione correctamente
            // EF Core procesa las navegaciones y las marca internamente
            ValidateRelationships(model, logger);

            // ================================================================
            // VALIDACIONES SALTADAS (no aplican a bases de datos documentales)
            // ================================================================
            // - ValidateNoShadowKeys: Las shadow FKs no aplican a documentos
            // - ValidateForeignKeys: No hay FKs en bases de datos documentales
            // - ValidateFieldMapping: FirestoreDocumentDeserializer maneja esto
            // - ValidateQueryFilters: Implementación diferente en documentos
            // - ValidateData: No usamos HasData() seeding en Firestore
            // - ValidateTriggers: Firestore Cloud Functions son externas al modelo

            // ================================================================
            // VALIDACIONES ESPECÍFICAS DE FIRESTORE
            // ================================================================
            ValidateFirestoreConstraints(model, logger);
            ValidateNoUnsupportedRelationships(model, logger);
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
        ///
        /// NOTA: Many-to-Many siempre es un error porque no tiene equivalente en Firestore.
        /// Las relaciones 1:N y 1:1 no configuradas generan una advertencia en lugar de error
        /// para mantener compatibilidad hacia atrás con modelos existentes.
        /// </summary>
        internal void ValidateNoUnsupportedRelationships(IModel model, IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
        {
            foreach (var entityType in model.GetEntityTypes())
            {
                // Validar relaciones N:M (SkipNavigations) - siempre es error
                ValidateNoManyToManyRelationships(entityType);

                // Validar relaciones 1:N y 1:1 no configuradas - advertencia para compatibilidad
                ValidateNavigationsAreConfigured(entityType, logger);
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
        /// Valida que todas las navegaciones estén configuradas como SubCollection o DocumentReference.
        /// Las navegaciones no configuradas generan advertencias para mantener compatibilidad hacia atrás.
        /// </summary>
        private static void ValidateNavigationsAreConfigured(
            IReadOnlyEntityType entityType,
            IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
        {
            var navigations = entityType.GetNavigations().ToList();

            foreach (var navigation in navigations)
            {
                // Si está configurada para Firestore, está OK
                if (navigation.IsFirestoreConfigured())
                {
                    continue;
                }

                // Determinar el tipo de relación para dar un mensaje de advertencia apropiado
                var isCollection = navigation.IsCollection;
                var isPrincipal = navigation.IsOnDependent == false;

                string message;
                if (isCollection)
                {
                    // Es una colección (1:N desde el lado principal)
                    message = $"One-to-Many relationship detected: '{entityType.DisplayName()}.{navigation.Name}' -> '{navigation.TargetEntityType.DisplayName()}'. " +
                        $"Consider using SubCollections instead: entity.SubCollection(e => e.{navigation.Name})";
                }
                else if (isPrincipal)
                {
                    // Es el lado principal de 1:1
                    message = $"One-to-One relationship detected: '{entityType.DisplayName()}.{navigation.Name}' -> '{navigation.TargetEntityType.DisplayName()}'. " +
                        $"Consider using DocumentReferences instead: entity.Reference(e => e.{navigation.Name})";
                }
                else
                {
                    // Es el lado dependiente (tiene la FK)
                    message = $"Foreign Key relationship detected: '{entityType.DisplayName()}.{navigation.Name}' -> '{navigation.TargetEntityType.DisplayName()}'. " +
                        $"Consider using DocumentReferences instead: entity.Reference(e => e.{navigation.Name})";
                }

                // Emitir advertencia en lugar de error para mantener compatibilidad
                logger.Logger.LogWarning(message);
            }
        }
    }
}
