namespace Fudie.Firestore.Example.Models;

/// <summary>
/// SubCollection example - stored as a subcollection under Store documents.
/// Firestore path: stores/{storeId}/products/{productId}
///
/// Demonstrates:
/// - SubCollection relationship with Store
/// - Reference to Category (DocumentReference)
/// - Array of strings (Tags)
/// </summary>
public class Product
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public bool IsAvailable { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Array of strings example - stored as an array field in Firestore.
    /// Auto-detected by convention.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Reference example - stored as DocumentReference in Firestore.
    /// Points to a Category document in the categories collection.
    /// Requires explicit configuration with entity.Reference().
    /// </summary>
    public Category? Category { get; set; }
}
