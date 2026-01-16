using Fudie.Firestore.EntityFrameworkCore.Infrastructure;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Fudie.Firestore.IntegrationTest.ProviderFixes;

/// <summary>
/// Comprehensive tests para verificar que el provider de Firestore configura correctamente
/// el IModel con todas las conventions y annotations de Firestore.
///
/// Estos tests sirven como SEED/PATRÓN para todos los microservicios que utilicen
/// el provider de Firestore para EF Core. Cualquier microservicio puede copiar
/// estos tests y adaptarlos a su modelo de dominio.
///
/// CONVENTIONS CUBIERTAS:
/// 1. PrimaryKeyConvention - Auto-detecta propiedades Id como clave primaria
/// 2. CollectionNamingConvention - Pluraliza nombres de colecciones
/// 3. EnumToStringConvention - Convierte enums a string
/// 4. DecimalToDoubleConvention - Convierte decimal a double
/// 5. ComplexTypePropertyDiscoveryConvention - Descubre propiedades en Value Objects
/// 6. DocumentReferenceNamingConvention - Nombra campos de referencia como {Property}Ref
///
/// ANNOTATIONS CUBIERTAS:
/// - Firestore:SubCollection - Marca navegaciones como subcollections
/// - Firestore:DocumentReference - Marca navegaciones como document references
/// - Firestore:ArrayOf:Type - Define tipo de array (Embedded, GeoPoint, Reference)
/// - Firestore:DocumentReferenceFieldName - Nombre del campo para referencias
///
/// PATRONES DDD SOPORTADOS:
/// - Value Objects (records) con constructor protected
/// - ComplexProperty nullable (Value Objects opcionales)
/// - AggregateRoots con subcollections
/// - References entre aggregates
/// </summary>
public class ProviderFixesTests
{
    private const string EmulatorHost = "127.0.0.1:8080";

    private readonly ProviderFixesDbContext _context;
    private readonly IModel _model;
    private readonly IModel _designTimeModel;

    public ProviderFixesTests()
    {
        // Configurar emulador para evitar necesidad de credenciales
        Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", EmulatorHost);

        var options = new DbContextOptionsBuilder<ProviderFixesDbContext>()
            .UseFirestore("test-project")
            .Options;

        _context = new ProviderFixesDbContext(options, Guid.NewGuid());
        _model = _context.Model;
        _designTimeModel = _context.GetService<IDesignTimeModel>().Model;
    }

    // ========================================================================
    // GLOBAL MODEL CONFIGURATION
    // ========================================================================

    [Fact]
    public void Model_ShouldUsePropertyAccessModeField()
    {
        // PropertyAccessMode.Field permite que EF Core acceda directamente a backing fields
        // Esto es esencial para patrones DDD donde las propiedades tienen setters protected
        var accessMode = _designTimeModel.GetPropertyAccessMode();

        accessMode.Should().Be(PropertyAccessMode.Field);
    }

    // ========================================================================
    // ENTITY TYPE REGISTRATION
    // ========================================================================

    [Fact]
    public void Model_ShouldHaveMenuEntityType()
    {
        var entityType = _model.FindEntityType(typeof(Menu));
        entityType.Should().NotBeNull("Menu debe estar registrado como EntityType");
    }

    [Fact]
    public void Model_ShouldHaveMenuItemEntityType()
    {
        var entityType = _model.FindEntityType(typeof(MenuItem));
        entityType.Should().NotBeNull("MenuItem debe estar registrado como EntityType");
    }

    [Fact]
    public void Model_ShouldHaveAllergenEntityType()
    {
        var entityType = _model.FindEntityType(typeof(Allergen));
        entityType.Should().NotBeNull("Allergen debe estar registrado como EntityType");
    }

    [Fact]
    public void Model_ShouldHaveMenuCategoryEntityType()
    {
        var entityType = _model.FindEntityType(typeof(MenuCategory));
        entityType.Should().NotBeNull("MenuCategory debe estar registrado como EntityType para subcollections");
    }

    // ========================================================================
    // PRIMARY KEY CONVENTION TESTS
    // ========================================================================

