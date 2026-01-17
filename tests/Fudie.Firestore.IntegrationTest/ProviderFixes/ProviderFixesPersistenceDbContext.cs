using Fudie.Firestore.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.ProviderFixes;

/// <summary>
/// DbContext para tests de persistencia con QueryFilter.
/// </summary>
public class ProviderFixesPersistenceDbContext(DbContextOptions<ProviderFixesPersistenceDbContext> options, Guid tenantId)
    : DbContext(options)
{
    public Guid TenantId { get; } = tenantId;

    // Root collections (aggregates)
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Allergen> Allergens => Set<Allergen>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Global: use backing fields for all properties
        modelBuilder.UsePropertyAccessMode(PropertyAccessMode.Field);

        // Menu aggregate configuration
        modelBuilder.Entity<Menu>(entity =>
        {
            entity.HasQueryFilter(m => m.TenantId == TenantId);

            // ComplexType: DepositPolicy (embedded object, no Id)
            entity.ComplexProperty(m => m.DepositPolicy);

            // SubCollection: Categories under Menu
            entity.SubCollection(m => m.Categories, category =>
            {
                // ArrayOf embedded: CategoryItem contains Reference to MenuItem
                category.ArrayOf(c => c.Items, item =>
                {
                    item.Reference(i => i.MenuItem);
                });
            });
        });

        // MenuItem aggregate configuration
        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.HasQueryFilter(m => m.TenantId == TenantId);

            // ComplexTypes (embedded objects)
            entity.ComplexProperty(m => m.DepositOverride);
            entity.ComplexProperty(m => m.NutritionalInfo);

            // ArrayOf embedded: PriceOptions
            // Ignore computed properties (getters without backing fields)
            entity.ArrayOf(m => m.PriceOptions, option =>
            {
                option.Ignore(o => o.RequiresMarketPrice);
                option.Ignore(o => o.DisplayPrice);
            });            

            // ArrayOf Reference: Allergens (references to Allergen aggregate)
            entity.ArrayOf(m => m.Allergens).AsReferences();
        });

        // Allergen is simple - conventions handle it automatically
    }
}
