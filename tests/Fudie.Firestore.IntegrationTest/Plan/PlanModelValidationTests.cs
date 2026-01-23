using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;
using Fudie.Firestore.IntegrationTest.Helpers.Plan;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Fudie.Firestore.IntegrationTest.Plan;

/// <summary>
/// Tests de validación del modelo Plan.
/// Verifica que las convenciones y configuración del DbContext son correctas:
/// - ComplexType anidado: Money con Currency
/// - ArrayOf: Features, ProviderConfigurations
/// - Propiedades computed ignoradas
/// </summary>
public class PlanModelValidationTests
{
    private const string EmulatorHost = "127.0.0.1:8080";
    private const string ProjectId = "demo-project";

    private readonly PlanDbContext _context;
    private readonly IModel _model;

    public PlanModelValidationTests()
    {
        Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", EmulatorHost);

        var options = new DbContextOptionsBuilder<PlanDbContext>()
            .UseFirestore(ProjectId)
            .Options;

        _context = new PlanDbContext(options);
        _model = _context.Model;
    }

    [Fact]
    public void Model_ShouldHavePlanEntityConfigured()
    {
        // Act
        var planEntityType = _model.FindEntityType(typeof(Helpers.Plan.Plan));

        // Assert
        planEntityType.Should().NotBeNull("Plan entity should be configured in the model");
    }

    [Fact]
    public void Model_Price_ShouldBeConfiguredAsComplexType()
    {
        // Act
        var planEntityType = _model.FindEntityType(typeof(Helpers.Plan.Plan));
        var priceProperty = planEntityType?.FindComplexProperty("Price");

        // Assert
        priceProperty.Should().NotBeNull("Price should be configured as ComplexType");
    }

    [Fact]
    public void Model_Currency_ShouldBeNestedComplexType()
    {
        // Act
        var planEntityType = _model.FindEntityType(typeof(Helpers.Plan.Plan));
        var priceProperty = planEntityType?.FindComplexProperty("Price");
        var currencyProperty = priceProperty?.ComplexType.FindComplexProperty("Currency");

        // Assert
        currencyProperty.Should().NotBeNull("Currency should be nested ComplexType inside Money");
    }

    [Fact]
    public void Model_HasActiveProvider_ShouldBeIgnored()
    {
        // Act
        var planEntityType = _model.FindEntityType(typeof(Helpers.Plan.Plan));
        var hasActiveProviderProperty = planEntityType?.FindProperty("HasActiveProvider");

        // Assert
        hasActiveProviderProperty.Should().BeNull("HasActiveProvider is a computed property and should be ignored");
    }

    [Fact]
    public void Model_MoneyComputedProperties_ShouldBeIgnored()
    {
        // Act
        var planEntityType = _model.FindEntityType(typeof(Helpers.Plan.Plan));
        var priceComplexType = planEntityType?.FindComplexProperty("Price")?.ComplexType;

        var isZeroProperty = priceComplexType?.FindProperty("IsZero");
        var isPositiveProperty = priceComplexType?.FindProperty("IsPositive");
        var isNegativeProperty = priceComplexType?.FindProperty("IsNegative");

        // Assert
        isZeroProperty.Should().BeNull("IsZero is a computed property and should be ignored");
        isPositiveProperty.Should().BeNull("IsPositive is a computed property and should be ignored");
        isNegativeProperty.Should().BeNull("IsNegative is a computed property and should be ignored");
    }

    [Fact]
    public void Model_Features_ShouldHaveArrayOfAnnotation()
    {
        // Act
        var planEntityType = _model.FindEntityType(typeof(Helpers.Plan.Plan));

        // Assert
        planEntityType.Should().NotBeNull();
        planEntityType!.IsArrayOf("Features").Should().BeTrue("Features should be configured as ArrayOf");
        planEntityType!.IsArrayOfEmbedded("Features").Should().BeTrue("Features should be ArrayOf Embedded type");
    }

    [Fact]
    public void Model_ProviderConfigurations_ShouldHaveArrayOfAnnotation()
    {
        // Act
        var planEntityType = _model.FindEntityType(typeof(Helpers.Plan.Plan));

        // Assert
        planEntityType.Should().NotBeNull();
        planEntityType!.IsArrayOf("ProviderConfigurations").Should().BeTrue("ProviderConfigurations should be configured as ArrayOf");
        planEntityType!.IsArrayOfEmbedded("ProviderConfigurations").Should().BeTrue("ProviderConfigurations should be ArrayOf Embedded type");
    }

    [Fact]
    public void Model_Features_ShouldHaveCorrectElementType()
    {
        // Act
        var planEntityType = _model.FindEntityType(typeof(Helpers.Plan.Plan));
        var elementType = planEntityType?.GetArrayOfElementClrType("Features");

        // Assert
        elementType.Should().Be(typeof(Feature), "Features element type should be Feature");
    }

    [Fact]
    public void Model_CurrencyProperties_ShouldBeConfigured()
    {
        // Act
        var planEntityType = _model.FindEntityType(typeof(Helpers.Plan.Plan));
        var priceComplexType = planEntityType?.FindComplexProperty("Price")?.ComplexType;
        var currencyComplexType = priceComplexType?.FindComplexProperty("Currency")?.ComplexType;

        // Assert
        currencyComplexType.Should().NotBeNull("Currency ComplexType should exist");

        var codeProperty = currencyComplexType?.FindProperty("Code");
        var symbolProperty = currencyComplexType?.FindProperty("Symbol");
        var decimalPlacesProperty = currencyComplexType?.FindProperty("DecimalPlaces");

        codeProperty.Should().NotBeNull("Currency.Code should be mapped");
        symbolProperty.Should().NotBeNull("Currency.Symbol should be mapped");
        decimalPlacesProperty.Should().NotBeNull("Currency.DecimalPlaces should be mapped");
    }

    [Fact]
    public void Model_ShouldBuildWithoutExceptions()
    {
        // Este test verifica que el modelo se construye sin excepciones
        Action buildModel = () =>
        {
            var options = new DbContextOptionsBuilder<PlanDbContext>()
                .UseFirestore(ProjectId)
                .Options;

            using var context = new PlanDbContext(options);
            _ = context.Model;
        };

        buildModel.Should().NotThrow(
            "El modelo debe construirse sin excepciones cuando las conventions están correctamente configuradas");
    }
}
