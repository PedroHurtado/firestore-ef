using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.SubCollections;

/// <summary>
/// Tests de integración para verificar el ChangeTracking correcto en SubCollections.
/// Bug 005: Cuando se añade una entidad nueva a una subcolección vacía, el ChangeTracker
/// la marca incorrectamente como Modified en lugar de Added.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class SubCollectionChangeTrackingTests
{
    private readonly FirestoreTestFixture _fixture;
    private readonly Guid _tenantId = Guid.NewGuid();

    public SubCollectionChangeTrackingTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Crea el DbContext con el TenantId del test.
    /// </summary>
    private Bug005DbContext CreateContext()
    {
        var options = _fixture.CreateOptions<Bug005DbContext>();
        return new Bug005DbContext(options, _tenantId);
    }

    #region Bug 005: ESCENARIO EXACTO DE PRODUCCIÓN

    /// <summary>
    /// Bug 005 - Test PRINCIPAL con modelo de producción completo.
    ///
    /// ESCENARIO EXACTO:
    /// 1. Crear MenuItem (entidad referenciada)
    /// 2. Crear Menu SIN categories (con ComplexProperty DepositPolicy)
    /// 3. Leer Menu con Include(Categories).ThenInclude(Items).ThenInclude(MenuItem)
    /// 4. Añadir nueva Category
    /// 5. Verificar que Category tiene estado Added (NO Modified)
    ///
    /// SI ESTE TEST PASA, NO ESTAMOS REPRODUCIENDO EL BUG.
    /// </summary>
    [Fact]
    public async Task Bug005_AddNewCategory_WithFullProductionModel_ShouldBeTrackedAsAdded()
    {
        // Arrange - Crear MenuItem
        using var setupContext = CreateContext();
        var menuItemId = Guid.NewGuid();

        var menuItem = MenuItem.Create(menuItemId, _tenantId, "Pizza Margherita", 12.99m);
        setupContext.MenuItems.Add(menuItem);
        await setupContext.SaveChangesAsync();

        // Crear Menu SIN categorías (con DepositPolicy como en producción)
        using var createContext = CreateContext();
        var menuId = Guid.NewGuid();

        var menu = Menu.Create(menuId, _tenantId, "Menu Bug 005 Test");
        menu.SetDepositPolicy(DepositPolicy.Create(DepositType.PerPerson, 10.00m));

        createContext.Menus.Add(menu);
        await createContext.SaveChangesAsync();

        // Act - Leer menu con la query COMPLETA (igual que producción)
        using var readContext = CreateContext();
        var menuLeido = await readContext.Menus
            .Include(m => m.Categories)
                .ThenInclude(mc => mc.Items)
                    .ThenInclude(ci => ci.MenuItem)
            .FirstOrDefaultAsync(m => m.Id == menuId);

        menuLeido.Should().NotBeNull();
        menuLeido!.Categories.Should().BeEmpty();

        // Añadir nueva categoría (SIN items por ahora)
        var nuevaCategoriaId = Guid.NewGuid();
        var nuevaCategoria = MenuCategory.Create(nuevaCategoriaId, "Pizzas", 1, true);
        menuLeido.AddCategory(nuevaCategoria);

        // Assert - Verificar estado en ChangeTracker ANTES de SaveChanges
        readContext.ChangeTracker.DetectChanges();

        // DEBUG: Ver estado después de DetectChanges
        var debugAfterDetect = readContext.ChangeTracker.Entries()
            .Where(e => e.Entity.GetType().Name == "MenuCategory")
            .SelectMany(e => e.Properties.Select(p => new
            {
                State = e.State,
                Property = p.Metadata.Name,
                Current = p.CurrentValue,
                Original = p.OriginalValue,
                IsModified = p.IsModified,
                IsShadow = p.Metadata.IsShadowProperty()
            }))
            .ToList();

        // Print debug info
        foreach (var prop in debugAfterDetect)
        {
            Console.WriteLine($"  [{prop.State}] {prop.Property} (Shadow={prop.IsShadow}, Modified={prop.IsModified}): Current={prop.Current}, Original={prop.Original}");
        }

        var entries = readContext.ChangeTracker.Entries().ToList();
        var categoriaEntry = entries.FirstOrDefault(e => e.Entity == nuevaCategoria);

        categoriaEntry.Should().NotBeNull("La nueva categoría debe estar en el ChangeTracker");

        // ⚠️ BUG 005: Esto DEBE FALLAR si reproducimos el bug correctamente
        // El bug hace que llegue como Modified en lugar de Added
        categoriaEntry!.State.Should().Be(EntityState.Added,
            "Una entidad NUEVA añadida a una subcolección debe tener estado Added, no Modified");
    }

    /// <summary>
    /// Bug 005 - Test SaveChanges que DEBE FALLAR con "no entity to update".
    /// </summary>
    [Fact]
    public async Task Bug005_SaveChanges_WithNewCategory_ShouldNotThrowNoEntityToUpdate()
    {
        // Arrange - Crear MenuItem
        using var setupContext = CreateContext();
        var menuItemId = Guid.NewGuid();

        var menuItem = MenuItem.Create(menuItemId, _tenantId, "Burger", 9.99m);
        setupContext.MenuItems.Add(menuItem);
        await setupContext.SaveChangesAsync();

        // Crear Menu SIN categorías
        using var createContext = CreateContext();
        var menuId = Guid.NewGuid();

        var menu = Menu.Create(menuId, _tenantId, "Menu Save Test");
        createContext.Menus.Add(menu);
        await createContext.SaveChangesAsync();

        // Act - Leer con query completa, añadir categoría, guardar
        using var updateContext = CreateContext();
        var menuLeido = await updateContext.Menus
            .Include(m => m.Categories)
                .ThenInclude(mc => mc.Items)
                    .ThenInclude(ci => ci.MenuItem)
            .FirstOrDefaultAsync(m => m.Id == menuId);

        var nuevaCategoriaId = Guid.NewGuid();
        var nuevaCategoria = MenuCategory.Create(nuevaCategoriaId, "Burgers", 1, true);
        menuLeido!.AddCategory(nuevaCategoria);

        // ⚠️ BUG 005: Esto DEBE FALLAR con "no entity to update" si el bug existe
        await updateContext.SaveChangesAsync();

        // Verify - Si llegamos aquí sin excepción, el bug está arreglado
        using var verifyContext = CreateContext();
        var menuVerificado = await verifyContext.Menus
            .Include(m => m.Categories)
            .FirstOrDefaultAsync(m => m.Id == menuId);

        menuVerificado.Should().NotBeNull();
        menuVerificado!.Categories.Should().HaveCount(1);
        menuVerificado.Categories.Should().Contain(c => c.Id == nuevaCategoriaId);
    }

    /// <summary>
    /// Bug 005 - Test con Category que tiene Items con Reference.
    /// Este es el escenario MÁS COMPLETO.
    /// </summary>
    [Fact]
    public async Task Bug005_SaveChanges_WithCategoryContainingItems_ShouldCreateDocument()
    {
        // Arrange - Crear MenuItem
        using var setupContext = CreateContext();
        var menuItemId = Guid.NewGuid();

        var menuItem = MenuItem.Create(menuItemId, _tenantId, "Pasta Carbonara", 14.99m);
        setupContext.MenuItems.Add(menuItem);
        await setupContext.SaveChangesAsync();

        // Crear Menu SIN categorías
        using var createContext = CreateContext();
        var menuId = Guid.NewGuid();

        var menu = Menu.Create(menuId, _tenantId, "Menu Full Test");
        menu.SetDepositPolicy(DepositPolicy.Create(DepositType.FixedAmount, 25.00m));
        createContext.Menus.Add(menu);
        await createContext.SaveChangesAsync();

        // Act - Leer, añadir categoría CON items, guardar
        using var updateContext = CreateContext();
        var menuLeido = await updateContext.Menus
            .Include(m => m.Categories)
                .ThenInclude(mc => mc.Items)
                    .ThenInclude(ci => ci.MenuItem)
            .FirstOrDefaultAsync(m => m.Id == menuId);

        // Obtener el MenuItem para crear la Reference
        var menuItemRef = await updateContext.MenuItems.FindAsync(menuItemId);
        menuItemRef.Should().NotBeNull();

        var nuevaCategoriaId = Guid.NewGuid();
        var nuevaCategoria = MenuCategory.Create(nuevaCategoriaId, "Pastas", 1, true);

        // Añadir item con Reference - ESCENARIO COMPLETO
        var categoryItem = CategoryItem.Create(menuItemRef!, 1);
        nuevaCategoria.AddItem(categoryItem);

        menuLeido!.AddCategory(nuevaCategoria);

        // ⚠️ BUG 005: Esto falla con "no entity to update"
        await updateContext.SaveChangesAsync();

        // Verify
        using var verifyContext = CreateContext();
        var menuVerificado = await verifyContext.Menus
            .Include(m => m.Categories)
                .ThenInclude(mc => mc.Items)
                .ThenInclude(i => i.MenuItem)
            .FirstOrDefaultAsync(m => m.Id == menuId);

        menuVerificado.Should().NotBeNull();
        menuVerificado!.Categories.Should().HaveCount(1);

        var categoriaVerificada = menuVerificado.Categories.First();
        categoriaVerificada.Id.Should().Be(nuevaCategoriaId);
        categoriaVerificada.Items.Should().HaveCount(1);
    }

    #endregion
}