using Firestore.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Helpers.ArrayOf;

public class ArrayOfTestDbContext(DbContextOptions<ArrayOfTestDbContext> options) : DbContext(options)
{
    public DbSet<TiendaConHorarios> Tiendas => Set<TiendaConHorarios>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TiendaConHorarios>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
            entity.ArrayOf(e => e.Horarios);
        });
    }
}

public class ArrayOfGeoPointTestDbContext(DbContextOptions<ArrayOfGeoPointTestDbContext> options) : DbContext(options)
{
    public DbSet<TiendaConUbicaciones> Tiendas => Set<TiendaConUbicaciones>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TiendaConUbicaciones>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
            entity.ArrayOf(e => e.Ubicaciones).AsGeoPoints();
        });
    }
}

public class ArrayOfReferencesTestDbContext(DbContextOptions<ArrayOfReferencesTestDbContext> options) : DbContext(options)
{
    public DbSet<Etiqueta> Etiquetas => Set<Etiqueta>();
    public DbSet<ProductoConEtiquetas> Productos => Set<ProductoConEtiquetas>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Etiqueta>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
        });

        modelBuilder.Entity<ProductoConEtiquetas>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
            entity.ArrayOf(e => e.Etiquetas).AsReferences();
        });
    }
}

// ============================================================================
// DBCONTEXT PARA ARRAYOF ANIDADO (3 NIVELES)
// ============================================================================

public class ArrayOfNestedTestDbContext(DbContextOptions<ArrayOfNestedTestDbContext> options) : DbContext(options)
{
    public DbSet<LibroCocina> Libros => Set<LibroCocina>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LibroCocina>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Titulo).IsRequired();
            entity.ArrayOf(e => e.Categorias);
        });
    }
}

// ============================================================================
// DBCONTEXT PARA COMPLEXTYPE CON LIST<GEOPOINT>
// ============================================================================

public class ArrayOfComplexWithGeoPointTestDbContext(DbContextOptions<ArrayOfComplexWithGeoPointTestDbContext> options) : DbContext(options)
{
    public DbSet<EmpresaLogistica> Empresas => Set<EmpresaLogistica>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmpresaLogistica>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
            entity.ArrayOf(e => e.Rutas);
        });
    }
}

// ============================================================================
// DBCONTEXT PARA COMPLEXTYPE CON LIST<REFERENCES>
// ============================================================================

public class ArrayOfComplexWithReferencesTestDbContext(DbContextOptions<ArrayOfComplexWithReferencesTestDbContext> options) : DbContext(options)
{
    public DbSet<Etiqueta> Etiquetas => Set<Etiqueta>();
    public DbSet<Catalogo> Catalogos => Set<Catalogo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Etiqueta>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired();
        });

        modelBuilder.Entity<Catalogo>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Titulo).IsRequired();
            entity.ArrayOf(e => e.Secciones);
        });
    }
}

// ============================================================================
// DBCONTEXT PARA TEST DE AUTO-DETECCIÓN (SIN CONFIGURACIÓN EXPLÍCITA)
// ============================================================================

/// <summary>
/// DbContext con CERO configuración.
/// Demuestra que las conventions auto-detectan todo:
/// - PK por convention (propiedad Id)
/// - Direcciones → ArrayOf Embedded (clase sin Id)
/// - Ubicaciones → ArrayOf GeoPoint (tiene Lat/Lng sin Id)
/// </summary>
public class ArrayOfConventionTestDbContext(DbContextOptions<ArrayOfConventionTestDbContext> options) : DbContext(options)
{
    public DbSet<Oficina> Oficinas => Set<Oficina>();
}

// ============================================================================
// DBCONTEXT PARA SUBCOLLECTION CON ARRAYOF
// ============================================================================

/// <summary>
/// DbContext para probar ArrayOf dentro de SubCollections.
/// - Sucursales → SubCollection con ArrayOf Embedded (Horarios)
/// - Rutas → SubCollection con ArrayOf GeoPoint (Waypoints)
/// </summary>
public class ArrayOfSubCollectionTestDbContext(DbContextOptions<ArrayOfSubCollectionTestDbContext> options) : DbContext(options)
{
    public DbSet<Empresa> Empresas => Set<Empresa>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Empresa>(entity =>
        {
            entity.SubCollection(e => e.Sucursales);
            entity.SubCollection(e => e.Rutas);
        });
    }
}

