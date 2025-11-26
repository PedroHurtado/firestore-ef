using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Metadata.Builders;

var builder = Host.CreateApplicationBuilder(args);

ConfigureServices(builder.Services, builder.Configuration);

var host = builder.Build();

var context = host.Services.GetRequiredService<MiContexto>();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

await PruebaSubcollections(context, logger);

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

static async Task PruebaSubcollections(MiContexto context, ILogger logger)
{
    logger.LogInformation("=== PRUEBA DE SUBCOLLECTIONS ===\n");

    try
    {
        // ============= ESCENARIO 1: UNA SUBCOLLECTION =============
        logger.LogInformation("╔═══════════════════════════════════════════════════════════════╗");
        logger.LogInformation("║       ESCENARIO 1: UNA SUBCOLLECTION (Cliente->Pedidos)       ║");
        logger.LogInformation("╚═══════════════════════════════════════════════════════════════╝\n");

        logger.LogInformation("--- Construyendo cliente con pedidos en memoria ---");
        
        var cliente1 = new Cliente
        {
            Id = "cli-001",
            Nombre = "Juan Pérez",
            Email = "juan@example.com",
            Pedidos = 
            [
                new Pedido
                {
                    Id = "ped-001",
                    NumeroOrden = "ORD-2024-001",
                    Total = 1500.00m,
                    FechaPedido = DateTime.UtcNow,
                    Estado = EstadoPedido.Pendiente,
                    Lineas = []
                },
                new Pedido
                {
                    Id = "ped-002",
                    NumeroOrden = "ORD-2024-002",
                    Total = 2300.50m,
                    FechaPedido = DateTime.UtcNow,
                    Estado = EstadoPedido.Confirmado,
                    Lineas = []
                }
            ]
        };

        logger.LogInformation($"✓ Cliente: {cliente1.Nombre}");
        logger.LogInformation($"  → Pedidos: {cliente1.Pedidos.Count}");
        foreach (var p in cliente1.Pedidos)
        {
            logger.LogInformation($"    • {p.NumeroOrden}: ${p.Total}");
        }

        logger.LogInformation("\n--- Guardando con un solo SaveChanges ---");
        context.Clientes.Add(cliente1);
        await context.SaveChangesAsync();

        logger.LogInformation("✅ Cliente y pedidos guardados automáticamente");
        logger.LogInformation($"  → Path cliente: /clientes/{cliente1.Id}");
        logger.LogInformation($"  → Path pedido 1: /clientes/{cliente1.Id}/pedidos/{cliente1.Pedidos[0].Id}");
        logger.LogInformation($"  → Path pedido 2: /clientes/{cliente1.Id}/pedidos/{cliente1.Pedidos[1].Id}");
        
        logger.LogInformation("\n✅ ESCENARIO 1 COMPLETADO\n");

        // ============= ESCENARIO 2: DOS SUBCOLLECTIONS ANIDADAS =============
        logger.LogInformation("╔═══════════════════════════════════════════════════════════════╗");
        logger.LogInformation("║   ESCENARIO 2: DOS SUBCOLLECTIONS (Cliente->Pedidos->Lineas)  ║");
        logger.LogInformation("╚═══════════════════════════════════════════════════════════════╝\n");

        logger.LogInformation("--- Creando productos (entidades raíz separadas) ---");
        var producto1 = new Producto
        {
            Id = "prod-001",
            Nombre = "Laptop HP",
            Precio = 1299.99m
        };

        var producto2 = new Producto
        {
            Id = "prod-002",
            Nombre = "Mouse Logitech",
            Precio = 29.99m
        };

        context.Productos.Add(producto1);
        context.Productos.Add(producto2);
        await context.SaveChangesAsync();

        logger.LogInformation($"✓ Producto 1: {producto1.Nombre} - ${producto1.Precio}");
        logger.LogInformation($"✓ Producto 2: {producto2.Nombre} - ${producto2.Precio}\n");

        logger.LogInformation("--- Construyendo cliente con pedidos y líneas anidadas en memoria ---");
        
        var cliente2 = new Cliente
        {
            Id = "cli-002",
            Nombre = "María García",
            Email = "maria@example.com",
            Pedidos = 
            [
                new Pedido
                {
                    Id = "ped-003",
                    NumeroOrden = "ORD-2024-003",
                    Total = 1359.97m,
                    FechaPedido = DateTime.UtcNow,
                    Estado = EstadoPedido.Pendiente,
                    Lineas = 
                    [
                        new LineaPedido
                        {
                            Id = "lin-001",
                            Producto = producto1,
                            Cantidad = 1,
                            PrecioUnitario = producto1.Precio
                        },
                        new LineaPedido
                        {
                            Id = "lin-002",
                            Producto = producto2,
                            Cantidad = 2,
                            PrecioUnitario = producto2.Precio
                        }
                    ]
                },
                new Pedido
                {
                    Id = "ped-004",
                    NumeroOrden = "ORD-2024-004",
                    Total = 59.98m,
                    FechaPedido = DateTime.UtcNow,
                    Estado = EstadoPedido.Confirmado,
                    Lineas = 
                    [
                        new LineaPedido
                        {
                            Id = "lin-003",
                            Producto = producto2,
                            Cantidad = 2,
                            PrecioUnitario = producto2.Precio
                        }
                    ]
                }
            ]
        };

        logger.LogInformation($"✓ Cliente: {cliente2.Nombre}");
        logger.LogInformation($"  → Pedidos: {cliente2.Pedidos.Count}");
        foreach (var pedido in cliente2.Pedidos)
        {
            logger.LogInformation($"    • {pedido.NumeroOrden}: ${pedido.Total}");
            logger.LogInformation($"      Líneas: {pedido.Lineas.Count}");
            foreach (var linea in pedido.Lineas)
            {
                logger.LogInformation($"        - {linea.Producto.Nombre} x{linea.Cantidad} = ${linea.Cantidad * linea.PrecioUnitario}");
            }
        }

        logger.LogInformation("\n--- Guardando con un solo SaveChanges ---");
        context.Clientes.Add(cliente2);
        await context.SaveChangesAsync();

        logger.LogInformation("✅ Cliente, pedidos y líneas guardados automáticamente");
        logger.LogInformation($"  → Path cliente: /clientes/{cliente2.Id}");
        logger.LogInformation($"  → Path pedido 1: /clientes/{cliente2.Id}/pedidos/{cliente2.Pedidos[0].Id}");
        logger.LogInformation($"    → Path línea 1: /clientes/{cliente2.Id}/pedidos/{cliente2.Pedidos[0].Id}/lineas/{cliente2.Pedidos[0].Lineas[0].Id}");
        logger.LogInformation($"    → Path línea 2: /clientes/{cliente2.Id}/pedidos/{cliente2.Pedidos[0].Id}/lineas/{cliente2.Pedidos[0].Lineas[1].Id}");
        logger.LogInformation($"  → Path pedido 2: /clientes/{cliente2.Id}/pedidos/{cliente2.Pedidos[1].Id}");
        logger.LogInformation($"    → Path línea 3: /clientes/{cliente2.Id}/pedidos/{cliente2.Pedidos[1].Id}/lineas/{cliente2.Pedidos[1].Lineas[0].Id}");

        logger.LogInformation("\n✅ ESCENARIO 2 COMPLETADO\n");

        // ============= RESUMEN =============
        logger.LogInformation("╔═══════════════════════════════════════════════════════════════╗");
        logger.LogInformation("║                   RESUMEN DE SUBCOLLECTIONS                   ║");
        logger.LogInformation("╚═══════════════════════════════════════════════════════════════╝");

        logger.LogInformation("\n📋 CONFIGURACIÓN UTILIZADA:\n");
        logger.LogInformation("modelBuilder.Entity<Cliente>(entity => {");
        logger.LogInformation("    entity.SubCollection(c => c.Pedidos)");
        logger.LogInformation("          .SubCollection(p => p.Lineas);");
        logger.LogInformation("});\n");

        logger.LogInformation("📁 ESTRUCTURA ESPERADA EN FIRESTORE:\n");
        logger.LogInformation("/clientes/cli-001");
        logger.LogInformation("  └─ /pedidos/ped-001");
        logger.LogInformation("  └─ /pedidos/ped-002");
        logger.LogInformation("\n/clientes/cli-002");
        logger.LogInformation("  └─ /pedidos/ped-003");
        logger.LogInformation("      └─ /lineas/lin-001");
        logger.LogInformation("      └─ /lineas/lin-002");
        logger.LogInformation("  └─ /pedidos/ped-004");
        logger.LogInformation("      └─ /lineas/lin-003\n");

        logger.LogInformation("🔑 FLUJO CORRECTO:\n");
        logger.LogInformation("✅ 1. Construir grafo completo de objetos en memoria");
        logger.LogInformation("✅ 2. Un solo context.Add(cliente)");
        logger.LogInformation("✅ 3. Un solo SaveChanges() que guarda todo automáticamente");
        logger.LogInformation("✅ 4. El provider detecta subcollections y construye paths jerárquicos\n");

        logger.LogInformation("🔗 Firestore Console:");
        logger.LogInformation("   https://console.firebase.google.com/project/tapapear-f6f2b/firestore");
        logger.LogInformation("\n⚠️  Verifica en la consola que los paths se hayan creado correctamente");
    }
    catch (Exception ex)
    {
        logger.LogError($"\n✗ Error: {ex.Message}");
        logger.LogError($"StackTrace: {ex.StackTrace}");
        if (ex.InnerException != null)
        {
            logger.LogError($"InnerException: {ex.InnerException.Message}");
        }
    }
}

