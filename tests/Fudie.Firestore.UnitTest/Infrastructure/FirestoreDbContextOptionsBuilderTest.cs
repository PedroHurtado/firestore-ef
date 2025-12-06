using Firestore.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Fudie.Firestore.UnitTest.Infrastructure;

public class FirestoreDbContextOptionsBuilderTest
{
    [Fact]
    public void Constructor_ShouldThrow_WhenOptionsBuilderIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new FirestoreDbContextOptionsBuilder(null!));
    }

    [Fact]
    public void Constructor_ShouldCreateBuilder_WithValidOptionsBuilder()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();

        // Act
        var builder = new FirestoreDbContextOptionsBuilder(optionsBuilder);

        // Assert
        Assert.NotNull(builder);
    }

    [Fact]
    public void UseDatabaseId_ShouldReturnSameBuilder()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        optionsBuilder.UseFirestore("test-project");
        var builder = new FirestoreDbContextOptionsBuilder(optionsBuilder);

        // Act
        var result = builder.UseDatabaseId("my-database");

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void UseCredentials_ShouldReturnSameBuilder()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        optionsBuilder.UseFirestore("test-project");
        var builder = new FirestoreDbContextOptionsBuilder(optionsBuilder);

        // Act
        var result = builder.UseCredentials("/path/to/credentials.json");

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void MaxRetryAttempts_ShouldReturnSameBuilder()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        optionsBuilder.UseFirestore("test-project");
        var builder = new FirestoreDbContextOptionsBuilder(optionsBuilder);

        // Act
        var result = builder.MaxRetryAttempts(5);

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void MaxRetryAttempts_ShouldThrow_WhenNegative()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        optionsBuilder.UseFirestore("test-project");
        var builder = new FirestoreDbContextOptionsBuilder(optionsBuilder);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.MaxRetryAttempts(-1));
    }

    [Fact]
    public void MaxRetryAttempts_ShouldAcceptZero()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        optionsBuilder.UseFirestore("test-project");
        var builder = new FirestoreDbContextOptionsBuilder(optionsBuilder);

        // Act
        var result = builder.MaxRetryAttempts(0);

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void CommandTimeout_WithTimeSpan_ShouldReturnSameBuilder()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        optionsBuilder.UseFirestore("test-project");
        var builder = new FirestoreDbContextOptionsBuilder(optionsBuilder);

        // Act
        var result = builder.CommandTimeout(TimeSpan.FromSeconds(30));

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void CommandTimeout_WithSeconds_ShouldReturnSameBuilder()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        optionsBuilder.UseFirestore("test-project");
        var builder = new FirestoreDbContextOptionsBuilder(optionsBuilder);

        // Act
        var result = builder.CommandTimeout(30);

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void CommandTimeout_ShouldThrow_WhenZeroOrNegative()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        optionsBuilder.UseFirestore("test-project");
        var builder = new FirestoreDbContextOptionsBuilder(optionsBuilder);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.CommandTimeout(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.CommandTimeout(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void EnableDetailedLogging_ShouldReturnSameBuilder()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        optionsBuilder.UseFirestore("test-project");
        var builder = new FirestoreDbContextOptionsBuilder(optionsBuilder);

        // Act
        var result = builder.EnableDetailedLogging();

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void EnableDetailedLogging_WithFalse_ShouldReturnSameBuilder()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        optionsBuilder.UseFirestore("test-project");
        var builder = new FirestoreDbContextOptionsBuilder(optionsBuilder);

        // Act
        var result = builder.EnableDetailedLogging(false);

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void FluentConfiguration_ShouldSupportChaining()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        optionsBuilder.UseFirestore("test-project");
        var builder = new FirestoreDbContextOptionsBuilder(optionsBuilder);

        // Act
        var result = builder
            .UseDatabaseId("my-database")
            .UseCredentials("/path/to/creds.json")
            .MaxRetryAttempts(3)
            .CommandTimeout(60)
            .EnableDetailedLogging();

        // Assert
        Assert.Same(builder, result);
    }
}
