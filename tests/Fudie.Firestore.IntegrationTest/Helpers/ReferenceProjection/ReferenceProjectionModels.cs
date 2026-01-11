namespace Fudie.Firestore.IntegrationTest.Helpers.ReferenceProjection;

// ============================================================================
// ENTIDADES BASE PARA TESTS DE PROYECCIÓN CON REFERENCE
// ============================================================================

/// <summary>
/// Entity that is referenced by others (target of Reference).
/// </summary>
public class Autor
{
    public string Id { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Pais { get; set; } = string.Empty;
    public string Biografia { get; set; } = string.Empty;
    public int AnioNacimiento { get; set; }
}

/// <summary>
/// Entity that is referenced by others (target of Reference).
/// </summary>
public class Editorial
{
    public string Id { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Ciudad { get; set; } = string.Empty;
    public string Pais { get; set; } = string.Empty;
    public int AnioFundacion { get; set; }
}

/// <summary>
/// Root entity with Reference navigations for projection tests.
/// </summary>
public class Libro
{
    public string Id { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public int AnioPublicacion { get; set; }
    public decimal Precio { get; set; }

    // Reference navigation (single document reference)
    public Autor? Autor { get; set; }

    // Another Reference navigation
    public Editorial? Editorial { get; set; }
}

// ============================================================================
// DTOs Y RECORDS PARA PROYECCIONES
// ============================================================================

/// <summary>
/// DTO for projecting partial FK fields.
/// </summary>
public class LibroConAutorParcialDto
{
    public string Titulo { get; set; } = string.Empty;
    public string AutorNombre { get; set; } = string.Empty;
}

/// <summary>
/// Record for projecting partial FK fields.
/// </summary>
public record LibroConAutorParcialRecord(string Titulo, string AutorNombre);

/// <summary>
/// Record for projecting full FK.
/// </summary>
public record LibroConAutorCompletoRecord(string Titulo, int AnioPublicacion, Autor Autor);

/// <summary>
/// DTO for projecting full FK.
/// </summary>
public class LibroConAutorCompletoDto
{
    public string Titulo { get; set; } = string.Empty;
    public int AnioPublicacion { get; set; }
    public Autor Autor { get; set; } = null!;
}

// ============================================================================
// ENTIDADES PARA NESTED FK (FK de FK)
// ============================================================================

/// <summary>
/// Entity referenced by Autor (FK de FK).
/// </summary>
public class PaisEntity
{
    public string Id { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Continente { get; set; } = string.Empty;
    public string Idioma { get; set; } = string.Empty;
}

/// <summary>
/// Autor with nested FK to Pais.
/// </summary>
public class AutorConPais
{
    public string Id { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int AnioNacimiento { get; set; }

    // Nested FK
    public PaisEntity? PaisOrigen { get; set; }
}

/// <summary>
/// Libro with nested FK (Libro -> Autor -> Pais).
/// </summary>
public class LibroConAutorConPais
{
    public string Id { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public int AnioPublicacion { get; set; }

    // FK to AutorConPais (which has FK to Pais)
    public AutorConPais? Autor { get; set; }
}

// ============================================================================
// ENTIDADES PARA DOS FK DEL MISMO TIPO
// ============================================================================

/// <summary>
/// Entity with two References to the same type.
/// </summary>
public class LibroConDosAutores
{
    public string Id { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public int AnioPublicacion { get; set; }

    // Two FKs of the same type
    public Autor? AutorPrincipal { get; set; }
    public Autor? AutorSecundario { get; set; }
}

// ============================================================================
// ENTIDADES PARA COMPLEXTTYPE CON FK
// ============================================================================

/// <summary>
/// ComplexType for Libro.
/// </summary>
public record DatosPublicacion
{
    public int AnioPublicacion { get; init; }
    public string ISBN { get; init; } = string.Empty;
    public int NumeroPaginas { get; init; }
    public string Idioma { get; init; } = string.Empty;
}

/// <summary>
/// Libro with ComplexType and FK.
/// </summary>
public class LibroConComplexType
{
    public string Id { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public decimal Precio { get; set; }

    // ComplexType
    public DatosPublicacion DatosPublicacion { get; set; } = null!;

    // Reference FK
    public Autor? Autor { get; set; }
}

// ============================================================================
// ENTIDADES PARA SUBCOLLECTION CON FK
// ============================================================================

/// <summary>
/// Root entity for subcollection with FK tests.
/// </summary>
public class Biblioteca
{
    public string Id { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Ciudad { get; set; } = string.Empty;

    // SubCollection
    public List<Ejemplar> Ejemplares { get; set; } = [];
}

/// <summary>
/// SubCollection entity with FK.
/// </summary>
public class Ejemplar
{
    public string Id { get; set; } = string.Empty;
    public string CodigoBarras { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public DateTime FechaAdquisicion { get; set; }

    // FK from subcollection
    public Libro? Libro { get; set; }
}

// ============================================================================
// ENTIDADES PARA TEST DE DATETIME EN ROOT, SUBCOLLECTION Y COMPLEXTYPE
// ============================================================================

/// <summary>
/// ComplexType with DateTime field.
/// </summary>
public record MetadatosEvento
{
    public string Organizador { get; init; } = string.Empty;
    public DateTime FechaCreacionMetadatos { get; init; }
}

/// <summary>
/// SubCollection entity with DateTime field.
/// </summary>
public class Sesion
{
    public string Id { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public DateTime FechaHoraSesion { get; set; }
}

/// <summary>
/// Root entity with DateTime in root, ComplexType and SubCollection.
/// For testing DateTime UTC serialization at all levels.
/// </summary>
public class Evento
{
    public string Id { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;

    // DateTime en Root
    public DateTime FechaEvento { get; set; }

    // ComplexType con DateTime
    public MetadatosEvento Metadatos { get; set; } = null!;

    // SubCollection con DateTime
    public List<Sesion> Sesiones { get; set; } = [];
}
