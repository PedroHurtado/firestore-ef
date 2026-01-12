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

        public virtual FirestoreDbContextOptionsBuilder MaxRetryAttempts(int maxRetryAttempts)
        {
            if (maxRetryAttempts < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxRetryAttempts), 
                    "El número de reintentos debe ser 0 o mayor.");
            }

            var extension = GetOrCreateExtension();
            extension = extension.WithMaxRetryAttempts(maxRetryAttempts);
            
            ((IDbContextOptionsBuilderInfrastructure)_optionsBuilder)
                .AddOrUpdateExtension(extension);

            return this;
        }

        public virtual FirestoreDbContextOptionsBuilder CommandTimeout(TimeSpan timeout)
        {
            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeout), 
                    "El timeout debe ser mayor que cero.");
            }

            var extension = GetOrCreateExtension();
            extension = extension.WithCommandTimeout(timeout);
            
            ((IDbContextOptionsBuilderInfrastructure)_optionsBuilder)
                .AddOrUpdateExtension(extension);

            return this;
        }

        public virtual FirestoreDbContextOptionsBuilder CommandTimeout(int seconds)
        {
            return CommandTimeout(TimeSpan.FromSeconds(seconds));
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

        private FirestoreOptionsExtension GetOrCreateExtension()
        {
            return _optionsBuilder.Options.FindExtension<FirestoreOptionsExtension>()
                   ?? new FirestoreOptionsExtension();
        }
    }
}
