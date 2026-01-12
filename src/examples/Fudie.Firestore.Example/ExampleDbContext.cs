using Fudie.Firestore.EntityFrameworkCore.Infrastructure;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Builders;
using Fudie.Firestore.Example.Models;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.Example;

/// <summary>
/// Example DbContext showing how to configure Firestore provider.
/// Demonstrates the main features: Collection, SubCollection, Reference, ComplexType, and ArrayOf.
/// </summary>
public class ExampleDbContext(DbContextOptions<ExampleDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Root collection - stores
    /// </summary>
    public DbSet<Store> Stores => Set<Store>();

    /// <summary>
    /// Root collection - categories (referenced by Products)
    /// </summary>
    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // =============================================================================
        // CATEGORY - Root Collection (target for References)
        // =============================================================================
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(c => c.Id);
        });

        // =============================================================================
        // STORE - Root Collection with ComplexType, ArrayOf and SubCollection
        // =============================================================================
        modelBuilder.Entity<Store>(entity =>
        {
            entity.HasKey(s => s.Id);

            // ComplexType: Address - embedded object (auto-detected, but explicit for clarity)
            entity.ComplexProperty(s => s.Address);

            // ArrayOf ComplexTypes: OpeningHours - array of embedded objects
            entity.ArrayOf(s => s.OpeningHours);

            // SubCollection: Products - nested collection with Reference inside
            entity.SubCollection(s => s.Products, product =>
            {
                // Reference: Category - stored as DocumentReference in Firestore
                product.Reference(p => p.Category);
            });
        });
    }
}
