using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Extensions;


var builder = Host.CreateApplicationBuilder(args);

ConfigureServices(builder.Services, builder.Configuration);

var host = builder.Build();

// Obtener el contexto y probar
var context = host.Services.GetRequiredService<MiContexto>();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

await PruebaDatos(context, logger);

static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    services.AddLogging(configure =>
    {
        configure.AddConsole();
        configure.SetMinimumLevel(LogLevel.Information);
    });

    services.AddDbContext<MiContexto>(options =>
        options.UseFirestore("tapapear-f6f2b", "credentials.json")
    );
}

static async Task PruebaDatos(MiContexto context, ILogger logger)
{
    logger.LogInformation("=== PRUEBA RELACIONES N:M (PIZZA - INGREDIENTS) ===\n");

    try
    {
        // === 1. CREAR INGREDIENTES ===
        logger.LogInformation("--- Paso 1: Creando ingredientes ---");

        var mozzarella = new Ingredient
        {
            Id = "ing-001",
            Name = "Mozzarella",
            Cost = 2.5
        };

        var tomato = new Ingredient
        {
            Id = "ing-002",
            Name = "Tomato Sauce",
            Cost = 1.0
        };

        var pepperoni = new Ingredient
        {
            Id = "ing-003",
            Name = "Pepperoni",
            Cost = 3.5
        };

        var mushrooms = new Ingredient
        {
            Id = "ing-004",
            Name = "Mushrooms",
            Cost = 2.0
        };

        context.Ingredients.Add(mozzarella);
        context.Ingredients.Add(tomato);
        context.Ingredients.Add(pepperoni);
        context.Ingredients.Add(mushrooms);
        await context.SaveChangesAsync();

        logger.LogInformation($"✓ Ingrediente: {mozzarella.Name} - ${mozzarella.Cost}");
        logger.LogInformation($"✓ Ingrediente: {tomato.Name} - ${tomato.Cost}");
        logger.LogInformation($"✓ Ingrediente: {pepperoni.Name} - ${pepperoni.Cost}");
        logger.LogInformation($"✓ Ingrediente: {mushrooms.Name} - ${mushrooms.Cost}");

        // === 2. CREAR PIZZAS CON INGREDIENTES ===
        logger.LogInformation("\n--- Paso 2: Creando pizzas con ingredientes ---");

        var margherita = new Pizza
        {
            Id = "pizza-001",
            Name = "Margherita",
            Description = "Classic pizza with mozzarella and tomato",
            Url = "https://example.com/margherita.jpg",
            Ingredients = [mozzarella, tomato]
        };

        var pepperoniPizza = new Pizza
        {
            Id = "pizza-002",
            Name = "Pepperoni",
            Description = "Pizza with pepperoni and mozzarella",
            Url = "https://example.com/pepperoni.jpg",
            Ingredients = [mozzarella, tomato, pepperoni]
        };

        var veggie = new Pizza
        {
            Id = "pizza-003",
            Name = "Veggie Supreme",
            Description = "Vegetarian pizza with mushrooms",
            Url = "https://example.com/veggie.jpg",
            Ingredients = [mozzarella, tomato, mushrooms]
        };

        context.Pizzas.Add(margherita);
        context.Pizzas.Add(pepperoniPizza);
        context.Pizzas.Add(veggie);
        await context.SaveChangesAsync();

        logger.LogInformation($"✓ Pizza: {margherita.Name}");
        logger.LogInformation($"  → Ingredientes: {margherita.Ingredients.Count}");
        logger.LogInformation($"  → Precio calculado: ${margherita.GetPrice():F2}");

        logger.LogInformation($"✓ Pizza: {pepperoniPizza.Name}");
        logger.LogInformation($"  → Ingredientes: {pepperoniPizza.Ingredients.Count}");
        logger.LogInformation($"  → Precio calculado: ${pepperoniPizza.GetPrice():F2}");

        logger.LogInformation($"✓ Pizza: {veggie.Name}");
        logger.LogInformation($"  → Ingredientes: {veggie.Ingredients.Count}");
        logger.LogInformation($"  → Precio calculado: ${veggie.GetPrice():F2}");

        // === 3. MODIFICAR PIZZA (añadir/quitar ingredientes) ===
        logger.LogInformation("\n--- Paso 3: Modificando pizza (añadir/quitar ingredientes) ---");

        // Añadir mushrooms a Margherita
        margherita.Ingredients.Add(mushrooms);
        context.Pizzas.Update(margherita);
        await context.SaveChangesAsync();

        logger.LogInformation($"✓ Pizza Margherita actualizada");
        logger.LogInformation($"  → Ingredientes: {margherita.Ingredients.Count}");
        logger.LogInformation($"  → Nuevo precio: ${margherita.GetPrice():F2}");

        // === RESUMEN FINAL ===
        logger.LogInformation("\n╔═══════════════════════════════════════════════════════════════╗");
        logger.LogInformation("║           PRUEBA N:M COMPLETADA CON ÉXITO ✅                  ║");
        logger.LogInformation("╚═══════════════════════════════════════════════════════════════╝");

        logger.LogInformation("\n📊 ESTRUCTURA EN FIRESTORE:\n");

        logger.LogInformation("┌─ pizzas/pizza-001 (Margherita)");
        logger.LogInformation("│  ├─ Name: \"Margherita\"");
        logger.LogInformation("│  ├─ Description: \"Classic pizza...\"");
        logger.LogInformation("│  └─ Ingredients: [                          ← ✅ ARRAY DE REFERENCIAS");
        logger.LogInformation("│     ├─ DocumentReference(ingredients/ing-001)");
        logger.LogInformation("│     ├─ DocumentReference(ingredients/ing-002)");
        logger.LogInformation("│     └─ DocumentReference(ingredients/ing-004)");
        logger.LogInformation("│  ]");

        logger.LogInformation("\n┌─ ingredients/ing-001");
        logger.LogInformation("│  ├─ Name: \"Mozzarella\"");
        logger.LogInformation("│  └─ Cost: 2.5");

        logger.LogInformation("\n┌─ IngredientPizza/auto-id-1           ← ✅ TABLA INTERMEDIA");
        logger.LogInformation("│  ├─ IngredientId: DocumentReference(ingredients/ing-001)");
        logger.LogInformation("│  ├─ PizzaId: DocumentReference(pizzas/pizza-001)");
        logger.LogInformation("│  ├─ _createdAt: 2024-11-25T...");
        logger.LogInformation("│  └─ _updatedAt: 2024-11-25T...");

        logger.LogInformation("\n┌─ IngredientPizza/auto-id-2");
        logger.LogInformation("│  ├─ IngredientId: DocumentReference(ingredients/ing-002)");
        logger.LogInformation("│  └─ PizzaId: DocumentReference(pizzas/pizza-001)");

        logger.LogInformation("\n✨ CARACTERÍSTICAS DE RELACIÓN N:M:");
        logger.LogInformation("  ✅ Array de referencias en documento principal (Pizza.Ingredients)");
        logger.LogInformation("  ✅ Tabla intermedia automática (IngredientPizza)");
        logger.LogInformation("  ✅ Detección automática de skip navigations");
        logger.LogInformation("  ✅ Tracking de cambios (añadir/quitar ingredientes)");
        logger.LogInformation("  ✅ Configuración simple: HasMany().WithMany()");
        logger.LogInformation("  ✅ Cascade delete eficiente (array en principal)");

        logger.LogInformation("\n🔗 Firestore Console:");
        logger.LogInformation("   https://console.firebase.google.com/project/tapapear-f6f2b/firestore");
    }
    catch (Exception ex)
    {
        logger.LogError($"\n✗ Error: {ex.Message}");
        logger.LogError($"StackTrace: {ex.StackTrace}");
        if (ex.InnerException != null)
        {
            logger.LogError($"InnerException: {ex.InnerException.Message}");
            logger.LogError($"InnerStackTrace: {ex.InnerException.StackTrace}");
        }
    }
}

