using Firestore.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;

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

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            optionsBuilder.UseFirestore(null!));
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
                .MaxRetryAttempts(5)
                .CommandTimeout(30);
        });

        // Assert
        var extension = optionsBuilder.Options.FindExtension<FirestoreOptionsExtension>();
        Assert.NotNull(extension);
        Assert.Equal("test-project", extension.ProjectId);
        Assert.Equal("my-database", extension.DatabaseId);
    }

    private class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
    }
}
