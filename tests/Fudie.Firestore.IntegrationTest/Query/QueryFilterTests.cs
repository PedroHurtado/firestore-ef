using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Query;

/// <summary>
/// Integration tests for EF Core Global Query Filters (multi-tenancy).
/// Verifies that HasQueryFilter automatically applies tenant filtering.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class QueryFilterTests
{
    private readonly FirestoreTestFixture _fixture;

    public QueryFilterTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region Test 1: Buscar sin filtro - Solo devuelve entidades del tenant

    [Fact]
    public async Task QueryFilter_ToList_ReturnsOnlyCurrentTenantEntities()
    {
        // Arrange - Crear entidades para dos tenants diferentes
        var tenantA = $"tenant-A-{Guid.NewGuid():N}";
        var tenantB = $"tenant-B-{Guid.NewGuid():N}";

        // Usar contexto sin filtro para insertar datos de ambos tenants
        using (var setupContextA = _fixture.CreateTenantContext(tenantA))
        {
            setupContextA.TenantEntities.AddRange(
                new TenantEntity { Id = FirestoreTestFixture.GenerateId("qf"), Name = "Entity-A1", TenantId = tenantA },
                new TenantEntity { Id = FirestoreTestFixture.GenerateId("qf"), Name = "Entity-A2", TenantId = tenantA }
            );
            await setupContextA.SaveChangesAsync();
        }

        using (var setupContextB = _fixture.CreateTenantContext(tenantB))
        {
            setupContextB.TenantEntities.Add(
                new TenantEntity { Id = FirestoreTestFixture.GenerateId("qf"), Name = "Entity-B1", TenantId = tenantB }
            );
            await setupContextB.SaveChangesAsync();
        }

        // Act - Consultar con contexto de tenant A
        using var readContext = _fixture.CreateTenantContext(tenantA);
        var results = await readContext.TenantEntities.ToListAsync();

        // Assert - Solo debe devolver entidades del tenant A
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(e => e.TenantId.Should().Be(tenantA));
        results.Should().Contain(e => e.Name == "Entity-A1");
        results.Should().Contain(e => e.Name == "Entity-A2");
    }

    #endregion

    #region Test 2: Buscar con Id - Devuelve entidad del tenant

    [Fact]
    public async Task QueryFilter_FindById_ReturnsEntityFromCurrentTenant()
    {
        // Arrange
        var tenantA = $"tenant-A-{Guid.NewGuid():N}";
        var targetId = FirestoreTestFixture.GenerateId("qf");

        using (var setupContext = _fixture.CreateTenantContext(tenantA))
        {
            setupContext.TenantEntities.Add(
                new TenantEntity { Id = targetId, Name = "Target-Entity", TenantId = tenantA, Price = 100m }
            );
            await setupContext.SaveChangesAsync();
        }

        // Act - Buscar por Id con el mismo tenant
        using var readContext = _fixture.CreateTenantContext(tenantA);
        var result = await readContext.TenantEntities
            .Where(e => e.Id == targetId)
            .FirstOrDefaultAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(targetId);
        result.TenantId.Should().Be(tenantA);
        result.Name.Should().Be("Target-Entity");
    }

    #endregion

    #region Test 3: Buscar con filtro adicional - Aplica tenant automáticamente

    [Fact]
    public async Task QueryFilter_WithAdditionalFilter_AppliesTenantFilter()
    {
        // Arrange
        var tenantA = $"tenant-A-{Guid.NewGuid():N}";
        var tenantB = $"tenant-B-{Guid.NewGuid():N}";

        using (var setupContextA = _fixture.CreateTenantContext(tenantA))
        {
            setupContextA.TenantEntities.AddRange(
                new TenantEntity { Id = FirestoreTestFixture.GenerateId("qf"), Name = "Active-A", TenantId = tenantA, IsActive = true },
                new TenantEntity { Id = FirestoreTestFixture.GenerateId("qf"), Name = "Inactive-A", TenantId = tenantA, IsActive = false }
            );
            await setupContextA.SaveChangesAsync();
        }

        using (var setupContextB = _fixture.CreateTenantContext(tenantB))
        {
            setupContextB.TenantEntities.Add(
                new TenantEntity { Id = FirestoreTestFixture.GenerateId("qf"), Name = "Active-B", TenantId = tenantB, IsActive = true }
            );
            await setupContextB.SaveChangesAsync();
        }

        // Act - Filtrar por IsActive=true con contexto de tenant A
        using var readContext = _fixture.CreateTenantContext(tenantA);
        var results = await readContext.TenantEntities
            .Where(e => e.IsActive == true)
            .ToListAsync();

        // Assert - Solo debe devolver la entidad activa del tenant A (no la del tenant B)
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Active-A");
        results[0].TenantId.Should().Be(tenantA);
    }

    #endregion

    #region Test 4: Buscar con Id de otro tenant - No devuelve nada

    [Fact]
    public async Task QueryFilter_FindByIdFromOtherTenant_ReturnsNull()
    {
        // Arrange - Crear entidad en tenant A
        var tenantA = $"tenant-A-{Guid.NewGuid():N}";
        var tenantB = $"tenant-B-{Guid.NewGuid():N}";
        var targetId = FirestoreTestFixture.GenerateId("qf");

        using (var setupContext = _fixture.CreateTenantContext(tenantA))
        {
            setupContext.TenantEntities.Add(
                new TenantEntity { Id = targetId, Name = "Secret-Entity", TenantId = tenantA }
            );
            await setupContext.SaveChangesAsync();
        }

        // Act - Intentar buscar con contexto de tenant B
        using var readContext = _fixture.CreateTenantContext(tenantB);
        var result = await readContext.TenantEntities
            .Where(e => e.Id == targetId)
            .FirstOrDefaultAsync();

        // Assert - No debe devolver nada (seguridad multi-tenant)
        result.Should().BeNull();
    }

    #endregion

    #region Test 5: Buscar con filtro - Lista vacía si no pertenece al tenant

    [Fact]
    public async Task QueryFilter_WithFilterNoMatchingTenant_ReturnsEmptyList()
    {
        // Arrange - Crear entidades solo en tenant A
        var tenantA = $"tenant-A-{Guid.NewGuid():N}";
        var tenantB = $"tenant-B-{Guid.NewGuid():N}";
        var uniquePrice = 99999.99m + new Random().Next(1, 1000);

        using (var setupContext = _fixture.CreateTenantContext(tenantA))
        {
            setupContext.TenantEntities.AddRange(
                new TenantEntity { Id = FirestoreTestFixture.GenerateId("qf"), Name = "Price-Entity-1", TenantId = tenantA, Price = uniquePrice },
                new TenantEntity { Id = FirestoreTestFixture.GenerateId("qf"), Name = "Price-Entity-2", TenantId = tenantA, Price = uniquePrice }
            );
            await setupContext.SaveChangesAsync();
        }

        // Act - Buscar con contexto de tenant B usando el mismo filtro de precio
        using var readContext = _fixture.CreateTenantContext(tenantB);
        var results = await readContext.TenantEntities
            .Where(e => e.Price == uniquePrice)
            .ToListAsync();

        // Assert - Lista vacía porque no hay entidades del tenant B con ese precio
        results.Should().BeEmpty();
    }

    #endregion

    #region Test 6: IgnoreQueryFilters - Desactivar filtro global

    [Fact]
    public async Task QueryFilter_IgnoreQueryFilters_ReturnsAllTenantEntities()
    {
        // Arrange - Crear entidades para dos tenants diferentes con un marcador único
        var tenantA = $"tenant-A-{Guid.NewGuid():N}";
        var tenantB = $"tenant-B-{Guid.NewGuid():N}";
        var uniqueMarker = $"IgnoreFilter-{Guid.NewGuid():N}";

        using (var setupContextA = _fixture.CreateTenantContext(tenantA))
        {
            setupContextA.TenantEntities.AddRange(
                new TenantEntity { Id = FirestoreTestFixture.GenerateId("iqf"), Name = uniqueMarker, TenantId = tenantA },
                new TenantEntity { Id = FirestoreTestFixture.GenerateId("iqf"), Name = uniqueMarker, TenantId = tenantA }
            );
            await setupContextA.SaveChangesAsync();
        }

        using (var setupContextB = _fixture.CreateTenantContext(tenantB))
        {
            setupContextB.TenantEntities.Add(
                new TenantEntity { Id = FirestoreTestFixture.GenerateId("iqf"), Name = uniqueMarker, TenantId = tenantB }
            );
            await setupContextB.SaveChangesAsync();
        }

        // Act - Usar IgnoreQueryFilters() para obtener TODAS las entidades (admin/superuser scenario)
        using var readContext = _fixture.CreateTenantContext(tenantA);
        var results = await readContext.TenantEntities
            .IgnoreQueryFilters()
            .Where(e => e.Name == uniqueMarker)
            .ToListAsync();

        // Assert - Debe devolver entidades de AMBOS tenants (3 en total)
        results.Should().HaveCount(3);
        results.Should().Contain(e => e.TenantId == tenantA);
        results.Should().Contain(e => e.TenantId == tenantB);
    }

    #endregion
}
