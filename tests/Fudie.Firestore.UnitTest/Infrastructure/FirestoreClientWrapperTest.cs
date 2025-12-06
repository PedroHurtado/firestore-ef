using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Infrastructure.Internal;
using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Fudie.Firestore.UnitTest.Infrastructure;

public class FirestoreClientWrapperTest
{
    [Fact]
    public void FirestoreClientWrapper_ShouldImplementIFirestoreClientWrapper()
    {
        // Assert
        Assert.True(typeof(IFirestoreClientWrapper).IsAssignableFrom(typeof(FirestoreClientWrapper)));
    }

    [Fact]
    public void FirestoreClientWrapper_ShouldImplementIDisposable()
    {
        // Assert
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(FirestoreClientWrapper)));
    }

    [Fact]
    public void FirestoreClientWrapper_ShouldHaveDatabaseProperty()
    {
        // Assert
        var property = typeof(FirestoreClientWrapper).GetProperty("Database");
        Assert.NotNull(property);
        Assert.Equal(typeof(FirestoreDb), property.PropertyType);
    }

    [Fact]
    public void FirestoreClientWrapper_ShouldHaveGetDocumentAsyncMethod()
    {
        // Assert
        var method = typeof(FirestoreClientWrapper).GetMethod("GetDocumentAsync");
        Assert.NotNull(method);
    }

    [Fact]
    public void FirestoreClientWrapper_ShouldHaveDocumentExistsAsyncMethod()
    {
        // Assert
        var method = typeof(FirestoreClientWrapper).GetMethod("DocumentExistsAsync");
        Assert.NotNull(method);
    }

    [Fact]
    public void FirestoreClientWrapper_ShouldHaveGetCollectionAsyncMethod()
    {
        // Assert
        var method = typeof(FirestoreClientWrapper).GetMethod("GetCollectionAsync");
        Assert.NotNull(method);
    }

    [Fact]
    public void FirestoreClientWrapper_ShouldHaveSetDocumentAsyncMethod()
    {
        // Assert
        var method = typeof(FirestoreClientWrapper).GetMethod("SetDocumentAsync");
        Assert.NotNull(method);
    }

    [Fact]
    public void FirestoreClientWrapper_ShouldHaveUpdateDocumentAsyncMethod()
    {
        // Assert
        var method = typeof(FirestoreClientWrapper).GetMethod("UpdateDocumentAsync");
        Assert.NotNull(method);
    }

    [Fact]
    public void FirestoreClientWrapper_ShouldHaveDeleteDocumentAsyncMethod()
    {
        // Assert
        var method = typeof(FirestoreClientWrapper).GetMethod("DeleteDocumentAsync");
        Assert.NotNull(method);
    }

    [Fact]
    public void FirestoreClientWrapper_ShouldHaveExecuteQueryAsyncMethod()
    {
        // Assert
        var method = typeof(FirestoreClientWrapper).GetMethod("ExecuteQueryAsync");
        Assert.NotNull(method);
    }

    [Fact]
    public void FirestoreClientWrapper_ShouldHaveRunTransactionAsyncMethod()
    {
        // Assert
        var method = typeof(FirestoreClientWrapper).GetMethod("RunTransactionAsync");
        Assert.NotNull(method);
    }

    [Fact]
    public void FirestoreClientWrapper_ShouldHaveCreateBatchMethod()
    {
        // Assert
        var method = typeof(FirestoreClientWrapper).GetMethod("CreateBatch");
        Assert.NotNull(method);
        Assert.Equal(typeof(WriteBatch), method.ReturnType);
    }

    [Fact]
    public void FirestoreClientWrapper_ShouldHaveGetCollectionMethod()
    {
        // Assert
        var method = typeof(FirestoreClientWrapper).GetMethod("GetCollection");
        Assert.NotNull(method);
        Assert.Equal(typeof(CollectionReference), method.ReturnType);
    }

    [Fact]
    public void FirestoreClientWrapper_ShouldHaveGetDocumentMethod()
    {
        // Assert
        var method = typeof(FirestoreClientWrapper).GetMethod("GetDocument");
        Assert.NotNull(method);
        Assert.Equal(typeof(DocumentReference), method.ReturnType);
    }

    [Fact]
    public void FirestoreClientWrapper_Constructor_ShouldRequireDbContextOptions()
    {
        // Assert
        var constructors = typeof(FirestoreClientWrapper).GetConstructors();
        Assert.Single(constructors);

        var parameters = constructors[0].GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(IDbContextOptions), parameters[0].ParameterType);
    }

    [Fact]
    public void IFirestoreClientWrapper_ShouldDefineAllRequiredMethods()
    {
        // Assert
        var interfaceType = typeof(IFirestoreClientWrapper);

        Assert.NotNull(interfaceType.GetProperty("Database"));
        Assert.NotNull(interfaceType.GetMethod("GetDocumentAsync"));
        Assert.NotNull(interfaceType.GetMethod("DocumentExistsAsync"));
        Assert.NotNull(interfaceType.GetMethod("GetCollectionAsync"));
        Assert.NotNull(interfaceType.GetMethod("SetDocumentAsync"));
        Assert.NotNull(interfaceType.GetMethod("UpdateDocumentAsync"));
        Assert.NotNull(interfaceType.GetMethod("DeleteDocumentAsync"));
        Assert.NotNull(interfaceType.GetMethod("ExecuteQueryAsync"));
        Assert.NotNull(interfaceType.GetMethod("RunTransactionAsync"));
        Assert.NotNull(interfaceType.GetMethod("CreateBatch"));
        Assert.NotNull(interfaceType.GetMethod("GetCollection"));
        Assert.NotNull(interfaceType.GetMethod("GetDocument"));
    }
}
