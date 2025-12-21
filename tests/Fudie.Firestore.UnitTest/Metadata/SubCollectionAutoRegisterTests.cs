using Firestore.EntityFrameworkCore.Metadata.Builders;

namespace Fudie.Firestore.UnitTest.Metadata;

/// <summary>
/// Tests para validar que SubCollection auto-registra entidades hijas en el modelo.
/// Esto permite usar SubCollection sin necesidad de DbSet para entidades que no son agregados raíz.
/// </summary>
public class SubCollectionAutoRegisterTests
{
    #region Test Entities

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
        public ICollection<LineaPedido> Lineas { get; set; } = new List<LineaPedido>();
    }

    private class LineaPedido
    {
        public string Id { get; set; } = default!;
        public string PedidoId { get; set; } = default!;
        public string ProductoNombre { get; set; } = default!;
        public int Cantidad { get; set; }
    }

    #endregion

    #region Test DbContexts

    /// <summary>
    /// Contexto que solo registra Cliente (agregado raíz), sin DbSet de Pedido.
    /// SubCollection debería auto-registrar Pedido.
    /// </summary>
    private class ClienteOnlyContext : DbContext
    {
        public DbSet<Cliente> Clientes => Set<Cliente>();
        // NO hay DbSet<Pedido> - debe auto-registrarse

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Cliente>(entity =>
            {
                entity.SubCollection(c => c.Pedidos);
            });
        }
    }

    /// <summary>
    /// Contexto que solo registra Cliente, con SubCollections anidadas.
    /// Tanto Pedido como LineaPedido deben auto-registrarse.
    /// </summary>
    private class ClienteWithNestedSubCollectionContext : DbContext
    {
        public DbSet<Cliente> Clientes => Set<Cliente>();
        // NO hay DbSet<Pedido> ni DbSet<LineaPedido>

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Cliente>(entity =>
            {
                entity.SubCollection(c => c.Pedidos)
                      .SubCollection(p => p.Lineas);
            });
        }
    }

    /// <summary>
    /// Contexto donde Pedido ya está registrado con DbSet.
    /// SubCollection no debe fallar aunque ya exista.
    /// </summary>
    private class ClienteWithExistingPedidoContext : DbContext
    {
        public DbSet<Cliente> Clientes => Set<Cliente>();
        public DbSet<Pedido> Pedidos => Set<Pedido>(); // Ya registrado

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Cliente>(entity =>
            {
                entity.SubCollection(c => c.Pedidos);
            });
        }
    }

    #endregion

    #region Tests

    [Fact]
    public void SubCollection_WhenEntityNotRegistered_ShouldAutoRegisterIt()
    {
        // Arrange & Act
        using var context = new ClienteOnlyContext();
        var model = context.Model;

        // Assert - Pedido fue auto-registrado
        var pedidoType = model.FindEntityType(typeof(Pedido));

        pedidoType.Should().NotBeNull("Pedido should be auto-registered when used in SubCollection");
    }

    [Fact]
    public void SubCollection_Nested_ShouldAutoRegisterAllLevels()
    {
        // Arrange & Act
        using var context = new ClienteWithNestedSubCollectionContext();
        var model = context.Model;

        // Assert - ambos tipos fueron auto-registrados
        model.FindEntityType(typeof(Pedido))
            .Should().NotBeNull("Pedido should be auto-registered as first level SubCollection");

        model.FindEntityType(typeof(LineaPedido))
            .Should().NotBeNull("LineaPedido should be auto-registered as nested SubCollection");
    }

    [Fact]
    public void SubCollection_WhenEntityAlreadyRegistered_ShouldNotFail()
    {
        // Arrange & Act - no debería fallar
        using var context = new ClienteWithExistingPedidoContext();
        var model = context.Model;

        // Assert
        model.FindEntityType(typeof(Pedido)).Should().NotBeNull();
    }

    #endregion
}
