using Fudie.Firestore.EntityFrameworkCore.Extensions;
using Fudie.Firestore.IntegrationTest.Helpers;

namespace Fudie.Firestore.IntegrationTest.ComplexPropertyCollections;

// ============= TEST MODELS =============

public class TestBookingPolicy
{
    public string Name { get; set; } = "";
    public IReadOnlyDictionary<string, int> StandardDurations { get; set; }
        = new Dictionary<string, int>();
}

public class TestMetadata
{
    public string Description { get; set; } = "";
    public List<string> Tags { get; set; } = new();
}

public class TestServiceWithComplexCollections
{
    public string Id { get; set; } = "";
    public string ServiceName { get; set; } = "";
    public TestBookingPolicy Policy { get; set; } = new();
    public TestMetadata Metadata { get; set; } = new();
}

// ============= DB CONTEXT =============

public class ComplexPropertyCollectionDbContext : DbContext
{
    public ComplexPropertyCollectionDbContext(
        DbContextOptions<ComplexPropertyCollectionDbContext> options)
        : base(options) { }

    public DbSet<TestServiceWithComplexCollections> Services { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestServiceWithComplexCollections>(entity =>
        {
            entity.ComplexProperty(s => s.Policy, policy =>
            {
                policy.MapOf(p => p.StandardDurations);
            });
            entity.ComplexProperty(s => s.Metadata, meta =>
            {
                meta.ArrayOf(m => m.Tags);
            });
        });
    }
}

// ============= TESTS =============

[Collection(nameof(FirestoreTestCollection))]
public class ComplexPropertyCollectionTests
{
    private readonly FirestoreTestFixture _fixture;

    public ComplexPropertyCollectionTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void ModelBuilds_WithMapOfAndArrayOfInComplexType_ShouldNotThrow()
    {
        // Act & Assert - accessing the Model forces model finalization
        var act = () =>
        {
            using var context = _fixture
                .CreateContext<ComplexPropertyCollectionDbContext>();
            _ = context.Model;
        };

        act.Should().NotThrow();
    }

    [Fact]
    public async Task Insert_EntityWithMapOfInComplexType_ShouldPersist()
    {
        // Arrange
        using var context = _fixture
            .CreateContext<ComplexPropertyCollectionDbContext>();
        var id = FirestoreTestFixture.GenerateId("cpc-map");

        var entity = new TestServiceWithComplexCollections
        {
            Id = id,
            ServiceName = "Corte de pelo",
            Policy = new TestBookingPolicy
            {
                Name = "Standard",
                StandardDurations = new Dictionary<string, int>
                {
                    ["corte"] = 30,
                    ["tinte"] = 60,
                    ["peinado"] = 45
                }
            },
            Metadata = new TestMetadata { Description = "test" }
        };

        // Act
        context.Services.Add(entity);
        await context.SaveChangesAsync();

        // Assert - read back in a fresh context
        using var readContext = _fixture
            .CreateContext<ComplexPropertyCollectionDbContext>();
        var loaded = await readContext.Services
            .FirstOrDefaultAsync(s => s.Id == id);

        loaded.Should().NotBeNull();
        loaded!.Policy.Name.Should().Be("Standard");
        loaded.Policy.StandardDurations.Should().HaveCount(3);
        loaded.Policy.StandardDurations["corte"].Should().Be(30);
        loaded.Policy.StandardDurations["tinte"].Should().Be(60);
        loaded.Policy.StandardDurations["peinado"].Should().Be(45);
    }

    [Fact]
    public async Task Insert_EntityWithArrayOfInComplexType_ShouldPersist()
    {
        // Arrange
        using var context = _fixture
            .CreateContext<ComplexPropertyCollectionDbContext>();
        var id = FirestoreTestFixture.GenerateId("cpc-arr");

        var entity = new TestServiceWithComplexCollections
        {
            Id = id,
            ServiceName = "Masaje",
            Policy = new TestBookingPolicy { Name = "Basic" },
            Metadata = new TestMetadata
            {
                Description = "Servicio de masaje",
                Tags = new List<string> { "relax", "spa", "bienestar" }
            }
        };

        // Act
        context.Services.Add(entity);
        await context.SaveChangesAsync();

        // Assert
        using var readContext = _fixture
            .CreateContext<ComplexPropertyCollectionDbContext>();
        var loaded = await readContext.Services
            .FirstOrDefaultAsync(s => s.Id == id);

        loaded.Should().NotBeNull();
        loaded!.Metadata.Description.Should().Be("Servicio de masaje");
        loaded.Metadata.Tags.Should().HaveCount(3);
        loaded.Metadata.Tags.Should().Contain("relax");
        loaded.Metadata.Tags.Should().Contain("spa");
        loaded.Metadata.Tags.Should().Contain("bienestar");
    }

