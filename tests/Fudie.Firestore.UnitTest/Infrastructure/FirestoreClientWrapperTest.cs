using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Infrastructure.Internal;
using Google.Cloud.Firestore;
using Moq;

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

    #region Ciclo 7: Nuevos métodos para eliminar bypasses

    /// <summary>
    /// Ciclo 7: Verifica que IFirestoreClientWrapper tiene método ExecuteAggregateQueryAsync.
    /// Necesario para eliminar bypasses de agregaciones (Count, Sum, Average, Min, Max).
    /// </summary>
    [Fact]
    public void IFirestoreClientWrapper_ShouldHaveExecuteAggregateQueryAsyncMethod()
    {
        // Assert
        var method = typeof(IFirestoreClientWrapper).GetMethod("ExecuteAggregateQueryAsync");
        Assert.NotNull(method);
    }

    /// <summary>
    /// Ciclo 7: Verifica que IFirestoreClientWrapper tiene método GetSubCollectionAsync.
    /// Necesario para eliminar bypass en LoadSubCollectionAsync del Visitor.
    /// </summary>
    [Fact]
    public void IFirestoreClientWrapper_ShouldHaveGetSubCollectionAsyncMethod()
    {
        // Assert
        var method = typeof(IFirestoreClientWrapper).GetMethod("GetSubCollectionAsync");
        Assert.NotNull(method);
    }

    /// <summary>
    /// Ciclo 7: Verifica que IFirestoreClientWrapper tiene método GetDocumentByReferenceAsync.
    /// Necesario para eliminar bypasses en LoadReferenceAsync del Visitor.
    /// </summary>
    [Fact]
    public void IFirestoreClientWrapper_ShouldHaveGetDocumentByReferenceAsyncMethod()
    {
        // Assert
        var method = typeof(IFirestoreClientWrapper).GetMethod("GetDocumentByReferenceAsync");
        Assert.NotNull(method);
    }

    /// <summary>
    /// Ciclo 7: Verifica que FirestoreClientWrapper implementa el nuevo método ExecuteAggregateQueryAsync.
    /// </summary>
    [Fact]
    public void FirestoreClientWrapper_ShouldHaveExecuteAggregateQueryAsyncMethod()
    {
        // Assert
        var method = typeof(FirestoreClientWrapper).GetMethod("ExecuteAggregateQueryAsync");
        Assert.NotNull(method);
    }

    /// <summary>
    /// Ciclo 7: Verifica que FirestoreClientWrapper implementa el nuevo método GetSubCollectionAsync.
    /// </summary>
    [Fact]
    public void FirestoreClientWrapper_ShouldHaveGetSubCollectionAsyncMethod()
    {
        // Assert
        var method = typeof(FirestoreClientWrapper).GetMethod("GetSubCollectionAsync");
        Assert.NotNull(method);
    }

    /// <summary>
    /// Ciclo 7: Verifica que FirestoreClientWrapper implementa el nuevo método GetDocumentByReferenceAsync.
    /// </summary>
    [Fact]
    public void FirestoreClientWrapper_ShouldHaveGetDocumentByReferenceAsyncMethod()
    {
        // Assert
        var method = typeof(FirestoreClientWrapper).GetMethod("GetDocumentByReferenceAsync");
        Assert.NotNull(method);
    }

    /// <summary>
    /// Ciclo 7: Verifica que IFirestoreClientWrapper puede ser mockeado con ExecuteAggregateQueryAsync.
    /// Esto asegura que el método está correctamente definido para ser usado por otros componentes.
    /// </summary>
    [Fact]
    public void IFirestoreClientWrapper_ExecuteAggregateQueryAsync_CanBeMocked()
    {
        // Arrange
        var mockWrapper = new Mock<IFirestoreClientWrapper>();

        // Act - Configurar el mock (verifica que el método existe y tiene la firma correcta)
        mockWrapper
            .Setup(w => w.ExecuteAggregateQueryAsync(
                It.IsAny<AggregateQuery>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<AggregateQuerySnapshot>(null!));

        // Assert - El mock se configura sin errores
        Assert.NotNull(mockWrapper.Object);
    }

    /// <summary>
    /// Ciclo 7: Verifica que IFirestoreClientWrapper puede ser mockeado con GetSubCollectionAsync.
    /// </summary>
    [Fact]
    public void IFirestoreClientWrapper_GetSubCollectionAsync_CanBeMocked()
    {
        // Arrange
        var mockWrapper = new Mock<IFirestoreClientWrapper>();

        // Act - Configurar el mock
        mockWrapper
            .Setup(w => w.GetSubCollectionAsync(
                It.IsAny<DocumentReference>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<QuerySnapshot>(null!));

        // Assert
        Assert.NotNull(mockWrapper.Object);
    }

    /// <summary>
    /// Ciclo 7: Verifica que IFirestoreClientWrapper puede ser mockeado con GetDocumentByReferenceAsync.
    /// </summary>
    [Fact]
    public void IFirestoreClientWrapper_GetDocumentByReferenceAsync_CanBeMocked()
    {
        // Arrange
        var mockWrapper = new Mock<IFirestoreClientWrapper>();

        // Act - Configurar el mock
        mockWrapper
            .Setup(w => w.GetDocumentByReferenceAsync(
                It.IsAny<DocumentReference>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<DocumentSnapshot>(null!));

        // Assert
        Assert.NotNull(mockWrapper.Object);
    }

    /// <summary>
    /// Ciclo 7: Verifica que los nuevos métodos tienen los tipos de retorno correctos.
    /// </summary>
    [Fact]
    public void IFirestoreClientWrapper_NewMethods_ShouldHaveCorrectReturnTypes()
    {
        // Assert - ExecuteAggregateQueryAsync
        var aggregateMethod = typeof(IFirestoreClientWrapper).GetMethod("ExecuteAggregateQueryAsync");
        Assert.NotNull(aggregateMethod);
        Assert.Equal(typeof(Task<AggregateQuerySnapshot>), aggregateMethod.ReturnType);

        // Assert - GetSubCollectionAsync
        var subCollectionMethod = typeof(IFirestoreClientWrapper).GetMethod("GetSubCollectionAsync");
        Assert.NotNull(subCollectionMethod);
        Assert.Equal(typeof(Task<QuerySnapshot>), subCollectionMethod.ReturnType);

        // Assert - GetDocumentByReferenceAsync
        var docByRefMethod = typeof(IFirestoreClientWrapper).GetMethod("GetDocumentByReferenceAsync");
        Assert.NotNull(docByRefMethod);
        Assert.Equal(typeof(Task<DocumentSnapshot>), docByRefMethod.ReturnType);
    }

    /// <summary>
    /// Ciclo 7: Verifica que los nuevos métodos tienen los parámetros correctos.
    /// </summary>
    [Fact]
    public void IFirestoreClientWrapper_NewMethods_ShouldHaveCorrectParameters()
    {
        // ExecuteAggregateQueryAsync: (AggregateQuery, CancellationToken)
        var aggregateMethod = typeof(IFirestoreClientWrapper).GetMethod("ExecuteAggregateQueryAsync");
        Assert.NotNull(aggregateMethod);
        var aggregateParams = aggregateMethod.GetParameters();
        Assert.Equal(2, aggregateParams.Length);
        Assert.Equal(typeof(AggregateQuery), aggregateParams[0].ParameterType);
        Assert.Equal(typeof(CancellationToken), aggregateParams[1].ParameterType);

        // GetSubCollectionAsync: (DocumentReference, string, CancellationToken)
        var subCollectionMethod = typeof(IFirestoreClientWrapper).GetMethod("GetSubCollectionAsync");
        Assert.NotNull(subCollectionMethod);
        var subCollectionParams = subCollectionMethod.GetParameters();
        Assert.Equal(3, subCollectionParams.Length);
        Assert.Equal(typeof(DocumentReference), subCollectionParams[0].ParameterType);
        Assert.Equal(typeof(string), subCollectionParams[1].ParameterType);
        Assert.Equal(typeof(CancellationToken), subCollectionParams[2].ParameterType);

        // GetDocumentByReferenceAsync: (DocumentReference, CancellationToken)
        var docByRefMethod = typeof(IFirestoreClientWrapper).GetMethod("GetDocumentByReferenceAsync");
        Assert.NotNull(docByRefMethod);
        var docByRefParams = docByRefMethod.GetParameters();
        Assert.Equal(2, docByRefParams.Length);
        Assert.Equal(typeof(DocumentReference), docByRefParams[0].ParameterType);
        Assert.Equal(typeof(CancellationToken), docByRefParams[1].ParameterType);
    }

    #endregion
}
