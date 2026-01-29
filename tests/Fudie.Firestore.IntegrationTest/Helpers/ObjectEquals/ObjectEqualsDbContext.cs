using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Helpers.ObjectEquals;

/// <summary>
/// DbContext para tests de traducción de object.Equals().
/// </summary>
public class ObjectEqualsDbContext : DbContext
{
    public ObjectEqualsDbContext(DbContextOptions<ObjectEqualsDbContext> options) : base(options)
    {
    }

    public DbSet<EntityWithStringId> EntitiesWithStringId => Set<EntityWithStringId>();
    public DbSet<EntityWithGuidId> EntitiesWithGuidId => Set<EntityWithGuidId>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EntityWithStringId>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
        });

        modelBuilder.Entity<EntityWithGuidId>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
        });
    }
}
