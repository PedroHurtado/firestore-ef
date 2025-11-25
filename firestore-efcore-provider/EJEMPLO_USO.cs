using Microsoft.EntityFrameworkCore;
using Firestore.EntityFrameworkCore.Infrastructure;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Firestore.EntityFrameworkCore
{
    // === ENTIDADES ===
    
    [Table("productos")]
    public class Producto
    {
        public string? Id { get; set; }  // Nullable porque se genera automáticamente
        public required string Nombre { get; set; }  // Required porque es obligatorio
        public decimal Precio { get; set; }
        public int Stock { get; set; }
        public DateTime FechaCreacion { get; set; }
    }

    [Table("clientes")]
    public class Cliente
    {
        public string? Id { get; set; }  // Nullable porque se genera automáticamente
        public required string Nombre { get; set; }  // Required porque es obligatorio
        public required string Email { get; set; }  // Required porque es obligatorio
    }

    // === CONTEXTO ===
    
    public class MiContexto : DbContext
    {
        public DbSet<Producto> Productos { get; set; }
        public DbSet<Cliente> Clientes { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Opción 1: Solo ProjectId (usa Application Default Credentials)
            optionsBuilder.UseFirestore("mi-proyecto-firebase");

            // Opción 2: Con credenciales específicas
            // optionsBuilder.UseFirestore(
            //     "mi-proyecto-firebase",
            //     "path/to/credentials.json");

            // Opción 3: Con configuración completa
            // optionsBuilder.UseFirestore("mi-proyecto-firebase", firestore =>
            // {
            //     firestore
            //         .UseDatabaseId("(default)")
            //         .UseCredentials("path/to/credentials.json")
            //         .MaxRetryAttempts(5)
            //         .CommandTimeout(60)
            //         .EnableDetailedLogging();
            // });
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configuración opcional de entidades
            modelBuilder.Entity<Producto>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nombre).IsRequired();
            });

            modelBuilder.Entity<Cliente>(entity =>
            {
                entity.HasKey(e => e.Id);
            });
        }
    }

    // === EJEMPLOS DE USO ===
    
    public class Program
    {
        public static async Task Main(string[] args)
        {
            using var context = new MiContexto();

            // === INSERTAR ===
            var nuevoProducto = new Producto
            {
                // El ID se genera automáticamente si no se proporciona
                Nombre = "Laptop Dell",
                Precio = 999.99m,
                Stock = 10,
                FechaCreacion = DateTime.UtcNow
            };

            context.Productos.Add(nuevoProducto);
            await context.SaveChangesAsync();
            
            Console.WriteLine($"Producto creado con ID: {nuevoProducto.Id}");

            // === ACTUALIZAR ===
            var producto = await context.Productos.FindAsync("id-del-producto");
            if (producto != null)
            {
                producto.Stock -= 1;
                await context.SaveChangesAsync();
                Console.WriteLine("Stock actualizado");
            }

            // === ELIMINAR ===
            var productoAEliminar = await context.Productos.FindAsync("id-del-producto");
            if (productoAEliminar != null)
            {
                context.Productos.Remove(productoAEliminar);
                await context.SaveChangesAsync();
                Console.WriteLine("Producto eliminado");
            }

            // === TRANSACCIONES ===
            using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                context.Productos.Add(new Producto 
                { 
                    Nombre = "Mouse", 
                    Precio = 25.99m 
                });
                
                context.Clientes.Add(new Cliente 
                { 
                    Nombre = "Juan Pérez", 
                    Email = "juan@email.com" 
                });

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
                
                Console.WriteLine("Transacción completada");
            }
            catch
            {
                await transaction.RollbackAsync();
                Console.WriteLine("Transacción revertida");
            }
        }
    }

    // === USO CON DEPENDENCY INJECTION ===
    
    public class Startup
    {
        public static void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<MiContexto>(options =>
                options.UseFirestore(
                    "mi-proyecto-firebase",
                    "path/to/credentials.json",
                    firestore => firestore
                        .MaxRetryAttempts(3)
                        .CommandTimeout(30)));
        }
    }
}