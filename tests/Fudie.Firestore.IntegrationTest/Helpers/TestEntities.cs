namespace Fudie.Firestore.IntegrationTest.Helpers;

/// <summary>
/// Entidad simple para tests básicos de CRUD.
/// </summary>
public class Producto
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public decimal Precio { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Enum para tests con valores de enumeración.
/// </summary>
public enum EstadoPedido
{
    Pendiente,
    Confirmado,
    Enviado,
    Entregado,
    Cancelado
}

/// <summary>
/// Entidad raíz con subcollection para tests de relaciones.
/// </summary>
public class Cliente
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public required string Email { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    // Subcollection
    public List<Pedido> Pedidos { get; set; } = [];
}

/// <summary>
/// Entidad subcollection de Cliente.
/// Path: /clientes/{clienteId}/pedidos/{pedidoId}
/// </summary>
public class Pedido
{
    public string? Id { get; set; }
    public required string NumeroOrden { get; set; }
    public decimal Total { get; set; }
    public DateTime FechaPedido { get; set; } = DateTime.UtcNow;
    public EstadoPedido Estado { get; set; } = EstadoPedido.Pendiente;

    // Subcollection anidada
    public List<LineaPedido> Lineas { get; set; } = [];
}

/// <summary>
/// Entidad subcollection anidada de Pedido.
/// Path: /clientes/{clienteId}/pedidos/{pedidoId}/lineas/{lineaId}
/// </summary>
public class LineaPedido
{
    public string? Id { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public string? ProductoId { get; set; }

    // Navegación a Producto (referencia)
    public Producto? Producto { get; set; }
}

// ============================================================================
// ENTIDADES PARA TESTS DE QUERY (WHERE, ORDERBY, ETC.)
// ============================================================================

/// <summary>
/// Enum for Query tests.
/// </summary>
public enum Category
{
    Electronics,
    Clothing,
    Food,
    Home
}

/// <summary>
/// Entity designed for comprehensive Where clause testing.
/// Contains all common types to verify equality, comparison, null checks, etc.
/// </summary>
public class QueryTestEntity
{
    public string? Id { get; set; }
    public required string Name { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
    public Category Category { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Description { get; set; }  // Nullable for null tests
    public List<string> Tags { get; set; } = [];  // For array contains tests
}

// ============================================================================
// ENTIDADES PARA TESTS DE CONVENTIONS
// ============================================================================

/// <summary>
/// Record para GeoPoint. Detectado por estructura (Latitude + Longitude).
/// </summary>
public record GeoLocation(double Latitude, double Longitude);

/// <summary>
/// Record con GeoPoint anidado para tests de ComplexType.
/// </summary>
public record Coordenadas
{
    public double Altitud { get; init; }
    public required GeoLocation Posicion { get; init; }
}

/// <summary>
/// ComplexType con Coordenadas anidadas.
/// </summary>
public record Direccion
{
    public required string Calle { get; init; }
    public required string Ciudad { get; init; }
    public required string CodigoPostal { get; init; }
    public required Coordenadas Coordenadas { get; init; }
}

/// <summary>
/// Enum para tests de EnumToStringConvention.
/// </summary>
public enum CategoriaProducto
{
    Electronica,
    Ropa,
    Alimentos,
    Hogar
}

/// <summary>
/// Entidad con TODAS las conventions para tests completos.
/// - DecimalToDouble: Precio
/// - EnumToString: Categoria
/// - ListDecimalToDouble: Precios
/// - ListEnumToString: Tags
/// - ArrayConvention: Cantidades, Etiquetas
/// - GeoPoint: Ubicacion (directo)
/// - ComplexType: Direccion (con GeoPoint anidado)
/// - Timestamp: FechaCreacion
/// </summary>
public class ProductoCompleto
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public decimal Precio { get; set; }
    public CategoriaProducto Categoria { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public required GeoLocation Ubicacion { get; set; }
    public required Direccion Direccion { get; set; }
    public List<decimal> Precios { get; set; } = [];
    public List<CategoriaProducto> Tags { get; set; } = [];
    public List<int> Cantidades { get; set; } = [];
    public List<string> Etiquetas { get; set; } = [];
}