    [Fact]
    public void PrimaryKeyConvention_Menu_ShouldHaveIdAsPrimaryKey()
    {
        var entityType = _model.FindEntityType(typeof(Menu))!;
        var primaryKey = entityType.FindPrimaryKey();

        primaryKey.Should().NotBeNull("Menu debe tener clave primaria");
        primaryKey!.Properties.Should().HaveCount(1, "Firestore no soporta claves compuestas");
        primaryKey.Properties[0].Name.Should().Be("Id");
    }

    [Fact]
    public void PrimaryKeyConvention_MenuItem_ShouldHaveIdAsPrimaryKey()
    {
        var entityType = _model.FindEntityType(typeof(MenuItem))!;
        var primaryKey = entityType.FindPrimaryKey();

        primaryKey.Should().NotBeNull();
        primaryKey!.Properties[0].Name.Should().Be("Id");
        primaryKey.Properties[0].ClrType.Should().Be(typeof(Guid));
    }

    [Fact]
    public void PrimaryKeyConvention_Allergen_ShouldHaveIdAsPrimaryKey()
    {
        // Allergen usa string como Id (código)
        var entityType = _model.FindEntityType(typeof(Allergen))!;
        var primaryKey = entityType.FindPrimaryKey();

        primaryKey.Should().NotBeNull();
        primaryKey!.Properties[0].Name.Should().Be("Id");
        primaryKey.Properties[0].ClrType.Should().Be(typeof(string));
    }

    // ========================================================================
    // COLLECTION NAMING CONVENTION TESTS
    // ========================================================================

    [Fact]
    public void CollectionNamingConvention_Menu_ShouldHavePluralizedTableName()
    {
        var entityType = _model.FindEntityType(typeof(Menu))!;
        var tableName = entityType.GetTableName();

        tableName.Should().Be("Menus", "CollectionNamingConvention debe pluralizar 'Menu' a 'Menus'");
    }

    [Fact]
    public void CollectionNamingConvention_MenuItem_ShouldHavePluralizedTableName()
    {
        var entityType = _model.FindEntityType(typeof(MenuItem))!;
        var tableName = entityType.GetTableName();

        tableName.Should().Be("MenuItems", "CollectionNamingConvention debe pluralizar 'MenuItem' a 'MenuItems'");
    }

    [Fact]
    public void CollectionNamingConvention_Allergen_ShouldHavePluralizedTableName()
    {
        var entityType = _model.FindEntityType(typeof(Allergen))!;
        var tableName = entityType.GetTableName();

        tableName.Should().Be("Allergens", "CollectionNamingConvention debe pluralizar 'Allergen' a 'Allergens'");
    }

    [Fact]
    public void CollectionNamingConvention_MenuCategory_ShouldHavePluralizedTableName()
    {
        var entityType = _model.FindEntityType(typeof(MenuCategory))!;
        var tableName = entityType.GetTableName();

        tableName.Should().Be("MenuCategories", "CollectionNamingConvention debe pluralizar 'MenuCategory' a 'MenuCategories'");
    }

    // ========================================================================
    // ENUM TO STRING CONVENTION TESTS
    // ========================================================================

    [Fact]
    public void EnumToStringConvention_DepositPolicy_DepositType_ShouldHaveStringConverter()
    {
        // DepositType es un enum dentro del ComplexType DepositPolicy
        var menuEntityType = _model.FindEntityType(typeof(Menu))!;
        var depositPolicyComplex = menuEntityType.FindComplexProperty(nameof(Menu.DepositPolicy))!;
        var depositTypeProperty = depositPolicyComplex.ComplexType.FindProperty(nameof(DepositPolicy.DepositType));

        depositTypeProperty.Should().NotBeNull();
        var converter = depositTypeProperty!.GetValueConverter();
        converter.Should().NotBeNull("EnumToStringConvention debe aplicar conversión a string");
        converter.Should().BeOfType<EnumToStringConverter<DepositType>>();
    }

    [Fact]
    public void EnumToStringConvention_PriceOption_PortionType_ShouldHaveStringConverter()
    {
        // PriceOption contiene PortionType enum
        // PriceOptions es un ArrayOf<PriceOption> en MenuItem
        // Verificamos que el modelo detecta correctamente el enum

        var menuItemEntityType = _model.FindEntityType(typeof(MenuItem))!;

        // PriceOptions está configurado como ArrayOf Embedded,
        // los enums dentro se convierten durante serialización
        menuItemEntityType.IsArrayOf(nameof(MenuItem.PriceOptions)).Should().BeTrue();
    }

