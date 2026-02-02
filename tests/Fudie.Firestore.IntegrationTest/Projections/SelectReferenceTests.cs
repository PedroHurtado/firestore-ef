using Fudie.Firestore.IntegrationTest.Helpers;
using Fudie.Firestore.IntegrationTest.Helpers.ReferenceProjection;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Projections;

/// <summary>
/// Integration tests for Select with Reference navigations.
/// Tests projections that include referenced entities.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class SelectReferenceTests
{
    private readonly FirestoreTestFixture _fixture;

    public SelectReferenceTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    #region 1. Parte de FK proyectada a DTO

    [Fact]
    public async Task Select_PartialFkToDto_ReturnsProjectedFields()
    {
        // Arrange
        using var context = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];

        var autor = new Autor
        {
            Id = $"autor-dto-{uniqueId}",
            Nombre = "Gabriel Garcia Marquez",
            Pais = "Colombia",
            Biografia = "Escritor colombiano",
            AnioNacimiento = 1927
        };

        var libro = new Libro
        {
            Id = $"libro-dto-{uniqueId}",
            Titulo = "Cien Anos de Soledad",
            AnioPublicacion = 1967,
            Precio = 29.99m,
            Autor = autor
        };

        context.Autores.Add(autor);
        context.Libros.Add(libro);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var result = await readContext.Libros
            .Where(l => l.Id == libro.Id)
            .Select(l => new LibroConAutorParcialDto
            {
                Titulo = l.Titulo,
                AutorNombre = l.Autor!.Nombre
            })
            .FirstOrDefaultAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Titulo.Should().Be("Cien Anos de Soledad");
        result.AutorNombre.Should().Be("Gabriel Garcia Marquez");
    }

    #endregion

    #region 2. Parte de FK proyectada a Record

    [Fact]
    public async Task Select_PartialFkToRecord_ReturnsProjectedFields()
    {
        // Arrange
        using var context = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];

        var autor = new Autor
        {
            Id = $"autor-rec-{uniqueId}",
            Nombre = "Jorge Luis Borges",
            Pais = "Argentina",
            Biografia = "Escritor argentino",
            AnioNacimiento = 1899
        };

        var libro = new Libro
        {
            Id = $"libro-rec-{uniqueId}",
            Titulo = "Ficciones",
            AnioPublicacion = 1944,
            Precio = 24.99m,
            Autor = autor
        };

        context.Autores.Add(autor);
        context.Libros.Add(libro);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var result = await readContext.Libros
            .Where(l => l.Id == libro.Id)
            .Select(l => new LibroConAutorParcialRecord(l.Titulo, l.Autor!.Nombre))
            .FirstOrDefaultAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Titulo.Should().Be("Ficciones");
        result.AutorNombre.Should().Be("Jorge Luis Borges");
    }

    #endregion

    #region 3. FK completa proyectada a Record

    [Fact]
    public async Task Select_FullFkToRecord_ReturnsEntityWithFullReference()
    {
        // Arrange
        using var context = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];

        var autor = new Autor
        {
            Id = $"autor-full-rec-{uniqueId}",
            Nombre = "Pablo Neruda",
            Pais = "Chile",
            Biografia = "Poeta chileno",
            AnioNacimiento = 1904
        };

        var libro = new Libro
        {
            Id = $"libro-full-rec-{uniqueId}",
            Titulo = "Veinte Poemas de Amor",
            AnioPublicacion = 1924,
            Precio = 19.99m,
            Autor = autor
        };

        context.Autores.Add(autor);
        context.Libros.Add(libro);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var result = await readContext.Libros
            .Where(l => l.Id == libro.Id)
            .Select(l => new LibroConAutorCompletoRecord(l.Titulo, l.AnioPublicacion, l.Autor!))
            .FirstOrDefaultAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Titulo.Should().Be("Veinte Poemas de Amor");
        result.AnioPublicacion.Should().Be(1924);
        result.Autor.Should().NotBeNull();
        result.Autor.Nombre.Should().Be("Pablo Neruda");
        result.Autor.Pais.Should().Be("Chile");
        result.Autor.Biografia.Should().Be("Poeta chileno");
    }

    #endregion

    #region 4. FK completa proyectada a DTO

    [Fact]
    public async Task Select_FullFkToDto_ReturnsEntityWithFullReference()
    {
        // Arrange
        using var context = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];

        var autor = new Autor
        {
            Id = $"autor-full-dto-{uniqueId}",
            Nombre = "Octavio Paz",
            Pais = "Mexico",
            Biografia = "Poeta mexicano",
            AnioNacimiento = 1914
        };

        var libro = new Libro
        {
            Id = $"libro-full-dto-{uniqueId}",
            Titulo = "El Laberinto de la Soledad",
            AnioPublicacion = 1950,
            Precio = 22.99m,
            Autor = autor
        };

        context.Autores.Add(autor);
        context.Libros.Add(libro);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var result = await readContext.Libros
            .Where(l => l.Id == libro.Id)
            .Select(l => new LibroConAutorCompletoDto
            {
                Titulo = l.Titulo,
                AnioPublicacion = l.AnioPublicacion,
                Autor = l.Autor!
            })
            .FirstOrDefaultAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Titulo.Should().Be("El Laberinto de la Soledad");
        result.AnioPublicacion.Should().Be(1950);
        result.Autor.Should().NotBeNull();
        result.Autor.Nombre.Should().Be("Octavio Paz");
        result.Autor.Pais.Should().Be("Mexico");
    }

    #endregion

    #region 5. ComplexType con parte de complextype y parte de FK

    [Fact]
    public async Task Select_PartialComplexTypeAndPartialFk_ReturnsProjectedFields()
    {
        // Arrange
        using var context = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];

        var autor = new Autor
        {
            Id = $"autor-complex-{uniqueId}",
            Nombre = "Mario Vargas Llosa",
            Pais = "Peru",
            Biografia = "Escritor peruano",
            AnioNacimiento = 1936
        };

        var libro = new LibroConComplexType
        {
            Id = $"libro-complex-{uniqueId}",
            Titulo = "La Ciudad y los Perros",
            Precio = 27.99m,
            DatosPublicacion = new DatosPublicacion
            {
                AnioPublicacion = 1963,
                ISBN = "978-84-204-3816-5",
                NumeroPaginas = 432,
                Idioma = "Espanol"
            },
            Autor = autor
        };

        context.Autores.Add(autor);
        context.LibrosConComplexType.Add(libro);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var result = await readContext.LibrosConComplexType
            .Where(l => l.Id == libro.Id)
            .Select(l => new
            {
                l.Titulo,
                l.DatosPublicacion.ISBN,
                l.DatosPublicacion.NumeroPaginas,
                AutorNombre = l.Autor!.Nombre,
                AutorPais = l.Autor.Pais
            })
            .FirstOrDefaultAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Titulo.Should().Be("La Ciudad y los Perros");
        result.ISBN.Should().Be("978-84-204-3816-5");
        result.NumeroPaginas.Should().Be(432);
        result.AutorNombre.Should().Be("Mario Vargas Llosa");
        result.AutorPais.Should().Be("Peru");
    }

    #endregion

    #region 6. ComplexType completo incluyendo toda la FK

    [Fact]
    public async Task Select_FullComplexTypeAndFullFk_ReturnsCompleteEntities()
    {
        // Arrange
        using var context = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];

        var autor = new Autor
        {
            Id = $"autor-fullcomplex-{uniqueId}",
            Nombre = "Julio Cortazar",
            Pais = "Argentina",
            Biografia = "Escritor argentino",
            AnioNacimiento = 1914
        };

        var libro = new LibroConComplexType
        {
            Id = $"libro-fullcomplex-{uniqueId}",
            Titulo = "Rayuela",
            Precio = 25.99m,
            DatosPublicacion = new DatosPublicacion
            {
                AnioPublicacion = 1963,
                ISBN = "978-84-376-0340-2",
                NumeroPaginas = 600,
                Idioma = "Espanol"
            },
            Autor = autor
        };

        context.Autores.Add(autor);
        context.LibrosConComplexType.Add(libro);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var result = await readContext.LibrosConComplexType
            .Where(l => l.Id == libro.Id)
            .Select(l => new
            {
                l.Titulo,
                l.DatosPublicacion,
                l.Autor
            })
            .FirstOrDefaultAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Titulo.Should().Be("Rayuela");
        result.DatosPublicacion.Should().NotBeNull();
        result.DatosPublicacion.AnioPublicacion.Should().Be(1963);
        result.DatosPublicacion.ISBN.Should().Be("978-84-376-0340-2");
        result.DatosPublicacion.NumeroPaginas.Should().Be(600);
        result.DatosPublicacion.Idioma.Should().Be("Espanol");
        result.Autor.Should().NotBeNull();
        result.Autor!.Nombre.Should().Be("Julio Cortazar");
        result.Autor.Pais.Should().Be("Argentina");
    }

    #endregion

    #region 7. Nested FK (FK de FK)

    [Fact]
    public async Task Select_NestedFk_ReturnsAllNestedFields()
    {
        // Arrange
        using var context = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];

        var pais = new PaisEntity
        {
            Id = $"pais-{uniqueId}",
            Nombre = "Colombia",
            Continente = "America del Sur",
            Idioma = "Espanol"
        };

        var autor = new AutorConPais
        {
            Id = $"autor-nested-{uniqueId}",
            Nombre = "Gabriel Garcia Marquez",
            AnioNacimiento = 1927,
            PaisOrigen = pais
        };

        var libro = new LibroConAutorConPais
        {
            Id = $"libro-nested-{uniqueId}",
            Titulo = "El Amor en los Tiempos del Colera",
            AnioPublicacion = 1985,
            Autor = autor
        };

        context.Paises.Add(pais);
        context.AutoresConPais.Add(autor);
        context.LibrosConAutorConPais.Add(libro);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var result = await readContext.LibrosConAutorConPais
            .Where(l => l.Id == libro.Id)
            .Select(l => new
            {
                l.Titulo,
                AutorNombre = l.Autor!.Nombre,
                PaisNombre = l.Autor.PaisOrigen!.Nombre,
                PaisContinente = l.Autor.PaisOrigen.Continente
            })
            .FirstOrDefaultAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Titulo.Should().Be("El Amor en los Tiempos del Colera");
        result.AutorNombre.Should().Be("Gabriel Garcia Marquez");
        result.PaisNombre.Should().Be("Colombia");
        result.PaisContinente.Should().Be("America del Sur");
    }

    [Fact]
    public async Task Select_NestedFkFullEntities_ReturnsCompleteHierarchy()
    {
        // Arrange
        using var context = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];

        var pais = new PaisEntity
        {
            Id = $"pais-full-{uniqueId}",
            Nombre = "Mexico",
            Continente = "America del Norte",
            Idioma = "Espanol"
        };

        var autor = new AutorConPais
        {
            Id = $"autor-nested-full-{uniqueId}",
            Nombre = "Juan Rulfo",
            AnioNacimiento = 1917,
            PaisOrigen = pais
        };

        var libro = new LibroConAutorConPais
        {
            Id = $"libro-nested-full-{uniqueId}",
            Titulo = "Pedro Paramo",
            AnioPublicacion = 1955,
            Autor = autor
        };

        context.Paises.Add(pais);
        context.AutoresConPais.Add(autor);
        context.LibrosConAutorConPais.Add(libro);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var result = await readContext.LibrosConAutorConPais
            .Where(l => l.Id == libro.Id)
            .Select(l => new
            {
                l.Titulo,
                l.Autor
            })
            .FirstOrDefaultAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Titulo.Should().Be("Pedro Paramo");
        result.Autor.Should().NotBeNull();
        result.Autor!.Nombre.Should().Be("Juan Rulfo");
        // Shallow loading: PaisOrigen was not projected, so it should be null.
        // To get PaisOrigen, the user must explicitly project it: l.Autor.PaisOrigen
        result.Autor.PaisOrigen.Should().BeNull();
    }

    #endregion

    #region 8. Modelo con dos FK del mismo tipo

    [Fact]
    public async Task Select_TwoFksOfSameType_ReturnsBothReferences()
    {
        // Arrange
        using var context = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];

        var autorPrincipal = new Autor
        {
            Id = $"autor-principal-{uniqueId}",
            Nombre = "Elena Poniatowska",
            Pais = "Mexico",
            Biografia = "Escritora mexicana",
            AnioNacimiento = 1932
        };

        var autorSecundario = new Autor
        {
            Id = $"autor-secundario-{uniqueId}",
            Nombre = "Carlos Fuentes",
            Pais = "Mexico",
            Biografia = "Escritor mexicano",
            AnioNacimiento = 1928
        };

        var libro = new LibroConDosAutores
        {
            Id = $"libro-dosautores-{uniqueId}",
            Titulo = "Obra Colaborativa",
            AnioPublicacion = 1980,
            AutorPrincipal = autorPrincipal,
            AutorSecundario = autorSecundario
        };

        context.Autores.Add(autorPrincipal);
        context.Autores.Add(autorSecundario);
        context.LibrosConDosAutores.Add(libro);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var result = await readContext.LibrosConDosAutores
            .Where(l => l.Id == libro.Id)
            .Select(l => new
            {
                l.Titulo,
                AutorPrincipalNombre = l.AutorPrincipal!.Nombre,
                AutorSecundarioNombre = l.AutorSecundario!.Nombre
            })
            .FirstOrDefaultAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Titulo.Should().Be("Obra Colaborativa");
        result.AutorPrincipalNombre.Should().Be("Elena Poniatowska");
        result.AutorSecundarioNombre.Should().Be("Carlos Fuentes");
    }

    [Fact]
    public async Task Select_TwoFksOfSameType_FullEntities_ReturnsBothCompleteReferences()
    {
        // Arrange
        using var context = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];

        var autorPrincipal = new Autor
        {
            Id = $"autor-p-full-{uniqueId}",
            Nombre = "Isabel Allende",
            Pais = "Chile",
            Biografia = "Escritora chilena",
            AnioNacimiento = 1942
        };

        var autorSecundario = new Autor
        {
            Id = $"autor-s-full-{uniqueId}",
            Nombre = "Roberto Bolano",
            Pais = "Chile",
            Biografia = "Escritor chileno",
            AnioNacimiento = 1953
        };

        var libro = new LibroConDosAutores
        {
            Id = $"libro-dosaut-full-{uniqueId}",
            Titulo = "Antologia Chilena",
            AnioPublicacion = 1995,
            AutorPrincipal = autorPrincipal,
            AutorSecundario = autorSecundario
        };

        context.Autores.Add(autorPrincipal);
        context.Autores.Add(autorSecundario);
        context.LibrosConDosAutores.Add(libro);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var result = await readContext.LibrosConDosAutores
            .Where(l => l.Id == libro.Id)
            .Select(l => new
            {
                l.Titulo,
                l.AutorPrincipal,
                l.AutorSecundario
            })
            .FirstOrDefaultAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Titulo.Should().Be("Antologia Chilena");
        result.AutorPrincipal.Should().NotBeNull();
        result.AutorPrincipal!.Nombre.Should().Be("Isabel Allende");
        result.AutorPrincipal.Pais.Should().Be("Chile");
        result.AutorSecundario.Should().NotBeNull();
        result.AutorSecundario!.Nombre.Should().Be("Roberto Bolano");
        result.AutorSecundario.Pais.Should().Be("Chile");
    }

    #endregion

    #region 9. Subcollection con FK - parte root, parte subcollection, parte FK

    [Fact]
    public async Task Select_SubcollectionWithFk_ReturnsAllProjectedFields()
    {
        // Arrange
        using var context = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];

        var autor = new Autor
        {
            Id = $"autor-bib-{uniqueId}",
            Nombre = "Ernesto Sabato",
            Pais = "Argentina",
            Biografia = "Escritor argentino",
            AnioNacimiento = 1911
        };

        var libro = new Libro
        {
            Id = $"libro-bib-{uniqueId}",
            Titulo = "El Tunel",
            AnioPublicacion = 1948,
            Precio = 18.99m,
            Autor = autor
        };

        var biblioteca = new Biblioteca
        {
            Id = $"biblioteca-{uniqueId}",
            Nombre = "Biblioteca Nacional",
            Ciudad = "Buenos Aires",
            Ejemplares =
            [
                new Ejemplar
                {
                    Id = $"ejemplar-{uniqueId}",
                    CodigoBarras = "123456789",
                    Estado = "Disponible",
                    FechaAdquisicion = new DateTime(2020, 1, 15),
                    Libro = libro
                }
            ]
        };

        context.Autores.Add(autor);
        context.Libros.Add(libro);
        context.Bibliotecas.Add(biblioteca);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var result = await readContext.Bibliotecas
            .Where(b => b.Id == biblioteca.Id)
            .Select(b => new
            {
                // Parte del root
                b.Nombre,
                b.Ciudad,
                // Parte de subcollection con FK
                Ejemplares = b.Ejemplares.Select(e => new
                {
                    e.CodigoBarras,
                    e.Estado,
                    // Parte de FK desde subcollection
                    LibroTitulo = e.Libro!.Titulo,
                    LibroAutorNombre = e.Libro.Autor!.Nombre
                })
            })
            .FirstOrDefaultAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Nombre.Should().Be("Biblioteca Nacional");
        result.Ciudad.Should().Be("Buenos Aires");
        result.Ejemplares.Should().HaveCount(1);
        var ejemplar = result.Ejemplares.First();
        ejemplar.CodigoBarras.Should().Be("123456789");
        ejemplar.Estado.Should().Be("Disponible");
        ejemplar.LibroTitulo.Should().Be("El Tunel");
        ejemplar.LibroAutorNombre.Should().Be("Ernesto Sabato");
    }

    [Fact]
    public async Task Select_SubcollectionWithFullFk_ReturnsCompleteEntities()
    {
        // Arrange
        using var context = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];

        var autor = new Autor
        {
            Id = $"autor-bib-full-{uniqueId}",
            Nombre = "Horacio Quiroga",
            Pais = "Uruguay",
            Biografia = "Escritor uruguayo",
            AnioNacimiento = 1878
        };

        var libro = new Libro
        {
            Id = $"libro-bib-full-{uniqueId}",
            Titulo = "Cuentos de la Selva",
            AnioPublicacion = 1918,
            Precio = 16.99m,
            Autor = autor
        };

        var biblioteca = new Biblioteca
        {
            Id = $"biblioteca-full-{uniqueId}",
            Nombre = "Biblioteca Municipal",
            Ciudad = "Montevideo",
            Ejemplares =
            [
                new Ejemplar
                {
                    Id = $"ejemplar-full-{uniqueId}",
                    CodigoBarras = "987654321",
                    Estado = "Prestado",
                    FechaAdquisicion = new DateTime(2019, 6, 20),
                    Libro = libro
                }
            ]
        };

        context.Autores.Add(autor);
        context.Libros.Add(libro);
        context.Bibliotecas.Add(biblioteca);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var result = await readContext.Bibliotecas
            .Where(b => b.Id == biblioteca.Id)
            .Select(b => new
            {
                b.Nombre,
                Ejemplares = b.Ejemplares.Select(e => new
                {
                    e.CodigoBarras,
                    e.Libro
                })
            })
            .FirstOrDefaultAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Nombre.Should().Be("Biblioteca Municipal");
        result.Ejemplares.Should().HaveCount(1);
        var ejemplar = result.Ejemplares.First();
        ejemplar.CodigoBarras.Should().Be("987654321");
        ejemplar.Libro.Should().NotBeNull();
        ejemplar.Libro!.Titulo.Should().Be("Cuentos de la Selva");
        // Shallow loading: Autor was not projected, so it should be null.
        // To get Autor, the user must explicitly project it: e.Libro.Autor
        ejemplar.Libro.Autor.Should().BeNull();
    }

    #endregion

    #region Tests adicionales - Múltiples campos de FK

    [Fact]
    public async Task Select_MultipleFkFields_ReturnsAllRequestedFields()
    {
        // Arrange
        using var context = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];

        var autor = new Autor
        {
            Id = $"autor-multi-{uniqueId}",
            Nombre = "Alejo Carpentier",
            Pais = "Cuba",
            Biografia = "Escritor cubano",
            AnioNacimiento = 1904
        };

        var editorial = new Editorial
        {
            Id = $"edit-multi-{uniqueId}",
            Nombre = "Siglo XXI",
            Ciudad = "Mexico DF",
            Pais = "Mexico",
            AnioFundacion = 1965
        };

        var libro = new Libro
        {
            Id = $"libro-multi-{uniqueId}",
            Titulo = "El Reino de Este Mundo",
            AnioPublicacion = 1949,
            Precio = 21.99m,
            Autor = autor,
            Editorial = editorial
        };

        context.Autores.Add(autor);
        context.Editoriales.Add(editorial);
        context.Libros.Add(libro);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var result = await readContext.Libros
            .Where(l => l.Id == libro.Id)
            .Select(l => new
            {
                l.Titulo,
                l.AnioPublicacion,
                // Múltiples campos de Autor
                AutorNombre = l.Autor!.Nombre,
                AutorPais = l.Autor.Pais,
                AutorBiografia = l.Autor.Biografia,
                AutorAnioNacimiento = l.Autor.AnioNacimiento,
                // Múltiples campos de Editorial
                EditorialNombre = l.Editorial!.Nombre,
                EditorialCiudad = l.Editorial.Ciudad,
                EditorialPais = l.Editorial.Pais
            })
            .FirstOrDefaultAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Titulo.Should().Be("El Reino de Este Mundo");
        result.AutorNombre.Should().Be("Alejo Carpentier");
        result.AutorPais.Should().Be("Cuba");
        result.AutorBiografia.Should().Be("Escritor cubano");
        result.AutorAnioNacimiento.Should().Be(1904);
        result.EditorialNombre.Should().Be("Siglo XXI");
        result.EditorialCiudad.Should().Be("Mexico DF");
        result.EditorialPais.Should().Be("Mexico");
    }

    #endregion

    #region Tests adicionales - FK con NULL

    [Fact]
    public async Task Select_WithNullFk_ReturnsNullForReferenceFields()
    {
        // Arrange
        using var context = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];

        var libro = new Libro
        {
            Id = $"libro-null-{uniqueId}",
            Titulo = "Libro Sin Autor",
            AnioPublicacion = 2000,
            Precio = 15.99m,
            Autor = null,
            Editorial = null
        };

        context.Libros.Add(libro);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var result = await readContext.Libros
            .Where(l => l.Id == libro.Id)
            .Select(l => new
            {
                l.Titulo,
                l.Autor
            })
            .FirstOrDefaultAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Titulo.Should().Be("Libro Sin Autor");
        result.Autor.Should().BeNull();
    }

    #endregion

    #region Tests adicionales - Proyección a tipo anónimo con mezcla

    [Fact]
    public async Task Select_MixedProjection_RootScalarsFkScalarsAndFkEntity()
    {
        // Arrange
        using var context = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];

        var autor = new Autor
        {
            Id = $"autor-mix-{uniqueId}",
            Nombre = "Jose Donoso",
            Pais = "Chile",
            Biografia = "Escritor chileno",
            AnioNacimiento = 1924
        };

        var editorial = new Editorial
        {
            Id = $"edit-mix-{uniqueId}",
            Nombre = "Alfaguara",
            Ciudad = "Madrid",
            Pais = "Espana",
            AnioFundacion = 1964
        };

        var libro = new Libro
        {
            Id = $"libro-mix-{uniqueId}",
            Titulo = "El Obsceno Pajaro de la Noche",
            AnioPublicacion = 1970,
            Precio = 23.99m,
            Autor = autor,
            Editorial = editorial
        };

        context.Autores.Add(autor);
        context.Editoriales.Add(editorial);
        context.Libros.Add(libro);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var result = await readContext.Libros
            .Where(l => l.Id == libro.Id)
            .Select(l => new
            {
                // Root scalars
                l.Titulo,
                l.Precio,
                // FK scalars
                AutorNombre = l.Autor!.Nombre,
                // FK entity completa
                l.Editorial
            })
            .FirstOrDefaultAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Titulo.Should().Be("El Obsceno Pajaro de la Noche");
        result.Precio.Should().Be(23.99m);
        result.AutorNombre.Should().Be("Jose Donoso");
        result.Editorial.Should().NotBeNull();
        result.Editorial!.Nombre.Should().Be("Alfaguara");
        result.Editorial.Ciudad.Should().Be("Madrid");
    }

    #endregion

    #region 10. DateTime en Root, SubCollection y ComplexType

    [Fact]
    public async Task Select_DateTimeInRootSubCollectionAndComplexType_ReturnsAllDateTimesCorrectly()
    {
        // Arrange
        using var context = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];

        // DateTime values (Local time - must be converted to UTC by serializer)
        var fechaEvento = new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Local);
        var fechaMetadatos = new DateTime(2025, 5, 1, 14, 30, 0, DateTimeKind.Local);
        var fechaSesion = new DateTime(2025, 6, 15, 11, 0, 0, DateTimeKind.Local);

        var evento = new Evento
        {
            Id = $"evento-dt-{uniqueId}",
            Titulo = "Conferencia de Tecnologia",
            FechaEvento = fechaEvento,
            Metadatos = new MetadatosEvento
            {
                Organizador = "Tech Corp",
                FechaCreacionMetadatos = fechaMetadatos
            },
            Sesiones =
            [
                new Sesion
                {
                    Id = $"sesion-dt-{uniqueId}",
                    Nombre = "Keynote",
                    FechaHoraSesion = fechaSesion
                }
            ]
        };

        context.Eventos.Add(evento);
        await context.SaveChangesAsync();

        // Act
        using var readContext = _fixture.CreateContext<ReferenceProjectionDbContext>();
        var result = await readContext.Eventos
            .Where(e => e.Id == evento.Id)
            .Select(e => new
            {
                e.Titulo,
                // DateTime en Root
                e.FechaEvento,
                // DateTime en ComplexType
                e.Metadatos.FechaCreacionMetadatos,
                // DateTime en SubCollection
                Sesiones = e.Sesiones.Select(s => new
                {
                    s.Nombre,
                    s.FechaHoraSesion
                })
            })
            .FirstOrDefaultAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Titulo.Should().Be("Conferencia de Tecnologia");

        // Verify DateTime in Root (returned as local time)
        result.FechaEvento.Should().BeCloseTo(fechaEvento, TimeSpan.FromSeconds(1));

        // Verify DateTime in ComplexType (returned as local time)
        result.FechaCreacionMetadatos.Should().BeCloseTo(fechaMetadatos, TimeSpan.FromSeconds(1));

        // Verify DateTime in SubCollection (returned as local time)
        result.Sesiones.Should().HaveCount(1);
        var sesion = result.Sesiones.First();
        sesion.Nombre.Should().Be("Keynote");
        sesion.FechaHoraSesion.Should().BeCloseTo(fechaSesion, TimeSpan.FromSeconds(1));
    }

    #endregion
}
