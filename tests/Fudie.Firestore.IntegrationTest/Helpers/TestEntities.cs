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
    public string? TenantId { get; set; }  // For multi-tenancy tests (Id + TenantId)
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

// ============================================================================
// ENTIDADES PARA TESTS DE QUERY FILTERS (MULTI-TENANCY)
// ============================================================================

/// <summary>
/// Entidad diseñada para tests de Query Filters globales (multi-tenancy).
/// El filtro de TenantId se aplica automáticamente en TenantDbContext.
/// </summary>
public class TenantEntity
{
    public string? Id { get; set; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
    public required string TenantId { get; set; }
}

// ============================================================================
// ENTIDADES PARA TESTS DE CONVENTIONS
// ============================================================================

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

// ============================================================================
// ENTIDADES PARA TESTS DE CONSTRUCTORES CON PARÁMETROS (Ciclo 3)
// ============================================================================

// ============================================================================
// ENTIDADES PARA TESTS DE TIPOS DE COLECCIÓN EN NAVEGACIONES (Ciclos 4, 5, 6)
// ============================================================================

/// <summary>
/// Base para pedidos en tests de tipos de colección.
/// </summary>
public abstract class PedidoBase
{
    public string? Id { get; set; }
    public required string NumeroOrden { get; set; }
    public decimal Total { get; set; }
    public DateTime FechaPedido { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Pedido para ClienteConList (Ciclo 4).
/// </summary>
public class PedidoList : PedidoBase { }

/// <summary>
/// Pedido para ClienteConICollection (Ciclo 5).
/// </summary>
public class PedidoICollection : PedidoBase { }

/// <summary>
/// Pedido para ClienteConHashSet (Ciclo 6).
/// Incluye Equals y GetHashCode para funcionamiento correcto del HashSet.
/// </summary>
public class PedidoHashSet : PedidoBase
{
    public override bool Equals(object? obj)
    {
        if (obj is PedidoHashSet other)
            return Id == other.Id;
        return false;
    }

    public override int GetHashCode()
    {
        return Id?.GetHashCode() ?? 0;
    }
}

/// <summary>
/// Cliente con List{T} para subcollection (Ciclo 4 - baseline, ya funciona).
/// </summary>
public class ClienteConList
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public required string Email { get; set; }

    // SubCollection como List<T>
    public List<PedidoList> Pedidos { get; set; } = [];
}

/// <summary>
/// Cliente con ICollection{T} para subcollection (Ciclo 5).
/// </summary>
public class ClienteConICollection
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public required string Email { get; set; }

    // SubCollection como ICollection<T>
    public ICollection<PedidoICollection> Pedidos { get; set; } = new List<PedidoICollection>();
}

/// <summary>
/// Cliente con HashSet{T} para subcollection (Ciclo 6).
/// </summary>
public class ClienteConHashSet
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public required string Email { get; set; }

    // SubCollection como HashSet<T>
    public HashSet<PedidoHashSet> Pedidos { get; set; } = [];
}

// ============================================================================
// ENTIDADES PARA TESTS DE CONSTRUCTORES CON PARÁMETROS (Ciclo 3)
// ============================================================================

/// <summary>
/// Entidad con constructor que recibe TODOS los parámetros.
/// No tiene constructor sin parámetros.
/// </summary>
public class EntityWithFullConstructor
{
    public EntityWithFullConstructor(string id, string nombre, decimal precio)
    {
        Id = id;
        Nombre = nombre;
        Precio = precio;
    }

    public string Id { get; private set; }
    public string Nombre { get; private set; }
    public decimal Precio { get; private set; }
}

/// <summary>
/// Entidad con constructor que recibe SOLO ALGUNAS propiedades.
/// Las demás propiedades tienen setters públicos.
/// </summary>
public class EntityWithPartialConstructor
{
    public EntityWithPartialConstructor(string id, string nombre)
    {
        Id = id;
        Nombre = nombre;
    }

    public string Id { get; private set; }
    public string Nombre { get; private set; }

    // Estas propiedades NO están en el constructor
    public decimal Precio { get; set; }
    public bool Activo { get; set; }
}

/// <summary>
/// Record (inmutable) - Solo constructor, propiedades son init-only.
/// </summary>
public record EntityRecord(string Id, string Nombre, decimal Precio);
