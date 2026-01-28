using Fudie.Firestore.IntegrationTest.Helpers.Plan;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.IntegrationTest.Plan;

/// <summary>
/// Tests de integración CRUD para el agregado Plan.
/// Valida que ComplexTypes anidados (Money con Currency) y ArrayOf
/// se persisten y recuperan correctamente en Firestore.
/// </summary>
public class PlanCrudTests
{
    private const string EmulatorHost = "127.0.0.1:8080";
    private const string ProjectId = "demo-project";

    private readonly DbContextOptions<PlanDbContext> _options;

    public PlanCrudTests()
    {
        Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", EmulatorHost);

        _options = new DbContextOptionsBuilder<PlanDbContext>()
            .UseFirestore(ProjectId)
            .Options;
    }

    private PlanDbContext CreateWriteContext() => new(_options);
    private PlanDbContext CreateReadContext() => new(_options);

    private static string GenerateId(string prefix = "plan") => $"{prefix}-{Guid.NewGuid():N}";

    #region Create Tests

    [Fact]
    public async Task Add_PlanWithNestedComplexType_ShouldPersist()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new Helpers.Plan.Plan(planId)
        {
            Name = $"Plan Premium-{GenerateId()}",
            Description = "Plan con todas las funcionalidades",
            Price = new Money(99.99m, Currency.EUR),
            BillingPeriod = BillingPeriod.Monthly,
            IsActive = true
        };

        // Act
        using (var writeContext = CreateWriteContext())
        {
            writeContext.Plans.Add(plan);
            await writeContext.SaveChangesAsync();
        }

        // Assert
        using var readContext = CreateReadContext();
        var planLeido = await readContext.Plans.FirstOrDefaultAsync(p => p.Id == planId);

