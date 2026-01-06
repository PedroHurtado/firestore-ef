namespace Fudie.Firestore.IntegrationTest.Helpers.ArrayOf;

// ============================================================================
// ENTIDADES PARA ARRAYOF EMBEDDED
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
// ENTIDADES PARA ARRAYOF GEOPOINTS
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
// ENTIDADES PARA ARRAYOF REFERENCES
// ============================================================================

public class Etiqueta
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
}

public class ProductoConEtiquetas
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public List<Etiqueta> Etiquetas { get; set; } = [];
}

// ============================================================================
// ENTIDADES PARA ARRAYOF ANIDADO (3 NIVELES)
// ============================================================================

/// <summary>
/// Nivel 3: Elemento más interno
/// </summary>
public class Ingrediente
{
    public required string Nombre { get; set; }
    public required string Cantidad { get; set; }
}

/// <summary>
/// Nivel 2: Contiene lista de ingredientes
/// </summary>
public class Receta
{
    public required string Nombre { get; set; }
    public required string Instrucciones { get; set; }
    public List<Ingrediente> Ingredientes { get; set; } = [];
}

/// <summary>
/// Nivel 1: Contiene lista de recetas (que a su vez contienen ingredientes)
/// </summary>
public class Categoria
{
    public required string Nombre { get; set; }
    public List<Receta> Recetas { get; set; } = [];
}

/// <summary>
/// Entidad raíz con 3 niveles de anidamiento
/// </summary>
public class LibroCocina
{
    public string? Id { get; set; }
    public required string Titulo { get; set; }
    public List<Categoria> Categorias { get; set; } = [];
}

// ============================================================================
// ENTIDADES PARA COMPLEXTYPE CON LIST<GEOPOINT>
// ============================================================================

/// <summary>
/// ComplexType que contiene una lista de ubicaciones geográficas
/// </summary>
public class RutaEntrega
{
    public required string Nombre { get; set; }
    public required string Descripcion { get; set; }
    public List<UbicacionGeo> Puntos { get; set; } = [];
}

/// <summary>
/// Entidad con List<ComplexType> donde ComplexType tiene List<GeoPoint>
/// </summary>
public class EmpresaLogistica
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public List<RutaEntrega> Rutas { get; set; } = [];
}

// ============================================================================
// ENTIDADES PARA COMPLEXTYPE CON LIST<REFERENCES>
// ============================================================================

/// <summary>
/// ComplexType que contiene una lista de referencias a productos
/// </summary>
public class Seccion
{
    public required string Nombre { get; set; }
    public required int Orden { get; set; }
    public List<Etiqueta> EtiquetasDestacadas { get; set; } = [];
}

/// <summary>
/// Entidad con List<ComplexType> donde ComplexType tiene List<Reference>
/// </summary>
public class Catalogo
{
    public string? Id { get; set; }
    public required string Titulo { get; set; }
    public List<Seccion> Secciones { get; set; } = [];
}
