using Fudie.Firestore.EntityFrameworkCore.Infrastructure;
using Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;
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

    #region Pipeline Configuration Tests

    [Fact]
    public void QueryLogLevel_ShouldReturnSameBuilder()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        optionsBuilder.UseFirestore("test-project");
        var builder = new FirestoreDbContextOptionsBuilder(optionsBuilder);

        // Act
        var result = builder.QueryLogLevel(QueryLogLevel.Full);

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void QueryLogLevel_ShouldUpdateExtension()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        optionsBuilder.UseFirestore("test-project");
        var builder = new FirestoreDbContextOptionsBuilder(optionsBuilder);

        // Act
        builder.QueryLogLevel(QueryLogLevel.Ids);

        // Assert
        var extension = optionsBuilder.Options.FindExtension<FirestoreOptionsExtension>();
        Assert.NotNull(extension);
        Assert.Equal(QueryLogLevel.Ids, extension.PipelineOptions.QueryLogLevel);
    }

    [Fact]
    public void EnableAstLogging_ShouldReturnSameBuilder()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        optionsBuilder.UseFirestore("test-project");
        var builder = new FirestoreDbContextOptionsBuilder(optionsBuilder);

        // Act
        var result = builder.EnableAstLogging();

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void EnableAstLogging_ShouldUpdateExtension()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        optionsBuilder.UseFirestore("test-project");
        var builder = new FirestoreDbContextOptionsBuilder(optionsBuilder);

        // Act
        builder.EnableAstLogging(true);

        // Assert
        var extension = optionsBuilder.Options.FindExtension<FirestoreOptionsExtension>();
        Assert.NotNull(extension);
        Assert.True(extension.PipelineOptions.EnableAstLogging);
    }

    [Fact]
    public void EnableCaching_ShouldReturnSameBuilder()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        optionsBuilder.UseFirestore("test-project");
        var builder = new FirestoreDbContextOptionsBuilder(optionsBuilder);

        // Act
        var result = builder.EnableCaching();

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void PipelineMaxRetries_ShouldReturnSameBuilder()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        optionsBuilder.UseFirestore("test-project");
        var builder = new FirestoreDbContextOptionsBuilder(optionsBuilder);

        // Act
        var result = builder.PipelineMaxRetries(5);

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void PipelineMaxRetries_ShouldThrow_WhenNegative()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        optionsBuilder.UseFirestore("test-project");
        var builder = new FirestoreDbContextOptionsBuilder(optionsBuilder);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.PipelineMaxRetries(-1));
    }

    [Fact]
    public void PipelineRetryInitialDelay_ShouldReturnSameBuilder()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        optionsBuilder.UseFirestore("test-project");
        var builder = new FirestoreDbContextOptionsBuilder(optionsBuilder);

        // Act
        var result = builder.PipelineRetryInitialDelay(TimeSpan.FromMilliseconds(200));

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void PipelineRetryInitialDelay_ShouldThrow_WhenNegative()
    {
        // Arrange
        var optionsBuilder = new DbContextOptionsBuilder();
        optionsBuilder.UseFirestore("test-project");
        var builder = new FirestoreDbContextOptionsBuilder(optionsBuilder);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.PipelineRetryInitialDelay(TimeSpan.FromMilliseconds(-1)));
    }

    #endregion

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
            .QueryLogLevel(QueryLogLevel.Count)
            .PipelineMaxRetries(3)
            .EnableDetailedLogging();

        // Assert
        Assert.Same(builder, result);
    }
}
