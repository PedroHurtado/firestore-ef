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
    logger.LogInformation("=== PRUEBA COMPLETA - DETECCIÓN AUTOMÁTICA DE REFERENCIAS ===\n");

    try
    {
        // === 1. CREAR CATÁLOGOS (Pais y Provincia) ===
        logger.LogInformation("--- Paso 1: Creando catálogos (País y Provincia) ---");

        var espana = new Pais
        {
            Id = "ES",
            Nombre = "España",
            Codigo = "ESP"
        };

        var andalucia = new Provincia
        {
            Id = "AN",
            Nombre = "Andalucía",
            Codigo = "AN"
        };

        var madrid = new Provincia
        {
            Id = "MD",
            Nombre = "Madrid",
            Codigo = "MD"
        };

        context.Paises.Add(espana);
        context.Provincias.Add(andalucia);
        context.Provincias.Add(madrid);
        await context.SaveChangesAsync();

        logger.LogInformation($"✓ País creado: {espana.Nombre} ({espana.Id})");
        logger.LogInformation($"✓ Provincia creada: {andalucia.Nombre} ({andalucia.Id})");
        logger.LogInformation($"✓ Provincia creada: {madrid.Nombre} ({madrid.Id})");

        // === 2. CREAR CLIENTE CON DIRECCIÓN QUE TIENE REFERENCIAS Y COMPLEXTYPES ANIDADOS ===
        logger.LogInformation("\n--- Paso 2: Creando cliente con dirección compleja ---");

        var coordenadasCliente = new Coordenadas
        {
            Altitud = 667.0,
            Posicion = new Ubicacion(40.4168, -3.7038)  // ✅ GeoPoint anidado
        };

        var direccionCliente = new Direccion
        {
            Calle = "Calle Principal 123",
            Ciudad = "Madrid",
            CodigoPostal = "28001",
            Pais = espana,                      // ✅ Referencia a entidad (DbSet) - Automático
            Provincia = madrid,                 // ✅ Referencia a entidad (DbSet) - Automático
            Coordenadas = coordenadasCliente    // ✅ ComplexType con GeoPoint dentro
        };

        var cliente = new Cliente
        {
            Id = "cliente-001",
            Nombre = "Juan Pérez",
            Email = "juan@example.com",
            Direccion = direccionCliente,
            Ubicacion = new Ubicacion(40.4168, -3.7038),
            Pedidos = new List<Pedido>()
        };

        context.Clientes.Add(cliente);
        await context.SaveChangesAsync();

        logger.LogInformation($"✓ Cliente creado: {cliente.Id} - {cliente.Nombre}");
        logger.LogInformation($"  → Dirección.Pais: {espana.Id} (DocumentReference - automático)");
        logger.LogInformation($"  → Dirección.Provincia: {madrid.Id} (DocumentReference - automático)");
        logger.LogInformation($"  → Dirección.Coordenadas.Posicion: GeoPoint (anidado)");
        logger.LogInformation($"  → Dirección.Coordenadas.Altitud: {coordenadasCliente.Altitud}m");

        // === 3. CREAR PRODUCTOS CON COMPLEXTYPES ANIDADOS ===
        logger.LogInformation("\n--- Paso 3: Creando productos con dirección compleja ---");

        var coordenadasAlmacen = new Coordenadas
        {
            Altitud = 180.0,
            Posicion = new Ubicacion(38.2366, -1.4206)  // ✅ GeoPoint anidado
        };

        var direccionAlmacen = new Direccion
        {
            Calle = "Polígono Industrial 5",
            Ciudad = "Cieza",
            CodigoPostal = "30530",
            Pais = espana,
            Provincia = andalucia,
            Coordenadas = coordenadasAlmacen    // ✅ ComplexType con GeoPoint dentro
        };

        var infoAdicional = new InformacionAdicional
        {
            Garantia = "2 años",
            Fabricante = "Dell Inc.",
            Contacto = new ContactoFabricante    // ✅ ComplexType dentro de ComplexType
            {
                Email = "support@dell.com",
                Telefono = "+1-800-DELL",
                HorarioAtencion = "Lun-Vie 9:00-18:00"
            }
        };

        var producto1 = new Producto
        {
            Id = "prod-001",
            Nombre = "Laptop Dell",
            Precio = 999.99m,
            Stock = 10,
            FechaCreacion = DateTime.UtcNow,
            Categoria = CategoriaProducto.Electronica,
            DireccionAlmacen = direccionAlmacen,
            InformacionAdicional = infoAdicional,
            DataInt = [1, 2, 3],
            DataDecimal = [1.1m, 1.2m, 1.3m],
            DataEnum = [CategoriaProducto.Electronica, CategoriaProducto.Ropa]
        };

        var producto2 = new Producto
        {
            Id = "prod-002",
            Nombre = "Mouse Logitech",
            Precio = 25.99m,
            Stock = 50,
            FechaCreacion = DateTime.UtcNow,
            Categoria = CategoriaProducto.Electronica,
            DireccionAlmacen = direccionAlmacen,
            InformacionAdicional = new InformacionAdicional
            {
                Garantia = "1 año",
                Fabricante = "Logitech",
                Contacto = new ContactoFabricante
                {
                    Email = "support@logitech.com",
                    Telefono = "+1-800-LOG",
                    HorarioAtencion = "24/7"
                }
            },
            DataInt = [4, 5, 6],
            DataDecimal = [2.1m, 2.2m, 2.3m],
            DataEnum = [CategoriaProducto.Alimentos, CategoriaProducto.Ropa]
        };

        context.Productos.Add(producto1);
        context.Productos.Add(producto2);
        await context.SaveChangesAsync();

        logger.LogInformation($"✓ Producto 1: {producto1.Id} - {producto1.Nombre}");
        logger.LogInformation($"  → DireccionAlmacen.Pais: {espana.Id} (DocumentReference)");
        logger.LogInformation($"  → DireccionAlmacen.Provincia: {andalucia.Id} (DocumentReference)");
        logger.LogInformation($"  → DireccionAlmacen.Coordenadas.Posicion: GeoPoint (anidado)");
        logger.LogInformation($"  → DireccionAlmacen.Coordenadas.Altitud: {coordenadasAlmacen.Altitud}m");
        logger.LogInformation($"  → InformacionAdicional.Contacto: ComplexType anidado");
        logger.LogInformation($"✓ Producto 2: {producto2.Id} - {producto2.Nombre}");

        // === 4. CREAR LÍNEAS DE PEDIDO CON REFERENCIAS AUTOMÁTICAS ===
        logger.LogInformation("\n--- Paso 4: Creando líneas de pedido (referencia individual automática) ---");

        var linea1 = new LineaPedido
        {
            Id = "linea-001",
            Producto = producto1,
            Cantidad = 2,
            PrecioUnitario = 999.99m
        };

        var linea2 = new LineaPedido
        {
            Id = "linea-002",
            Producto = producto2,
            Cantidad = 5,
            PrecioUnitario = 25.99m
        };

        var linea3 = new LineaPedido
        {
            Id = "linea-003",
            Producto = producto1,
            Cantidad = 1,
            PrecioUnitario = 999.99m
        };

        //context.LineasPedido.Add(linea1);
        //context.LineasPedido.Add(linea2);
        //context.LineasPedido.Add(linea3);
        //await context.SaveChangesAsync();

        //logger.LogInformation($"✓ Línea 1: {linea1.Id} - Producto: {producto1.Id} (DocumentReference - automático)");
        //logger.LogInformation($"✓ Línea 2: {linea2.Id} - Producto: {producto2.Id} (DocumentReference - automático)");
        //logger.LogInformation($"✓ Línea 3: {linea3.Id} - Producto: {producto1.Id} (DocumentReference - automático)");

        // === 5. CREAR PEDIDOS CON COLECCIONES DE REFERENCIAS AUTOMÁTICAS ===
        logger.LogInformation("\n--- Paso 5: Creando pedidos (colecciones de referencias automáticas) ---");

        var pedido1 = new Pedido
        {
            Id = "pedido-001",
            NumeroOrden = "ORD-2024-001",
            FechaPedido = DateTime.UtcNow,
            Cliente = cliente,
            Lineas = [linea1, linea2]
        };

        var pedido2 = new Pedido
        {
            Id = "pedido-002",
            NumeroOrden = "ORD-2024-002",
            FechaPedido = DateTime.UtcNow.AddDays(-1),
            Cliente = cliente,
            Lineas = [linea3]
        };

        context.Pedidos.Add(pedido1);
        context.Pedidos.Add(pedido2);
        await context.SaveChangesAsync();

        logger.LogInformation($"✓ Pedido 1: {pedido1.Id} - {pedido1.NumeroOrden}");
        logger.LogInformation($"  → Cliente: {cliente.Id} (DocumentReference - automático)");
        logger.LogInformation($"  → Lineas: Array[{pedido1.Lineas.Count}] DocumentReferences (automático)");
        logger.LogInformation($"✓ Pedido 2: {pedido2.Id} - {pedido2.NumeroOrden}");
        logger.LogInformation($"  → Cliente: {cliente.Id} (DocumentReference - automático)");
        logger.LogInformation($"  → Lineas: Array[{pedido2.Lineas.Count}] DocumentReferences (automático)");

        // === 6. ACTUALIZAR CLIENTE CON COLECCIÓN DE PEDIDOS ===
        logger.LogInformation("\n--- Paso 6: Actualizando cliente con colección de pedidos ---");

        cliente.Pedidos = [pedido1, pedido2];
        context.Clientes.Update(cliente);
        await context.SaveChangesAsync();

        logger.LogInformation($"✓ Cliente actualizado: {cliente.Id}");
        logger.LogInformation($"  → Pedidos: Array[{cliente.Pedidos.Count}] DocumentReferences (automático)");

        // === RESUMEN FINAL ===
        logger.LogInformation("\n╔═══════════════════════════════════════════════════════════════╗");
        logger.LogInformation("║           PRUEBA COMPLETADA CON ÉXITO ✅                      ║");
        logger.LogInformation("╚═══════════════════════════════════════════════════════════════╝");

        logger.LogInformation("\n📊 ESTRUCTURA EN FIRESTORE (DETECCIÓN AUTOMÁTICA):\n");

        logger.LogInformation("┌─ clientes/cliente-001");
        logger.LogInformation("│  ├─ Nombre: \"Juan Pérez\"");
        logger.LogInformation("│  ├─ Email: \"juan@example.com\"");
        logger.LogInformation("│  ├─ Ubicacion: GeoPoint(40.4168, -3.7038)");
        logger.LogInformation("│  ├─ Direccion: {");
        logger.LogInformation("│  │  ├─ Calle: \"Calle Principal 123\"");
        logger.LogInformation("│  │  ├─ Ciudad: \"Madrid\"");
        logger.LogInformation("│  │  ├─ Pais: DocumentReference(paises/ES)       ← ✅ NESTED REF AUTOMÁTICA");
        logger.LogInformation("│  │  ├─ Provincia: DocumentReference(provincias/MD) ← ✅ NESTED REF AUTOMÁTICA");
        logger.LogInformation("│  │  └─ Coordenadas: {                           ← ✅ COMPLEXTYPE ANIDADO");
        logger.LogInformation("│  │     ├─ Altitud: 667.0");
        logger.LogInformation("│  │     └─ Posicion: GeoPoint(40.4168, -3.7038) ← ✅ GEOPOINT ANIDADO");
        logger.LogInformation("│  │  }");
        logger.LogInformation("│  └─ Pedidos: [");
        logger.LogInformation("│     ├─ DocumentReference(pedidos/pedido-001)");
        logger.LogInformation("│     └─ DocumentReference(pedidos/pedido-002)");
        logger.LogInformation("│  ]");

        logger.LogInformation("\n┌─ productos/prod-001");
        logger.LogInformation("│  ├─ Nombre: \"Laptop Dell\"");
        logger.LogInformation("│  ├─ Precio: 999.99");
        logger.LogInformation("│  ├─ DireccionAlmacen: {");
        logger.LogInformation("│  │  ├─ Calle: \"Polígono Industrial 5\"");
        logger.LogInformation("│  │  ├─ Pais: DocumentReference(paises/ES)");
        logger.LogInformation("│  │  ├─ Provincia: DocumentReference(provincias/AN)");
        logger.LogInformation("│  │  └─ Coordenadas: {");
        logger.LogInformation("│  │     ├─ Altitud: 180.0");
        logger.LogInformation("│  │     └─ Posicion: GeoPoint(38.2366, -1.4206) ← ✅ GEOPOINT ANIDADO");
        logger.LogInformation("│  │  }");
        logger.LogInformation("│  ├─ InformacionAdicional: {");
        logger.LogInformation("│  │  ├─ Garantia: \"2 años\"");
        logger.LogInformation("│  │  ├─ Fabricante: \"Dell Inc.\"");
        logger.LogInformation("│  │  └─ Contacto: {                              ← ✅ COMPLEXTYPE ANIDADO");
        logger.LogInformation("│  │     ├─ Email: \"support@dell.com\"");
        logger.LogInformation("│  │     ├─ Telefono: \"+1-800-DELL\"");
        logger.LogInformation("│  │     └─ HorarioAtencion: \"Lun-Vie 9:00-18:00\"");
        logger.LogInformation("│  │  }");
        logger.LogInformation("│  ├─ DataInt: [1, 2, 3]");
        logger.LogInformation("│  ├─ DataDecimal: [1.1, 1.2, 1.3]");
        logger.LogInformation("│  └─ DataEnum: [\"Electronica\", \"Ropa\"]");

        logger.LogInformation("\n✨ CARACTERÍSTICAS IMPLEMENTADAS:");
        logger.LogInformation("  ✅ Referencias individuales automáticas (sin HasReference)");
        logger.LogInformation("  ✅ Colecciones de referencias automáticas (List<Entity>)");
        logger.LogInformation("  ✅ Referencias anidadas en ComplexProperty (Pais, Provincia en Direccion)");
        logger.LogInformation("  ✅ ComplexType dentro de ComplexType (Coordenadas, ContactoFabricante)");
        logger.LogInformation("  ✅ GeoPoint dentro de ComplexType anidado (Coordenadas.Posicion)");
        logger.LogInformation("  ✅ Anidación multinivel de ComplexTypes");
        logger.LogInformation("  ✅ Foreign Keys omitidas automáticamente (sin redundancia)");
        logger.LogInformation("  ✅ Conversión automática a DocumentReference");
        logger.LogInformation("  ✅ GeoPoint para Ubicacion");
        logger.LogInformation("  ✅ Arrays de primitivos (int, decimal, enum)");
        logger.LogInformation("  ✅ ComplexProperty embebidos");

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

// ============= CONTEXTO =============

public class MiContexto : DbContext
{
    public DbSet<Producto> Productos { get; set; }
    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<Pedido> Pedidos { get; set; }
    public DbSet<LineaPedido> LineasPedido { get; set; }
    public DbSet<Pais> Paises { get; set; }
    public DbSet<Provincia> Provincias { get; set; }

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
    }
}