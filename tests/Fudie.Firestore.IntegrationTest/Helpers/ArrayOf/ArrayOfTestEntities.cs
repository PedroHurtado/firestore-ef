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
