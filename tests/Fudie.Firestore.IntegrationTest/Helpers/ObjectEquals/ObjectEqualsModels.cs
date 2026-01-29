namespace Fudie.Firestore.IntegrationTest.Helpers.ObjectEquals;

// ============================================================================
// INTERFACES (simulan las interfaces del proyecto real)
// ============================================================================

/// <summary>
/// Marker interface for entities.
/// </summary>
public interface IEntity;

/// <summary>
/// Interface for entities with a typed Id.
/// </summary>
public interface IEntity<TId> : IEntity where TId : notnull
{
    TId Id { get; }
}

// ============================================================================
// ENTIDADES PARA TESTS DE object.Equals()
// ============================================================================

/// <summary>
/// Entidad con Id de tipo string.
/// Simula entidades como Allergen del proyecto real.
/// </summary>
public class EntityWithStringId : IEntity<string>
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Entidad con Id de tipo Guid.
/// Simula entidades como MenuItem del proyecto real.
/// </summary>
public class EntityWithGuidId : IEntity<Guid>
{
    public Guid Id { get; init; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
}
