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
using Firestore.EntityFrameworkCore.Query.Pipeline;
using Firestore.EntityFrameworkCore.Query.Resolved;
using Firestore.EntityFrameworkCore.Metadata.Conventions;
using Firestore.EntityFrameworkCore.Metadata;
using Firestore.EntityFrameworkCore.Infrastructure.Internal;
using Firestore.EntityFrameworkCore.Diagnostics;
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
                    .TryAddScoped<IFirestoreDocumentDeserializer, FirestoreDocumentDeserializer>()
                    .TryAddScoped<IFirestoreQueryExecutor, FirestoreQueryExecutor>()
                    .TryAddSingleton<IFirestoreCollectionManager, FirestoreCollectionManager>());

            // Query Pipeline
            serviceCollection.AddScoped<IQueryPipelineMediator, QueryPipelineMediator>();

            // Pipeline Handlers (order matters - this is the execution order)
            serviceCollection.AddScoped<IQueryPipelineHandler, ErrorHandlingHandler>();
            serviceCollection.AddScoped<IQueryPipelineHandler, ResolverHandler>();
            serviceCollection.AddScoped<IQueryPipelineHandler, LogQueryHandler>();
            serviceCollection.AddScoped<IQueryPipelineHandler, ExecutionHandler>();
            serviceCollection.AddScoped<IQueryPipelineHandler, ConvertHandler>();
            serviceCollection.AddScoped<IQueryPipelineHandler, TrackingHandler>();
            serviceCollection.AddScoped<IQueryPipelineHandler, ProxyHandler>();
            serviceCollection.AddScoped<IQueryPipelineHandler, IncludeHandler>();

            // Pipeline Services
            serviceCollection.AddScoped<IFirestoreAstResolver, FirestoreAstResolver>();
            serviceCollection.AddScoped<IQueryBuilder, FirestoreQueryBuilder>();
            serviceCollection.AddScoped<ITypeConverter, FirestoreTypeConverter>();
            serviceCollection.AddScoped<IIncludeLoader, FirestoreIncludeLoader>();
            serviceCollection.AddTransient<ILazyLoader, FirestoreLazyLoader>();

            // Pipeline Options
            serviceCollection.AddSingleton<FirestoreErrorHandlingOptions>();

            return serviceCollection;
        }
    }
}
