using Firestore.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Helpers.PrimaryKey;

/// <summary>
/// DbContext for testing different primary key configurations.
/// Tests explicit PKs, convention-based PKs (Id and EntityNameId), and Guid PKs.
/// </summary>
public class PrimaryKeyTestDbContext : DbContext
{
    public PrimaryKeyTestDbContext(DbContextOptions<PrimaryKeyTestDbContext> options) : base(options)
    {
    }

    public DbSet<ProductoConCodigo> ProductosConCodigo => Set<ProductoConCodigo>();
    public DbSet<ArticuloConId> ArticulosConId => Set<ArticuloConId>();
    public DbSet<CategoriaConEntityId> CategoriasConEntityId => Set<CategoriaConEntityId>();
    public DbSet<OrdenConGuid> OrdenesConGuid => Set<OrdenConGuid>();
    public DbSet<ItemConNumero> ItemsConNumero => Set<ItemConNumero>();
    public DbSet<ProveedorConCodigo> ProveedoresConCodigo => Set<ProveedorConCodigo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Explicit PK configuration - "Codigo" is NOT a convention name
        modelBuilder.Entity<ProductoConCodigo>(entity =>
        {
            entity.HasKey(e => e.Codigo);
            
        });

        // Convention PK: "Id" - NO explicit HasKey, let convention discover it
        modelBuilder.Entity<ArticuloConId>();

        // Convention PK: "{EntityName}Id" - NO explicit HasKey, let convention discover it
        modelBuilder.Entity<CategoriaConEntityId>();

        // Explicit PK with Guid type
        modelBuilder.Entity<OrdenConGuid>(entity =>
        {
            entity.HasKey(e => e.OrdenId);
        });

        // Explicit PK with int type
        modelBuilder.Entity<ItemConNumero>(entity =>
        {
            entity.HasKey(e => e.NumeroItem);
        });

        // Explicit PK with subcollection
        modelBuilder.Entity<ProveedorConCodigo>(entity =>
        {
            entity.HasKey(e => e.CodigoProveedor);
            entity.SubCollection(e => e.Contactos);
        });

        // Subcollection with explicit PK
        modelBuilder.Entity<ContactoProveedor>(entity =>
        {
            entity.HasKey(e => e.ContactoId);
        });
    }
}