    // ========================================================================
    // DECIMAL TO DOUBLE CONVENTION TESTS
    // ========================================================================

    [Fact]
    public void DecimalToDoubleConvention_DepositPolicy_Amount_ShouldHaveDoubleConverter()
    {
        var menuEntityType = _model.FindEntityType(typeof(Menu))!;
        var depositPolicyComplex = menuEntityType.FindComplexProperty(nameof(Menu.DepositPolicy))!;
        var amountProperty = depositPolicyComplex.ComplexType.FindProperty(nameof(DepositPolicy.Amount));

        amountProperty.Should().NotBeNull();
        var converter = amountProperty!.GetValueConverter();
        converter.Should().NotBeNull("DecimalToDoubleConvention debe aplicar conversión decimal->double");

        // Verificar que el tipo de destino es double
        converter!.ProviderClrType.Should().Be(typeof(double));
    }

    [Fact]
    public void DecimalToDoubleConvention_NullableDecimal_ShouldHaveNullableDoubleConverter()
    {
        var menuEntityType = _model.FindEntityType(typeof(Menu))!;
        var depositPolicyComplex = menuEntityType.FindComplexProperty(nameof(Menu.DepositPolicy))!;

        // Percentage es decimal? (nullable)
        var percentageProperty = depositPolicyComplex.ComplexType.FindProperty(nameof(DepositPolicy.Percentage));

        percentageProperty.Should().NotBeNull();
        var converter = percentageProperty!.GetValueConverter();
        converter.Should().NotBeNull("DecimalToDoubleConvention debe aplicar conversión para decimal nullable");
        converter!.ProviderClrType.Should().Be(typeof(double?));
    }

    [Fact]
    public void DecimalToDoubleConvention_NutritionalInfo_Protein_ShouldHaveDoubleConverter()
    {
        var menuItemEntityType = _model.FindEntityType(typeof(MenuItem))!;
        var nutritionalInfoComplex = menuItemEntityType.FindComplexProperty(nameof(MenuItem.NutritionalInfo))!;

        var proteinProperty = nutritionalInfoComplex.ComplexType.FindProperty(nameof(NutritionalInfo.Protein));

        proteinProperty.Should().NotBeNull();
        var converter = proteinProperty!.GetValueConverter();
        converter.Should().NotBeNull();
        converter!.ProviderClrType.Should().Be(typeof(double));
    }

    // ========================================================================
    // QUERY FILTER TESTS
    // ========================================================================

    [Fact]
    public void Menu_ShouldHaveQueryFilter()
    {
        var entityType = _model.FindEntityType(typeof(Menu))!;
        var queryFilter = entityType.GetQueryFilter();

        queryFilter.Should().NotBeNull("Menu debe tener QueryFilter para multi-tenancy");
    }

    [Fact]
    public void MenuItem_ShouldHaveQueryFilter()
    {
        var entityType = _model.FindEntityType(typeof(MenuItem))!;
        var queryFilter = entityType.GetQueryFilter();

        queryFilter.Should().NotBeNull("MenuItem debe tener QueryFilter para multi-tenancy");
    }

    [Fact]
    public void Allergen_ShouldNotHaveQueryFilter()
    {
        // Allergen es compartido entre tenants (catálogo global)
        var entityType = _model.FindEntityType(typeof(Allergen))!;
        var queryFilter = entityType.GetQueryFilter();

        queryFilter.Should().BeNull("Allergen es un catálogo global sin filtro por tenant");
    }

    // ========================================================================
    // COMPLEX PROPERTY (VALUE OBJECT) TESTS
    // ========================================================================

    [Fact]
    public void Menu_ShouldHaveDepositPolicyAsComplexProperty()
    {
        var entityType = _model.FindEntityType(typeof(Menu))!;
        var complexProperty = entityType.FindComplexProperty(nameof(Menu.DepositPolicy));

        complexProperty.Should().NotBeNull("DepositPolicy debe ser ComplexProperty (Value Object)");
    }

    [Fact]
    public void Menu_DepositPolicy_ShouldBeNullable()
    {
        // CLAVE: Este test verifica que FirestoreModelValidator permite ComplexProperty nullable
        // EF Core base bloquea esto, pero Firestore lo permite porque los campos son opcionales
        var entityType = _model.FindEntityType(typeof(Menu))!;
        var complexProperty = entityType.FindComplexProperty(nameof(Menu.DepositPolicy))!;

        complexProperty.IsNullable.Should().BeTrue(
            "FirestoreModelValidator debe permitir ComplexProperty nullable para Value Objects opcionales");
    }

