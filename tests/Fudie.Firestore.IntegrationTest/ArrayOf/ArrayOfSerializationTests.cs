using Google.Api.Gax;
using Google.Cloud.Firestore;
using Fudie.Firestore.IntegrationTest.Helpers;
using Firestore.EntityFrameworkCore.Metadata.Builders;

namespace Fudie.Firestore.IntegrationTest.ArrayOf;

/// <summary>
/// Tests de integración para verificar que ArrayOf se serializa
/// correctamente en Firestore.
///
/// Patrón: Guardar con EF Core → Leer con SDK de Google → Verificar estructura
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class ArrayOfSerializationTests
{
    private readonly FirestoreTestFixture _fixture;

    public ArrayOfSerializationTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Verificar que List&lt;ValueObject&gt; configurado con ArrayOf
    /// se serializa como array de maps en Firestore.
    /// </summary>
    [Fact]
    public async Task Serialization_ArrayOfEmbedded_ShouldStoreAsArrayOfMaps()
    {
        // Arrange
        var tiendaId = FirestoreTestFixture.GenerateId("tienda");

        using var context = _fixture.CreateContext<ArrayOfTestDbContext>();

        var tienda = new TiendaConHorarios
        {
            Id = tiendaId,
            Nombre = "Tienda Centro",
            Horarios =
            [
                new HorarioAtencion { Dia = "Lunes", Apertura = "09:00", Cierre = "18:00" },
                new HorarioAtencion { Dia = "Martes", Apertura = "09:00", Cierre = "18:00" },
                new HorarioAtencion { Dia = "Sábado", Apertura = "10:00", Cierre = "14:00" }
            ]
        };

        // Act - Guardar usando EF Core
        context.Tiendas.Add(tienda);
        await context.SaveChangesAsync();

        // Assert - Leer raw de Firestore para verificar la estructura
        var firestoreDb = await GetFirestoreDbAsync();
        var collectionName = GetCollectionName<TiendaConHorarios>();
        var docSnapshot = await firestoreDb
            .Collection(collectionName)
            .Document(tiendaId)
            .GetSnapshotAsync();

        docSnapshot.Exists.Should().BeTrue("El documento debe existir en Firestore");

        var rawData = docSnapshot.ToDictionary();
        rawData.Should().ContainKey("Horarios", "Debe existir el campo Horarios");

        // Verificar que es un array
        var horariosValue = rawData["Horarios"];
        horariosValue.Should().BeAssignableTo<IEnumerable<object>>(
            "El campo debe ser un array");

        var horarios = ((IEnumerable<object>)horariosValue).ToList();
        horarios.Should().HaveCount(3, "Debe tener 3 horarios");

        // Verificar estructura del primer elemento
        var primerHorario = horarios[0] as Dictionary<string, object>;
        primerHorario.Should().NotBeNull("Cada elemento debe ser un map/dictionary");
        primerHorario.Should().ContainKey("Dia");
        primerHorario.Should().ContainKey("Apertura");
        primerHorario.Should().ContainKey("Cierre");
        primerHorario!["Dia"].Should().Be("Lunes");
        primerHorario["Apertura"].Should().Be("09:00");
        primerHorario["Cierre"].Should().Be("18:00");
    }

    /// <summary>
    /// Verificar que List&lt;GeoLocation&gt; configurado con ArrayOf.AsGeoPoints()
    /// se serializa como array de GeoPoint en Firestore.
    /// </summary>
    [Fact]
    public async Task Serialization_ArrayOfGeoPoints_ShouldStoreAsArrayOfGeoPoints()
    {
        // Arrange
        var tiendaId = FirestoreTestFixture.GenerateId("tienda");

        using var context = _fixture.CreateContext<ArrayOfGeoPointTestDbContext>();

        var tienda = new TiendaConUbicaciones
        {
            Id = tiendaId,
            Nombre = "Tienda Multi-Sucursal",
            Ubicaciones =
            [
                new UbicacionGeo { Latitude = 40.4168, Longitude = -3.7038 },  // Madrid
                new UbicacionGeo { Latitude = 41.3851, Longitude = 2.1734 },   // Barcelona
                new UbicacionGeo { Latitude = 37.3891, Longitude = -5.9845 }   // Sevilla
            ]
        };

        // Act - Guardar usando EF Core
        context.Tiendas.Add(tienda);
        await context.SaveChangesAsync();

        // Assert - Leer raw de Firestore para verificar la estructura
        var firestoreDb = await GetFirestoreDbAsync();
        var collectionName = GetCollectionName<TiendaConUbicaciones>();
        var docSnapshot = await firestoreDb
            .Collection(collectionName)
            .Document(tiendaId)
            .GetSnapshotAsync();

        docSnapshot.Exists.Should().BeTrue("El documento debe existir en Firestore");

        var rawData = docSnapshot.ToDictionary();
        rawData.Should().ContainKey("Ubicaciones", "Debe existir el campo Ubicaciones");

        // Verificar que es un array
        var ubicacionesValue = rawData["Ubicaciones"];
        ubicacionesValue.Should().BeAssignableTo<IEnumerable<object>>(
            "El campo debe ser un array");

        var ubicaciones = ((IEnumerable<object>)ubicacionesValue).ToList();
        ubicaciones.Should().HaveCount(3, "Debe tener 3 ubicaciones");

        // Verificar que cada elemento es GeoPoint
        var primerUbicacion = ubicaciones[0];
        primerUbicacion.Should().BeOfType<GeoPoint>(
            "Cada elemento debe ser un GeoPoint nativo de Firestore");

        var geoPoint = (GeoPoint)primerUbicacion;
        geoPoint.Latitude.Should().BeApproximately(40.4168, 0.0001);
        geoPoint.Longitude.Should().BeApproximately(-3.7038, 0.0001);
    }

    /// <summary>
    /// Verificar que ArrayOf con lista vacía se serializa correctamente.
    /// </summary>
    [Fact]
    public async Task Serialization_ArrayOfEmbedded_EmptyList_ShouldStoreEmptyArray()
    {
        // Arrange
        var tiendaId = FirestoreTestFixture.GenerateId("tienda");

        using var context = _fixture.CreateContext<ArrayOfTestDbContext>();

        var tienda = new TiendaConHorarios
        {
            Id = tiendaId,
            Nombre = "Tienda Sin Horarios",
            Horarios = [] // Lista vacía
        };

        // Act - Guardar usando EF Core
        context.Tiendas.Add(tienda);
        await context.SaveChangesAsync();

        // Assert - Leer raw de Firestore
        var firestoreDb = await GetFirestoreDbAsync();
        var collectionName = GetCollectionName<TiendaConHorarios>();
        var docSnapshot = await firestoreDb
            .Collection(collectionName)
            .Document(tiendaId)
            .GetSnapshotAsync();

        docSnapshot.Exists.Should().BeTrue("El documento debe existir en Firestore");

        var rawData = docSnapshot.ToDictionary();
        rawData.Should().ContainKey("Horarios", "Debe existir el campo Horarios aunque esté vacío");

        var horarios = rawData["Horarios"] as IEnumerable<object>;
        horarios.Should().NotBeNull();
        horarios.Should().BeEmpty("La lista vacía debe serializarse como array vacío");
    }

    private async Task<FirestoreDb> GetFirestoreDbAsync()
    {
        return await new FirestoreDbBuilder
        {
            ProjectId = FirestoreTestFixture.ProjectId,
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.BuildAsync();
    }

#pragma warning disable EF1001 // Internal EF Core API usage - required for test to get correct collection name
    private static string GetCollectionName<T>()
    {
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<global::Firestore.EntityFrameworkCore.Infrastructure.Internal.FirestoreCollectionManager>();
        var collectionManager = new global::Firestore.EntityFrameworkCore.Infrastructure.Internal.FirestoreCollectionManager(logger);
        return collectionManager.GetCollectionName(typeof(T));
    }
#pragma warning restore EF1001
}

// ============================================================================
// ENTIDADES PARA TEST DE ARRAYOF EMBEDDED
// ============================================================================

public class HorarioAtencion
{
    public required string Dia { get; set; }
    public required string Apertura { get; set; }
    public required string Cierre { get; set; }
}

public class TiendaConHorarios
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public List<HorarioAtencion> Horarios { get; set; } = [];
}

