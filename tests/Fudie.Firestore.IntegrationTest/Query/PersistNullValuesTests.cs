using Firestore.EntityFrameworkCore.Extensions;
using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Query;

/// <summary>
/// Integration tests for PersistNullValues feature.
/// Tests the opt-in mechanism for persisting null values in Firestore.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class PersistNullValuesTests
{
    private readonly FirestoreTestFixture _fixture;

    public PersistNullValuesTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Test 5.4: Query sin PersistNullValues (debe fallar)

    [Fact]
    public async Task Where_IsNull_WithoutPersistNullValues_ThrowsNotSupportedException()
    {
        // Arrange - usar QueryTestDbContext que NO tiene PersistNullValues configurado
        using var context = _fixture.CreateContext<QueryTestDbContext>();

        // Act & Assert
        string? nullValue = null;
        var exception = await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await context.QueryTestEntities
                .Where(e => e.Description == nullValue)
                .ToListAsync();
        });

        // Verify the error message is descriptive
        exception.Message.Should().Contain("Description");
        exception.Message.Should().Contain("PersistNullValues");
    }

    [Fact]
    public async Task Where_IsNotNull_WithoutPersistNullValues_ThrowsNotSupportedException()
    {
        // Arrange
        using var context = _fixture.CreateContext<QueryTestDbContext>();

        // Act & Assert
        string? nullValue = null;
        var exception = await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await context.QueryTestEntities
                .Where(e => e.Description != nullValue)
                .ToListAsync();
        });

        exception.Message.Should().Contain("PersistNullValues");
    }

    #endregion

    #region Test 5.5: Query con PersistNullValues (debe funcionar)

    [Fact]
    public async Task Where_IsNull_WithPersistNullValues_ReturnsCorrectEntities()
    {
        // Arrange - usar NullTestDbContext que SÍ tiene PersistNullValues configurado
        using var context = _fixture.CreateContext<NullTestDbContext>();
        var uniqueTenant = $"NullTest-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new NullTestEntity { Id = FirestoreTestFixture.GenerateId("nulltest"), Name = "WithNull-A", TenantId = uniqueTenant, NullableField = null },
            new NullTestEntity { Id = FirestoreTestFixture.GenerateId("nulltest"), Name = "WithValue-B", TenantId = uniqueTenant, NullableField = "Has value" },
            new NullTestEntity { Id = FirestoreTestFixture.GenerateId("nulltest"), Name = "WithNull-C", TenantId = uniqueTenant, NullableField = null },
            new NullTestEntity { Id = FirestoreTestFixture.GenerateId("nulltest"), Name = "WithValue-D", TenantId = uniqueTenant, NullableField = "Another value" }
        };

        context.NullTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Filter where NullableField is null
        string? nullValue = null;
        using var readContext = _fixture.CreateContext<NullTestDbContext>();
        var results = await readContext.NullTestEntities
            .Where(e => e.TenantId == uniqueTenant && e.NullableField == nullValue)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == "WithNull-A");
        results.Should().Contain(e => e.Name == "WithNull-C");
        results.Should().NotContain(e => e.Name == "WithValue-B");
        results.Should().NotContain(e => e.Name == "WithValue-D");
    }

    [Fact]
    public async Task Where_IsNotNull_WithPersistNullValues_ReturnsCorrectEntities()
    {
        // Arrange
        using var context = _fixture.CreateContext<NullTestDbContext>();
        var uniqueTenant = $"NotNullTest-{Guid.NewGuid():N}";
        var entities = new[]
        {
            new NullTestEntity { Id = FirestoreTestFixture.GenerateId("nulltest"), Name = "WithNull-A", TenantId = uniqueTenant, NullableField = null },
            new NullTestEntity { Id = FirestoreTestFixture.GenerateId("nulltest"), Name = "WithValue-B", TenantId = uniqueTenant, NullableField = "Has value" },
            new NullTestEntity { Id = FirestoreTestFixture.GenerateId("nulltest"), Name = "WithNull-C", TenantId = uniqueTenant, NullableField = null },
            new NullTestEntity { Id = FirestoreTestFixture.GenerateId("nulltest"), Name = "WithValue-D", TenantId = uniqueTenant, NullableField = "Another value" }
        };

        context.NullTestEntities.AddRange(entities);
        await context.SaveChangesAsync();

        // Act - Filter where NullableField is NOT null
        string? nullValue = null;
        using var readContext = _fixture.CreateContext<NullTestDbContext>();
        var results = await readContext.NullTestEntities
            .Where(e => e.TenantId == uniqueTenant && e.NullableField != nullValue)
            .ToListAsync();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == "WithValue-B");
        results.Should().Contain(e => e.Name == "WithValue-D");
        results.Should().NotContain(e => e.Name == "WithNull-A");
        results.Should().NotContain(e => e.Name == "WithNull-C");
    }

    #endregion

    #region Test 5.2 & 5.3: Serialization tests (verificar que null se guarda solo con PersistNullValues)

    [Fact]
    public async Task SaveChanges_WithPersistNullValues_StoresNullFieldExplicitly()
    {
        // Arrange
        using var context = _fixture.CreateContext<NullTestDbContext>();
        var entity = new NullTestEntity
        {
            Id = FirestoreTestFixture.GenerateId("nulltest"),
            Name = "TestEntity",
            TenantId = $"SerializeTest-{Guid.NewGuid():N}",
            NullableField = null  // This should be stored as null in Firestore
        };

        // Act
        context.NullTestEntities.Add(entity);
        await context.SaveChangesAsync();

        // Assert - Query by null should find it (proving null was stored)
        string? nullValue = null;
        using var readContext = _fixture.CreateContext<NullTestDbContext>();
        var result = await readContext.NullTestEntities
            .Where(e => e.Id == entity.Id && e.NullableField == nullValue)
            .FirstOrDefaultAsync();

        result.Should().NotBeNull();
        result!.Name.Should().Be("TestEntity");
    }

    #endregion
}

/// <summary>
/// Entity for null value tests with PersistNullValues configured.
/// </summary>
public class NullTestEntity
{
    public string? Id { get; set; }
    public required string Name { get; set; }
    public string? TenantId { get; set; }
    public string? NullableField { get; set; }  // This will have PersistNullValues configured
}

/// <summary>
/// DbContext with PersistNullValues configured for NullableField.
/// </summary>
public class NullTestDbContext : DbContext
{
    public NullTestDbContext(DbContextOptions<NullTestDbContext> options) : base(options)
    {
    }

    public DbSet<NullTestEntity> NullTestEntities => Set<NullTestEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NullTestEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();

            // Configure NullableField to persist null values
            entity.Property(e => e.NullableField).PersistNullValues();
        });
    }
}
