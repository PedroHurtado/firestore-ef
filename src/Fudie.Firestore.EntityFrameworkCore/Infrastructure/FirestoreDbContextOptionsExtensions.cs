using Fudie.Firestore.EntityFrameworkCore.ChangeTracking;
using Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Fudie.Firestore.EntityFrameworkCore.Infrastructure
{
    public static class FirestoreDbContextOptionsExtensions
    {
        /// <summary>
        /// Configura Firestore usando el IServiceProvider para resolver IConfiguration.
        /// Lee la configuración de la sección "Firestore" del appsettings.json.
        /// </summary>
        /// <example>
        /// Program.cs:
        /// builder.Services.AddDbContext&lt;MyDbContext&gt;((sp, options) =>
        /// {
        ///     options.UseFirestore(sp);
        /// });
        /// </example>
        public static DbContextOptionsBuilder UseFirestore(
            this DbContextOptionsBuilder optionsBuilder,
            IServiceProvider serviceProvider,
            Action<FirestoreDbContextOptionsBuilder>? firestoreOptionsAction = null)
        {
            ArgumentNullException.ThrowIfNull(optionsBuilder);
            ArgumentNullException.ThrowIfNull(serviceProvider);

            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            return UseFirestore(optionsBuilder, configuration, firestoreOptionsAction);
        }

        /// <summary>
        /// Configura Firestore usando el IServiceProvider para resolver IConfiguration (versión genérica).
        /// </summary>
        public static DbContextOptionsBuilder<TContext> UseFirestore<TContext>(
            this DbContextOptionsBuilder<TContext> optionsBuilder,
            IServiceProvider serviceProvider,
            Action<FirestoreDbContextOptionsBuilder>? firestoreOptionsAction = null)
            where TContext : DbContext
        {
            return (DbContextOptionsBuilder<TContext>)UseFirestore(
                (DbContextOptionsBuilder)optionsBuilder,
                serviceProvider,
                firestoreOptionsAction);
        }

        /// <summary>
        /// Configura Firestore usando IConfiguration.
        /// Lee la configuración de la sección "Firestore" del appsettings.json.
        /// </summary>
        /// <example>
        /// appsettings.json:
        /// {
        ///   "Firestore": {
        ///     "ProjectId": "my-project",
        ///     "EmulatorHost": "127.0.0.1:8080",
        ///     "QueryLogLevel": "Count"
        ///   }
        /// }
        ///
        /// Program.cs:
        /// builder.Services.AddDbContext&lt;MyDbContext&gt;(options =>
        /// {
        ///     options.UseFirestore(builder.Configuration);
        /// });
        /// </example>
        public static DbContextOptionsBuilder UseFirestore(
            this DbContextOptionsBuilder optionsBuilder,
            IConfiguration configuration,
            Action<FirestoreDbContextOptionsBuilder>? firestoreOptionsAction = null)
        {
            ArgumentNullException.ThrowIfNull(optionsBuilder);
            ArgumentNullException.ThrowIfNull(configuration);

            var section = configuration.GetSection("Firestore");
            var projectId = section["ProjectId"];

            if (string.IsNullOrWhiteSpace(projectId))
            {
                throw new InvalidOperationException(
                    "Firestore:ProjectId is required in configuration. " +
                    "Add it to appsettings.json under the 'Firestore' section.");
            }

            var extension = GetOrCreateExtension(optionsBuilder);
            extension = extension.WithProjectId(projectId);

            // Optional: EmulatorHost
            var emulatorHost = section["EmulatorHost"];
            if (!string.IsNullOrEmpty(emulatorHost))
            {
                extension = extension.WithEmulatorHost(emulatorHost);
            }

            // Optional: CredentialsPath
            var credentialsPath = section["CredentialsPath"];
            if (!string.IsNullOrEmpty(credentialsPath))
            {
                extension = extension.WithCredentialsPath(credentialsPath);
            }

            // Optional: DatabaseId
            var databaseId = section["DatabaseId"];
            if (!string.IsNullOrEmpty(databaseId))
            {
                extension = extension.WithDatabaseId(databaseId);
            }

            // Optional: Pipeline section (2nd level)
            var pipelineSection = section.GetSection("Pipeline");
            if (pipelineSection.Exists())
            {
                // QueryLogLevel
                var queryLogLevelStr = pipelineSection["QueryLogLevel"];
                if (!string.IsNullOrEmpty(queryLogLevelStr) &&
                    Enum.TryParse<QueryLogLevel>(queryLogLevelStr, ignoreCase: true, out var queryLogLevel))
                {
                    extension = extension.WithQueryLogLevel(queryLogLevel);
                }

                // EnableAstLogging
                var enableAstLoggingStr = pipelineSection["EnableAstLogging"];
                if (!string.IsNullOrEmpty(enableAstLoggingStr) &&
                    bool.TryParse(enableAstLoggingStr, out var enableAstLogging))
                {
                    extension = extension.WithEnableAstLogging(enableAstLogging);
                }

                // EnableCaching
                var enableCachingStr = pipelineSection["EnableCaching"];
                if (!string.IsNullOrEmpty(enableCachingStr) &&
                    bool.TryParse(enableCachingStr, out var enableCaching))
                {
                    extension = extension.WithEnableCaching(enableCaching);
                }

                // MaxRetries
                var maxRetriesStr = pipelineSection["MaxRetries"];
                if (!string.IsNullOrEmpty(maxRetriesStr) &&
                    int.TryParse(maxRetriesStr, out var maxRetries))
                {
                    extension = extension.WithPipelineMaxRetries(maxRetries);
                }

                // RetryInitialDelayMs
                var retryDelayStr = pipelineSection["RetryInitialDelayMs"];
                if (!string.IsNullOrEmpty(retryDelayStr) &&
                    int.TryParse(retryDelayStr, out var retryDelayMs))
                {
                    extension = extension.WithPipelineRetryInitialDelay(TimeSpan.FromMilliseconds(retryDelayMs));
                }
            }

            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder)
                .AddOrUpdateExtension(extension);

            ConfigureWarnings(optionsBuilder);

            // Add Firestore change tracking interceptor (ArrayOf and MapOf)
            optionsBuilder.AddInterceptors(new FirestoreSaveChangesInterceptor());

            firestoreOptionsAction?.Invoke(new FirestoreDbContextOptionsBuilder(optionsBuilder));

            return optionsBuilder;
        }

        /// <summary>
        /// Configura Firestore usando IConfiguration (versión genérica).
        /// </summary>
        public static DbContextOptionsBuilder<TContext> UseFirestore<TContext>(
            this DbContextOptionsBuilder<TContext> optionsBuilder,
            IConfiguration configuration,
            Action<FirestoreDbContextOptionsBuilder>? firestoreOptionsAction = null)
            where TContext : DbContext
        {
            return (DbContextOptionsBuilder<TContext>)UseFirestore(
                (DbContextOptionsBuilder)optionsBuilder,
                configuration,
                firestoreOptionsAction);
        }

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

            // Add Firestore change tracking interceptor (ArrayOf and MapOf)
            optionsBuilder.AddInterceptors(new FirestoreSaveChangesInterceptor());

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

            // Add Firestore change tracking interceptor (ArrayOf and MapOf)
            optionsBuilder.AddInterceptors(new FirestoreSaveChangesInterceptor());

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