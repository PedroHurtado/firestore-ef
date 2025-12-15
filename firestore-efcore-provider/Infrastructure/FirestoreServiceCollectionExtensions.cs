using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.Extensions.DependencyInjection;
using System;
using Firestore.EntityFrameworkCore.Storage;
using Firestore.EntityFrameworkCore.Update;
using Firestore.EntityFrameworkCore.Query;
using Firestore.EntityFrameworkCore.Query.Visitors;
using Firestore.EntityFrameworkCore.Metadata.Conventions;
using Firestore.EntityFrameworkCore.Metadata;
using Firestore.EntityFrameworkCore.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Firestore.EntityFrameworkCore.Infrastructure
{
    public static class FirestoreServiceCollectionExtensions
    {
        public static IServiceCollection AddEntityFrameworkFirestore(
            this IServiceCollection serviceCollection)
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);

            serviceCollection.AddSingleton<LoggingDefinitions, FirestoreLoggingDefinitions>();
            serviceCollection.AddLogging();

            var builder = new EntityFrameworkServicesBuilder(serviceCollection);

            builder.TryAddCoreServices();

            serviceCollection.AddScoped<IProviderConventionSetBuilder, FirestoreConventionSetBuilder>();

            builder
                .TryAdd<IDatabaseProvider, DatabaseProvider<FirestoreOptionsExtension>>()
                .TryAdd<IDatabase, FirestoreDatabase>()
                .TryAdd<IDbContextTransactionManager, FirestoreTransactionManager>()
                .TryAdd<IQueryContextFactory, FirestoreQueryContextFactory>()
                .TryAdd<IQueryableMethodTranslatingExpressionVisitorFactory, FirestoreQueryableMethodTranslatingExpressionVisitorFactory>()
                .TryAdd<IShapedQueryCompilingExpressionVisitorFactory, FirestoreShapedQueryCompilingExpressionVisitorFactory>()
                .TryAdd<ITypeMappingSource, FirestoreTypeMappingSource>()
                .TryAdd<IModelValidator, FirestoreModelValidator>()
                .TryAdd<IDatabaseCreator, FirestoreDatabaseCreator>()
                .TryAdd<IExecutionStrategyFactory, FirestoreExecutionStrategyFactory>();

            // Override services that need to replace defaults from TryAddCoreServices
            // Must use AddScoped (not TryAdd) since TryAddCoreServices already registered defaults
            serviceCollection.AddScoped<IQueryCompilationContextFactory, FirestoreQueryCompilationContextFactory>();
            serviceCollection.AddScoped<IQueryTranslationPreprocessorFactory, FirestoreQueryTranslationPreprocessorFactory>();

            builder.TryAddProviderSpecificServices(b => b
                    .TryAddScoped<IUpdateSqlGenerator, FirestoreUpdateSqlGenerator>()
                    .TryAddScoped<IModificationCommandBatchFactory, FirestoreModificationCommandBatchFactory>()
                    .TryAddScoped<IFirestoreClientWrapper, FirestoreClientWrapper>()
                    .TryAddSingleton<IFirestoreIdGenerator, FirestoreIdGenerator>()
                    .TryAddSingleton<IFirestoreDocumentSerializer, FirestoreDocumentSerializer>()
                    .TryAddScoped<IFirestoreCollectionManager, FirestoreCollectionManager>());

            return serviceCollection;
        }
    }
}
