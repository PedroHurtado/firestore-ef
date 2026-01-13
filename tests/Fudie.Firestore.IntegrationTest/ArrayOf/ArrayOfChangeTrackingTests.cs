using Fudie.Firestore.EntityFrameworkCore.Metadata.Builders;
using Fudie.Firestore.IntegrationTest.Helpers;
using Fudie.Firestore.IntegrationTest.Helpers.ArrayOf;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.ArrayOf;

/// <summary>
/// Tests de integración para verificar que los cambios en propiedades ArrayOf
/// son detectados y persistidos correctamente en Firestore.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class ArrayOfChangeTrackingTests
{
    private readonly FirestoreTestFixture _fixture;

    public ArrayOfChangeTrackingTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ModifyArrayElement_ShouldMarkEntityAsModified_AndPersistChange()
    {
        // Arrange
        var tiendaId = FirestoreTestFixture.GenerateId("tienda");
        using var context = _fixture.CreateContext<ArrayOfChangeTrackingTestDbContext>();

        var tienda = new TiendaConHorarios
        {
            Id = tiendaId,
            Nombre = "Tienda Test",
            Horarios =
            [
                new HorarioAtencion { Dia = "Lunes", Apertura = "09:00", Cierre = "18:00" },
                new HorarioAtencion { Dia = "Martes", Apertura = "09:00", Cierre = "18:00" }
            ]
        };

        context.Tiendas.Add(tienda);
        await context.SaveChangesAsync();

        // Act - Load entity and modify array element
        using var context2 = _fixture.CreateContext<ArrayOfChangeTrackingTestDbContext>();
        var loadedTienda = await context2.Tiendas.FirstOrDefaultAsync(t => t.Id == tiendaId);

        loadedTienda.Should().NotBeNull();
        var originalState = context2.Entry(loadedTienda!).State;
        originalState.Should().Be(EntityState.Unchanged);

        // Modify array element
        loadedTienda!.Horarios[0].Apertura = "10:00";
        await context2.SaveChangesAsync();

        // Assert - Verify change was persisted
        // Note: ArrayUnion moves modified elements to the end, so we search by "Dia" instead of index
        var rawData = await GetDocumentRawData<TiendaConHorarios>(tiendaId);
        var horarios = ((IEnumerable<object>)rawData["Horarios"])
            .Cast<Dictionary<string, object>>()
            .ToList();

        var horarioLunes = horarios.First(h => (string)h["Dia"] == "Lunes");
        horarioLunes["Apertura"].Should().Be("10:00");
    }

    [Fact]
    public async Task AddArrayElement_ShouldPersistChange()
    {
        // Arrange
        var tiendaId = FirestoreTestFixture.GenerateId("tienda");
        using var context = _fixture.CreateContext<ArrayOfChangeTrackingTestDbContext>();

        var tienda = new TiendaConHorarios
        {
            Id = tiendaId,
            Nombre = "Tienda Test",
            Horarios =
            [
                new HorarioAtencion { Dia = "Lunes", Apertura = "09:00", Cierre = "18:00" }
            ]
        };

        context.Tiendas.Add(tienda);
        await context.SaveChangesAsync();

        // Act - Load entity and add element
        using var context2 = _fixture.CreateContext<ArrayOfChangeTrackingTestDbContext>();
        var loadedTienda = await context2.Tiendas.FirstOrDefaultAsync(t => t.Id == tiendaId);

        loadedTienda!.Horarios.Add(new HorarioAtencion { Dia = "Martes", Apertura = "09:00", Cierre = "18:00" });
        await context2.SaveChangesAsync();

        // Assert - Verify change was persisted
        var rawData = await GetDocumentRawData<TiendaConHorarios>(tiendaId);
        var horarios = ((IEnumerable<object>)rawData["Horarios"]).ToList();

        horarios.Should().HaveCount(2);
        var segundoHorario = horarios[1] as Dictionary<string, object>;
        segundoHorario!["Dia"].Should().Be("Martes");
    }

    [Fact]
    public async Task RemoveArrayElement_ShouldPersistChange()
    {
        // Arrange
        var tiendaId = FirestoreTestFixture.GenerateId("tienda");
        using var context = _fixture.CreateContext<ArrayOfChangeTrackingTestDbContext>();

        var tienda = new TiendaConHorarios
        {
            Id = tiendaId,
            Nombre = "Tienda Test",
            Horarios =
            [
                new HorarioAtencion { Dia = "Lunes", Apertura = "09:00", Cierre = "18:00" },
                new HorarioAtencion { Dia = "Martes", Apertura = "09:00", Cierre = "18:00" }
            ]
        };

        context.Tiendas.Add(tienda);
        await context.SaveChangesAsync();

        // Act - Load entity and remove element
        using var context2 = _fixture.CreateContext<ArrayOfChangeTrackingTestDbContext>();
        var loadedTienda = await context2.Tiendas.FirstOrDefaultAsync(t => t.Id == tiendaId);

        loadedTienda!.Horarios.RemoveAt(1);
        await context2.SaveChangesAsync();

        // Assert - Verify change was persisted
        var rawData = await GetDocumentRawData<TiendaConHorarios>(tiendaId);
        var horarios = ((IEnumerable<object>)rawData["Horarios"]).ToList();

        horarios.Should().HaveCount(1);
    }

    [Fact]
    public async Task NoChangesToArray_ShouldNotTriggerUpdate()
    {
        // Arrange
        var tiendaId = FirestoreTestFixture.GenerateId("tienda");
        using var context = _fixture.CreateContext<ArrayOfChangeTrackingTestDbContext>();

        var tienda = new TiendaConHorarios
        {
            Id = tiendaId,
            Nombre = "Tienda Test",
            Horarios =
            [
                new HorarioAtencion { Dia = "Lunes", Apertura = "09:00", Cierre = "18:00" }
            ]
        };

        context.Tiendas.Add(tienda);
        await context.SaveChangesAsync();

        // Act - Load entity without modifying and save (should not trigger update)
        using var context2 = _fixture.CreateContext<ArrayOfChangeTrackingTestDbContext>();
        var loadedTienda = await context2.Tiendas.FirstOrDefaultAsync(t => t.Id == tiendaId);

        var stateBefore = context2.Entry(loadedTienda!).State;

        // SaveChanges internally calls SyncArrayOfChanges - entity should remain Unchanged
        var changesCount = await context2.SaveChangesAsync();

        // Assert - Entity should remain Unchanged (no changes detected)
        stateBefore.Should().Be(EntityState.Unchanged);
        changesCount.Should().Be(0, "No changes should be detected when array is not modified");
    }

    [Fact]
    public async Task ModifyGeoPointArray_ShouldPersistChange()
    {
        // Arrange
        var tiendaId = FirestoreTestFixture.GenerateId("tienda");
        using var context = _fixture.CreateContext<ArrayOfGeoPointChangeTrackingTestDbContext>();

        var tienda = new TiendaConUbicaciones
        {
            Id = tiendaId,
            Nombre = "Tienda Multi-Ubicación",
            Ubicaciones =
            [
                new UbicacionGeo { Latitude = 40.4168, Longitude = -3.7038 },
                new UbicacionGeo { Latitude = 41.3851, Longitude = 2.1734 }
            ]
        };

        context.Tiendas.Add(tienda);
        await context.SaveChangesAsync();

        // Act - Modify GeoPoint
        using var context2 = _fixture.CreateContext<ArrayOfGeoPointChangeTrackingTestDbContext>();
        var loadedTienda = await context2.Tiendas.FirstOrDefaultAsync(t => t.Id == tiendaId);

        loadedTienda!.Ubicaciones[0].Latitude = 40.5000;
        await context2.SaveChangesAsync();

        // Assert
        // Note: ArrayUnion moves modified elements to the end, so we search by approximate Longitude
        // (the original Madrid location had Longitude = -3.7038)
        var rawData = await GetDocumentRawData<TiendaConUbicaciones>(tiendaId);
        var ubicaciones = ((IEnumerable<object>)rawData["Ubicaciones"])
            .Cast<GeoPoint>()
            .ToList();

        var ubicacionMadrid = ubicaciones.First(u => Math.Abs(u.Longitude - (-3.7038)) < 0.001);
        ubicacionMadrid.Latitude.Should().BeApproximately(40.5000, 0.0001);
    }

    #region Helpers

    private async Task<Dictionary<string, object>> GetDocumentRawData<T>(string documentId)
    {
        var firestoreDb = await new FirestoreDbBuilder
        {
            ProjectId = FirestoreTestFixture.ProjectId,
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.BuildAsync();

        var collectionName = GetCollectionName<T>();
        var docSnapshot = await firestoreDb
            .Collection(collectionName)
            .Document(documentId)
            .GetSnapshotAsync();

        docSnapshot.Exists.Should().BeTrue($"El documento {documentId} debe existir");
        return docSnapshot.ToDictionary();
    }

#pragma warning disable EF1001
    private static string GetCollectionName<T>()
    {
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<global::Fudie.Firestore.EntityFrameworkCore.Infrastructure.Internal.FirestoreCollectionManager>();
        var collectionManager = new global::Fudie.Firestore.EntityFrameworkCore.Infrastructure.Internal.FirestoreCollectionManager(logger);
        return collectionManager.GetCollectionName(typeof(T));
    }
#pragma warning restore EF1001

    #endregion
}

// ============================================================================
// DBCONTEXTS FOR CHANGE TRACKING TESTS
// ============================================================================

/// <summary>
/// DbContext for ArrayOf change tracking tests.
/// No SaveChanges override needed - FirestoreDatabase handles it transparently.
/// </summary>
public class ArrayOfChangeTrackingTestDbContext(DbContextOptions<ArrayOfChangeTrackingTestDbContext> options) : DbContext(options)
{
    public DbSet<TiendaConHorarios> Tiendas => Set<TiendaConHorarios>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TiendaConHorarios>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
            entity.ArrayOf(e => e.Horarios);
        });
    }
}

/// <summary>
/// DbContext for GeoPoint change tracking tests.
/// No SaveChanges override needed - FirestoreDatabase handles it transparently.
/// </summary>
public class ArrayOfGeoPointChangeTrackingTestDbContext(DbContextOptions<ArrayOfGeoPointChangeTrackingTestDbContext> options) : DbContext(options)
{
    public DbSet<TiendaConUbicaciones> Tiendas => Set<TiendaConUbicaciones>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TiendaConUbicaciones>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
            entity.ArrayOf(e => e.Ubicaciones).AsGeoPoints();
        });
    }
}
