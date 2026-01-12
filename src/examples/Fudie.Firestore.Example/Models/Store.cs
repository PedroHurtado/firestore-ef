namespace Fudie.Firestore.Example.Models;

/// <summary>
/// Root Collection example - stored as a top-level collection in Firestore.
/// Firestore path: stores/{storeId}
///
/// Demonstrates:
/// - Root collection entity
/// - ComplexType (Address) - embedded object
/// - SubCollection (Products) - nested collection
/// - ArrayOf ComplexTypes (OpeningHours)
/// </summary>
public class Store
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// ComplexType example - stored as embedded object in the Store document.
    /// Auto-detected by convention (Address has no Id property).
    /// </summary>
    public Address Address { get; set; } = new();

    /// <summary>
    /// ArrayOf ComplexTypes example - stored as array of objects in Firestore.
    /// Auto-detected by convention (OpeningHour has no Id property).
    /// </summary>
    public List<OpeningHour> OpeningHours { get; set; } = [];

    /// <summary>
    /// SubCollection example - stored as a subcollection under this Store document.
    /// Firestore path: stores/{storeId}/products
    /// Requires explicit configuration with entity.SubCollection().
    /// </summary>
    public List<Product> Products { get; set; } = [];
}
