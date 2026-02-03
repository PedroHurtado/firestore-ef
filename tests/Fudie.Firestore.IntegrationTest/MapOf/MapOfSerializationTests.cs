using Google.Api.Gax;
using Google.Cloud.Firestore;
using Fudie.Firestore.IntegrationTest.Helpers;
using Fudie.Firestore.IntegrationTest.Helpers.MapOf;

namespace Fudie.Firestore.IntegrationTest.MapOf;

/// <summary>
/// Tests de integración para verificar que MapOf se serializa correctamente en Firestore.
/// Patrón: Guardar con EF Core → Leer con SDK de Google → Verificar estructura
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class MapOfSerializationTests
{
    private readonly FirestoreTestFixture _fixture;

    public MapOfSerializationTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Serialization_MapOfWithEnumKey_ShouldStoreAsMapWithStringKeys()
    {
        // Arrange
        var restauranteId = FirestoreTestFixture.GenerateId("rest");
        using var context = _fixture.CreateContext<MapOfEnumKeyTestDbContext>();

        var restaurante = new RestauranteConHorarios
        {
            Id = restauranteId,
            Nombre = "La Parrilla",
            Direccion = "Calle Principal 123"
        };

        restaurante.SetHorario(DiaSemana.Lunes, new HorarioDia
        {
            Cerrado = false,
            HoraApertura = "09:00",
            HoraCierre = "22:00"
        });

        restaurante.SetHorario(DiaSemana.Domingo, new HorarioDia
        {
            Cerrado = true,
            HoraApertura = null,
            HoraCierre = null
        });

        // Act
        context.Restaurantes.Add(restaurante);
        await context.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<RestauranteConHorarios>(restauranteId);
        rawData.Should().ContainKey("HorariosSemanal");

        var horarios = rawData["HorariosSemanal"] as Dictionary<string, object>;
        horarios.Should().NotBeNull();
        horarios.Should().ContainKey("Lunes");
        horarios.Should().ContainKey("Domingo");

        var lunes = horarios!["Lunes"] as Dictionary<string, object>;
        lunes.Should().NotBeNull();
        lunes!["Cerrado"].Should().Be(false);
        lunes["HoraApertura"].Should().Be("09:00");
        lunes["HoraCierre"].Should().Be("22:00");

        var domingo = horarios["Domingo"] as Dictionary<string, object>;
        domingo.Should().NotBeNull();
        domingo!["Cerrado"].Should().Be(true);
    }

    [Fact]
    public async Task Serialization_MapOfWithStringKey_ShouldStoreAsMapWithStringKeys()
    {
        // Arrange
        var appId = FirestoreTestFixture.GenerateId("app");
        using var context = _fixture.CreateContext<MapOfStringKeyTestDbContext>();

        var app = new AplicacionConConfiguraciones
        {
            Id = appId,
            Nombre = "MiApp",
            Version = "1.0.0"
        };

        app.SetConfiguracion("tema", new ConfiguracionValor
        {
            Nombre = "Tema Visual",
            Valor = "oscuro",
            Activo = true
        });

        app.SetConfiguracion("idioma", new ConfiguracionValor
        {
            Nombre = "Idioma",
            Valor = "es",
            Activo = true
        });

        // Act
        context.Aplicaciones.Add(app);
        await context.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<AplicacionConConfiguraciones>(appId);
        rawData.Should().ContainKey("Configuraciones");

        var configs = rawData["Configuraciones"] as Dictionary<string, object>;
        configs.Should().NotBeNull();
        configs.Should().ContainKey("tema");
        configs.Should().ContainKey("idioma");

        var tema = configs!["tema"] as Dictionary<string, object>;
        tema.Should().NotBeNull();
        tema!["Nombre"].Should().Be("Tema Visual");
        tema["Valor"].Should().Be("oscuro");
        tema["Activo"].Should().Be(true);
    }

    [Fact]
    public async Task Serialization_MapOfWithIntKey_ShouldStoreAsMapWithStringKeys()
    {
        // Arrange
        var almacenId = FirestoreTestFixture.GenerateId("alm");
        using var context = _fixture.CreateContext<MapOfIntKeyTestDbContext>();

        var almacen = new AlmacenConSecciones
        {
            Id = almacenId,
            Nombre = "Almacén Central",
            Ubicacion = "Zona Industrial"
        };

        almacen.SetSeccion(1, new ConfiguracionValor
        {
            Nombre = "Electrónica",
            Valor = "A1-A10",
            Activo = true
        });

        almacen.SetSeccion(2, new ConfiguracionValor
        {
            Nombre = "Hogar",
            Valor = "B1-B5",
            Activo = true
        });

        // Act
        context.Almacenes.Add(almacen);
        await context.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<AlmacenConSecciones>(almacenId);
        rawData.Should().ContainKey("Secciones");

        var secciones = rawData["Secciones"] as Dictionary<string, object>;
        secciones.Should().NotBeNull();
        // Las claves int se convierten a string
        secciones.Should().ContainKey("1");
        secciones.Should().ContainKey("2");

        var seccion1 = secciones!["1"] as Dictionary<string, object>;
        seccion1.Should().NotBeNull();
        seccion1!["Nombre"].Should().Be("Electrónica");
    }

    [Fact]
    public async Task Serialization_MapOfWithMutableDictionary_ShouldStoreAsMapWithStringKeys()
    {
        // Arrange
        var tiendaId = FirestoreTestFixture.GenerateId("tienda");
        using var context = _fixture.CreateContext<MapOfMutableTestDbContext>();

        var tienda = new TiendaConCategorias
        {
            Id = tiendaId,
            Nombre = "Tienda Express",
            Categorias = new Dictionary<string, ConfiguracionValor>
            {
                ["electronica"] = new ConfiguracionValor
                {
                    Nombre = "Electrónica",
                    Valor = "Pasillo 1",
                    Activo = true
                },
                ["ropa"] = new ConfiguracionValor
                {
                    Nombre = "Ropa",
                    Valor = "Pasillo 2",
                    Activo = false
                }
            }
        };

        // Act
        context.Tiendas.Add(tienda);
        await context.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<TiendaConCategorias>(tiendaId);
        rawData.Should().ContainKey("Categorias");

        var categorias = rawData["Categorias"] as Dictionary<string, object>;
        categorias.Should().NotBeNull();
        categorias.Should().ContainKey("electronica");
        categorias.Should().ContainKey("ropa");
    }

    [Fact]
    public async Task Serialization_MapOfEmpty_ShouldNotBeStored()
    {
        // Arrange
        var restauranteId = FirestoreTestFixture.GenerateId("rest");
        using var context = _fixture.CreateContext<MapOfEnumKeyTestDbContext>();

        var restaurante = new RestauranteConHorarios
        {
            Id = restauranteId,
            Nombre = "Restaurante Vacío",
            Direccion = "Sin Horarios"
            // No se agregan horarios
        };

        // Act
        context.Restaurantes.Add(restaurante);
        await context.SaveChangesAsync();

        // Assert - Empty maps should NOT be stored in Firestore (saves document size)
        var rawData = await GetDocumentRawData<RestauranteConHorarios>(restauranteId);
        rawData.Should().NotContainKey("HorariosSemanal", "empty maps should not be stored in Firestore");
    }

    [Fact]
    public async Task Serialization_MapOfWithDecimalValue_ShouldConvertToDouble()
    {
        // Arrange
        var hotelId = FirestoreTestFixture.GenerateId("hotel");
        using var context = _fixture.CreateContext<MapOfTipoHabitacionTestDbContext>();

        var hotel = new HotelConPrecios
        {
            Id = hotelId,
            Nombre = "Hotel Estrella",
            Estrellas = 5
        };

        hotel.SetPrecio(TipoHabitacion.Suite, new ConfiguracionPrecio
        {
            PrecioBase = 299.99m,
            PrecioTemporadaAlta = 399.99m,
            CapacidadMaxima = 4
        });

        // Act
        context.Hoteles.Add(hotel);
        await context.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<HotelConPrecios>(hotelId);
        rawData.Should().ContainKey("PreciosHabitaciones");

        var precios = rawData["PreciosHabitaciones"] as Dictionary<string, object>;
        precios.Should().ContainKey("Suite");

        var suite = precios!["Suite"] as Dictionary<string, object>;
        suite.Should().NotBeNull();

        // Firestore almacena decimales como double
        var precioBase = Convert.ToDouble(suite!["PrecioBase"]);
        precioBase.Should().BeApproximately(299.99, 0.01);
    }

    [Fact]
    public async Task Serialization_MapOfAutoDetected_ShouldStoreCorrectly()
    {
        // Arrange
        var productoId = FirestoreTestFixture.GenerateId("prod");
        using var context = _fixture.CreateContext<MapOfConventionTestDbContext>();

        var producto = new ProductoConTraducciones
        {
            Id = productoId,
            Codigo = "PROD-001",
            Precio = 99.99m
        };

        producto.SetTraduccion("es", new ConfiguracionValor
        {
            Nombre = "Nombre",
            Valor = "Producto Ejemplo",
            Activo = true
        });

        producto.SetTraduccion("en", new ConfiguracionValor
        {
            Nombre = "Name",
            Valor = "Example Product",
            Activo = true
        });

        // Act
        context.Productos.Add(producto);
        await context.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<ProductoConTraducciones>(productoId);
        rawData.Should().ContainKey("Traducciones");

        var traducciones = rawData["Traducciones"] as Dictionary<string, object>;
        traducciones.Should().NotBeNull();
        traducciones.Should().ContainKey("es");
        traducciones.Should().ContainKey("en");
    }

    // ========================================================================
    // CASO COMPLEJO 1: PROPIEDADES IGNORADAS EN ELEMENTOS
    // ========================================================================

    [Fact]
    public async Task Serialization_MapOfWithIgnoredProperties_ShouldNotStoreIgnoredProperties()
    {
        // Arrange
        var tiendaId = FirestoreTestFixture.GenerateId("tienda");
        using var context = _fixture.CreateContext<MapOfIgnoredPropertiesTestDbContext>();

        var tienda = new TiendaConPreciosCalculados
        {
            Id = tiendaId,
            Nombre = "Tienda Descuentos"
        };

        tienda.SetPrecio("electronica", new PrecioConDescuento
        {
            PrecioBase = 100m,
            PorcentajeDescuento = 10m
        });

        tienda.SetPrecio("ropa", new PrecioConDescuento
        {
            PrecioBase = 50m,
            PorcentajeDescuento = 20m
        });

        // Act
        context.Tiendas.Add(tienda);
        await context.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<TiendaConPreciosCalculados>(tiendaId);
        rawData.Should().ContainKey("PreciosPorCategoria");

        var precios = rawData["PreciosPorCategoria"] as Dictionary<string, object>;
        precios.Should().NotBeNull();
        precios.Should().ContainKey("electronica");
        precios.Should().ContainKey("ropa");

        var electronica = precios!["electronica"] as Dictionary<string, object>;
        electronica.Should().NotBeNull();
        electronica.Should().ContainKey("PrecioBase");
        electronica.Should().ContainKey("PorcentajeDescuento");
        // Las propiedades calculadas (PrecioFinal, Descripcion) NO deben estar presentes
        electronica.Should().NotContainKey("PrecioFinal", "propiedades ignoradas no deben serializarse");
        electronica.Should().NotContainKey("Descripcion", "propiedades ignoradas no deben serializarse");

        var precioBase = Convert.ToDouble(electronica!["PrecioBase"]);
        precioBase.Should().BeApproximately(100.0, 0.01);
    }

    // ========================================================================
    // CASO COMPLEJO 2: ARRAYOF DENTRO DE ELEMENTOS DE MAPOF
    // ========================================================================

    [Fact]
    public async Task Serialization_MapOfWithNestedArrayOf_ShouldStoreArrayInsideMapElement()
    {
        // Arrange
        var negocioId = FirestoreTestFixture.GenerateId("negocio");
        using var context = _fixture.CreateContext<MapOfWithArrayOfTestDbContext>();

        var negocio = new NegocioConHorariosFranjas
        {
            Id = negocioId,
            Nombre = "Restaurante El Sol",
            Tipo = "Restaurante"
        };

        negocio.SetHorario(DiaSemana.Lunes, new HorarioConFranjas
        {
            Cerrado = false,
            Nota = "Horario normal",
            Franjas =
            [
                new FranjaHoraria { Apertura = "09:00", Cierre = "14:00" },
                new FranjaHoraria { Apertura = "17:00", Cierre = "22:00" }
            ]
        });

        negocio.SetHorario(DiaSemana.Domingo, new HorarioConFranjas
        {
            Cerrado = true,
            Nota = "Cerrado domingos",
            Franjas = []
        });

        // Act
        context.Negocios.Add(negocio);
        await context.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<NegocioConHorariosFranjas>(negocioId);
        rawData.Should().ContainKey("Horarios");

        var horarios = rawData["Horarios"] as Dictionary<string, object>;
        horarios.Should().NotBeNull();
        horarios.Should().ContainKey("Lunes");
        horarios.Should().ContainKey("Domingo");

        // Verificar Lunes con franjas
        var lunes = horarios!["Lunes"] as Dictionary<string, object>;
        lunes.Should().NotBeNull();
        lunes!["Cerrado"].Should().Be(false);
        lunes["Nota"].Should().Be("Horario normal");
        lunes.Should().ContainKey("Franjas");

        var franjasLunes = ((IEnumerable<object>)lunes["Franjas"]).ToList();
        franjasLunes.Should().HaveCount(2);

        var primeraFranja = franjasLunes[0] as Dictionary<string, object>;
        primeraFranja.Should().NotBeNull();
        primeraFranja!["Apertura"].Should().Be("09:00");
        primeraFranja["Cierre"].Should().Be("14:00");

        // Verificar Domingo sin franjas (cerrado)
        var domingo = horarios["Domingo"] as Dictionary<string, object>;
        domingo.Should().NotBeNull();
        domingo!["Cerrado"].Should().Be(true);
        // Arrays vacíos no se almacenan
        domingo.Should().NotContainKey("Franjas", "arrays vacíos no se almacenan en Firestore");
    }

    // ========================================================================
    // CASO COMPLEJO 3: REFERENCES DENTRO DE ELEMENTOS DE MAPOF
    // ========================================================================

    [Fact]
    public async Task Serialization_MapOfWithReference_ShouldStoreDocumentReference()
    {
        // Arrange
        var empresaId = FirestoreTestFixture.GenerateId("empresa");
        var usuarioId = FirestoreTestFixture.GenerateId("usuario");
        using var context = _fixture.CreateContext<MapOfWithReferenceTestDbContext>();

        // Crear primero el usuario referenciado
        var usuario = new Usuario
        {
            Id = usuarioId,
            Nombre = "Juan Pérez",
            Email = "juan@empresa.com"
        };
        context.Usuarios.Add(usuario);

        var empresa = new EmpresaConAreas
        {
            Id = empresaId,
            RazonSocial = "Empresa S.A."
        };

        empresa.SetArea("TI", new ConfiguracionArea
        {
            Nombre = "Tecnología",
            Prioridad = 1,
            Responsable = usuario
        });

        empresa.SetArea("RRHH", new ConfiguracionArea
        {
            Nombre = "Recursos Humanos",
            Prioridad = 2,
            Responsable = null // Sin responsable asignado
        });

        // Act
        context.Empresas.Add(empresa);
        await context.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<EmpresaConAreas>(empresaId);
        rawData.Should().ContainKey("Areas");

        var areas = rawData["Areas"] as Dictionary<string, object>;
        areas.Should().NotBeNull();
        areas.Should().ContainKey("TI");
        areas.Should().ContainKey("RRHH");

        // Verificar área TI con responsable
        var areaTI = areas!["TI"] as Dictionary<string, object>;
        areaTI.Should().NotBeNull();
        areaTI!["Nombre"].Should().Be("Tecnología");
        areaTI["Prioridad"].Should().Be(1L);
        areaTI.Should().ContainKey("Responsable");

        // El responsable debe ser un DocumentReference
        var responsableTI = areaTI["Responsable"];
        responsableTI.Should().BeOfType<DocumentReference>();
        var docRef = (DocumentReference)responsableTI;
        docRef.Id.Should().Be(usuarioId);

        // Verificar área RRHH sin responsable
        var areaRRHH = areas["RRHH"] as Dictionary<string, object>;
        areaRRHH.Should().NotBeNull();
        areaRRHH!["Nombre"].Should().Be("Recursos Humanos");
        // Responsable null no debe guardarse
        areaRRHH.Should().NotContainKey("Responsable", "valores null no deben guardarse");
    }

    // ========================================================================
    // CASO COMPLEJO 4: ARRAYOF REFERENCES DENTRO DE ELEMENTOS DE MAPOF
    // ========================================================================

    [Fact]
    public async Task Serialization_MapOfWithArrayOfReferences_ShouldStoreArrayOfDocumentReferences()
    {
        // Arrange
        var fabricaId = FirestoreTestFixture.GenerateId("fabrica");
        var usuario1Id = FirestoreTestFixture.GenerateId("usuario");
        var usuario2Id = FirestoreTestFixture.GenerateId("usuario");
        var usuario3Id = FirestoreTestFixture.GenerateId("usuario");
        using var context = _fixture.CreateContext<MapOfWithArrayOfReferencesTestDbContext>();

        // Crear primero los usuarios referenciados
        var usuario1 = new Usuario { Id = usuario1Id, Nombre = "Ana García", Email = "ana@fabrica.com" };
        var usuario2 = new Usuario { Id = usuario2Id, Nombre = "Carlos López", Email = "carlos@fabrica.com" };
        var usuario3 = new Usuario { Id = usuario3Id, Nombre = "María Ruiz", Email = "maria@fabrica.com" };
        context.Usuarios.AddRange(usuario1, usuario2, usuario3);

        var fabrica = new FabricaConTurnos
        {
            Id = fabricaId,
            Nombre = "Fábrica Central"
        };

        fabrica.SetTurno(DiaSemana.Lunes, new TurnoTrabajo
        {
            Nombre = "Turno Mañana",
            HoraInicio = "06:00",
            HoraFin = "14:00",
            Empleados = [usuario1, usuario2]
        });

        fabrica.SetTurno(DiaSemana.Martes, new TurnoTrabajo
        {
            Nombre = "Turno Tarde",
            HoraInicio = "14:00",
            HoraFin = "22:00",
            Empleados = [usuario2, usuario3]
        });

        // Act
        context.Fabricas.Add(fabrica);
        await context.SaveChangesAsync();

        // Assert
        var rawData = await GetDocumentRawData<FabricaConTurnos>(fabricaId);
        rawData.Should().ContainKey("Turnos");

        var turnos = rawData["Turnos"] as Dictionary<string, object>;
        turnos.Should().NotBeNull();
        turnos.Should().ContainKey("Lunes");
        turnos.Should().ContainKey("Martes");

        // Verificar turno Lunes
        var turnoLunes = turnos!["Lunes"] as Dictionary<string, object>;
        turnoLunes.Should().NotBeNull();
        turnoLunes!["Nombre"].Should().Be("Turno Mañana");
        turnoLunes.Should().ContainKey("Empleados");

        var empleadosLunes = ((IEnumerable<object>)turnoLunes["Empleados"]).ToList();
        empleadosLunes.Should().HaveCount(2);

        // Cada empleado debe ser un DocumentReference
        empleadosLunes[0].Should().BeOfType<DocumentReference>();
        var ref1 = (DocumentReference)empleadosLunes[0];
        ref1.Id.Should().Be(usuario1Id);

        var ref2 = (DocumentReference)empleadosLunes[1];
        ref2.Id.Should().Be(usuario2Id);
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