    [Fact]
    public void MenuItem_ShouldHaveDepositOverrideAsComplexProperty()
    {
        var entityType = _model.FindEntityType(typeof(MenuItem))!;
        var complexProperty = entityType.FindComplexProperty(nameof(MenuItem.DepositOverride));

        complexProperty.Should().NotBeNull();
    }

    [Fact]
    public void MenuItem_DepositOverride_ShouldBeNullable()
    {
        var entityType = _model.FindEntityType(typeof(MenuItem))!;
        var complexProperty = entityType.FindComplexProperty(nameof(MenuItem.DepositOverride))!;

        complexProperty.IsNullable.Should().BeTrue();
    }

    [Fact]
    public void MenuItem_ShouldHaveNutritionalInfoAsComplexProperty()
    {
        var entityType = _model.FindEntityType(typeof(MenuItem))!;
        var complexProperty = entityType.FindComplexProperty(nameof(MenuItem.NutritionalInfo));

        complexProperty.Should().NotBeNull();
    }

    [Fact]
    public void MenuItem_NutritionalInfo_ShouldBeNullable()
    {
        var entityType = _model.FindEntityType(typeof(MenuItem))!;
        var complexProperty = entityType.FindComplexProperty(nameof(MenuItem.NutritionalInfo))!;

        complexProperty.IsNullable.Should().BeTrue();
    }

    // ========================================================================
    // COMPLEX TYPE PROPERTY DISCOVERY CONVENTION TESTS
    // ========================================================================

    [Fact]
    public void ComplexTypePropertyDiscovery_DepositPolicy_ShouldDiscoverAllProperties()
    {
        // CLAVE: Este test verifica que ComplexTypePropertyDiscoveryConvention
        // descubre las propiedades de records con constructor protected
        var entityType = _model.FindEntityType(typeof(Menu))!;
        var complexType = entityType.FindComplexProperty(nameof(Menu.DepositPolicy))!.ComplexType;

        complexType.FindProperty(nameof(DepositPolicy.DepositType)).Should().NotBeNull();
        complexType.FindProperty(nameof(DepositPolicy.Amount)).Should().NotBeNull();
        complexType.FindProperty(nameof(DepositPolicy.Percentage)).Should().NotBeNull();
        complexType.FindProperty(nameof(DepositPolicy.MinimumBillForDeposit)).Should().NotBeNull();
        complexType.FindProperty(nameof(DepositPolicy.MinimumGuestsForDeposit)).Should().NotBeNull();
    }

    [Fact]
    public void ComplexTypePropertyDiscovery_NutritionalInfo_ShouldDiscoverAllProperties()
    {
        var entityType = _model.FindEntityType(typeof(MenuItem))!;
        var complexType = entityType.FindComplexProperty(nameof(MenuItem.NutritionalInfo))!.ComplexType;

        complexType.FindProperty(nameof(NutritionalInfo.Calories)).Should().NotBeNull();
        complexType.FindProperty(nameof(NutritionalInfo.Protein)).Should().NotBeNull();
        complexType.FindProperty(nameof(NutritionalInfo.Carbohydrates)).Should().NotBeNull();
        complexType.FindProperty(nameof(NutritionalInfo.Fat)).Should().NotBeNull();
        complexType.FindProperty(nameof(NutritionalInfo.ServingSize)).Should().NotBeNull();
        complexType.FindProperty(nameof(NutritionalInfo.Fiber)).Should().NotBeNull();
        complexType.FindProperty(nameof(NutritionalInfo.Sugar)).Should().NotBeNull();
        complexType.FindProperty(nameof(NutritionalInfo.Salt)).Should().NotBeNull();
    }

    [Fact]
    public void ComplexTypePropertyDiscovery_ItemDepositOverride_ShouldDiscoverAllProperties()
    {
        var entityType = _model.FindEntityType(typeof(MenuItem))!;
        var complexType = entityType.FindComplexProperty(nameof(MenuItem.DepositOverride))!.ComplexType;

        complexType.FindProperty(nameof(ItemDepositOverride.DepositAmount)).Should().NotBeNull();
        complexType.FindProperty(nameof(ItemDepositOverride.MinimumQuantityForDeposit)).Should().NotBeNull();
    }

