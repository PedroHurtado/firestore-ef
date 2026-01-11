using Firestore.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Helpers.ReferenceProjection;

/// <summary>
/// DbContext for testing Reference navigations in projections.
/// </summary>
public class ReferenceProjectionDbContext : DbContext
{
    public ReferenceProjectionDbContext(DbContextOptions<ReferenceProjectionDbContext> options) : base(options)
    {
    }

    // Entidades base
    public DbSet<Autor> Autores => Set<Autor>();
    public DbSet<Editorial> Editoriales => Set<Editorial>();
    public DbSet<Libro> Libros => Set<Libro>();

    // Entidades para nested FK
    public DbSet<PaisEntity> Paises => Set<PaisEntity>();
    public DbSet<AutorConPais> AutoresConPais => Set<AutorConPais>();
    public DbSet<LibroConAutorConPais> LibrosConAutorConPais => Set<LibroConAutorConPais>();

    // Entidades para dos FK del mismo tipo
    public DbSet<LibroConDosAutores> LibrosConDosAutores => Set<LibroConDosAutores>();

    // Entidades para ComplexType con FK
    public DbSet<LibroConComplexType> LibrosConComplexType => Set<LibroConComplexType>();

    // Entidades para SubCollection con FK
    public DbSet<Biblioteca> Bibliotecas => Set<Biblioteca>();

    // Entidades para test de DateTime en Root, SubCollection y ComplexType
    public DbSet<Evento> Eventos => Set<Evento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configuración de Libro con References
        modelBuilder.Entity<Libro>(entity =>
        {
            entity.Reference(e => e.Autor);
            entity.Reference(e => e.Editorial);
        });

        // Configuración de AutorConPais con Reference a Pais
        modelBuilder.Entity<AutorConPais>(entity =>
        {
            entity.Reference(e => e.PaisOrigen);
        });

        // Configuración de LibroConAutorConPais con nested FK
        modelBuilder.Entity<LibroConAutorConPais>(entity =>
        {
            entity.Reference(e => e.Autor);
        });

        // Configuración de LibroConDosAutores con dos FKs del mismo tipo
        modelBuilder.Entity<LibroConDosAutores>(entity =>
        {
            entity.Reference(e => e.AutorPrincipal);
            entity.Reference(e => e.AutorSecundario);
        });

        // Configuración de LibroConComplexType
        modelBuilder.Entity<LibroConComplexType>(entity =>
        {
            entity.ComplexProperty(e => e.DatosPublicacion);
            entity.Reference(e => e.Autor);
        });

        // Configuración de Biblioteca con SubCollection de Ejemplares
        modelBuilder.Entity<Biblioteca>(entity =>
        {
            entity.SubCollection(e => e.Ejemplares);
        });

        // Configuración de Ejemplar con FK a Libro
        modelBuilder.Entity<Ejemplar>(entity =>
        {
            entity.Reference(e => e.Libro);
        });

        // Configuración de Evento con ComplexType y SubCollection (ambos con DateTime)
        modelBuilder.Entity<Evento>(entity =>
        {
            entity.ComplexProperty(e => e.Metadatos);
            entity.SubCollection(e => e.Sesiones);
        });
    }
}
