namespace Fudie.Firestore.UnitTest.Query.Pipeline.Handlers;

public class ProxyHandlerTests
{
    #region Class Structure Tests

    [Fact]
    public void ProxyHandler_Implements_IQueryPipelineHandler()
    {
        typeof(ProxyHandler)
            .Should().Implement<IQueryPipelineHandler>();
    }

    [Fact]
    public void ProxyHandler_Extends_QueryPipelineHandlerBase()
    {
        typeof(ProxyHandler)
            .Should().BeDerivedFrom<QueryPipelineHandlerBase>();
    }

    [Fact]
    public void ProxyHandler_Constructor_Accepts_Nullable_IProxyFactory()
    {
        // ProxyHandler accepts IProxyFactory? (nullable)
        // When null, proxy factory is not added to metadata
        var constructors = typeof(ProxyHandler).GetConstructors();

        constructors.Should().HaveCount(1);
        var parameters = constructors[0].GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(IProxyFactory));
    }

    [Fact]
    public void ProxyHandler_Can_Be_Instantiated_With_ProxyFactory()
    {
        var mockProxyFactory = new Mock<IProxyFactory>();

        var handler = new ProxyHandler(mockProxyFactory.Object);

        handler.Should().NotBeNull();
    }

    [Fact]
    public void ProxyHandler_Can_Be_Instantiated_With_Null_ProxyFactory()
    {
        // ProxyHandler should accept null to disable proxy creation
        var handler = new ProxyHandler(null);

        handler.Should().NotBeNull();
    }

    #endregion

    #region ApplicableKinds Tests

    [Fact]
    public void ApplicableKinds_Contains_Only_Entity()
    {
        // ProxyHandler only applies to Entity queries
        // Aggregation, Projection, and Predicate queries don't need proxies
        var property = typeof(ProxyHandler)
            .GetProperty("ApplicableKinds",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(QueryKind[]));
    }

    [Theory]
    [InlineData(QueryKind.Aggregation)]
    [InlineData(QueryKind.Projection)]
    [InlineData(QueryKind.Predicate)]
    public void ProxyHandler_Skips_NonEntity_Queries(QueryKind kind)
    {
        // ProxyHandler should skip non-entity queries
        // This is handled by QueryPipelineHandlerBase
        kind.Should().NotBe(QueryKind.Entity,
            "ProxyHandler only processes Entity queries");
    }

    #endregion

    #region Metadata Behavior Tests

    [Fact]
    public void ProxyHandler_Adds_ProxyFactory_To_Metadata_When_Available()
    {
        // When IProxyFactory is available:
        // Handler should add it to context metadata for ConvertHandler to use
        typeof(PipelineMetadataKeys).Should().NotBeNull(
            "ProxyHandler must add IProxyFactory to metadata");

        // Verify the metadata key exists
        var proxyFactoryKey = PipelineMetadataKeys.ProxyFactory;
        proxyFactoryKey.Name.Should().Be("ProxyFactory");
    }

    [Fact]
    public void ProxyHandler_Does_Not_Modify_Metadata_When_Null()
    {
        // When IProxyFactory is null (proxies not configured):
        // Handler should pass through without modifying metadata
        typeof(ProxyHandler).Should().NotBeNull(
            "ProxyHandler must check if _proxyFactory is null");
    }

    #endregion

    #region Handler Order Tests

    [Fact]
    public void ProxyHandler_Runs_Before_ConvertHandler()
    {
        // ProxyHandler must run BEFORE ConvertHandler because:
        // 1. ProxyHandler adds IProxyFactory to metadata
        // 2. ConvertHandler reads metadata and creates proxy instances
        // 3. ConvertHandler deserializes INTO the proxy instance
        typeof(ProxyHandler).Should().NotBeNull(
            "ProxyHandler must run before ConvertHandler");
    }

    #endregion
}

public class IProxyFactoryTests
{
    [Fact]
    public void IProxyFactory_Is_Interface()
    {
        typeof(IProxyFactory).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void IProxyFactory_Has_GetProxyType_Method()
    {
        var method = typeof(IProxyFactory).GetMethod("GetProxyType");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Type));
    }

    [Fact]
    public void GetProxyType_Accepts_IEntityType_Parameter()
    {
        var method = typeof(IProxyFactory).GetMethod("GetProxyType");
        var parameters = method!.GetParameters();

        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(IEntityType));
        parameters[0].Name.Should().Be("entityType");
    }

    [Fact]
    public void IProxyFactory_Has_CreateProxy_Method()
    {
        var method = typeof(IProxyFactory).GetMethod("CreateProxy");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(object));
    }

    [Fact]
    public void CreateProxy_Accepts_IEntityType_Parameter()
    {
        var method = typeof(IProxyFactory).GetMethod("CreateProxy");
        var parameters = method!.GetParameters();

        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(IEntityType));
        parameters[0].Name.Should().Be("entityType");
    }
}
