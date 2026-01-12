using Fudie.Firestore.EntityFrameworkCore.Infrastructure;
using Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Fudie.Firestore.UnitTest.Infrastructure;

public class FirestoreDbContextOptionsExtensionsTest
{
    [Fact]
    public void UseFirestore_ShouldThrow_WhenOptionsBuilderIsNull()
    {
        // Arrange
        DbContextOptionsBuilder? optionsBuilder = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            optionsBuilder!.UseFirestore("test-project"));
    }

    [Fact]
    public void UseFirestore_ShouldThrow_WhenProjectIdIsNull()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        string? projectId = null;

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            optionsBuilder.UseFirestore(projectId!));
    }

    [Fact]
    public void UseFirestore_ShouldThrow_WhenProjectIdIsEmpty()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            optionsBuilder.UseFirestore(""));
    }

    [Fact]
    public void UseFirestore_ShouldThrow_WhenProjectIdIsWhitespace()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            optionsBuilder.UseFirestore("   "));
    }

    [Fact]
    public void UseFirestore_ShouldReturnOptionsBuilder()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();

        // Act
        var result = optionsBuilder.UseFirestore("test-project");

        // Assert
        Assert.Same(optionsBuilder, result);
    }

    [Fact]
    public void UseFirestore_ShouldAddFirestoreOptionsExtension()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();

        // Act
        optionsBuilder.UseFirestore("test-project");

        // Assert
        var extension = optionsBuilder.Options.FindExtension<FirestoreOptionsExtension>();
        Assert.NotNull(extension);
        Assert.Equal("test-project", extension.ProjectId);
    }

    [Fact]
    public void UseFirestore_WithCredentialsPath_ShouldAddExtension()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();

        // Act
        optionsBuilder.UseFirestore("test-project", "/path/to/credentials.json");

        // Assert
        var extension = optionsBuilder.Options.FindExtension<FirestoreOptionsExtension>();
        Assert.NotNull(extension);
        Assert.Equal("test-project", extension.ProjectId);
        Assert.Equal("/path/to/credentials.json", extension.CredentialsPath);
    }

    [Fact]
    public void UseFirestore_WithOptionsAction_ShouldInvokeAction()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        var actionInvoked = false;

        // Act
        optionsBuilder.UseFirestore("test-project", builder =>
        {
            actionInvoked = true;
            builder.UseDatabaseId("my-db");
        });

        // Assert
        Assert.True(actionInvoked);
    }

    [Fact]
    public void UseFirestore_WithNullOptionsAction_ShouldNotThrow()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();

        // Act & Assert
        var exception = Record.Exception(() =>
            optionsBuilder.UseFirestore("test-project", null));

        Assert.Null(exception);
    }

    [Fact]
    public void UseFirestore_Generic_ShouldReturnTypedOptionsBuilder()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();

        // Act
        var result = optionsBuilder.UseFirestore("test-project");

        // Assert
        Assert.IsType<DbContextOptionsBuilder<TestDbContext>>(result);
    }

    [Fact]
    public void UseFirestore_Generic_WithCredentials_ShouldReturnTypedOptionsBuilder()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();

        // Act
        var result = optionsBuilder.UseFirestore("test-project", "/path/to/creds.json");

        // Assert
        Assert.IsType<DbContextOptionsBuilder<TestDbContext>>(result);
    }

    [Fact]
    public void UseFirestore_ShouldAllowMultipleConfigurations()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();

        // Act
        optionsBuilder.UseFirestore("test-project", builder =>
        {
            builder
                .UseDatabaseId("my-database")
                .QueryLogLevel(QueryLogLevel.Full)
                .PipelineMaxRetries(5);
        });

        // Assert
        var extension = optionsBuilder.Options.FindExtension<FirestoreOptionsExtension>();
        Assert.NotNull(extension);
        Assert.Equal("test-project", extension.ProjectId);
        Assert.Equal("my-database", extension.DatabaseId);
        Assert.Equal(QueryLogLevel.Full, extension.PipelineOptions.QueryLogLevel);
        Assert.Equal(5, extension.PipelineOptions.MaxRetries);
    }

    #region UseFirestore(IServiceProvider) Tests

    [Fact]
    public void UseFirestore_WithServiceProvider_ShouldThrow_WhenOptionsBuilderIsNull()
    {
        // Arrange
        DbContextOptionsBuilder? optionsBuilder = null;
        var serviceProvider = CreateServiceProviderWithConfiguration(new Dictionary<string, string?>
        {
            ["Firestore:ProjectId"] = "test-project"
        });

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            optionsBuilder!.UseFirestore(serviceProvider));
    }

    [Fact]
    public void UseFirestore_WithServiceProvider_ShouldThrow_WhenServiceProviderIsNull()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        IServiceProvider? serviceProvider = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            optionsBuilder.UseFirestore(serviceProvider!));
    }

    [Fact]
    public void UseFirestore_WithServiceProvider_ShouldReadProjectIdFromConfiguration()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        var serviceProvider = CreateServiceProviderWithConfiguration(new Dictionary<string, string?>
        {
            ["Firestore:ProjectId"] = "my-project-from-config"
        });

        // Act
        optionsBuilder.UseFirestore(serviceProvider);

        // Assert
        var extension = optionsBuilder.Options.FindExtension<FirestoreOptionsExtension>();
        Assert.NotNull(extension);
        Assert.Equal("my-project-from-config", extension.ProjectId);
    }

    [Fact]
    public void UseFirestore_WithServiceProvider_ShouldReadAllFirstLevelSettings()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        var serviceProvider = CreateServiceProviderWithConfiguration(new Dictionary<string, string?>
        {
            ["Firestore:ProjectId"] = "test-project",
            ["Firestore:EmulatorHost"] = "localhost:8080",
            ["Firestore:CredentialsPath"] = "/path/to/creds.json",
            ["Firestore:DatabaseId"] = "my-database"
        });

        // Act
        optionsBuilder.UseFirestore(serviceProvider);

        // Assert
        var extension = optionsBuilder.Options.FindExtension<FirestoreOptionsExtension>();
        Assert.NotNull(extension);
        Assert.Equal("test-project", extension.ProjectId);
        Assert.Equal("localhost:8080", extension.EmulatorHost);
        Assert.Equal("/path/to/creds.json", extension.CredentialsPath);
        Assert.Equal("my-database", extension.DatabaseId);
    }

    [Fact]
    public void UseFirestore_WithServiceProvider_ShouldReadPipelineSection()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        var serviceProvider = CreateServiceProviderWithConfiguration(new Dictionary<string, string?>
        {
            ["Firestore:ProjectId"] = "test-project",
            ["Firestore:Pipeline:QueryLogLevel"] = "Full",
            ["Firestore:Pipeline:EnableAstLogging"] = "true",
            ["Firestore:Pipeline:EnableCaching"] = "false",
            ["Firestore:Pipeline:MaxRetries"] = "5",
            ["Firestore:Pipeline:RetryInitialDelayMs"] = "200"
        });

        // Act
        optionsBuilder.UseFirestore(serviceProvider);

        // Assert
        var extension = optionsBuilder.Options.FindExtension<FirestoreOptionsExtension>();
        Assert.NotNull(extension);
        Assert.Equal(QueryLogLevel.Full, extension.PipelineOptions.QueryLogLevel);
        Assert.True(extension.PipelineOptions.EnableAstLogging);
        Assert.False(extension.PipelineOptions.EnableCaching);
        Assert.Equal(5, extension.PipelineOptions.MaxRetries);
        Assert.Equal(TimeSpan.FromMilliseconds(200), extension.PipelineOptions.RetryInitialDelay);
    }

    [Fact]
    public void UseFirestore_WithServiceProvider_Generic_ShouldReturnTypedOptionsBuilder()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        var serviceProvider = CreateServiceProviderWithConfiguration(new Dictionary<string, string?>
        {
            ["Firestore:ProjectId"] = "test-project"
        });

        // Act
        var result = optionsBuilder.UseFirestore(serviceProvider);

        // Assert
        Assert.IsType<DbContextOptionsBuilder<TestDbContext>>(result);
    }

    #endregion

    #region UseFirestore(IConfiguration) Tests

    [Fact]
    public void UseFirestore_WithConfiguration_ShouldThrow_WhenOptionsBuilderIsNull()
    {
        // Arrange
        DbContextOptionsBuilder? optionsBuilder = null;
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Firestore:ProjectId"] = "test-project"
        });

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            optionsBuilder!.UseFirestore(configuration));
    }

    [Fact]
    public void UseFirestore_WithConfiguration_ShouldThrow_WhenConfigurationIsNull()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        IConfiguration? configuration = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            optionsBuilder.UseFirestore(configuration!));
    }

    [Fact]
    public void UseFirestore_WithConfiguration_ShouldThrow_WhenProjectIdIsMissing()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Firestore:EmulatorHost"] = "localhost:8080"
        });

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            optionsBuilder.UseFirestore(configuration));
        Assert.Contains("ProjectId", exception.Message);
    }

    [Fact]
    public void UseFirestore_WithConfiguration_ShouldReadProjectId()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Firestore:ProjectId"] = "config-project"
        });

        // Act
        optionsBuilder.UseFirestore(configuration);

        // Assert
        var extension = optionsBuilder.Options.FindExtension<FirestoreOptionsExtension>();
        Assert.NotNull(extension);
        Assert.Equal("config-project", extension.ProjectId);
    }

    [Fact]
    public void UseFirestore_WithConfiguration_ShouldReadAllFirstLevelSettings()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Firestore:ProjectId"] = "test-project",
            ["Firestore:EmulatorHost"] = "127.0.0.1:8080",
            ["Firestore:CredentialsPath"] = "/credentials.json",
            ["Firestore:DatabaseId"] = "custom-db"
        });

        // Act
        optionsBuilder.UseFirestore(configuration);

        // Assert
        var extension = optionsBuilder.Options.FindExtension<FirestoreOptionsExtension>();
        Assert.NotNull(extension);
        Assert.Equal("test-project", extension.ProjectId);
        Assert.Equal("127.0.0.1:8080", extension.EmulatorHost);
        Assert.Equal("/credentials.json", extension.CredentialsPath);
        Assert.Equal("custom-db", extension.DatabaseId);
    }

    [Fact]
    public void UseFirestore_WithConfiguration_ShouldReadPipelineSection()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Firestore:ProjectId"] = "test-project",
            ["Firestore:Pipeline:QueryLogLevel"] = "Ids",
            ["Firestore:Pipeline:EnableAstLogging"] = "true",
            ["Firestore:Pipeline:EnableCaching"] = "true",
            ["Firestore:Pipeline:MaxRetries"] = "10",
            ["Firestore:Pipeline:RetryInitialDelayMs"] = "500"
        });

        // Act
        optionsBuilder.UseFirestore(configuration);

        // Assert
        var extension = optionsBuilder.Options.FindExtension<FirestoreOptionsExtension>();
        Assert.NotNull(extension);
        Assert.Equal(QueryLogLevel.Ids, extension.PipelineOptions.QueryLogLevel);
        Assert.True(extension.PipelineOptions.EnableAstLogging);
        Assert.True(extension.PipelineOptions.EnableCaching);
        Assert.Equal(10, extension.PipelineOptions.MaxRetries);
        Assert.Equal(TimeSpan.FromMilliseconds(500), extension.PipelineOptions.RetryInitialDelay);
    }

    [Fact]
    public void UseFirestore_WithConfiguration_ShouldHandleCaseInsensitiveQueryLogLevel()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Firestore:ProjectId"] = "test-project",
            ["Firestore:Pipeline:QueryLogLevel"] = "count"
        });

        // Act
        optionsBuilder.UseFirestore(configuration);

        // Assert
        var extension = optionsBuilder.Options.FindExtension<FirestoreOptionsExtension>();
        Assert.NotNull(extension);
        Assert.Equal(QueryLogLevel.Count, extension.PipelineOptions.QueryLogLevel);
    }

    [Fact]
    public void UseFirestore_WithConfiguration_ShouldIgnoreInvalidQueryLogLevel()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Firestore:ProjectId"] = "test-project",
            ["Firestore:Pipeline:QueryLogLevel"] = "InvalidLevel"
        });

        // Act
        optionsBuilder.UseFirestore(configuration);

        // Assert
        var extension = optionsBuilder.Options.FindExtension<FirestoreOptionsExtension>();
        Assert.NotNull(extension);
        Assert.Equal(QueryLogLevel.None, extension.PipelineOptions.QueryLogLevel);
    }

    [Fact]
    public void UseFirestore_WithConfiguration_ShouldInvokeOptionsAction()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Firestore:ProjectId"] = "test-project"
        });
        var actionInvoked = false;

        // Act
        optionsBuilder.UseFirestore(configuration, builder =>
        {
            actionInvoked = true;
            builder.UseDatabaseId("override-db");
        });

        // Assert
        Assert.True(actionInvoked);
        var extension = optionsBuilder.Options.FindExtension<FirestoreOptionsExtension>();
        Assert.NotNull(extension);
        Assert.Equal("override-db", extension.DatabaseId);
    }

    [Fact]
    public void UseFirestore_WithConfiguration_Generic_ShouldReturnTypedOptionsBuilder()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Firestore:ProjectId"] = "test-project"
        });

        // Act
        var result = optionsBuilder.UseFirestore(configuration);

        // Assert
        Assert.IsType<DbContextOptionsBuilder<TestDbContext>>(result);
    }

    [Fact]
    public void UseFirestore_WithConfiguration_ShouldUseDefaultsWhenPipelineSectionMissing()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Firestore:ProjectId"] = "test-project"
        });

        // Act
        optionsBuilder.UseFirestore(configuration);

        // Assert
        var extension = optionsBuilder.Options.FindExtension<FirestoreOptionsExtension>();
        Assert.NotNull(extension);
        Assert.Equal(QueryLogLevel.None, extension.PipelineOptions.QueryLogLevel);
        Assert.False(extension.PipelineOptions.EnableAstLogging);
        Assert.False(extension.PipelineOptions.EnableCaching); // Default is false
    }

    #endregion

    #region Helper Methods

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> settings)
    {
        var mockConfiguration = new Mock<IConfiguration>();
        var mockFirestoreSection = new Mock<IConfigurationSection>();
        var mockPipelineSection = new Mock<IConfigurationSection>();

        // Setup Firestore section values
        mockFirestoreSection.Setup(s => s["ProjectId"]).Returns(settings.GetValueOrDefault("Firestore:ProjectId"));
        mockFirestoreSection.Setup(s => s["EmulatorHost"]).Returns(settings.GetValueOrDefault("Firestore:EmulatorHost"));
        mockFirestoreSection.Setup(s => s["CredentialsPath"]).Returns(settings.GetValueOrDefault("Firestore:CredentialsPath"));
        mockFirestoreSection.Setup(s => s["DatabaseId"]).Returns(settings.GetValueOrDefault("Firestore:DatabaseId"));

        // Setup Pipeline section values
        mockPipelineSection.Setup(s => s["QueryLogLevel"]).Returns(settings.GetValueOrDefault("Firestore:Pipeline:QueryLogLevel"));
        mockPipelineSection.Setup(s => s["EnableAstLogging"]).Returns(settings.GetValueOrDefault("Firestore:Pipeline:EnableAstLogging"));
        mockPipelineSection.Setup(s => s["EnableCaching"]).Returns(settings.GetValueOrDefault("Firestore:Pipeline:EnableCaching"));
        mockPipelineSection.Setup(s => s["MaxRetries"]).Returns(settings.GetValueOrDefault("Firestore:Pipeline:MaxRetries"));
        mockPipelineSection.Setup(s => s["RetryInitialDelayMs"]).Returns(settings.GetValueOrDefault("Firestore:Pipeline:RetryInitialDelayMs"));

        // Pipeline section Exists() checks Value or GetChildren()
        // If any Pipeline keys exist, return children so Exists() returns true
        var hasPipelineKeys = settings.Keys.Any(k => k.StartsWith("Firestore:Pipeline:"));
        if (hasPipelineKeys)
        {
            mockPipelineSection.Setup(s => s.GetChildren())
                .Returns(new[] { new Mock<IConfigurationSection>().Object });
        }
        else
        {
            mockPipelineSection.Setup(s => s.GetChildren())
                .Returns(Array.Empty<IConfigurationSection>());
        }

        // Wire up sections
        mockFirestoreSection.Setup(s => s.GetSection("Pipeline")).Returns(mockPipelineSection.Object);
        mockConfiguration.Setup(c => c.GetSection("Firestore")).Returns(mockFirestoreSection.Object);

        return mockConfiguration.Object;
    }

    private static IServiceProvider CreateServiceProviderWithConfiguration(Dictionary<string, string?> settings)
    {
        var configuration = CreateConfiguration(settings);
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        return services.BuildServiceProvider();
    }

    #endregion

    private class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
    }
}