// ============================================================================
// DBCONTEXT PARA TEST COMPLETO DE ARRAYOF (SECCIÓN 2 DEL PLAN)
// ============================================================================

/// <summary>
/// DbContext que implementa EXACTAMENTE la sección 2.2 del plan ARRAYOF_IMPLEMENTATION_PLAN.md
/// Cubre los 5 casos de ArrayOf:
/// - CASO 1: Embedded simple
/// - CASO 2: GeoPoints
/// - CASO 3: References
/// - CASO 4: Embedded con Reference dentro
/// - CASO 5: Embedded anidado con Reference al final
/// </summary>
public class RestauranteTestDbContext(DbContextOptions<RestauranteTestDbContext> options) : DbContext(options)
{
    public DbSet<Restaurante> Restaurantes => Set<Restaurante>();
    public DbSet<CategoriaRestaurante> Categorias => Set<CategoriaRestaurante>();
    public DbSet<Certificador> Certificadores => Set<Certificador>();
    public DbSet<Plato> Platos => Set<Plato>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Restaurante>(entity =>
        {
            // CASO 1: Embedded simple - Sin config (convention detecta)
            entity.ArrayOf(e => e.Horarios);

            // CASO 2: GeoPoints
            entity.ArrayOf(e => e.ZonasCobertura).AsGeoPoints();

            // CASO 3: References
            entity.ArrayOf(e => e.Categorias).AsReferences();

            // CASO 4: Embedded con Reference
            entity.ArrayOf(e => e.Certificaciones, c =>
            {
                c.Reference(x => x.Certificador);
            });

            // CASO 5: Embedded anidado con Reference al final
            entity.ArrayOf(e => e.Menus, menu =>
            {
                menu.ArrayOf(m => m.Secciones, seccion =>
                {
                    seccion.ArrayOf(s => s.Items, item =>
                    {
                        item.Reference(i => i.Plato);
                    });
                });
            });
        });
    }
}

// ============================================================================
// DBCONTEXT CON MINIMAL CONFIGURATION (AUTO-DETECCIÓN)
// ============================================================================

/// <summary>
/// DbContext con CONFIGURACIÓN MÍNIMA para los 5 casos de ArrayOf.
/// Demuestra qué puede auto-detectar la convention vs qué requiere config explícita.
///
/// AUTO-DETECTADO (sin config):
/// - CASO 1: List&lt;Horario&gt; → Embedded (clase sin Id)
/// - CASO 2: List&lt;Coordenada&gt; → GeoPoint (tiene Lat/Lng sin Id)
/// - CASO 3: List&lt;CategoriaRestaurante&gt; → Reference (entidad registrada con DbSet)
///
/// REQUIERE CONFIG EXPLÍCITA:
/// - CASO 4: Certificacion.Certificador → Reference() (propiedad de navegación dentro de ComplexType)
/// - CASO 5: ItemMenu.Plato → Reference() (propiedad de navegación dentro de ComplexType anidado)
/// </summary>
public class RestauranteMinimalConfigDbContext(DbContextOptions<RestauranteMinimalConfigDbContext> options) : DbContext(options)
{
    public DbSet<Restaurante> Restaurantes => Set<Restaurante>();
    public DbSet<CategoriaRestaurante> Categorias => Set<CategoriaRestaurante>();
    public DbSet<Certificador> Certificadores => Set<Certificador>();
    public DbSet<Plato> Platos => Set<Plato>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Restaurante>(entity =>
        {
            // CASO 1: Embedded simple - AUTO-DETECTADO ✅
            // Horario no tiene Id → Convention detecta como Embedded

            // CASO 2: GeoPoints - AUTO-DETECTADO ✅
            // Coordenada tiene Lat/Lng sin Id → Convention detecta como GeoPoint

            // CASO 3: References - AUTO-DETECTADO ✅
            // CategoriaRestaurante tiene DbSet → Convention detecta como Reference

            // CASO 4: Embedded con Reference - REQUIERE CONFIG ⚠️
            // Certificacion es Embedded (auto), pero Certificador es navegación a entidad
            entity.ArrayOf(e => e.Certificaciones, c =>
            {
                c.Reference(x => x.Certificador);
            });

            // CASO 5: Embedded anidado con Reference - REQUIERE CONFIG ⚠️
            // Menu/SeccionMenu son Embedded (auto), pero ItemMenu.Plato es navegación a entidad
            entity.ArrayOf(e => e.Menus, menu =>
            {
                menu.ArrayOf(m => m.Secciones, seccion =>
                {
                    seccion.ArrayOf(s => s.Items, item =>
                    {
                        item.Reference(i => i.Plato);
                    });
                });
            });
        });
    }
}

