namespace Fudie.Firestore.Example.Models;

/// <summary>
/// ComplexType example - stored as embedded object in the parent document.
/// ComplexTypes don't have their own collection, they are part of the parent entity.
/// Detected automatically by convention (no Id property).
/// </summary>
public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}
