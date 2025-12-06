using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage;

namespace Fudie.Firestore.UnitTest.Storage;

public class FirestoreTransactionTest
{
    [Fact]
    public void FirestoreTransaction_ShouldImplementIDbContextTransaction()
    {
        // Assert - verify that FirestoreTransaction implements IDbContextTransaction
        Assert.True(typeof(IDbContextTransaction).IsAssignableFrom(typeof(FirestoreTransaction)));
    }

    [Fact]
    public void FirestoreTransaction_ShouldHaveTransactionIdProperty()
    {
        // Assert - verify that FirestoreTransaction has TransactionId property
        var property = typeof(FirestoreTransaction).GetProperty("TransactionId");
        Assert.NotNull(property);
        Assert.Equal(typeof(Guid), property.PropertyType);
    }

    [Fact]
    public void FirestoreTransaction_ShouldHaveNativeBatchProperty()
    {
        // Assert - verify that FirestoreTransaction has NativeBatch property
        var property = typeof(FirestoreTransaction).GetProperty("NativeBatch");
        Assert.NotNull(property);
    }

    [Fact]
    public void FirestoreTransaction_ShouldHaveCommitMethod()
    {
        // Assert - verify that FirestoreTransaction has Commit method
        var method = typeof(FirestoreTransaction).GetMethod("Commit");
        Assert.NotNull(method);
    }

    [Fact]
    public void FirestoreTransaction_ShouldHaveCommitAsyncMethod()
    {
        // Assert - verify that FirestoreTransaction has CommitAsync method
        var method = typeof(FirestoreTransaction).GetMethod("CommitAsync");
        Assert.NotNull(method);
    }

    [Fact]
    public void FirestoreTransaction_ShouldHaveRollbackMethod()
    {
        // Assert - verify that FirestoreTransaction has Rollback method
        var method = typeof(FirestoreTransaction).GetMethod("Rollback");
        Assert.NotNull(method);
    }

    [Fact]
    public void FirestoreTransaction_ShouldHaveRollbackAsyncMethod()
    {
        // Assert - verify that FirestoreTransaction has RollbackAsync method
        var method = typeof(FirestoreTransaction).GetMethod("RollbackAsync");
        Assert.NotNull(method);
    }

    [Fact]
    public void FirestoreTransaction_ShouldImplementIDisposable()
    {
        // Assert - verify that FirestoreTransaction implements IDisposable
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(FirestoreTransaction)));
    }

    [Fact]
    public void FirestoreTransaction_ShouldImplementIAsyncDisposable()
    {
        // Assert - verify that FirestoreTransaction implements IAsyncDisposable
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(typeof(FirestoreTransaction)));
    }

    [Fact]
    public void FirestoreTransaction_Constructor_ShouldRequireTransactionManager()
    {
        // Assert - verify constructor parameters
        var constructors = typeof(FirestoreTransaction).GetConstructors();
        Assert.Single(constructors);

        var parameters = constructors[0].GetParameters();
        Assert.Equal(3, parameters.Length);
        Assert.Equal(typeof(FirestoreTransactionManager), parameters[0].ParameterType);
    }

    [Fact]
    public void FirestoreTransaction_Constructor_ShouldRequireClientWrapper()
    {
        // Assert - verify constructor parameters
        var constructors = typeof(FirestoreTransaction).GetConstructors();
        var parameters = constructors[0].GetParameters();

        Assert.Equal(typeof(IFirestoreClientWrapper), parameters[2].ParameterType);
    }
}