// ============================================================================
// DBCONTEXT CON HASHSET Y RECORDS (TEST DE ICollection<T>)
// ============================================================================

/// <summary>
/// DbContext con HashSet y records para verificar que ICollection&lt;T&gt; funciona correctamente.
/// Usa MINIMAL CONFIGURATION - todo auto-detectado por conventions.
///
/// AUTO-DETECTADO:
/// - HashSet&lt;Tag&gt; → Embedded (record sin Id)
/// - HashSet&lt;Ubicacion&gt; → GeoPoint (record con Lat/Lng sin Id)
/// - HashSet&lt;Proveedor&gt; → Reference (record con Id = entidad registrada)
/// </summary>
public class ProductoHashSetDbContext(DbContextOptions<ProductoHashSetDbContext> options) : DbContext(options)
{
    public DbSet<ProductoConHashSet> Productos => Set<ProductoConHashSet>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();

    // Sin OnModelCreating - todo auto-detectado por conventions

    /*protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductoConHashSet>(entity =>
        {
           entity.ArrayOf(p=>p.Proveedores).AsReferences();
        });
    }*/
}

// ============================================================================
// DBCONTEXT PARA TEST DE SUBCOLLECTION CON CALLBACK
// ============================================================================

/// <summary>
/// DbContext para probar SubCollection con Reference dentro.
/// Usa la nueva API: SubCollection(e => e.X, c => c.Reference(...))
/// </summary>
public class SubCollectionWithReferenceDbContext(DbContextOptions<SubCollectionWithReferenceDbContext> options) : DbContext(options)
{
    public DbSet<ClienteConVendedor> Clientes => Set<ClienteConVendedor>();
    public DbSet<Vendedor> Vendedores => Set<Vendedor>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClienteConVendedor>(entity =>
        {
            entity.SubCollection(c => c.Pedidos, pedido =>
            {
                pedido.Reference(p => p.Vendedor);
            });
        });
    }
}

/// <summary>
/// DbContext para probar SubCollection con ArrayOf dentro.
/// Usa la nueva API: SubCollection(e => e.X, c => c.ArrayOf(...))
/// </summary>
public class SubCollectionWithArrayOfDbContext(DbContextOptions<SubCollectionWithArrayOfDbContext> options) : DbContext(options)
{
    public DbSet<ClienteConLineas> Clientes => Set<ClienteConLineas>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClienteConLineas>(entity =>
        {
            entity.SubCollection(c => c.Pedidos, pedido =>
            {
                pedido.ArrayOf(p => p.Lineas);
            });
        });
    }
}

// ============================================================================
// DBCONTEXT PARA TEST COMPLETO DE SUBCOLLECTION CON ARRAYS
// ============================================================================

/// <summary>
/// DbContext para probar SubCollection con múltiples tipos de arrays:
/// - Array de References (productos)
/// - Array de GeoPoints (puntos de entrega)
/// - Array de ValueObjects (descuentos)
/// </summary>
public class SubCollectionWithAllArraysDbContext(DbContextOptions<SubCollectionWithAllArraysDbContext> options) : DbContext(options)
{
    public DbSet<ClienteCompleto> Clientes => Set<ClienteCompleto>();
    public DbSet<ProductoRef> Productos => Set<ProductoRef>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClienteCompleto>(entity =>
        {
            entity.SubCollection(c => c.Ordenes, orden =>
            {
                // Array de References a productos
                orden.ArrayOf(o => o.Productos).AsReferences();

                // Array de GeoPoints para ruta de entrega
                orden.ArrayOf(o => o.RutaEntrega).AsGeoPoints();

                // Array de ValueObjects embebidos (auto-detectado, pero explícito para claridad)
                orden.ArrayOf(o => o.Descuentos);
            });
        });
    }
}