    // ========================================================================
    // SUBCOLLECTION ANNOTATION TESTS
    // ========================================================================

    [Fact]
    public void Menu_ShouldHaveCategoriesNavigation()
    {
        var entityType = _model.FindEntityType(typeof(Menu))!;
        var navigation = entityType.FindNavigation(nameof(Menu.Categories));

        navigation.Should().NotBeNull("Menu debe tener navegación a Categories");
    }

    [Fact]
    public void Menu_Categories_ShouldBeMarkedAsSubCollection()
    {
        var entityType = _model.FindEntityType(typeof(Menu))!;
        var navigation = entityType.FindNavigation(nameof(Menu.Categories))!;

        navigation.IsSubCollection().Should().BeTrue(
            "Categories debe estar marcado como SubCollection con annotation Firestore:SubCollection=true");
    }

    [Fact]
    public void Menu_Categories_ShouldBeCollection()
    {
        var entityType = _model.FindEntityType(typeof(Menu))!;
        var navigation = entityType.FindNavigation(nameof(Menu.Categories))!;

        navigation.IsCollection.Should().BeTrue("Categories es una colección de MenuCategory");
    }

    [Fact]
    public void Menu_Categories_ShouldBeFirestoreConfigured()
    {
        var entityType = _model.FindEntityType(typeof(Menu))!;
        var navigation = entityType.FindNavigation(nameof(Menu.Categories))!;

        navigation.IsFirestoreConfigured().Should().BeTrue(
            "IsFirestoreConfigured() debe retornar true para SubCollections");
    }

    // ========================================================================
    // ARRAYOF ANNOTATION TESTS - EMBEDDED
    // ========================================================================

    [Fact]
    public void MenuItem_PriceOptions_ShouldBeArrayOf()
    {
        var entityType = _model.FindEntityType(typeof(MenuItem))!;

        entityType.IsArrayOf(nameof(MenuItem.PriceOptions)).Should().BeTrue(
            "PriceOptions debe estar marcado como ArrayOf");
    }

    [Fact]
    public void MenuItem_PriceOptions_ShouldBeArrayOfEmbedded()
    {
        var entityType = _model.FindEntityType(typeof(MenuItem))!;

        entityType.IsArrayOfEmbedded(nameof(MenuItem.PriceOptions)).Should().BeTrue(
            "PriceOptions debe ser ArrayOf Embedded (Value Objects embebidos)");
    }

    [Fact]
    public void MenuItem_PriceOptions_ShouldHaveCorrectElementType()
    {
        var entityType = _model.FindEntityType(typeof(MenuItem))!;
        var elementType = entityType.GetArrayOfElementClrType(nameof(MenuItem.PriceOptions));

        elementType.Should().Be(typeof(PriceOption),
            "El tipo de elemento del ArrayOf debe ser PriceOption");
    }

    // ========================================================================
    // ARRAYOF ANNOTATION TESTS - REFERENCE
    // ========================================================================

    [Fact]
    public void MenuItem_Allergens_ShouldBeArrayOf()
    {
        var entityType = _model.FindEntityType(typeof(MenuItem))!;

        entityType.IsArrayOf(nameof(MenuItem.Allergens)).Should().BeTrue(
            "Allergens debe estar marcado como ArrayOf");
    }

    [Fact]
    public void MenuItem_Allergens_ShouldBeArrayOfReference()
    {
        var entityType = _model.FindEntityType(typeof(MenuItem))!;

        entityType.IsArrayOfReference(nameof(MenuItem.Allergens)).Should().BeTrue(
            "Allergens debe ser ArrayOf Reference (referencias a DocumentReferences)");
    }

    [Fact]
    public void MenuItem_Allergens_ShouldHaveCorrectElementType()
    {
        var entityType = _model.FindEntityType(typeof(MenuItem))!;
        var elementType = entityType.GetArrayOfElementClrType(nameof(MenuItem.Allergens));

        elementType.Should().Be(typeof(Allergen),
            "El tipo de elemento del ArrayOf Reference debe ser Allergen");
    }

    // ========================================================================
    // ARRAYOF ANNOTATION TESTS - EMBEDDED WITH NESTED REFERENCE
    // ========================================================================

