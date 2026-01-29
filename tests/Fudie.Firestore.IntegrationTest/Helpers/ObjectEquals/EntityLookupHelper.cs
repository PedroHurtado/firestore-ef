using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Helpers.ObjectEquals;

/// <summary>
/// Helper que simula el patrón IEntityLookup del proyecto real.
/// El método GetRequiredAsync usa genéricos lo que causa que .Equals()
/// se resuelva a object.Equals(object) en lugar de TId.Equals(TId).
/// </summary>
public class EntityLookupHelper
{
    private readonly DbContext _context;

    public EntityLookupHelper(DbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Busca una entidad por Id usando el patrón genérico problemático.
    /// Este método reproduce el bug donde .Equals() no se puede traducir
    /// porque el compilador resuelve a object.Equals(object).
    /// </summary>
    public async Task<T> GetRequiredAsync<T, TId>(
        TId id,
        CancellationToken cancellationToken = default)
        where T : class, IEntity<TId>
        where TId : notnull
    {
        var query = _context.Set<T>().AsQueryable();

        // ESTE ES EL CÓDIGO PROBLEMÁTICO:
        // Con genéricos, el compilador resuelve .Equals(id) a object.Equals(object)
        // porque TId es desconocido en tiempo de compilación.
        // El provider no puede traducir object.Equals(object) a Firestore.
        var entity = await query.FirstOrDefaultAsync(e => e.Id.Equals(id), cancellationToken);

        return entity ?? throw new KeyNotFoundException($"{typeof(T).Name} with ID '{id}' not found.");
    }
}