// ============= ENUMS =============

public enum EstadoPedido
{
    Pendiente,
    Confirmado,
    Enviado,
    Entregado,
    Cancelado
}

// ============= ENTIDADES =============

/// <summary>
/// Entidad raíz - Colección principal
/// </summary>
public class Cliente
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public required string Email { get; set; }
    
    // Subcollection de primer nivel
    public required List<Pedido> Pedidos { get; set; }
}

/// <summary>
/// Entidad subcollection de Cliente
/// Path: /clientes/{clienteId}/pedidos/{pedidoId}
/// </summary>
public class Pedido
{
    public string? Id { get; set; }
    public required string NumeroOrden { get; set; }
    public decimal Total { get; set; }
    public DateTime FechaPedido { get; set; }
    public EstadoPedido Estado { get; set; }
    
    // Subcollection de segundo nivel (anidada)
    public required List<LineaPedido> Lineas { get; set; }
}

/// <summary>
/// Entidad subcollection de Pedido (anidada)
/// Path: /clientes/{clienteId}/pedidos/{pedidoId}/lineas/{lineaId}
/// </summary>
public class LineaPedido
{
    public string? Id { get; set; }
    public required Producto Producto { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
}

/// <summary>
/// Entidad raíz - Para referencia desde LineaPedido
/// </summary>
public class Producto
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public decimal Precio { get; set; }
}

// ============= CONTEXTO =============

public class MiContexto : DbContext
{
    // Entidades raíz
    public DbSet<Cliente> Clientes { get; set; } = null!;
    public DbSet<Producto> Productos { get; set; } = null!;
    
    // Entidades subcollection (necesitan DbSet para Collection Group Queries)
    public DbSet<Pedido> Pedidos { get; set; } = null!;
    public DbSet<LineaPedido> LineasPedido { get; set; } = null!;

    public MiContexto(DbContextOptions<MiContexto> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ============= CONFIGURACIÓN DE SUBCOLLECTIONS =============
        
        modelBuilder.Entity<Cliente>(entity =>
        {
            // Configuración encadenada: Cliente -> Pedidos -> Lineas
            entity.SubCollection(c => c.Pedidos)
                  .SubCollection(p => p.Lineas);
        });

        // Configuración adicional (validaciones, etc.)
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.Property(e => e.Nombre).IsRequired();
            entity.Property(e => e.Email).IsRequired();
        });

        modelBuilder.Entity<Pedido>(entity =>
        {
            entity.Property(e => e.NumeroOrden).IsRequired();
        });

        modelBuilder.Entity<Producto>(entity =>
        {
            entity.Property(e => e.Nombre).IsRequired();
        });
    }
}