        planLeido.Should().NotBeNull();
        planLeido!.Name.Should().StartWith("Plan Premium-");
        planLeido.Price.Amount.Should().Be(99.99m);
        planLeido.Price.Currency.Code.Should().Be("EUR");
        planLeido.Price.Currency.Symbol.Should().Be("€");
        planLeido.Price.Currency.DecimalPlaces.Should().Be(2);
    }

    [Fact]
    public async Task Add_PlanWithDifferentCurrency_ShouldPersist()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new Helpers.Plan.Plan(planId)
        {
            Name = $"Plan USD-{GenerateId()}",
            Description = "Plan en dólares",
            Price = new Money(149.99m, Currency.USD),
            BillingPeriod = BillingPeriod.Yearly,
            IsActive = true
        };

        // Act
        using (var writeContext = CreateWriteContext())
        {
            writeContext.Plans.Add(plan);
            await writeContext.SaveChangesAsync();
        }

        // Assert
        using var readContext = CreateReadContext();
        var planLeido = await readContext.Plans.FirstOrDefaultAsync(p => p.Id == planId);

        planLeido.Should().NotBeNull();
        planLeido!.Price.Currency.Code.Should().Be("USD");
        planLeido.Price.Currency.Symbol.Should().Be("$");
    }

    [Fact]
    public async Task Add_PlanWithFeatures_ShouldPersistArrayOf()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new Helpers.Plan.Plan(planId)
        {
            Name = $"Plan con Features-{GenerateId()}",
            Description = "Plan con características",
            Price = new Money(49.99m, Currency.EUR),
            BillingPeriod = BillingPeriod.Monthly,
            IsActive = true
        };

        plan._features.Add(new Feature("USERS", "Usuarios", "Número máximo de usuarios", FeatureType.Limit, 10, "usuarios"));
        plan._features.Add(new Feature("STORAGE", "Almacenamiento", "Espacio de almacenamiento", FeatureType.Limit, 100, "GB"));
        plan._features.Add(new Feature("SUPPORT", "Soporte Premium", "Soporte 24/7", FeatureType.Boolean));
        plan._features.Add(new Feature("API", "API Access", "Acceso ilimitado a la API", FeatureType.Unlimited));

        // Act
        using (var writeContext = CreateWriteContext())
        {
            writeContext.Plans.Add(plan);
            await writeContext.SaveChangesAsync();
        }

        // Assert
        using var readContext = CreateReadContext();
        var planLeido = await readContext.Plans.FirstOrDefaultAsync(p => p.Id == planId);

        planLeido.Should().NotBeNull();
        planLeido!.Features.Should().HaveCount(4);

        var usersFeature = planLeido.Features.FirstOrDefault(f => f.Code == "USERS");
        usersFeature.Should().NotBeNull();
        usersFeature!.Name.Should().Be("Usuarios");
        usersFeature.Type.Should().Be(FeatureType.Limit);
        usersFeature.Limit.Should().Be(10);
        usersFeature.Unit.Should().Be("usuarios");
    }

    [Fact]
    public async Task Add_PlanWithProviderConfigurations_ShouldPersistArrayOf()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new Helpers.Plan.Plan(planId)
        {
            Name = $"Plan con Providers-{GenerateId()}",
            Description = "Plan con configuraciones de pago",
            Price = new Money(29.99m, Currency.GBP),
            BillingPeriod = BillingPeriod.Quarterly,
            IsActive = true
        };

        plan._providerConfigurations.Add(new PaymentProviderConfig("Stripe", "prod_123", "price_456", true));
        plan._providerConfigurations.Add(new PaymentProviderConfig("PayPal", "prod_789", "price_012", false));

        // Act
        using (var writeContext = CreateWriteContext())
        {
            writeContext.Plans.Add(plan);
            await writeContext.SaveChangesAsync();
        }

        // Assert
        using var readContext = CreateReadContext();
        var planLeido = await readContext.Plans.FirstOrDefaultAsync(p => p.Id == planId);

        planLeido.Should().NotBeNull();
        planLeido!.ProviderConfigurations.Should().HaveCount(2);

        var stripeConfig = planLeido.ProviderConfigurations.FirstOrDefault(p => p.Provider == "Stripe");
        stripeConfig.Should().NotBeNull();
        stripeConfig!.ExternalProductId.Should().Be("prod_123");
        stripeConfig.ExternalPriceId.Should().Be("price_456");
        stripeConfig.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Add_CompletePlan_ShouldPersistAllData()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new Helpers.Plan.Plan(planId)
        {
            Name = $"Plan Enterprise-{GenerateId()}",
            Description = "Plan empresarial completo",
            Price = new Money(999.99m, Currency.EUR),
            BillingPeriod = BillingPeriod.Yearly,
            IsActive = true
        };

        plan._features.Add(new Feature("USERS", "Usuarios", null, FeatureType.Unlimited));
        plan._features.Add(new Feature("STORAGE", "Almacenamiento", "Espacio ilimitado", FeatureType.Unlimited));
        plan._features.Add(new Feature("SUPPORT", "Soporte", "Soporte prioritario", FeatureType.Boolean));

        plan._providerConfigurations.Add(new PaymentProviderConfig("Stripe", "prod_ent", "price_ent", true));

        // Act
        using (var writeContext = CreateWriteContext())
        {
            writeContext.Plans.Add(plan);
            await writeContext.SaveChangesAsync();
        }

        // Assert
        using var readContext = CreateReadContext();
        var planLeido = await readContext.Plans.FirstOrDefaultAsync(p => p.Id == planId);

        planLeido.Should().NotBeNull();
        planLeido!.Name.Should().StartWith("Plan Enterprise-");
        planLeido.Price.Amount.Should().Be(999.99m);
        planLeido.Price.Currency.Code.Should().Be("EUR");
        planLeido.BillingPeriod.Should().Be(BillingPeriod.Yearly);
        planLeido.Features.Should().HaveCount(3);
        planLeido.ProviderConfigurations.Should().HaveCount(1);
        planLeido.HasActiveProvider.Should().BeTrue();
    }

    #endregion

    #region Read Tests

    [Fact]
    public async Task Query_PlanByPrice_ShouldFilterCorrectly()
    {
        // Arrange
        var uniqueTag = GenerateId("price");
        var cheapPlanId = Guid.NewGuid();
        var expensivePlanId = Guid.NewGuid();

        using (var writeContext = CreateWriteContext())
        {
            var plans = new[]
            {
                new Helpers.Plan.Plan(cheapPlanId)
                {
                    Name = $"{uniqueTag}-Cheap",
                    Description = "Plan económico",
                    Price = new Money(9.99m, Currency.EUR),
                    BillingPeriod = BillingPeriod.Monthly,
                    IsActive = true
                },
                new Helpers.Plan.Plan(expensivePlanId)
                {
                    Name = $"{uniqueTag}-Expensive",
                    Description = "Plan premium",
                    Price = new Money(99.99m, Currency.EUR),
                    BillingPeriod = BillingPeriod.Monthly,
                    IsActive = true
                }
            };

            writeContext.Plans.AddRange(plans);
            await writeContext.SaveChangesAsync();
        }

        // Act
        using var readContext = CreateReadContext();
        var expensivePlans = await readContext.Plans
            .Where(p => p.Name.StartsWith(uniqueTag) && p.Price.Amount > 50m)
            .ToListAsync();

        // Assert
        expensivePlans.Should().HaveCount(1);
        expensivePlans[0].Name.Should().Contain("Expensive");
    }

    [Fact]
    public async Task Query_PlanByBillingPeriod_ShouldFilterByEnum()
    {
        // Arrange
        var uniqueTag = GenerateId("period");
        var monthlyPlanId = Guid.NewGuid();
        var yearlyPlanId = Guid.NewGuid();

        using (var writeContext = CreateWriteContext())
        {
            var plans = new[]
            {
                new Helpers.Plan.Plan(monthlyPlanId)
                {
                    Name = $"{uniqueTag}-Monthly",
                    Description = "Mensual",
                    Price = new Money(10m, Currency.EUR),
                    BillingPeriod = BillingPeriod.Monthly,
                    IsActive = true
                },
                new Helpers.Plan.Plan(yearlyPlanId)
                {
                    Name = $"{uniqueTag}-Yearly",
                    Description = "Anual",
                    Price = new Money(100m, Currency.EUR),
                    BillingPeriod = BillingPeriod.Yearly,
                    IsActive = true
                }
            };

            writeContext.Plans.AddRange(plans);
            await writeContext.SaveChangesAsync();
        }

        // Act
        using var readContext = CreateReadContext();
        var yearlyPlans = await readContext.Plans
            .Where(p => p.Name.StartsWith(uniqueTag) && p.BillingPeriod == BillingPeriod.Yearly)
            .ToListAsync();

        // Assert
        yearlyPlans.Should().HaveCount(1);
        yearlyPlans[0].BillingPeriod.Should().Be(BillingPeriod.Yearly);
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Update_PlanPrice_ShouldPersistNestedComplexTypeChanges()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new Helpers.Plan.Plan(planId)
        {
            Name = $"Plan Update Price-{GenerateId()}",
            Description = "Test update",
            Price = new Money(50m, Currency.EUR),
            BillingPeriod = BillingPeriod.Monthly,
            IsActive = true
        };

        using (var writeContext = CreateWriteContext())
        {
            writeContext.Plans.Add(plan);
            await writeContext.SaveChangesAsync();
        }

        // Act - Update price with different currency
        using (var updateContext = CreateWriteContext())
        {
            var planToUpdate = await updateContext.Plans.FirstOrDefaultAsync(p => p.Id == planId);
            planToUpdate!.Price = new Money(75m, Currency.USD);
            await updateContext.SaveChangesAsync();
        }

        // Assert
        using var readContext = CreateReadContext();
        var updatedPlan = await readContext.Plans.FirstOrDefaultAsync(p => p.Id == planId);

        updatedPlan.Should().NotBeNull();
        updatedPlan!.Price.Amount.Should().Be(75m);
        updatedPlan.Price.Currency.Code.Should().Be("USD");
        updatedPlan.Price.Currency.Symbol.Should().Be("$");
    }

    [Fact]
    public async Task Update_PlanFeatures_ShouldPersistArrayOfChanges()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new Helpers.Plan.Plan(planId)
        {
            Name = $"Plan Update Features-{GenerateId()}",
            Description = "Test update features",
            Price = new Money(30m, Currency.EUR),
            BillingPeriod = BillingPeriod.Monthly,
            IsActive = true
        };

        plan._features.Add(new Feature("USERS", "Usuarios", null, FeatureType.Limit, 5, "usuarios"));

        using (var writeContext = CreateWriteContext())
        {
            writeContext.Plans.Add(plan);
            await writeContext.SaveChangesAsync();
        }

        // Act - Add more features
        using (var updateContext = CreateWriteContext())
        {
            var planToUpdate = await updateContext.Plans.FirstOrDefaultAsync(p => p.Id == planId);

            var entry = updateContext.ChangeTracker.Entries<Helpers.Plan.Plan>().First();
            var shadowProp = entry.Properties.First(p => p.Metadata.Name == "__Features_Json");
            
            planToUpdate!._features.Add(new Feature("STORAGE", "Almacenamiento", null, FeatureType.Limit, 50, "GB"));
            planToUpdate._features.Add(new Feature("API", "API", null, FeatureType.Boolean));
            await updateContext.SaveChangesAsync();
        }

        // Assert
        using var readContext = CreateReadContext();
        var updatedPlan = await readContext.Plans.FirstOrDefaultAsync(p => p.Id == planId);

        updatedPlan.Should().NotBeNull();
        updatedPlan!.Features.Should().HaveCount(3);
    }

    [Fact]
    public async Task Update_PlanBasicProperties_ShouldPersist()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new Helpers.Plan.Plan(planId)
        {
            Name = $"Nombre Original-{GenerateId()}",
            Description = "Descripción Original",
            Price = new Money(25m, Currency.EUR),
            BillingPeriod = BillingPeriod.Monthly,
            IsActive = true
        };

        using (var writeContext = CreateWriteContext())
        {
            writeContext.Plans.Add(plan);
            await writeContext.SaveChangesAsync();
        }

        // Act
        using (var updateContext = CreateWriteContext())
        {
            var planToUpdate = await updateContext.Plans.FirstOrDefaultAsync(p => p.Id == planId);
            planToUpdate!.Name = $"Nombre Modificado-{GenerateId()}";
            planToUpdate.Description = "Descripción Modificada";
            planToUpdate.BillingPeriod = BillingPeriod.Semester;
            planToUpdate.IsActive = false;
            await updateContext.SaveChangesAsync();
        }

        // Assert
        using var readContext = CreateReadContext();
        var updatedPlan = await readContext.Plans.FirstOrDefaultAsync(p => p.Id == planId);

        updatedPlan.Should().NotBeNull();
        updatedPlan!.Name.Should().StartWith("Nombre Modificado-");
        updatedPlan.Description.Should().Be("Descripción Modificada");
        updatedPlan.BillingPeriod.Should().Be(BillingPeriod.Semester);
        updatedPlan.IsActive.Should().BeFalse();
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task Delete_Plan_ShouldRemoveFromFirestore()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new Helpers.Plan.Plan(planId)
        {
            Name = $"Plan a Eliminar-{GenerateId()}",
            Description = "Este plan será eliminado",
            Price = new Money(15m, Currency.EUR),
            BillingPeriod = BillingPeriod.Monthly,
            IsActive = true
        };

        plan._features.Add(new Feature("TEST", "Test", null, FeatureType.Boolean));

        using (var writeContext = CreateWriteContext())
        {
            writeContext.Plans.Add(plan);
            await writeContext.SaveChangesAsync();
        }

        // Act
        using (var deleteContext = CreateWriteContext())
        {
            var planToDelete = await deleteContext.Plans.FirstOrDefaultAsync(p => p.Id == planId);
            deleteContext.Plans.Remove(planToDelete!);
            await deleteContext.SaveChangesAsync();
        }

        // Assert
        using var readContext = CreateReadContext();
        var deletedPlan = await readContext.Plans.FirstOrDefaultAsync(p => p.Id == planId);

        deletedPlan.Should().BeNull();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Add_PlanWithZeroPrice_ShouldPersist()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new Helpers.Plan.Plan(planId)
        {
            Name = $"Plan Gratuito-{GenerateId()}",
            Description = "Plan sin costo",
            Price = Money.Zero(Currency.EUR),
            BillingPeriod = BillingPeriod.Monthly,
            IsActive = true
        };

        // Act
        using (var writeContext = CreateWriteContext())
        {
            writeContext.Plans.Add(plan);
            await writeContext.SaveChangesAsync();
        }

        // Assert
        using var readContext = CreateReadContext();
        var planLeido = await readContext.Plans.FirstOrDefaultAsync(p => p.Id == planId);

        planLeido.Should().NotBeNull();
        planLeido!.Price.Amount.Should().Be(0m);
        planLeido.Price.IsZero.Should().BeTrue();
    }

    [Fact]
    public async Task Add_PlanWithEmptyFeatures_ShouldPersist()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new Helpers.Plan.Plan(planId)
        {
            Name = $"Plan Sin Features-{GenerateId()}",
            Description = "Plan básico sin características",
            Price = new Money(5m, Currency.EUR),
            BillingPeriod = BillingPeriod.Monthly,
            IsActive = true
        };

        // Act
        using (var writeContext = CreateWriteContext())
        {
            writeContext.Plans.Add(plan);
            await writeContext.SaveChangesAsync();
        }

        // Assert
        using var readContext = CreateReadContext();
        var planLeido = await readContext.Plans.FirstOrDefaultAsync(p => p.Id == planId);

        planLeido.Should().NotBeNull();
        planLeido!.Features.Should().BeEmpty();
        planLeido.ProviderConfigurations.Should().BeEmpty();
    }

    [Fact]
    public async Task Query_PlanWithNestedCurrencyProperty_ShouldWork()
    {
        // Arrange
        var uniqueTag = GenerateId("currency");
        var eurPlanId = Guid.NewGuid();
        var usdPlanId = Guid.NewGuid();

        using (var writeContext = CreateWriteContext())
        {
            var plans = new[]
            {
                new Helpers.Plan.Plan(eurPlanId)
                {
                    Name = $"{uniqueTag}-EUR",
                    Description = "Plan en euros",
                    Price = new Money(50m, Currency.EUR),
                    BillingPeriod = BillingPeriod.Monthly,
                    IsActive = true
                },
                new Helpers.Plan.Plan(usdPlanId)
                {
                    Name = $"{uniqueTag}-USD",
                    Description = "Plan en dólares",
                    Price = new Money(50m, Currency.USD),
                    BillingPeriod = BillingPeriod.Monthly,
                    IsActive = true
                }
            };

            writeContext.Plans.AddRange(plans);
            await writeContext.SaveChangesAsync();
        }

        // Act - Query by nested currency code
        using var readContext = CreateReadContext();
        var eurPlans = await readContext.Plans
            .Where(p => p.Name.StartsWith(uniqueTag) && p.Price.Currency.Code == "EUR")
            .ToListAsync();

        // Assert
        eurPlans.Should().HaveCount(1);
        eurPlans[0].Price.Currency.Code.Should().Be("EUR");
    }

    #endregion
}
