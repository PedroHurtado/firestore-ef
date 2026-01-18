namespace Fudie.Firestore.IntegrationTest.Helpers.PrimitiveArrays;

/// <summary>
/// Enum para tests de List&lt;enum&gt;
/// </summary>
public enum Priority
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Entidad con List&lt;T&gt; para cada tipo primitivo soportado por Firestore.
///
/// Tipos Firestore:
/// - string
/// - number (int, long, double)
/// - boolean
/// - timestamp (DateTime)
/// - null (no aplica para List)
/// - geopoint (ya cubierto en ArrayOf)
/// - reference (ya cubierto en ArrayOf)
/// - map (ya cubierto en ArrayOf)
/// - array (List&lt;List&lt;T&gt;&gt;)
/// </summary>
public class PrimitiveArrayEntity
{
    public string? Id { get; set; }
    public required string Name { get; set; }

    // ========================================
    // TIPOS PRIMITIVOS BÁSICOS
    // ========================================

    /// <summary>List de strings</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>List de enteros</summary>
    public List<int> Quantities { get; set; } = [];

    /// <summary>List de longs</summary>
    public List<long> BigNumbers { get; set; } = [];

    /// <summary>List de doubles</summary>
    public List<double> Measurements { get; set; } = [];

    /// <summary>List de decimals (se convierten a double en Firestore)</summary>
    public List<decimal> Prices { get; set; } = [];

    /// <summary>List de booleans</summary>
    public List<bool> Flags { get; set; } = [];

    /// <summary>List de DateTimes (se convierten a Timestamp en Firestore)</summary>
    public List<DateTime> EventDates { get; set; } = [];

    /// <summary>List de enums (se convierten a string en Firestore)</summary>
    public List<Priority> Priorities { get; set; } = [];

    /// <summary>List de Guids (se convierten a string en Firestore)</summary>
    public List<Guid> ExternalIds { get; set; } = [];
}

/// <summary>
/// Entidad con List&lt;object&gt; para valores mixtos.
/// Firestore permite arrays con elementos de distintos tipos.
/// </summary>
public class MixedArrayEntity
{
    public string? Id { get; set; }
    public required string Name { get; set; }

    /// <summary>
    /// Array mixto: puede contener strings, numbers, booleans, etc.
    /// </summary>
    public List<object> MixedValues { get; set; } = [];
}

/// <summary>
/// Entidad con List&lt;List&lt;T&gt;&gt; para arrays anidados.
/// Firestore soporta arrays de arrays.
/// </summary>
public class NestedArrayEntity
{
    public string? Id { get; set; }
    public required string Name { get; set; }

    /// <summary>Array de arrays de strings</summary>
    public List<List<string>> StringMatrix { get; set; } = [];

    /// <summary>Array de arrays de ints</summary>
    public List<List<int>> NumberMatrix { get; set; } = [];

    /// <summary>Array de arrays de longs</summary>
    public List<List<long>> LongMatrix { get; set; } = [];

    /// <summary>Array de arrays de doubles</summary>
    public List<List<double>> DoubleMatrix { get; set; } = [];

    /// <summary>Array de arrays de decimals</summary>
    public List<List<decimal>> DecimalMatrix { get; set; } = [];

    /// <summary>Array de arrays de bools</summary>
    public List<List<bool>> BoolMatrix { get; set; } = [];

    /// <summary>Array de arrays de DateTimes</summary>
    public List<List<DateTime>> DateTimeMatrix { get; set; } = [];

    /// <summary>Array de arrays de enums</summary>
    public List<List<Priority>> EnumMatrix { get; set; } = [];

    /// <summary>Array de arrays de Guids</summary>
    public List<List<Guid>> GuidMatrix { get; set; } = [];
}