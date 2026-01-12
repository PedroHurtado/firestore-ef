using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using Fudie.Firestore.EntityFrameworkCore.Storage;
using Fudie.Firestore.EntityFrameworkCore.Update;
using Fudie.Firestore.EntityFrameworkCore.Query;
using Fudie.Firestore.EntityFrameworkCore.Query.Visitors;
using Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;
using Fudie.Firestore.EntityFrameworkCore.Query.Resolved;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;
using Fudie.Firestore.EntityFrameworkCore.Metadata;
using Fudie.Firestore.EntityFrameworkCore.Infrastructure.Internal;
using Fudie.Firestore.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Fudie.Firestore.EntityFrameworkCore.Infrastructure
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
                    .TryAddScoped<IProjectionMaterializer, ProjectionMaterializer>()
                    .TryAddSingleton<IFirestoreCollectionManager, FirestoreCollectionManager>()
                    .TryAddSingleton<IFirestoreValueConverter, FirestoreValueConverter>());

            // Query Pipeline
            serviceCollection.AddScoped<IQueryPipelineMediator, QueryPipelineMediator>();

            // Pipeline Handlers (order matters - middleware pattern)
            // Handlers that modify context run first, then each calls next() and processes the result.
            // Order: ErrorHandling → Resolver → Log → Proxy → Tracking → Convert → Execution
            // Result flows back: Execution returns docs (+ includes) → Convert converts to entities →
            //                    Tracking tracks → Proxy wraps → return
            // Note: Includes are loaded by ExecutionHandler directly, not by a separate handler
            serviceCollection.AddScoped<IQueryPipelineHandler, ErrorHandlingHandler>();
            serviceCollection.AddScoped<IQueryPipelineHandler, ResolverHandler>();
            serviceCollection.AddScoped<IQueryPipelineHandler, LogQueryHandler>();
            // ProxyHandler with optional IProxyFactory (null if proxies not configured)
            serviceCollection.AddScoped<IQueryPipelineHandler>(sp =>
                new ProxyHandler(sp.GetService<IProxyFactory>()));
            serviceCollection.AddScoped<IQueryPipelineHandler, TrackingHandler>();
            serviceCollection.AddScoped<IQueryPipelineHandler, ConvertHandler>();
            serviceCollection.AddScoped<IQueryPipelineHandler, ExecutionHandler>();

            // Pipeline Services
            serviceCollection.AddSingleton<IExpressionEvaluator, ExpressionEvaluator>();
            serviceCollection.AddSingleton<IFirestoreAstResolver>(sp =>
                new FirestoreAstResolver(
                    sp.GetRequiredService<IFirestoreValueConverter>(),
                    sp.GetRequiredService<IExpressionEvaluator>()));
            serviceCollection.AddScoped<IQueryBuilder, FirestoreQueryBuilder>();
            serviceCollection.AddScoped<ITypeConverter, FirestoreTypeConverter>();
            serviceCollection.AddTransient<ILazyLoader, FirestoreLazyLoader>();

            // Pipeline Options
            serviceCollection.TryAddSingleton<FirestoreErrorHandlingOptions>();

            // FirestorePipelineOptions is registered from the extension configuration
            serviceCollection.AddScoped<FirestorePipelineOptions>(sp =>
            {
                var options = sp.GetRequiredService<IDbContextOptions>();
                var extension = options.FindExtension<FirestoreOptionsExtension>();
                return extension?.PipelineOptions ?? new FirestorePipelineOptions();
            });

            return serviceCollection;
        }
    }
}