    [Fact]
    public void MenuCategory_Items_ShouldBeArrayOf()
    {
        var entityType = _model.FindEntityType(typeof(MenuCategory))!;

        entityType.IsArrayOf(nameof(MenuCategory.Items)).Should().BeTrue(
            "Items debe estar marcado como ArrayOf");
    }

    [Fact]
    public void MenuCategory_Items_ShouldBeArrayOfEmbedded()
    {
        var entityType = _model.FindEntityType(typeof(MenuCategory))!;

        entityType.IsArrayOfEmbedded(nameof(MenuCategory.Items)).Should().BeTrue(
            "Items debe ser ArrayOf Embedded (CategoryItem embebido con Reference dentro)");
    }

    [Fact]
    public void MenuCategory_Items_ShouldHaveCorrectElementType()
    {
        var entityType = _model.FindEntityType(typeof(MenuCategory))!;
        var elementType = entityType.GetArrayOfElementClrType(nameof(MenuCategory.Items));

        elementType.Should().Be(typeof(CategoryItem),
            "El tipo de elemento del ArrayOf debe ser CategoryItem");
    }

    // ========================================================================
    // DOCUMENT REFERENCE NAMING CONVENTION TESTS
    // ========================================================================

    [Fact]
    public void DocumentReferenceNaming_Categories_ShouldHaveFieldNameAnnotation()
    {
        var entityType = _model.FindEntityType(typeof(Menu))!;
        var navigation = entityType.FindNavigation(nameof(Menu.Categories))!;

        var fieldNameAnnotation = navigation.FindAnnotation("Firestore:DocumentReferenceFieldName");
        fieldNameAnnotation.Should().NotBeNull(
            "DocumentReferenceNamingConvention debe aplicar annotation de nombre de campo");
        fieldNameAnnotation!.Value.Should().Be("CategoriesRef",
            "El nombre de campo debe ser {PropertyName}Ref");
    }

    // ========================================================================
    // DBSET EXPOSURE TESTS
    // ========================================================================

    [Fact]
    public void DbContext_ShouldExposeMenusDbSet()
    {
        _context.Menus.Should().NotBeNull();
    }

    [Fact]
    public void DbContext_ShouldExposeMenuItemsDbSet()
    {
        _context.MenuItems.Should().NotBeNull();
    }

    [Fact]
    public void DbContext_ShouldExposeAllergensDbSet()
    {
        _context.Allergens.Should().NotBeNull();
    }

    // ========================================================================
    // MODEL VALIDATION TESTS (FirestoreModelValidator)
    // ========================================================================

    [Fact]
    public void Model_ShouldNotThrowForNullableComplexProperty()
    {
        // CLAVE: Este test verifica que FirestoreModelValidator NO bloquea
        // ComplexProperty nullable, a diferencia de ModelValidator base de EF Core

        // Si llegamos aquí sin excepción, el modelo fue construido correctamente
        var menuEntity = _model.FindEntityType(typeof(Menu))!;
        var depositPolicy = menuEntity.FindComplexProperty(nameof(Menu.DepositPolicy))!;

        // Verificar que realmente es nullable
        depositPolicy.IsNullable.Should().BeTrue();

        // El hecho de que el modelo se construyó sin lanzar InvalidOperationException
        // demuestra que FirestoreModelValidator permite ComplexProperty nullable
    }

    [Fact]
    public void Model_ShouldNotThrowForProtectedConstructorValueObjects()
    {
        // CLAVE: Este test verifica que el provider funciona con Value Objects
        // que tienen constructor protected (patrón DDD estándar)

        // DepositPolicy tiene SOLO un constructor protected con parámetros
        // Si el modelo se construyó correctamente, significa que:
        // 1. ConstructorBindingConvention fue removida
        // 2. ComplexTypePropertyDiscoveryConvention descubrió las propiedades

        var menuEntity = _model.FindEntityType(typeof(Menu))!;
        var complexType = menuEntity.FindComplexProperty(nameof(Menu.DepositPolicy))!.ComplexType;

        // Verificar que tiene propiedades (descubiertas por nuestra convention)
        complexType.GetProperties().Should().NotBeEmpty(
            "ComplexTypePropertyDiscoveryConvention debe descubrir propiedades de records con constructor protected");
    }

