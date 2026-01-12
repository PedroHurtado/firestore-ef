namespace Fudie.Firestore.Example.Models;

/// <summary>
/// Root Collection example - stored as a top-level collection in Firestore.
/// Categories can be referenced by Products using DocumentReference.
/// </summary>
public class Category
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
