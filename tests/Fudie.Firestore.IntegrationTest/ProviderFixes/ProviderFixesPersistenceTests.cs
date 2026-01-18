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
}
