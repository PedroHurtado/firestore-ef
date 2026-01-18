using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Helpers.PrimitiveArrays;

/// <summary>
/// DbContext para tests de List&lt;T&gt; de tipos primitivos.
/// Sin configuración especial - debe funcionar por convención.
/// </summary>
public class PrimitiveArrayTestDbContext(DbContextOptions<PrimitiveArrayTestDbContext> options)
    : DbContext(options)
{
    public DbSet<PrimitiveArrayEntity> PrimitiveArrays => Set<PrimitiveArrayEntity>();
}

/// <summary>
/// DbContext para tests de List&lt;object&gt; (arrays mixtos).
/// </summary>
public class MixedArrayTestDbContext(DbContextOptions<MixedArrayTestDbContext> options)
    : DbContext(options)
{
    public DbSet<MixedArrayEntity> MixedArrays => Set<MixedArrayEntity>();
}

/// <summary>
/// DbContext para tests de List&lt;List&lt;T&gt;&gt; (arrays anidados).
/// </summary>
public class NestedArrayTestDbContext(DbContextOptions<NestedArrayTestDbContext> options)
    : DbContext(options)
{
    public DbSet<NestedArrayEntity> NestedArrays => Set<NestedArrayEntity>();
}