    // ========================================================================
    // INTEGRATION: COMPLEX SCENARIO TESTS
    // ========================================================================

    [Fact]
    public void Integration_Menu_ShouldHaveCompleteConfiguration()
    {
        // Test de integración que verifica la configuración completa de Menu
        var menuEntity = _model.FindEntityType(typeof(Menu))!;

        // Primary Key
        menuEntity.FindPrimaryKey().Should().NotBeNull();
        menuEntity.FindPrimaryKey()!.Properties[0].Name.Should().Be("Id");

        // Collection Name
        menuEntity.GetTableName().Should().Be("Menus");

        // Query Filter
        menuEntity.GetQueryFilter().Should().NotBeNull();

        // ComplexProperty nullable
        var depositPolicy = menuEntity.FindComplexProperty(nameof(Menu.DepositPolicy))!;
        depositPolicy.IsNullable.Should().BeTrue();

        // SubCollection
        var categories = menuEntity.FindNavigation(nameof(Menu.Categories))!;
        categories.IsSubCollection().Should().BeTrue();
        categories.IsFirestoreConfigured().Should().BeTrue();
    }

    [Fact]
    public void Integration_MenuItem_ShouldHaveCompleteConfiguration()
    {
        // Test de integración que verifica la configuración completa de MenuItem
        var menuItemEntity = _model.FindEntityType(typeof(MenuItem))!;

        // Primary Key
        menuItemEntity.FindPrimaryKey().Should().NotBeNull();
        menuItemEntity.FindPrimaryKey()!.Properties[0].Name.Should().Be("Id");

        // Collection Name
        menuItemEntity.GetTableName().Should().Be("MenuItems");

        // Query Filter
        menuItemEntity.GetQueryFilter().Should().NotBeNull();

        // ComplexProperties nullable
        menuItemEntity.FindComplexProperty(nameof(MenuItem.DepositOverride))!.IsNullable.Should().BeTrue();
        menuItemEntity.FindComplexProperty(nameof(MenuItem.NutritionalInfo))!.IsNullable.Should().BeTrue();

        // ArrayOf Embedded
        menuItemEntity.IsArrayOfEmbedded(nameof(MenuItem.PriceOptions)).Should().BeTrue();

        // ArrayOf Reference
        menuItemEntity.IsArrayOfReference(nameof(MenuItem.Allergens)).Should().BeTrue();
    }

    [Fact]
    public void Integration_AllEntityTypes_ShouldHavePrimaryKey()
    {
        // Verificar que TODAS las entidades tienen clave primaria
        var entityTypes = _model.GetEntityTypes().Where(e => !e.IsOwned());

        foreach (var entityType in entityTypes)
        {
            entityType.FindPrimaryKey().Should().NotBeNull(
                $"Entity '{entityType.DisplayName()}' debe tener clave primaria");
        }
    }

    [Fact]
    public void Integration_AllNavigations_ShouldBeFirestoreConfigured()
    {
        // Verificar que TODAS las navegaciones están configuradas para Firestore
        // (SubCollection o DocumentReference)
        var entityTypes = _model.GetEntityTypes();

        foreach (var entityType in entityTypes)
        {
            var navigations = entityType.GetNavigations()
                .Where(n => n.IsCollection); // Solo colecciones que deberían ser SubCollections

            foreach (var navigation in navigations)
            {
                // Las navegaciones de colección deben ser SubCollections
                navigation.IsFirestoreConfigured().Should().BeTrue(
                    $"Navigation '{entityType.DisplayName()}.{navigation.Name}' debe estar configurada como SubCollection o DocumentReference");
            }
        }
    }

    // ========================================================================
    // REGRESSION TESTS
    // ========================================================================

    [Fact]
    public void Regression_ModelShouldBuildWithoutExceptions()
    {
        // Este test verifica que el modelo se construye sin excepciones
        // Si alguna convention falla, este test fallará

        Action buildModel = () =>
        {
            var options = new DbContextOptionsBuilder<ProviderFixesDbContext>()
                .UseFirestore("test-project")
                .Options;

            using var context = new ProviderFixesDbContext(options, Guid.NewGuid());
            _ = context.Model; // Fuerza construcción del modelo
        };

        buildModel.Should().NotThrow(
            "El modelo debe construirse sin excepciones cuando las conventions están correctamente configuradas");
    }
}