// ============================================================================
// DBCONTEXT PARA TEST DE ARRAYOF EMBEDDED
// ============================================================================

public class ArrayOfTestDbContext : DbContext
{
    public ArrayOfTestDbContext(DbContextOptions<ArrayOfTestDbContext> options)
        : base(options)
    {
    }

    public DbSet<TiendaConHorarios> Tiendas => Set<TiendaConHorarios>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TiendaConHorarios>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();

            // ✅ Configurar ArrayOf Embedded
            entity.ArrayOf(e => e.Horarios);
        });
    }
}

// ============================================================================
// ENTIDADES PARA TEST DE ARRAYOF GEOPOINTS
// ============================================================================

public class UbicacionGeo
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class TiendaConUbicaciones
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public List<UbicacionGeo> Ubicaciones { get; set; } = [];
}

// ============================================================================
// DBCONTEXT PARA TEST DE ARRAYOF GEOPOINTS
// ============================================================================

public class ArrayOfGeoPointTestDbContext : DbContext
{
    public ArrayOfGeoPointTestDbContext(DbContextOptions<ArrayOfGeoPointTestDbContext> options)
        : base(options)
    {
    }

    public DbSet<TiendaConUbicaciones> Tiendas => Set<TiendaConUbicaciones>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TiendaConUbicaciones>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();

            // ✅ Configurar ArrayOf GeoPoints
            entity.ArrayOf(e => e.Ubicaciones).AsGeoPoints();
        });
    }
}