    [Fact]
    public async Task Update_ScalarAndMapOfInComplexType_ShouldPersistBoth()
    {
        // Arrange - insert first
        using var context = _fixture
            .CreateContext<ComplexPropertyCollectionDbContext>();
        var id = FirestoreTestFixture.GenerateId("cpc-upd-map");

        var entity = new TestServiceWithComplexCollections
        {
            Id = id,
            ServiceName = "Manicura",
            Policy = new TestBookingPolicy
            {
                Name = "Original",
                StandardDurations = new Dictionary<string, int>
                {
                    ["basica"] = 20
                }
            },
            Metadata = new TestMetadata { Description = "test" }
        };

        context.Services.Add(entity);
        await context.SaveChangesAsync();

        // Act - update both scalar and map
        using var updateContext = _fixture
            .CreateContext<ComplexPropertyCollectionDbContext>();
        var toUpdate = await updateContext.Services
            .FirstOrDefaultAsync(s => s.Id == id);

        toUpdate.Should().NotBeNull();
        toUpdate!.Policy = new TestBookingPolicy
        {
            Name = "Updated",
            StandardDurations = new Dictionary<string, int>
            {
                ["basica"] = 25,
                ["premium"] = 40
            }
        };
        await updateContext.SaveChangesAsync();

        // Assert
        using var readContext = _fixture
            .CreateContext<ComplexPropertyCollectionDbContext>();
        var loaded = await readContext.Services
            .FirstOrDefaultAsync(s => s.Id == id);

        loaded.Should().NotBeNull();
        loaded!.Policy.Name.Should().Be("Updated");
        loaded.Policy.StandardDurations.Should().HaveCount(2);
        loaded.Policy.StandardDurations["basica"].Should().Be(25);
        loaded.Policy.StandardDurations["premium"].Should().Be(40);
    }

    [Fact]
    public async Task Update_ScalarAndArrayOfInComplexType_ShouldPersistBoth()
    {
        // Arrange - insert first
        using var context = _fixture
            .CreateContext<ComplexPropertyCollectionDbContext>();
        var id = FirestoreTestFixture.GenerateId("cpc-upd-arr");

        var entity = new TestServiceWithComplexCollections
        {
            Id = id,
            ServiceName = "Pedicura",
            Policy = new TestBookingPolicy { Name = "Basic" },
            Metadata = new TestMetadata
            {
                Description = "Original",
                Tags = new List<string> { "pies" }
            }
        };

        context.Services.Add(entity);
        await context.SaveChangesAsync();

        // Act - update both scalar and array
        using var updateContext = _fixture
            .CreateContext<ComplexPropertyCollectionDbContext>();
        var toUpdate = await updateContext.Services
            .FirstOrDefaultAsync(s => s.Id == id);

        toUpdate.Should().NotBeNull();
        toUpdate!.Metadata = new TestMetadata
        {
            Description = "Updated",
            Tags = new List<string> { "pies", "cuidado", "salon" }
        };
        await updateContext.SaveChangesAsync();

        // Assert
        using var readContext = _fixture
            .CreateContext<ComplexPropertyCollectionDbContext>();
        var loaded = await readContext.Services
            .FirstOrDefaultAsync(s => s.Id == id);

        loaded.Should().NotBeNull();
        loaded!.Metadata.Description.Should().Be("Updated");
        loaded.Metadata.Tags.Should().HaveCount(3);
        loaded.Metadata.Tags.Should().Contain("cuidado");
        loaded.Metadata.Tags.Should().Contain("salon");
    }
}
