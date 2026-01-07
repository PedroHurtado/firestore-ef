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

// ============================================================================
// ENTIDADES PARA TEST DE AUTO-DETECCIÓN (CONVENTION)
// ============================================================================

/// <summary>
/// ComplexType simple para test de auto-detección.
/// NO tiene Id, así que debe detectarse como Embedded.
/// </summary>
public class DireccionOficina
{
    public required string Calle { get; set; }
    public required string Ciudad { get; set; }
    public required string CodigoPostal { get; set; }
}

/// <summary>
/// GeoPoint para test de auto-detección.
/// Tiene Lat/Lng y NO tiene Id, así que debe detectarse como GeoPoint.
/// </summary>
public class PuntoGeo
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

/// <summary>
/// Entidad para probar auto-detección de ArrayOf.
/// NO requiere configuración explícita - la convention debe detectar:
/// - Direcciones → ArrayOf Embedded
/// - Ubicaciones → ArrayOf GeoPoint
/// </summary>
public class Oficina
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public List<DireccionOficina> Direcciones { get; set; } = [];
    public List<PuntoGeo> Ubicaciones { get; set; } = [];
}

// ============================================================================
// ENTIDADES PARA SUBCOLLECTION CON ARRAYOF
// ============================================================================

/// <summary>
/// Subcollection que contiene ArrayOf Embedded (horarios de sucursal)
/// </summary>
public class Sucursal
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public required string Direccion { get; set; }
    public List<HorarioAtencion> Horarios { get; set; } = [];
}

/// <summary>
/// Subcollection que contiene ArrayOf GeoPoint (puntos de ruta)
/// </summary>
public class RutaDistribucion
{
    public string? Id { get; set; }
    public required string Codigo { get; set; }
    public required string Descripcion { get; set; }
    public List<PuntoGeo> Waypoints { get; set; } = [];
}

/// <summary>
/// Entidad raíz con subcollections que tienen ArrayOf
/// </summary>
public class Empresa
{
    public string? Id { get; set; }
    public required string RazonSocial { get; set; }
    public required string Ruc { get; set; }
    public List<Sucursal> Sucursales { get; set; } = [];
    public List<RutaDistribucion> Rutas { get; set; } = [];
}

// ============================================================================
// ENTIDADES PARA TEST COMPLETO DE ARRAYOF (SECCIÓN 2 DEL PLAN)
// ============================================================================

// --- ENTIDADES PRINCIPALES ---

public class CategoriaRestaurante
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
}

public class Plato
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public decimal Precio { get; set; }
}

public class Certificador
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public required string Pais { get; set; }
}

// --- COMPLEX TYPES (Value Objects) ---

public class Horario
{
    public required string Dia { get; set; }
    public TimeSpan Apertura { get; set; }
    public TimeSpan Cierre { get; set; }
}

public class Coordenada
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class Certificacion
{
    public required string Nombre { get; set; }
    public DateTime FechaObtencion { get; set; }
    public Certificador? Certificador { get; set; }
}

public class ItemMenu
{
    public required string Descripcion { get; set; }
    public Plato? Plato { get; set; }
}

public class SeccionMenu
{
    public required string Titulo { get; set; }
    public List<ItemMenu> Items { get; set; } = [];
}

public class Menu
{
    public required string Nombre { get; set; }
    public List<SeccionMenu> Secciones { get; set; } = [];
}

// --- ENTIDAD PRINCIPAL ---

public class Restaurante
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }

    // CASO 1: Array de Embedded simple
    public List<Horario> Horarios { get; set; } = [];

    // CASO 2: Array de GeoPoints
    public List<Coordenada> ZonasCobertura { get; set; } = [];

    // CASO 3: Array de References
    public List<CategoriaRestaurante> Categorias { get; set; } = [];

    // CASO 4: Array de Embedded con Reference dentro
    public List<Certificacion> Certificaciones { get; set; } = [];

    // CASO 5: Array de Embedded anidado
    public List<Menu> Menus { get; set; } = [];
}
