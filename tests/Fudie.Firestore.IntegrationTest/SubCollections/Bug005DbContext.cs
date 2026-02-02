using Fudie.Firestore.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Fudie.Firestore.IntegrationTest.SubCollections;

/// <summary>
/// DbContext que replica EXACTAMENTE el escenario de producción del Bug 005.
/// Copia exacta de CustomerDbContext de producción.
/// </summary>
public class Bug005DbContext : DbContext
{
    private readonly Guid _tenantId;

    public Bug005DbContext(DbContextOptions<Bug005DbContext> options, Guid tenantId)
        : base(options)
    {
        _tenantId = tenantId;

        // IGUAL QUE EN PRODUCCIÓN
        ChangeTracker.Tracked += OnTracked;
        ChangeTracker.StateChanged += OnStateChanged;
    }

    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Allergen> Allergens => Set<Allergen>();

    private void OnTracked(object? sender, EntityTrackedEventArgs e)
    {
        Console.WriteLine($"[Tracked] {e.Entry.Entity.GetType().Name} → {e.Entry.State} | FromQuery: {e.FromQuery}");
    }

    private void OnStateChanged(object? sender, EntityStateChangedEventArgs e)
    {
        Console.WriteLine($"[StateChanged] {e.Entry.Entity.GetType().Name} | {e.OldState} → {e.NewState}");
    }

    public override EntityEntry Add(object entity)
    {
        return base.Add(entity);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //modelBuilder.UsePropertyAccessMode(PropertyAccessMode.Field);

        // Menu aggregate - ESTE ES EL ESCENARIO DEL BUG
        modelBuilder.Entity<Menu>(entity =>
        {
            entity.HasQueryFilter(m => m.TenantId == _tenantId);

            entity.ComplexProperty(m => m.DepositPolicy);

            entity.SubCollection(m => m.Categories, category =>
            {
                category.ArrayOf(c => c.Items, item =>
                {
                    item.Reference(i => i.MenuItem);
                });
            });
        });

        // MenuItem aggregate
        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.HasQueryFilter(m => m.TenantId == _tenantId);

            entity.Ignore(m => m.IsAvailableToday);
            entity.Ignore(m => m.CanBeOrdered);
            entity.Ignore(m => m.HasDepositOverride);
            entity.Ignore(m => m.HasActivePriceOption);

            entity.ComplexProperty(m => m.DepositOverride, complex =>
            {
                complex.Ignore(d => d!.AppliesToAllQuantities);
            });
            entity.ComplexProperty(m => m.NutritionalInfo);

            entity.ArrayOf(m => m.PriceOptions, priceOption =>
            {
                priceOption.Ignore(p => p.DisplayPrice);
                priceOption.Ignore(p => p.RequiresMarketPrice);
            });

            entity.ArrayOf(m => m.Allergens).AsReferences();
        });
    }
}