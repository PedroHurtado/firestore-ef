using Firestore.EntityFrameworkCore.Metadata;
using Firestore.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Fudie.Firestore.UnitTest.Metadata;

/// <summary>
/// Tests para validar que FirestoreModelValidator bloquea relaciones no soportadas.
/// Usa InMemory DbContext para crear modelos y llama directamente al método de validación.
/// </summary>
public class FirestoreModelValidatorRelationshipTests
{
    #region Test Entities

    private class Author
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public ICollection<Book> Books { get; set; } = new List<Book>();
    }

    private class Book
    {
        public string Id { get; set; } = default!;
        public string Title { get; set; } = default!;
        public ICollection<Author> Authors { get; set; } = new List<Author>();
    }

    private class Parent
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public ICollection<Child> Children { get; set; } = new List<Child>();
    }

    private class Child
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string ParentId { get; set; } = default!;
        public Parent Parent { get; set; } = default!;
    }

    private class Person
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public Passport? Passport { get; set; }
    }

    private class Passport
    {
        public string Id { get; set; } = default!;
        public string Number { get; set; } = default!;
        public string PersonId { get; set; } = default!;
        public Person Person { get; set; } = default!;
    }

    private class Articulo
    {
        public string Id { get; set; } = default!;
        public string Nombre { get; set; } = default!;
        public string CategoriaId { get; set; } = default!;
        public Categoria? Categoria { get; set; }
    }

    private class Categoria
    {
        public string Id { get; set; } = default!;
        public string Nombre { get; set; } = default!;
    }

    private class Cliente
    {
        public string Id { get; set; } = default!;
        public string Nombre { get; set; } = default!;
        public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
    }

    private class Pedido
    {
        public string Id { get; set; } = default!;
        public string ClienteId { get; set; } = default!;
        public decimal Total { get; set; }
    }

    #endregion

    #region Test DbContexts usando InMemory

    private class ManyToManyContext : DbContext
    {
        public DbSet<Author> Authors => Set<Author>();
        public DbSet<Book> Books => Set<Book>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Author>()
                .HasMany(a => a.Books)
                .WithMany(b => b.Authors);
        }
    }

    private class OneToManyWithoutSubCollectionContext : DbContext
    {
        public DbSet<Parent> Parents => Set<Parent>();
        public DbSet<Child> Children => Set<Child>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Parent>()
                .HasMany(p => p.Children)
                .WithOne(c => c.Parent)
                .HasForeignKey(c => c.ParentId);
        }
    }

    private class OneToOneWithoutReferenceContext : DbContext
    {
        public DbSet<Person> People => Set<Person>();
        public DbSet<Passport> Passports => Set<Passport>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Person>()
                .HasOne(p => p.Passport)
                .WithOne(pp => pp.Person)
                .HasForeignKey<Passport>(pp => pp.PersonId);
        }
    }

    private class ForeignKeyWithoutReferenceContext : DbContext
    {
        public DbSet<Articulo> Articulos => Set<Articulo>();
        public DbSet<Categoria> Categorias => Set<Categoria>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Articulo>()
                .HasOne(a => a.Categoria)
                .WithMany()
                .HasForeignKey(a => a.CategoriaId);
        }
    }

    private class SubCollectionContext : DbContext
    {
        public DbSet<Cliente> Clientes => Set<Cliente>();
        public DbSet<Pedido> Pedidos => Set<Pedido>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Cliente>(entity =>
            {
                entity.HasMany(c => c.Pedidos)
                    .WithOne()
                    .HasForeignKey(p => p.ClienteId);
            });

            // Marcar la navegación como SubCollection
            var clienteEntity = modelBuilder.Model.FindEntityType(typeof(Cliente));
            var navigation = clienteEntity?.FindNavigation("Pedidos") as IMutableNavigation;
            navigation?.SetIsSubCollection(true);
        }
    }

    private class DocumentReferenceContext : DbContext
    {
        public DbSet<Articulo> Articulos => Set<Articulo>();
        public DbSet<Categoria> Categorias => Set<Categoria>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Articulo>(entity =>
            {
                entity.HasOne(a => a.Categoria)
                    .WithMany()
                    .HasForeignKey(a => a.CategoriaId);
            });

            // Marcar la navegación como DocumentReference
            var articuloEntity = modelBuilder.Model.FindEntityType(typeof(Articulo));
            var navigation = articuloEntity?.FindNavigation("Categoria") as IMutableNavigation;
            navigation?.SetIsDocumentReference(true);
        }
    }

    #endregion

    #region Tests

    [Fact]
    public void ManyToMany_ShouldThrow_NotSupportedException()
    {
        // Arrange
        using var context = new ManyToManyContext();
        var model = context.Model;

        // Act & Assert
        var exception = Assert.Throws<NotSupportedException>(() =>
        {
            FirestoreModelValidator.ValidateNoUnsupportedRelationships(model);
        });

        exception.Message.Should().Contain("Many-to-Many");
        exception.Message.Should().Contain("not supported in Firestore");
    }

    [Fact]
    public void OneToMany_WithoutSubCollection_ShouldThrow_NotSupportedException()
    {
        // Arrange
        using var context = new OneToManyWithoutSubCollectionContext();
        var model = context.Model;

        // Act & Assert
        var exception = Assert.Throws<NotSupportedException>(() =>
        {
            FirestoreModelValidator.ValidateNoUnsupportedRelationships(model);
        });

        // The validator may detect either the collection side (One-to-Many) or the FK side (Foreign Key)
        // depending on which navigation is processed first
        exception.Message.Should().Contain("not supported in Firestore");
        (exception.Message.Contains("One-to-Many") || exception.Message.Contains("Foreign Key"))
            .Should().BeTrue("Expected error about One-to-Many or Foreign Key relationship");
    }

    [Fact]
    public void OneToOne_WithoutReference_ShouldThrow_NotSupportedException()
    {
        // Arrange
        using var context = new OneToOneWithoutReferenceContext();
        var model = context.Model;

        // Act & Assert
        var exception = Assert.Throws<NotSupportedException>(() =>
        {
            FirestoreModelValidator.ValidateNoUnsupportedRelationships(model);
        });

        exception.Message.Should().Contain("not supported in Firestore");
        exception.Message.Should().Contain("Reference");
    }

    [Fact]
    public void ForeignKey_WithoutReference_ShouldThrow_NotSupportedException()
    {
        // Arrange
        using var context = new ForeignKeyWithoutReferenceContext();
        var model = context.Model;

        // Act & Assert
        var exception = Assert.Throws<NotSupportedException>(() =>
        {
            FirestoreModelValidator.ValidateNoUnsupportedRelationships(model);
        });

        exception.Message.Should().Contain("not supported in Firestore");
        exception.Message.Should().Contain("Reference");
    }

    [Fact]
    public void Navigation_MarkedAsSubCollection_ShouldPass_Validation()
    {
        // Arrange
        using var context = new SubCollectionContext();
        var model = context.Model;

        // Act & Assert - Should not throw
        var ex = Record.Exception(() => FirestoreModelValidator.ValidateNoUnsupportedRelationships(model));
        ex.Should().BeNull("Navigation marked as SubCollection should pass validation");
    }

    [Fact]
    public void Navigation_MarkedAsDocumentReference_ShouldPass_Validation()
    {
        // Arrange
        using var context = new DocumentReferenceContext();
        var model = context.Model;

        // Act & Assert - Should not throw
        var ex = Record.Exception(() => FirestoreModelValidator.ValidateNoUnsupportedRelationships(model));
        ex.Should().BeNull("Navigation marked as DocumentReference should pass validation");
    }

    #endregion
}
