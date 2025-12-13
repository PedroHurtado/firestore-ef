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
