using Fudie.Firestore.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Helpers.Plan;

/// <summary>
/// DbContext para tests de integración del agregado Plan.
/// Configura las convenciones de Firestore para:
/// - ComplexType: Money (con Currency anidado)
/// - ArrayOf: Features, ProviderConfigurations
/// </summary>
public class PlanDbContext(DbContextOptions<PlanDbContext> options) : DbContext(options)
{
    public DbSet<Plan> Plans => Set<Plan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UsePropertyAccessMode(PropertyAccessMode.Field);

        modelBuilder.Entity<Plan>(entity =>
        {
            // Ignore: propiedades computed (no backing fields)
            entity.Ignore(p => p.HasActiveProvider);            

            // ComplexType: Price (Money con Currency anidado)
            entity.ComplexProperty(p => p.Price, price =>
            {
                // Ignore: propiedades computed de Money
                price.Ignore(m => m.IsZero);
                price.Ignore(m => m.IsPositive);
                price.Ignore(m => m.IsNegative);

                price.ComplexProperty(m => m.Currency);
            });

            // ArrayOf: Features (usa backing field _features)
            entity.ArrayOf(p => p.Features, feature =>
            {
                // Ignore: propiedades computed de Feature
                feature.Ignore(f => f.IsValid);
                feature.Ignore(f => f.DisplayValue);
            });

            // ArrayOf: ProviderConfigurations (usa backing field _providerConfigurations)
            entity.ArrayOf(p => p.ProviderConfigurations);
        });
    }
}