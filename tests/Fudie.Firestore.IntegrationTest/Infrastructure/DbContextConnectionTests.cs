using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Fudie.Firestore.IntegrationTest.Infrastructure;

/// <summary>
/// Tests de conexión al emulador usando DbContext con el provider de Firestore.
/// Requiere que el emulador esté corriendo: docker-compose up
/// </summary>
public class DbContextConnectionTests : IAsyncLifetime
{
    private const string ProjectId = "demo-project";
    private const string EmulatorHost = "127.0.0.1:8080";

    public Task InitializeAsync()
    {
        // Configurar el emulador ANTES de crear el DbContext
        Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", EmulatorHost);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public void DbContext_WithUseFirestore_ShouldCreateInstance()
    {
        // Arrange & Act
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseFirestore(ProjectId)
            .Options;

        using var context = new TestDbContext(options);

        // Assert
        context.Should().NotBeNull();
    }

    [Fact]
    public async Task DbContext_WithUseFirestore_ShouldConnectToEmulator()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseFirestore(ProjectId)
            .Options;

        using var context = new TestDbContext(options);

        // Act - Obtener el FirestoreClientWrapper del service provider
        var clientWrapper = context.GetService<IFirestoreClientWrapper>();

        // Assert - Verificar que podemos acceder al cliente y hacer una operación
        clientWrapper.Should().NotBeNull();
        clientWrapper.Database.Should().NotBeNull();
        clientWrapper.Database.ProjectId.Should().Be(ProjectId);

        // Verificar conexión real al emulador escribiendo un documento
        var testCollection = clientWrapper.GetCollection("dbcontext-connection-test");
        var docRef = await testCollection.AddAsync(new Dictionary<string, object>
        {
            { "test", "connection" },
            { "timestamp", DateTime.UtcNow }
        });

        docRef.Should().NotBeNull();
        docRef.Id.Should().NotBeNullOrEmpty();
    }
}

/// <summary>
/// DbContext de prueba simple para tests de integración
/// </summary>
public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
    {
    }

    public DbSet<TestEntity> TestEntities => Set<TestEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
        });
    }
}

/// <summary>
/// Entidad de prueba simple
/// </summary>
public class TestEntity
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
