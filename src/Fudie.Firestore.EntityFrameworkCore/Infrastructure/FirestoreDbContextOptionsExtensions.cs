using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;

namespace Fudie.Firestore.EntityFrameworkCore.Infrastructure
{
    public static class FirestoreDbContextOptionsExtensions
    {
        public static DbContextOptionsBuilder UseFirestore(
            this DbContextOptionsBuilder optionsBuilder,
            string projectId,
            Action<FirestoreDbContextOptionsBuilder>? firestoreOptionsAction = null)  // Error 2: Hacer nullable
        {
            if (optionsBuilder == null)
                throw new ArgumentNullException(nameof(optionsBuilder));

            if (string.IsNullOrWhiteSpace(projectId))
                throw new ArgumentException("El ProjectId no puede estar vacío.", nameof(projectId));

            var extension = GetOrCreateExtension(optionsBuilder);
            extension = extension.WithProjectId(projectId);

            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder)
                .AddOrUpdateExtension(extension);

            ConfigureWarnings(optionsBuilder);

            firestoreOptionsAction?.Invoke(new FirestoreDbContextOptionsBuilder(optionsBuilder));

            return optionsBuilder;
        }

        public static DbContextOptionsBuilder UseFirestore(
            this DbContextOptionsBuilder optionsBuilder,
            string projectId,
            string credentialsPath,
            Action<FirestoreDbContextOptionsBuilder>? firestoreOptionsAction = null)  // Error 3: Hacer nullable
        {
            ArgumentNullException.ThrowIfNull(optionsBuilder);

            if (string.IsNullOrWhiteSpace(projectId))
                throw new ArgumentException("El ProjectId no puede estar vacío.", nameof(projectId));

            var extension = GetOrCreateExtension(optionsBuilder);
            extension = extension
                .WithProjectId(projectId)
                .WithCredentialsPath(credentialsPath);

            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder)
                .AddOrUpdateExtension(extension);

            ConfigureWarnings(optionsBuilder);

            firestoreOptionsAction?.Invoke(new FirestoreDbContextOptionsBuilder(optionsBuilder));

            return optionsBuilder;
        }

        public static DbContextOptionsBuilder<TContext> UseFirestore<TContext>(
            this DbContextOptionsBuilder<TContext> optionsBuilder,
            string projectId,
            Action<FirestoreDbContextOptionsBuilder>? firestoreOptionsAction = null)  // Error 4: Hacer nullable
            where TContext : DbContext
        {
            return (DbContextOptionsBuilder<TContext>)UseFirestore(
                (DbContextOptionsBuilder)optionsBuilder,
                projectId,
                firestoreOptionsAction);
        }

        public static DbContextOptionsBuilder<TContext> UseFirestore<TContext>(
            this DbContextOptionsBuilder<TContext> optionsBuilder,
            string projectId,
            string credentialsPath,
            Action<FirestoreDbContextOptionsBuilder>? firestoreOptionsAction = null)  // Error 5: Hacer nullable
            where TContext : DbContext
        {
            return (DbContextOptionsBuilder<TContext>)UseFirestore(
                (DbContextOptionsBuilder)optionsBuilder,
                projectId,
                credentialsPath,
                firestoreOptionsAction);
        }

        private static FirestoreOptionsExtension GetOrCreateExtension(
            DbContextOptionsBuilder optionsBuilder)
        {
            var existing = optionsBuilder.Options.FindExtension<FirestoreOptionsExtension>();
            
            // Error 1: Clonar de forma diferente - no usar el constructor copy
            if (existing != null)
            {
                // Crear una nueva instancia y copiar las propiedades manualmente
                var newExtension = new FirestoreOptionsExtension();
                if (!string.IsNullOrEmpty(existing.ProjectId))
                    newExtension = newExtension.WithProjectId(existing.ProjectId);
                if (!string.IsNullOrEmpty(existing.DatabaseId))
                    newExtension = newExtension.WithDatabaseId(existing.DatabaseId);
                if (!string.IsNullOrEmpty(existing.CredentialsPath))
                    newExtension = newExtension.WithCredentialsPath(existing.CredentialsPath);
                return newExtension;
            }
            
            return new FirestoreOptionsExtension();
        }

        private static void ConfigureWarnings(DbContextOptionsBuilder optionsBuilder)
        {
            var coreOptionsExtension = optionsBuilder.Options
                .FindExtension<CoreOptionsExtension>()
                ?? new CoreOptionsExtension();

            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder)
                .AddOrUpdateExtension(coreOptionsExtension);
        }

        internal static FirestoreOptionsExtension GetFirestoreOptionsExtension(
            this IDbContextOptions options)
        {
            var extension = options.FindExtension<FirestoreOptionsExtension>();
            
            if (extension == null)
            {
                throw new InvalidOperationException(
                    "No se encontró la configuración de Firestore. " +
                    "Asegúrate de llamar a .UseFirestore() en el DbContextOptionsBuilder.");
            }

            return extension;
        }
    }
}