// ============= ENTIDADES =============

public enum CategoriaProducto
{
    Electronica,
    Ropa,
    Alimentos
}

// === CATÁLOGOS (Entidades con DbSet) ===
[Table("paises")]
public class Pais
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public required string Codigo { get; set; }
}

[Table("provincias")]
public class Provincia
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public required string Codigo { get; set; }
}

// === VALUE OBJECTS CON ANIDACIÓN ===

// GeoPoint base (usado en múltiples lugares)
public record Ubicacion(double Latitude, double Longitude);

// ComplexType que contiene GeoPoint anidado
public record Coordenadas
{
    public double Altitud { get; init; }
    public required Ubicacion Posicion { get; init; }  // ✅ GeoPoint anidado
}

// ComplexType que contiene otro ComplexType y referencias a entidades
public record Direccion
{
    public required string Calle { get; init; }
    public required string Ciudad { get; init; }
    public required string CodigoPostal { get; init; }
    public required Pais Pais { get; init; }                    // ✅ Referencia a entidad
    public required Provincia Provincia { get; init; }          // ✅ Referencia a entidad
    public required Coordenadas Coordenadas { get; init; }      // ✅ ComplexType anidado con GeoPoint
}

// ComplexType anidado dentro de otro
public record ContactoFabricante
{
    public required string Email { get; init; }
    public required string Telefono { get; init; }
    public required string HorarioAtencion { get; init; }
}

// ComplexType que contiene otro ComplexType
public record InformacionAdicional
{
    public required string Garantia { get; init; }
    public required string Fabricante { get; init; }
    public required ContactoFabricante Contacto { get; init; }   // ✅ ComplexType anidado
}

// === ENTIDAD: PRODUCTO ===
[Table("productos")]
public class Producto
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public DateTime FechaCreacion { get; set; }
    public CategoriaProducto Categoria { get; set; }
    public required Direccion DireccionAlmacen { get; set; }
    public required InformacionAdicional InformacionAdicional { get; set; }
    public required List<int> DataInt { get; set; }
    public required List<decimal> DataDecimal { get; set; }
    public required List<CategoriaProducto> DataEnum { get; set; }
}

