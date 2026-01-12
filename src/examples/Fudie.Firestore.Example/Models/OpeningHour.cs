namespace Fudie.Firestore.Example.Models;

/// <summary>
/// ComplexType example - used in an ArrayOf within Store.
/// Demonstrates Array of ComplexTypes pattern.
/// Detected automatically by convention (no Id property).
/// </summary>
public class OpeningHour
{
    public DayOfWeek Day { get; set; }
    public string OpenTime { get; set; } = string.Empty;
    public string CloseTime { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
}
