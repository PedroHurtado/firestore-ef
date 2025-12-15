using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Helpers;

/// <summary>
/// Fixture reutilizable para tests de integración con Firestore.
/// Configura el emulador y proporciona métodos helper para crear DbContexts.
/// </summary>
public class FirestoreTestFixture : IAsyncLifetime
{
    public const string ProjectId = "demo-project";
    public const string EmulatorHost = "localhost:8080";

    public Task InitializeAsync()
    {
        // Configurar el emulador ANTES de crear cualquier DbContext
        Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", EmulatorHost);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Crea las opciones de DbContext configuradas para Firestore con el emulador.
    /// </summary>
    public DbContextOptions<TContext> CreateOptions<TContext>() where TContext : DbContext
    {
        return new DbContextOptionsBuilder<TContext>()
            .UseFirestore(ProjectId)
            .Options;
    }

    /// <summary>
    /// Crea las opciones de DbContext con lazy loading proxies habilitados.
    /// </summary>
    public DbContextOptions<TContext> CreateOptionsWithLazyLoading<TContext>() where TContext : DbContext
    {
        return new DbContextOptionsBuilder<TContext>()
            .UseFirestore(ProjectId)
            .UseLazyLoadingProxies()
            .Options;
    }

    /// <summary>
    /// Crea una instancia del DbContext especificado.
    /// </summary>
    public TContext CreateContext<TContext>() where TContext : DbContext
    {
        var options = CreateOptions<TContext>();
        return (TContext)Activator.CreateInstance(typeof(TContext), options)!;
    }

    /// <summary>
    /// Crea una instancia del DbContext con lazy loading proxies habilitados.
    /// </summary>
    public TContext CreateContextWithLazyLoading<TContext>() where TContext : DbContext
    {
        var options = CreateOptionsWithLazyLoading<TContext>();
        return (TContext)Activator.CreateInstance(typeof(TContext), options)!;
    }

    /// <summary>
    /// Genera un ID único para evitar colisiones entre tests.
    /// </summary>
    public static string GenerateId(string prefix = "test")
    {
        return $"{prefix}-{Guid.NewGuid():N}";
    }
}

/// <summary>
/// Collection definition para compartir el fixture entre tests.
/// </summary>
[CollectionDefinition(nameof(FirestoreTestCollection))]
public class FirestoreTestCollection : ICollectionFixture<FirestoreTestFixture>
{
}
