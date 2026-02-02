using Fudie.Firestore.EntityFrameworkCore.Infrastructure;
using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.ProviderFixes;

/// <summary>
/// Tests de persistencia real contra Firestore Emulator.
/// Verifica que el Deserializer maneja correctamente:
/// - Value Objects (ComplexProperty) con constructor protected
/// - Value Objects nullable
/// - ArrayOf Embedded (PriceOptions)
/// - ArrayOf Reference (Allergens)
/// - SubCollections (Categories)
/// - Include y proyecciones
///
/// PATRÓN: Usa contextos separados para escritura y lectura
/// como todos los tests de integración del proyecto.
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class ProviderFixesPersistenceTests
{
    private readonly FirestoreTestFixture _fixture;
    private readonly Guid _tenantId;

    public ProviderFixesPersistenceTests(FirestoreTestFixture fixture)
    {
        _fixture = fixture;
        _tenantId = Guid.NewGuid();
    }

    private ProviderFixesPersistenceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ProviderFixesPersistenceDbContext>()
            .UseFirestore(FirestoreTestFixture.ProjectId)
            .Options;
        return new ProviderFixesPersistenceDbContext(options, _tenantId);
    }

    // ========================================================================
    // BASIC CRUD TESTS
    // ========================================================================

    [Fact]
    public async Task Insert_MenuItem_WithAllValueObjects_ShouldPersist()
    {
        // Arrange
        using var writeContext = CreateContext();
        var menuItemId = Guid.NewGuid();

        var menuItem = MenuItem.Create(
            tenantId: _tenantId,
            name: "Paella Valenciana",
            description: "Arroz con mariscos y pollo",
            displayOrder: 1,
            nutritionalInfo: NutritionalInfo.Create(
                calories: 450,
                protein: 25.5m,
                carbohydrates: 55.0m,
                fat: 12.3m,
                servingSize: 350,
                fiber: 3.2m,
                sugar: 2.1m,
                salt: 1.5m),
            depositOverride: ItemDepositOverride.Create(15.00m, minimumQuantity: 4),
            id: menuItemId
        )
        .WithPriceOptions(
            PriceOption.CreateSmall(12.50m),
            PriceOption.CreateFull(18.90m),
            PriceOption.CreateMarketPrice()
        );

        // Act - Write
        writeContext.MenuItems.Add(menuItem);
        await writeContext.SaveChangesAsync();

        // Assert - Read with separate context
        using var readContext = CreateContext();
        var retrieved = await readContext.MenuItems
            .FirstOrDefaultAsync(m => m.Id == menuItemId);

        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Paella Valenciana");
        retrieved.Description.Should().Be("Arroz con mariscos y pollo");

        // Value Objects
        retrieved.NutritionalInfo.Should().NotBeNull();
        retrieved.NutritionalInfo!.Calories.Should().Be(450);
        retrieved.NutritionalInfo.Protein.Should().Be(25.5m);

        retrieved.DepositOverride.Should().NotBeNull();
        retrieved.DepositOverride!.DepositAmount.Should().Be(15.00m);

        // ArrayOf Embedded
        retrieved.PriceOptions.Should().HaveCount(3);
    }

    [Fact]
    public async Task Insert_MenuItem_WithNullValueObjects_ShouldPersist()
    {
        // Arrange - MenuItem sin NutritionalInfo ni DepositOverride
        using var writeContext = CreateContext();
        var menuItemId = Guid.NewGuid();

        var menuItem = MenuItem.CreateSimple(_tenantId, "Agua Mineral", id: menuItemId);

        // Act
        writeContext.MenuItems.Add(menuItem);
        await writeContext.SaveChangesAsync();

        // Assert - Read with separate context
        using var readContext = CreateContext();
        var retrieved = await readContext.MenuItems
            .FirstOrDefaultAsync(m => m.Id == menuItemId);

        retrieved.Should().NotBeNull();
        retrieved!.NutritionalInfo.Should().BeNull("NutritionalInfo debe ser null");
        retrieved.DepositOverride.Should().BeNull("DepositOverride debe ser null");
    }

    [Fact]
    public async Task Insert_Menu_WithDepositPolicy_ShouldPersist()
    {
        // Arrange
        using var writeContext = CreateContext();
        var menuId = Guid.NewGuid();

        var menu = Menu.Create(
            tenantId: _tenantId,
            name: "Menú Degustación",
            displayOrder: 1,
            description: "Menú especial de temporada",
            depositPolicy: DepositPolicy.CreatePerPerson(10.00m, minimumGuests: 6),
            id: menuId
        );

        // Act
        writeContext.Menus.Add(menu);
        await writeContext.SaveChangesAsync();

        // Assert - Read with separate context
        using var readContext = CreateContext();
        var retrieved = await readContext.Menus
            .FirstOrDefaultAsync(m => m.Id == menuId);

        retrieved.Should().NotBeNull();
        retrieved!.DepositPolicy.Should().NotBeNull();
        retrieved.DepositPolicy!.DepositType.Should().Be(DepositType.PerPerson);
        retrieved.DepositPolicy.Amount.Should().Be(10.00m);
        retrieved.DepositPolicy.MinimumGuestsForDeposit.Should().Be(6);
    }

    [Fact]
    public async Task Insert_Menu_WithNullDepositPolicy_ShouldPersist()
    {
        // Arrange - Menu sin DepositPolicy
        using var writeContext = CreateContext();
        var menuId = Guid.NewGuid();

        var menu = Menu.Create(
            tenantId: _tenantId,
            name: "Menú del Día",
            displayOrder: 2,
            id: menuId
        );

        // Act
        writeContext.Menus.Add(menu);
        await writeContext.SaveChangesAsync();

        // Assert - Read with separate context
        using var readContext = CreateContext();
        var retrieved = await readContext.Menus
            .FirstOrDefaultAsync(m => m.Id == menuId);

        retrieved.Should().NotBeNull();
        retrieved!.DepositPolicy.Should().BeNull("DepositPolicy debe ser null");
    }

    // ========================================================================
    // ARRAYOF REFERENCE TESTS (Allergens)
    // ========================================================================

    [Fact]
    public async Task Insert_MenuItem_WithAllergenReferences_ShouldPersist()
    {
        // Arrange - Crear alérgenos primero
        using var setupContext = CreateContext();

        var glutenId = "GLUTEN-" + Guid.NewGuid().ToString("N")[..8];
        var dairyId = "DAIRY-" + Guid.NewGuid().ToString("N")[..8];
        var eggsId = "EGGS-" + Guid.NewGuid().ToString("N")[..8];

        var gluten = Allergen.Create(glutenId, "Gluten", 1);
        var dairy = Allergen.Create(dairyId, "Lácteos", 2);
        var eggs = Allergen.Create(eggsId, "Huevos", 3);

        setupContext.Allergens.AddRange(gluten, dairy, eggs);
        await setupContext.SaveChangesAsync();

        // Crear MenuItem con referencias a alérgenos
        using var writeContext = CreateContext();
        var menuItemId = Guid.NewGuid();

        var menuItem = MenuItem.Create(
            tenantId: _tenantId,
            name: "Tarta de Queso",
            allergenNotes: "Contiene trazas de frutos secos",
            id: menuItemId
        )
        .WithAllergens(gluten, dairy, eggs);

        writeContext.MenuItems.Add(menuItem);
        await writeContext.SaveChangesAsync();

        // Act - Read with Include
        using var readContext = CreateContext();
        var retrieved = await readContext.MenuItems
            .Include(m => m.Allergens)
            .FirstOrDefaultAsync(m => m.Id == menuItemId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Allergens.Should().HaveCount(3);
        retrieved.Allergens.Select(a => a.Id).Should().Contain(new[] { glutenId, dairyId, eggsId });
        retrieved.AllergenNotes.Should().Be("Contiene trazas de frutos secos");
    }

    // ========================================================================
    // PROJECTION TESTS
    // ========================================================================

    [Fact]
    public async Task Select_Projection_OnlyScalarProperties_ShouldWork()
    {
        // Arrange
        using var writeContext = CreateContext();
        var menuId = Guid.NewGuid();

        var menu = Menu.Create(
            tenantId: _tenantId,
            name: "Menú Ejecutivo",
            displayOrder: 5,
            depositPolicy: DepositPolicy.CreateFixedAmount(50.00m),
            id: menuId
        );

        writeContext.Menus.Add(menu);
        await writeContext.SaveChangesAsync();

        // Act - Projection with separate context
        using var readContext = CreateContext();
        var projection = await readContext.Menus
            .Where(m => m.Id == menuId)
            .Select(m => new { m.Name, m.DisplayOrder, m.IsActive })
            .FirstOrDefaultAsync();

        // Assert
        projection.Should().NotBeNull();
        projection!.Name.Should().Be("Menú Ejecutivo");
        projection.DisplayOrder.Should().Be(5);
        projection.IsActive.Should().BeTrue();
    }

    // ========================================================================
    // ENUM CONVERSION TESTS
    // ========================================================================

    [Fact]
    public async Task Insert_MenuItem_WithEnumInValueObject_ShouldPersistAsString()
    {
        // Arrange
        using var writeContext = CreateContext();
        var menuItemId = Guid.NewGuid();

        var menuItem = MenuItem.CreateSimple(_tenantId, "Test Enum", id: menuItemId)
            .WithPriceOptions(
                PriceOption.Create(PortionType.Half, 15.00m),
                PriceOption.Create(PortionType.MarketPrice, 0m, isActive: false)
            );

        // Act
        writeContext.MenuItems.Add(menuItem);
        await writeContext.SaveChangesAsync();

        // Assert - Read with separate context
        using var readContext = CreateContext();
        var retrieved = await readContext.MenuItems
            .FirstOrDefaultAsync(m => m.Id == menuItemId);

        retrieved.Should().NotBeNull();
        retrieved!.PriceOptions.Should().HaveCount(2);

        var halfPortion = retrieved.PriceOptions.FirstOrDefault(p => p.PortionType == PortionType.Half);
        halfPortion.Should().NotBeNull();
        halfPortion!.Price.Should().Be(15.00m);

        var marketPrice = retrieved.PriceOptions.FirstOrDefault(p => p.PortionType == PortionType.MarketPrice);
        marketPrice.Should().NotBeNull();
        marketPrice!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Insert_Menu_WithDepositTypeEnum_ShouldPersistAsString()
    {
        // Arrange - Create 3 menus with different deposit types
        using var writeContext = CreateContext();
        var menuId1 = Guid.NewGuid();
        var menuId2 = Guid.NewGuid();
        var menuId3 = Guid.NewGuid();

        var menu1 = Menu.Create(_tenantId, "Menu PerPerson",
            depositPolicy: DepositPolicy.CreatePerPerson(10m), id: menuId1);

        var menu2 = Menu.Create(_tenantId, "Menu Percentage",
            depositPolicy: DepositPolicy.CreatePercentage(15m, minimumBill: 100m), id: menuId2);

        var menu3 = Menu.Create(_tenantId, "Menu FixedAmount",
            depositPolicy: DepositPolicy.CreateFixedAmount(25m), id: menuId3);

        writeContext.Menus.AddRange(menu1, menu2, menu3);
        await writeContext.SaveChangesAsync();

        // Act - Read with separate context
        using var readContext = CreateContext();
        var retrieved1 = await readContext.Menus.FirstOrDefaultAsync(m => m.Id == menuId1);
        var retrieved2 = await readContext.Menus.FirstOrDefaultAsync(m => m.Id == menuId2);
        var retrieved3 = await readContext.Menus.FirstOrDefaultAsync(m => m.Id == menuId3);

        // Assert
        retrieved1!.DepositPolicy!.DepositType.Should().Be(DepositType.PerPerson);
        retrieved2!.DepositPolicy!.DepositType.Should().Be(DepositType.PercentageOfBill);
        retrieved3!.DepositPolicy!.DepositType.Should().Be(DepositType.FixedAmount);
    }

    // ========================================================================
    // DECIMAL TO DOUBLE CONVERSION TESTS
    // ========================================================================

    [Fact]
    public async Task Insert_MenuItem_WithDecimalValues_ShouldMaintainPrecision()
    {
        // Arrange
        using var writeContext = CreateContext();
        var menuItemId = Guid.NewGuid();

        var menuItem = MenuItem.Create(
            tenantId: _tenantId,
            name: "Test Decimals",
            nutritionalInfo: NutritionalInfo.Create(
                calories: 100,
                protein: 12.345m,
                carbohydrates: 45.678m,
                fat: 5.123m,
                fiber: 2.567m,
                sugar: 1.234m,
                salt: 0.789m
            ),
            depositOverride: ItemDepositOverride.Create(99.99m),
            id: menuItemId
        )
        .WithPriceOptions(PriceOption.CreateFull(123.45m));

        // Act
        writeContext.MenuItems.Add(menuItem);
        await writeContext.SaveChangesAsync();

        // Read with separate context
        using var readContext = CreateContext();
        var retrieved = await readContext.MenuItems
            .FirstOrDefaultAsync(m => m.Id == menuItemId);

        // Assert - Verify decimals are maintained
        // Note: Small differences possible due to decimal->double->decimal conversion
        retrieved.Should().NotBeNull();
        retrieved!.NutritionalInfo!.Protein.Should().BeApproximately(12.345m, 0.001m);
        retrieved.NutritionalInfo.Carbohydrates.Should().BeApproximately(45.678m, 0.001m);
        retrieved.DepositOverride!.DepositAmount.Should().BeApproximately(99.99m, 0.001m);
    }

    // ========================================================================
    // COMPLEX INTEGRATION TESTS
    // ========================================================================

    [Fact]
    public async Task FullIntegration_MenuItem_WithAllFeatures_ShouldPersistAndRetrieve()
    {
        // Arrange - Create allergens first
        using var setupContext = CreateContext();
        var glutenId = "GLUTEN-" + Guid.NewGuid().ToString("N")[..8];
        var dairyId = "DAIRY-" + Guid.NewGuid().ToString("N")[..8];

        var gluten = Allergen.Create(glutenId, "Gluten", 1);
        var dairy = Allergen.Create(dairyId, "Lácteos", 2);
        setupContext.Allergens.AddRange(gluten, dairy);
        await setupContext.SaveChangesAsync();

        // Create complete MenuItem
        using var writeContext = CreateContext();
        var menuItemId = Guid.NewGuid();

        var menuItem = MenuItem.Create(
            tenantId: _tenantId,
            name: "Risotto de Setas",
            description: "Cremoso risotto con setas silvestres",
            imageUrl: "https://example.com/risotto.jpg",
            displayOrder: 10,
            isActive: true,
            isHighRiskItem: false,
            requiresAdvanceOrder: true,
            minimumAdvanceOrderQuantity: 2,
            nutritionalInfo: NutritionalInfo.Create(
                calories: 380,
                protein: 12.0m,
                carbohydrates: 52.0m,
                fat: 14.5m,
                servingSize: 300,
                fiber: 2.8m,
                sugar: 1.2m,
                salt: 1.1m),
            depositOverride: ItemDepositOverride.Create(8.00m, minimumQuantity: 4),
            allergenNotes: "Puede contener trazas de apio",
            id: menuItemId
        )
        .WithPriceOptions(
            PriceOption.CreateSmall(14.50m),
            PriceOption.CreateHalf(18.00m),
            PriceOption.CreateFull(22.50m)
        )
        .WithAllergens(gluten, dairy)
        .WithAvailableDays(DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday);

        // Act - Write
        writeContext.MenuItems.Add(menuItem);
        await writeContext.SaveChangesAsync();

        // Read with Include using separate context
        using var readContext = CreateContext();
        var retrieved = await readContext.MenuItems
            .Include(m => m.Allergens)
            .FirstOrDefaultAsync(m => m.Id == menuItemId);

        // Assert - Verify all properties
        retrieved.Should().NotBeNull();

        // Basic properties
        retrieved!.Name.Should().Be("Risotto de Setas");
        retrieved.Description.Should().Be("Cremoso risotto con setas silvestres");
        retrieved.ImageUrl.Should().Be("https://example.com/risotto.jpg");
        retrieved.DisplayOrder.Should().Be(10);
        retrieved.IsActive.Should().BeTrue();
        retrieved.IsHighRiskItem.Should().BeFalse();
        retrieved.RequiresAdvanceOrder.Should().BeTrue();
        retrieved.MinimumAdvanceOrderQuantity.Should().Be(2);
        retrieved.AllergenNotes.Should().Be("Puede contener trazas de apio");

        // NutritionalInfo (nullable ComplexProperty)
        retrieved.NutritionalInfo.Should().NotBeNull();
        retrieved.NutritionalInfo!.Calories.Should().Be(380);
        retrieved.NutritionalInfo.Protein.Should().BeApproximately(12.0m, 0.01m);
        retrieved.NutritionalInfo.Carbohydrates.Should().BeApproximately(52.0m, 0.01m);
        retrieved.NutritionalInfo.Fat.Should().BeApproximately(14.5m, 0.01m);
        retrieved.NutritionalInfo.ServingSize.Should().Be(300);
        retrieved.NutritionalInfo.Fiber.Should().BeApproximately(2.8m, 0.01m);
        retrieved.NutritionalInfo.Sugar.Should().BeApproximately(1.2m, 0.01m);
        retrieved.NutritionalInfo.Salt.Should().BeApproximately(1.1m, 0.01m);

        // DepositOverride (nullable ComplexProperty)
        retrieved.DepositOverride.Should().NotBeNull();
        retrieved.DepositOverride!.DepositAmount.Should().BeApproximately(8.00m, 0.01m);
        retrieved.DepositOverride.MinimumQuantityForDeposit.Should().Be(4);

        // PriceOptions (ArrayOf Embedded)
        retrieved.PriceOptions.Should().HaveCount(3);
        retrieved.PriceOptions.Should().Contain(p => p.PortionType == PortionType.Small && p.Price == 14.50m);
        retrieved.PriceOptions.Should().Contain(p => p.PortionType == PortionType.Half && p.Price == 18.00m);
        retrieved.PriceOptions.Should().Contain(p => p.PortionType == PortionType.Full && p.Price == 22.50m);

        // Allergens (ArrayOf Reference)
        retrieved.Allergens.Should().HaveCount(2);
        retrieved.Allergens.Select(a => a.Id).Should().Contain(new[] { glutenId, dairyId });

        // AvailableDays (primitive array)
        retrieved.AvailableDays.Should().HaveCount(3);
        retrieved.AvailableDays.Should().Contain(new[] { DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday });
    }

    // ========================================================================
    // COMPLETE GRAPH TEST (Menu -> Categories -> Items -> Allergens)
    // ========================================================================

    [Fact]
    public async Task FullIntegration_Menu_WithCategories_Items_AndAllergens_ShouldPersistAndRetrieve()
    {
        // =====================================================================
        // ARRANGE - Create complete data hierarchy
        // =====================================================================

        // 1. Create Allergens (root aggregates)
        using var allergenContext = CreateContext();
        var glutenId = "GLUTEN-" + Guid.NewGuid().ToString("N")[..8];
        var dairyId = "DAIRY-" + Guid.NewGuid().ToString("N")[..8];
        var eggsId = "EGGS-" + Guid.NewGuid().ToString("N")[..8];
        var nutsId = "NUTS-" + Guid.NewGuid().ToString("N")[..8];
        var shellfishId = "SHELLFISH-" + Guid.NewGuid().ToString("N")[..8];

        var gluten = Allergen.Create(glutenId, "Gluten", 1, "https://icons.com/gluten.png");
        var dairy = Allergen.Create(dairyId, "Lácteos", 2, "https://icons.com/dairy.png");
        var eggs = Allergen.Create(eggsId, "Huevos", 3, "https://icons.com/eggs.png");
        var nuts = Allergen.Create(nutsId, "Frutos secos", 4, "https://icons.com/nuts.png");
        var shellfish = Allergen.Create(shellfishId, "Mariscos", 5, "https://icons.com/shellfish.png");

        allergenContext.Allergens.AddRange(gluten, dairy, eggs, nuts, shellfish);
        await allergenContext.SaveChangesAsync();

        // 2. Create MenuItems (root aggregates) with all features
        using var menuItemContext = CreateContext();

        var paellaId = Guid.NewGuid();
        var paella = MenuItem.Create(
            tenantId: _tenantId,
            name: "Paella Valenciana",
            description: "Auténtica paella valenciana con mariscos frescos",
            imageUrl: "https://images.com/paella.jpg",
            displayOrder: 1,
            isActive: true,
            isHighRiskItem: true,
            requiresAdvanceOrder: true,
            minimumAdvanceOrderQuantity: 4,
            nutritionalInfo: NutritionalInfo.Create(
                calories: 520,
                protein: 28.5m,
                carbohydrates: 62.0m,
                fat: 18.3m,
                servingSize: 400,
                fiber: 3.5m,
                sugar: 2.8m,
                salt: 2.1m),
            depositOverride: ItemDepositOverride.Create(20.00m, minimumQuantity: 2),
            allergenNotes: "Puede contener trazas de apio y mostaza",
            id: paellaId
        )
        .WithPriceOptions(
            PriceOption.CreateSmall(16.50m),
            PriceOption.CreateHalf(22.00m),
            PriceOption.CreateFull(28.90m)
        )
        .WithAllergens(shellfish, gluten)
        .WithAvailableDays(DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday);

        var tartaId = Guid.NewGuid();
        var tarta = MenuItem.Create(
            tenantId: _tenantId,
            name: "Tarta de Queso",
            description: "Tarta de queso cremosa al estilo San Sebastián",
            imageUrl: "https://images.com/tarta.jpg",
            displayOrder: 2,
            nutritionalInfo: NutritionalInfo.Create(
                calories: 380,
                protein: 8.2m,
                carbohydrates: 32.0m,
                fat: 24.5m,
                servingSize: 150,
                fiber: 0.5m,
                sugar: 22.0m,
                salt: 0.8m),
            id: tartaId
        )
        .WithPriceOptions(
            PriceOption.CreateSmall(6.50m),
            PriceOption.CreateFull(9.90m)
        )
        .WithAllergens(dairy, eggs, gluten);

        var ensaladaId = Guid.NewGuid();
        var ensalada = MenuItem.Create(
            tenantId: _tenantId,
            name: "Ensalada César",
            description: "Lechuga romana, pollo, parmesano y croutons",
            displayOrder: 3,
            nutritionalInfo: NutritionalInfo.CreateBasic(320, 18.0m, 12.0m, 22.0m),
            id: ensaladaId
        )
        .WithPriceOptions(PriceOption.CreateFull(12.50m))
        .WithAllergens(dairy, gluten, eggs)
        .WithAvailableDays(DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday);

        var mariscosId = Guid.NewGuid();
        var mariscos = MenuItem.Create(
            tenantId: _tenantId,
            name: "Mariscada del Chef",
            description: "Selección de mariscos frescos del día",
            displayOrder: 4,
            isHighRiskItem: true,
            requiresAdvanceOrder: true,
            minimumAdvanceOrderQuantity: 2,
            id: mariscosId
        )
        .WithPriceOptions(PriceOption.CreateMarketPrice())
        .WithAllergens(shellfish);

        menuItemContext.MenuItems.AddRange(paella, tarta, ensalada, mariscos);
        await menuItemContext.SaveChangesAsync();

        // 3. Create Menu with Categories (SubCollection) and CategoryItems (ArrayOf Embedded with References)
        using var menuContext = CreateContext();

        var menuId = Guid.NewGuid();
        var menu = Menu.Create(
            tenantId: _tenantId,
            name: "Carta de Verano 2024",
            description: "Nuestra selección especial de verano con productos de temporada",
            displayOrder: 1,
            isActive: true,
            effectiveFrom: new DateTime(2024, 6, 1),
            effectiveUntil: new DateTime(2024, 9, 30),
            depositPolicy: DepositPolicy.CreatePerPerson(15.00m, minimumGuests: 4),
            id: menuId
        );

        // Category 1: Entrantes
        var entrantesId = Guid.NewGuid();
        var entrantes = MenuCategory.Create(
            name: "Entrantes",
            displayOrder: 1,
            description: "Para empezar con buen pie",
            isActive: true,
            id: entrantesId
        )
        .WithItems(
            CategoryItem.Create(ensalada, displayOrder: 1),
            CategoryItem.Create(mariscos, displayOrder: 2,
                PriceOption.Create(PortionType.Small, 35.00m)) // Override price
        );

        // Category 2: Principales
        var principalesId = Guid.NewGuid();
        var principales = MenuCategory.Create(
            name: "Platos Principales",
            displayOrder: 2,
            description: "El corazón de nuestra cocina",
            isActive: true,
            id: principalesId
        )
        .WithItem(
            CategoryItem.Create(paella, displayOrder: 1,
                PriceOption.Create(PortionType.Full, 32.00m)) // Override price for this menu
        );

        // Category 3: Postres
        var postresId = Guid.NewGuid();
        var postres = MenuCategory.Create(
            name: "Postres",
            displayOrder: 3,
            description: "El broche de oro",
            isActive: true,
            id: postresId
        )
        .WithItem(CategoryItem.Create(tarta, displayOrder: 1));

        menu.WithCategories(entrantes, principales, postres);

        menuContext.Menus.Add(menu);
        await menuContext.SaveChangesAsync();

        // =====================================================================
        // ACT - Single query that loads entire graph
        // Menu → Categories (SubCollection) → Items (ArrayOf Embedded) → MenuItem (Reference) → Allergens (ArrayOf Reference)
        // =====================================================================

        using var readContext = CreateContext();

        var retrievedMenu = await readContext.Menus
            .Include(m => m.Categories)
            .ThenInclude(c => c.Items)
            .ThenInclude(i => i.MenuItem)
            .ThenInclude(item => item.Allergens)
            .FirstOrDefaultAsync(m => m.Id == menuId);

        // =====================================================================
        // ASSERT - Verify complete graph loaded from single query
        // =====================================================================

        // --- Menu assertions ---
        retrievedMenu.Should().NotBeNull();
        retrievedMenu!.Name.Should().Be("Carta de Verano 2024");
        retrievedMenu.IsActive.Should().BeTrue();

        // Menu.DepositPolicy (ComplexProperty)
        retrievedMenu.DepositPolicy.Should().NotBeNull();
        retrievedMenu.DepositPolicy!.DepositType.Should().Be(DepositType.PerPerson);
        retrievedMenu.DepositPolicy.Amount.Should().Be(15.00m);

        // --- Categories assertions (SubCollection) ---
        retrievedMenu.Categories.Should().HaveCount(3);

        var retrievedEntrantes = retrievedMenu.Categories.FirstOrDefault(c => c.Name == "Entrantes");
        retrievedEntrantes.Should().NotBeNull();
        retrievedEntrantes!.DisplayOrder.Should().Be(1);
        retrievedEntrantes.Items.Should().HaveCount(2);

        var retrievedPrincipales = retrievedMenu.Categories.FirstOrDefault(c => c.Name == "Platos Principales");
        retrievedPrincipales.Should().NotBeNull();
        retrievedPrincipales!.Items.Should().HaveCount(1);

        var retrievedPostres = retrievedMenu.Categories.FirstOrDefault(c => c.Name == "Postres");
        retrievedPostres.Should().NotBeNull();
        retrievedPostres!.Items.Should().HaveCount(1);

        // --- CategoryItems assertions (ArrayOf Embedded with Reference inside) ---
        // Entrantes: ensalada (order 1), mariscos (order 2)
        var ensaladaItem = retrievedEntrantes.Items.FirstOrDefault(i => i.DisplayOrder == 1);
        ensaladaItem.Should().NotBeNull();
        ensaladaItem!.MenuItem.Should().NotBeNull();
        ensaladaItem.MenuItem.Id.Should().Be(ensaladaId);
        ensaladaItem.MenuItem.Name.Should().Be("Ensalada César");
        ensaladaItem.PriceOverrides.Should().BeEmpty();

        var mariscosItem = retrievedEntrantes.Items.FirstOrDefault(i => i.DisplayOrder == 2);
        mariscosItem.Should().NotBeNull();
        mariscosItem!.MenuItem.Should().NotBeNull();
        mariscosItem.MenuItem.Id.Should().Be(mariscosId);
        mariscosItem.MenuItem.Name.Should().Be("Mariscada del Chef");
        mariscosItem.PriceOverrides.Should().HaveCount(1);
        mariscosItem.PriceOverrides.First().Price.Should().Be(35.00m);

        // Principales: paella
        var paellaItem = retrievedPrincipales.Items.First();
        paellaItem.MenuItem.Should().NotBeNull();
        paellaItem.MenuItem.Id.Should().Be(paellaId);
        paellaItem.MenuItem.Name.Should().Be("Paella Valenciana");
        paellaItem.PriceOverrides.Should().HaveCount(1);
        paellaItem.PriceOverrides.First().Price.Should().Be(32.00m);

        // Postres: tarta
        var tartaItem = retrievedPostres.Items.First();
        tartaItem.MenuItem.Should().NotBeNull();
        tartaItem.MenuItem.Id.Should().Be(tartaId);
        tartaItem.MenuItem.Name.Should().Be("Tarta de Queso");

        // --- MenuItem.Allergens assertions (ArrayOf Reference loaded via ThenInclude chain) ---
        // Ensalada has: dairy, gluten, eggs
        ensaladaItem.MenuItem.Allergens.Should().HaveCount(3);
        ensaladaItem.MenuItem.Allergens.Select(a => a.Name).Should().Contain(new[] { "Lácteos", "Gluten", "Huevos" });

        // Mariscos has: shellfish
        mariscosItem.MenuItem.Allergens.Should().HaveCount(1);
        mariscosItem.MenuItem.Allergens.First().Name.Should().Be("Mariscos");

        // Paella has: shellfish, gluten
        paellaItem.MenuItem.Allergens.Should().HaveCount(2);
        paellaItem.MenuItem.Allergens.Select(a => a.Name).Should().Contain(new[] { "Mariscos", "Gluten" });

        // Tarta has: dairy, eggs, gluten
        tartaItem.MenuItem.Allergens.Should().HaveCount(3);
        tartaItem.MenuItem.Allergens.Select(a => a.Name).Should().Contain(new[] { "Lácteos", "Huevos", "Gluten" });
    }

    // ========================================================================
    // SUBCOLLECTION CHANGE TRACKING TEST
    // ========================================================================

    [Fact]
    public async Task AddCategoryToExistingMenu_ShouldBeTrackedAsAdded()
    {
        // Arrange - Crear y guardar un Menu vacío (sin categories)
        using var writeContext = CreateContext();
        var menuId = Guid.NewGuid();

        var menu = Menu.Create(
            tenantId: _tenantId,
            name: "Menú de Prueba",
            displayOrder: 1,
            id: menuId
        );

        writeContext.Menus.Add(menu);
        await writeContext.SaveChangesAsync();

        // Act - Leer el menu en un contexto separado y añadir una categoría
        using var readContext = CreateContext();
        var menuLeido = await readContext.Menus
            .Include(m => m.Categories)
            .FirstOrDefaultAsync(m => m.Id == menuId);

        menuLeido.Should().NotBeNull();
        menuLeido!.Categories.Should().BeEmpty();

        // Añadir nueva categoría
        var nuevaCategoria = MenuCategory.Create(
            name: "Nueva Categoría",
            displayOrder: 1,
            id: Guid.NewGuid()
        );
        menuLeido.WithCategory(nuevaCategoria);

        // Assert - Verificar estado en ChangeTracker
        readContext.ChangeTracker.DetectChanges();

        var entries = readContext.ChangeTracker.Entries().ToList();
        var categoriaEntry = entries.FirstOrDefault(e => e.Entity == nuevaCategoria);

        categoriaEntry.Should().NotBeNull("La nueva categoría debe estar en el ChangeTracker");
        categoriaEntry!.State.Should().Be(EntityState.Added,
            "Una entidad nueva añadida a una SubCollection debe tener estado Added, no Modified");
    }

    [Fact]
    public async Task AddAndRemoveCategoryFromMenu_ShouldBeTrackedCorrectly()
    {
        // Arrange - Crear un Menu vacío y guardarlo
        using var writeContext = CreateContext();
        var menuId = Guid.NewGuid();

        var menu = Menu.Create(
            tenantId: _tenantId,
            name: "Menú de Prueba",
            displayOrder: 1,
            id: menuId
        );

        writeContext.Menus.Add(menu);
        await writeContext.SaveChangesAsync();

        // Act - Leer, añadir categoría, guardar, luego eliminarla
        using var context1 = CreateContext();
        var menuLeido = await context1.Menus
            .Include(m => m.Categories)
            .FirstOrDefaultAsync(m => m.Id == menuId);

        var nuevaCategoria = MenuCategory.Create(
            name: "Nueva Categoría",
            displayOrder: 1,
            id: Guid.NewGuid()
        );
        menuLeido!.WithCategory(nuevaCategoria);
        await context1.SaveChangesAsync();

        // Ahora leer de nuevo y eliminar la categoría
        using var context2 = CreateContext();
        var menuConCategoria = await context2.Menus
            .Include(m => m.Categories)
            .FirstOrDefaultAsync(m => m.Id == menuId);

        menuConCategoria!.Categories.Should().HaveCount(1);

        var categoriaAEliminar = menuConCategoria.Categories.First();

        // Eliminar usando el campo privado
        var categoriesField = typeof(Menu).GetField("_categories",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var categories = (HashSet<MenuCategory>)categoriesField!.GetValue(menuConCategoria)!;
        categories.Remove(categoriaAEliminar);

        // Act - SaveChanges debe corregir el estado y eliminar correctamente
        await context2.SaveChangesAsync();

        // Assert - Verificar que la categoría fue eliminada releyendo
        using var verifyContext = CreateContext();
        var menuVerificado = await verifyContext.Menus
            .Include(m => m.Categories)
            .FirstOrDefaultAsync(m => m.Id == menuId);

        menuVerificado!.Categories.Should().BeEmpty(
            "La categoría debe haber sido eliminada de la SubCollection");
    }

    [Fact]
    public async Task RemoveCategoryFromExistingMenu_ShouldBeTrackedAsDeleted()
    {
        // Arrange - Crear un Menu con dos categorías y guardarlo
        using var writeContext = CreateContext();
        var menuId = Guid.NewGuid();

        var categoria1 = MenuCategory.Create(
            name: "Entrantes",
            displayOrder: 1,
            id: Guid.NewGuid()
        );

        var categoria2 = MenuCategory.Create(
            name: "Principales",
            displayOrder: 2,
            id: Guid.NewGuid()
        );

        var menu = Menu.Create(
            tenantId: _tenantId,
            name: "Menú de Prueba",
            displayOrder: 1,
            id: menuId
        )
        .WithCategories(categoria1, categoria2);

        writeContext.Menus.Add(menu);
        await writeContext.SaveChangesAsync();

        // Act - Leer el menu en un contexto separado y eliminar una categoría
        using var readContext = CreateContext();
        var menuLeido = await readContext.Menus
            .Include(m => m.Categories)
            .FirstOrDefaultAsync(m => m.Id == menuId);

        menuLeido.Should().NotBeNull();
        menuLeido!.Categories.Should().HaveCount(2);

        // Obtener la categoría a eliminar
        var categoriaAEliminar = menuLeido.Categories.First(c => c.Name == "Entrantes");

        // Eliminar usando el campo privado (simulando domain method)
        var categoriesField = typeof(Menu).GetField("_categories",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var categories = (HashSet<MenuCategory>)categoriesField!.GetValue(menuLeido)!;
        categories.Remove(categoriaAEliminar);

        // Act - SaveChanges debe corregir el estado y eliminar correctamente
        await readContext.SaveChangesAsync();

        // Assert - Verificar que solo queda una categoría releyendo
        using var verifyContext = CreateContext();
        var menuVerificado = await verifyContext.Menus
            .Include(m => m.Categories)
            .FirstOrDefaultAsync(m => m.Id == menuId);

        menuVerificado!.Categories.Should().HaveCount(1,
            "Debe quedar solo una categoría después de eliminar 'Entrantes'");
        menuVerificado.Categories.First().Name.Should().Be("Principales");
    }

    [Fact]
    public async Task UpdateMenu_ShouldPersistChanges()
    {
        // Arrange - Crear un Menu y guardarlo
        using var writeContext = CreateContext();
        var menuId = Guid.NewGuid();

        var menu = Menu.Create(
            tenantId: _tenantId,
            name: "Menú Temporal",
            displayOrder: 1,
            id: menuId
        );

        writeContext.Menus.Add(menu);
        await writeContext.SaveChangesAsync();

        // Act - Leer el menu y actualizarlo
        using var updateContext = CreateContext();
        var menuLeido = await updateContext.Menus
            .FirstOrDefaultAsync(m => m.Id == menuId);

        menuLeido.Should().NotBeNull();

        // Actualizar propiedades usando reflection (simulando domain method)
        var effectiveFrom = new DateTime(2025, 6, 1);
        var effectiveUntil = new DateTime(2025, 8, 31);

        typeof(Menu).GetProperty("EffectiveFrom")!
            .SetValue(menuLeido, effectiveFrom);
        typeof(Menu).GetProperty("EffectiveUntil")!
            .SetValue(menuLeido, effectiveUntil);
        typeof(Menu).GetProperty("Name")!
            .SetValue(menuLeido, "Menú Verano");

        await updateContext.SaveChangesAsync();

        // Assert - Verificar que los cambios se guardaron
        using var verifyContext = CreateContext();
        var menuVerificado = await verifyContext.Menus
            .FirstOrDefaultAsync(m => m.Id == menuId);

        menuVerificado!.Name.Should().Be("Menú Verano");
        menuVerificado.EffectiveFrom.Should().Be(effectiveFrom);
        menuVerificado.EffectiveUntil.Should().Be(effectiveUntil);
    }
}
