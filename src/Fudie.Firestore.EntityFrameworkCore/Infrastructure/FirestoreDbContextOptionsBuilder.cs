using Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;

namespace Fudie.Firestore.EntityFrameworkCore.Infrastructure
{
    public class FirestoreDbContextOptionsBuilder(DbContextOptionsBuilder optionsBuilder)
    {
        private readonly DbContextOptionsBuilder _optionsBuilder = optionsBuilder ?? throw new ArgumentNullException(nameof(optionsBuilder));

        protected virtual DbContextOptionsBuilder OptionsBuilder => _optionsBuilder;

        public virtual FirestoreDbContextOptionsBuilder UseDatabaseId(string databaseId)
        {
            var extension = GetOrCreateExtension();
            extension = extension.WithDatabaseId(databaseId);
            
            ((IDbContextOptionsBuilderInfrastructure)_optionsBuilder)
                .AddOrUpdateExtension(extension);

            return this;
        }

        public virtual FirestoreDbContextOptionsBuilder UseCredentials(string credentialsPath)
        {
            var extension = GetOrCreateExtension();
            extension = extension.WithCredentialsPath(credentialsPath);
            
            ((IDbContextOptionsBuilderInfrastructure)_optionsBuilder)
                .AddOrUpdateExtension(extension);

            return this;
        }

        public virtual FirestoreDbContextOptionsBuilder EnableDetailedLogging(bool enable = true)
        {
            if (enable)
            {
                _optionsBuilder.EnableSensitiveDataLogging();
                _optionsBuilder.EnableDetailedErrors();
            }

            return this;
        }

        #region Pipeline Configuration

        /// <summary>
        /// Configura el nivel de logging de queries.
        /// Por defecto es None.
        /// </summary>
        public virtual FirestoreDbContextOptionsBuilder QueryLogLevel(QueryLogLevel level)
        {
            var extension = GetOrCreateExtension();
            extension = extension.WithQueryLogLevel(level);

            ((IDbContextOptionsBuilderInfrastructure)_optionsBuilder)
                .AddOrUpdateExtension(extension);

            return this;
        }

        /// <summary>
        /// Habilita logging del AST antes de ejecutar queries.
        /// </summary>
        public virtual FirestoreDbContextOptionsBuilder EnableAstLogging(bool enable = true)
        {
            var extension = GetOrCreateExtension();
            extension = extension.WithEnableAstLogging(enable);

            ((IDbContextOptionsBuilderInfrastructure)_optionsBuilder)
                .AddOrUpdateExtension(extension);

            return this;
        }

        /// <summary>
        /// Habilita caching de resultados de queries.
        /// </summary>
        public virtual FirestoreDbContextOptionsBuilder EnableCaching(bool enable = true)
        {
            var extension = GetOrCreateExtension();
            extension = extension.WithEnableCaching(enable);

            ((IDbContextOptionsBuilderInfrastructure)_optionsBuilder)
                .AddOrUpdateExtension(extension);

            return this;
        }

        /// <summary>
        /// Configura el número máximo de reintentos del pipeline para errores transitorios.
        /// </summary>
        public virtual FirestoreDbContextOptionsBuilder PipelineMaxRetries(int maxRetries)
        {
            if (maxRetries < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxRetries),
                    "El número de reintentos debe ser 0 o mayor.");
            }

            var extension = GetOrCreateExtension();
            extension = extension.WithPipelineMaxRetries(maxRetries);

            ((IDbContextOptionsBuilderInfrastructure)_optionsBuilder)
                .AddOrUpdateExtension(extension);

            return this;
        }

        /// <summary>
        /// Configura el delay inicial antes del primer reintento del pipeline.
        /// </summary>
        public virtual FirestoreDbContextOptionsBuilder PipelineRetryInitialDelay(TimeSpan delay)
        {
            if (delay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(delay),
                    "El delay debe ser 0 o mayor.");
            }

            var extension = GetOrCreateExtension();
            extension = extension.WithPipelineRetryInitialDelay(delay);

            ((IDbContextOptionsBuilderInfrastructure)_optionsBuilder)
                .AddOrUpdateExtension(extension);

            return this;
        }

        #endregion

        private FirestoreOptionsExtension GetOrCreateExtension()
        {
            return _optionsBuilder.Options.FindExtension<FirestoreOptionsExtension>()
                   ?? new FirestoreOptionsExtension();
        }
    }
}
