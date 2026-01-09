namespace Fudie.Firestore.IntegrationTest.Helpers.PrimaryKey;

/// <summary>
/// Entity with explicit primary key configuration (not following conventions).
/// Uses "Codigo" as PK instead of "Id" or "EntityNameId".
/// </summary>
public class ProductoConCodigo
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal Precio { get; set; }
}

/// <summary>
/// Entity following the "Id" convention for primary key.
/// Standard convention: property named "Id".
/// </summary>
public class ArticuloConId
{
    public string Id { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public int Stock { get; set; }
}

/// <summary>
/// Entity following the "{EntityName}Id" convention for primary key.
/// Standard convention: property named "CategoriaConEntityIdId".
/// </summary>
public class CategoriaConEntityId
{
    public string CategoriaConEntityIdId { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool Activa { get; set; }
}

/// <summary>
/// Entity with Guid primary key (explicit configuration).
/// Tests that non-string PKs work correctly.
/// </summary>
public class OrdenConGuid
{
    public Guid OrdenId { get; set; }
    public string Cliente { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public DateTime FechaCreacion { get; set; }
}

/// <summary>
/// Entity with int primary key (explicit configuration).
/// Tests that numeric PKs work correctly.
/// </summary>
public class ItemConNumero
{
    public int NumeroItem { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public decimal Valor { get; set; }
}

/// <summary>
/// Entity with explicit PK and subcollection.
/// Tests that Include operations work with custom PKs.
/// </summary>
public class ProveedorConCodigo
{
    public string CodigoProveedor { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public List<ContactoProveedor> Contactos { get; set; } = new();
}

/// <summary>
/// Subcollection entity with explicit PK.
/// </summary>
public class ContactoProveedor
{
    public string ContactoId { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
}