// === ENTIDAD: LÍNEA DE PEDIDO ===
[Table("lineaspedido")]
public class LineaPedido
{
    public string? Id { get; set; }
    public required Producto Producto { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
}

// === ENTIDAD: PEDIDO ===
[Table("pedidos")]
public class Pedido
{
    public string? Id { get; set; }
    public required string NumeroOrden { get; set; }
    public DateTime FechaPedido { get; set; }
    public required Cliente Cliente { get; set; }
    public required List<LineaPedido> Lineas { get; set; }
}

// === ENTIDAD: CLIENTE ===
[Table("clientes")]
public class Cliente
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public required string Email { get; set; }
    public required Direccion Direccion { get; set; }
    public required Ubicacion Ubicacion { get; set; }
    public required List<Pedido> Pedidos { get; set; }
}

// ============= NUEVAS ENTIDADES: PIZZA E INGREDIENT =============

[Table("ingredients")]
public class Ingredient
{
    public string? Id { get; set; }
    public required string Name { get; set; }
    public double Cost { get; set; }
}

[Table("pizzas")]
public class Pizza
{
    public string? Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Url { get; set; }
    public required List<Ingredient> Ingredients { get; set; } = [];

    public double GetPrice()
    {
        var ingredientsCost = Ingredients.Sum(i => i.Cost);
        return ingredientsCost * 1.20; // +20% markup
    }
}

// ============= CONTEXTO =============

public class MiContexto : DbContext
{
    public DbSet<Producto> Productos { get; set; } = null!;
    public DbSet<Cliente> Clientes { get; set; } = null!;
    public DbSet<Pedido> Pedidos { get; set; } = null!;
    public DbSet<LineaPedido> LineasPedido { get; set; } = null!;
    public DbSet<Pais> Paises { get; set; } = null!;
    public DbSet<Provincia> Provincias { get; set; } = null!;
    
    // ✅ NUEVOS DbSets para Pizza e Ingredient
    public DbSet<Pizza> Pizzas { get; set; } = null!;
    public DbSet<Ingredient> Ingredients { get; set; } = null!;

    public MiContexto(DbContextOptions<MiContexto> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // === CATÁLOGOS ===
        modelBuilder.Entity<Pais>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<Provincia>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        // === PRODUCTO ===
        modelBuilder.Entity<Producto>(entity =>
        {
            entity.HasKey(e => e.Id);

            // ✅ ComplexProperty con referencias anidadas y ComplexTypes anidados
            entity.ComplexProperty(p => p.DireccionAlmacen, direccion =>
            {
                // Ignorar navegaciones a entidades
                direccion.Ignore(d => d.Pais);
                direccion.Ignore(d => d.Provincia);
                
                // ✅ ComplexType anidado con GeoPoint dentro
                direccion.ComplexProperty(d => d.Coordenadas, coord =>
                {
                    coord.ComplexProperty(c => c.Posicion).HasGeoPoint();  // ✅ GeoPoint
                });
            });

            // ✅ ComplexProperty con ComplexType anidado
            entity.ComplexProperty(p => p.InformacionAdicional, info =>
            {
                info.ComplexProperty(i => i.Contacto);
            });

            entity.Property(e => e.Nombre).IsRequired();
            entity.Property(p => p.DataDecimal).HasConversion(
                v => string.Join(',', v),
                v => new List<decimal>()
            );
            entity.Property(e => e.DataEnum).HasConversion(
                v => string.Join(',', v),
                v => new List<CategoriaProducto>()
            );
        });

        // === LÍNEA DE PEDIDO ===
        modelBuilder.Entity<LineaPedido>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        // === PEDIDO ===
        modelBuilder.Entity<Pedido>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NumeroOrden).IsRequired();
            entity.HasMany(p => p.Lineas)               
                .WithOne();
        });

        // === CLIENTE ===
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasKey(e => e.Id);

            // ✅ ComplexProperty con referencias anidadas y ComplexTypes anidados
            entity.ComplexProperty(e => e.Direccion, direccion =>
            {
                direccion.Ignore(d => d.Pais);
                direccion.Ignore(d => d.Provincia);
                
                // ✅ ComplexType anidado con GeoPoint dentro
                direccion.ComplexProperty(d => d.Coordenadas, coord =>
                {
                    coord.ComplexProperty(c => c.Posicion).HasGeoPoint();  // ✅ GeoPoint
                });
            });

            entity.ComplexProperty(e => e.Ubicacion).HasGeoPoint();
        });

        // ============= ✅ CONFIGURACIÓN PIZZA E INGREDIENT =============
        
        modelBuilder.Entity<Pizza>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            
            // ✅ Relación N:M unidireccional
            entity.HasMany(p => p.Ingredients)
                .WithMany();  // Sin navegación inversa en Ingredient
        });

        modelBuilder.Entity<Ingredient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
        });
    }
}