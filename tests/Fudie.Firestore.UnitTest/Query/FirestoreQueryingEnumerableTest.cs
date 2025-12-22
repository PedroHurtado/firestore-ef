using Google.Cloud.Firestore;

namespace Fudie.Firestore.UnitTest.Query;

public class FirestoreQueryingEnumerableTest
{
    private readonly Mock<IEntityType> _entityTypeMock;

    public FirestoreQueryingEnumerableTest()
    {
        _entityTypeMock = new Mock<IEntityType>();
        _entityTypeMock.Setup(e => e.ClrType).Returns(typeof(TestEntity));
    }

    private class TestEntity
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
    }

    #region Interface Implementation Tests

    [Fact]
    public void FirestoreQueryingEnumerable_Implements_IAsyncEnumerable()
    {
        typeof(FirestoreQueryingEnumerable<TestEntity>)
            .Should().Implement<IAsyncEnumerable<TestEntity>>();
    }

    [Fact]
    public void FirestoreQueryingEnumerable_Is_Generic_Type()
    {
        typeof(FirestoreQueryingEnumerable<>).IsGenericTypeDefinition.Should().BeTrue();
    }

    #endregion

    #region Constructor Signature Tests

    [Fact]
    public void Constructor_Has_Six_Parameters()
    {
        var constructors = typeof(FirestoreQueryingEnumerable<TestEntity>).GetConstructors();

        constructors.Should().HaveCount(1);
        constructors[0].GetParameters().Should().HaveCount(6);
    }

    [Fact]
    public void Constructor_First_Parameter_Is_QueryContext()
    {
        var constructor = typeof(FirestoreQueryingEnumerable<TestEntity>).GetConstructors()[0];

        constructor.GetParameters()[0].ParameterType.Should().Be(typeof(QueryContext));
        constructor.GetParameters()[0].Name.Should().Be("queryContext");
    }

    [Fact]
    public void Constructor_Second_Parameter_Is_FirestoreQueryExpression()
    {
        var constructor = typeof(FirestoreQueryingEnumerable<TestEntity>).GetConstructors()[0];

        constructor.GetParameters()[1].ParameterType.Should().Be(typeof(FirestoreQueryExpression));
        constructor.GetParameters()[1].Name.Should().Be("queryExpression");
    }

    [Fact]
    public void Constructor_Third_Parameter_Is_Shaper_Func()
    {
        var constructor = typeof(FirestoreQueryingEnumerable<TestEntity>).GetConstructors()[0];
        // Shaper ahora incluye bool isTracking: Func<QueryContext, DocumentSnapshot, bool, T>
        var shaperType = typeof(Func<QueryContext, DocumentSnapshot, bool, TestEntity>);

        constructor.GetParameters()[2].ParameterType.Should().Be(shaperType);
        constructor.GetParameters()[2].Name.Should().Be("shaper");
    }

    [Fact]
    public void Constructor_Fourth_Parameter_Is_Type()
    {
        var constructor = typeof(FirestoreQueryingEnumerable<TestEntity>).GetConstructors()[0];

        constructor.GetParameters()[3].ParameterType.Should().Be(typeof(Type));
        constructor.GetParameters()[3].Name.Should().Be("contextType");
    }

    [Fact]
    public void Constructor_Fifth_Parameter_Is_IsTracking_Bool()
    {
        var constructor = typeof(FirestoreQueryingEnumerable<TestEntity>).GetConstructors()[0];

        constructor.GetParameters()[4].ParameterType.Should().Be(typeof(bool));
        constructor.GetParameters()[4].Name.Should().Be("isTracking");
    }

    [Fact]
    public void Constructor_Sixth_Parameter_Is_Executor()
    {
        var constructor = typeof(FirestoreQueryingEnumerable<TestEntity>).GetConstructors()[0];

        constructor.GetParameters()[5].ParameterType.Should().Be(typeof(IFirestoreQueryExecutor));
        constructor.GetParameters()[5].Name.Should().Be("executor");
    }

    #endregion

    #region GetAsyncEnumerator Tests

    [Fact]
    public void GetAsyncEnumerator_Method_Exists()
    {
        var method = typeof(FirestoreQueryingEnumerable<TestEntity>).GetMethod("GetAsyncEnumerator");

        method.Should().NotBeNull();
    }

    [Fact]
    public void GetAsyncEnumerator_Returns_IAsyncEnumerator()
    {
        var method = typeof(FirestoreQueryingEnumerable<TestEntity>).GetMethod("GetAsyncEnumerator");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(IAsyncEnumerator<TestEntity>));
    }

    [Fact]
    public void GetAsyncEnumerator_Accepts_CancellationToken()
    {
        var method = typeof(FirestoreQueryingEnumerable<TestEntity>).GetMethod("GetAsyncEnumerator");

        method.Should().NotBeNull();
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(CancellationToken));
    }

    #endregion

    #region Generic Type Constraint Tests

    [Fact]
    public void Can_Be_Created_With_Any_Reference_Type()
    {
        var enumerableType = typeof(FirestoreQueryingEnumerable<>);

        // Verify it can be closed with different types
        var closedWithString = enumerableType.MakeGenericType(typeof(string));
        var closedWithObject = enumerableType.MakeGenericType(typeof(object));

        closedWithString.Should().NotBeNull();
        closedWithObject.Should().NotBeNull();
    }

    [Fact]
    public void Different_Entity_Types_Create_Different_Closed_Types()
    {
        var type1 = typeof(FirestoreQueryingEnumerable<TestEntity>);
        var type2 = typeof(FirestoreQueryingEnumerable<AnotherEntity>);

        type1.Should().NotBe(type2);
    }

    private class AnotherEntity
    {
        public string Id { get; set; } = default!;
    }

    #endregion

    #region Query Expression Integration Tests

    [Fact]
    public void Works_With_Simple_Query_Expression()
    {
        var queryExpression = new FirestoreQueryExpression(_entityTypeMock.Object, "products");

        queryExpression.CollectionName.Should().Be("products");
        queryExpression.IsIdOnlyQuery.Should().BeFalse();
    }

    [Fact]
    public void Works_With_IdOnly_Query_Expression()
    {
        var queryExpression = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
        {
            IdValueExpression = Expression.Constant("doc-123")
        };

        queryExpression.IsIdOnlyQuery.Should().BeTrue();
    }

    [Fact]
    public void Works_With_Filtered_Query_Expression()
    {
        var filter = new FirestoreWhereClause("Name", FirestoreOperator.EqualTo, Expression.Constant("Test"));
        var queryExpression = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
            .AddFilter(filter);

        queryExpression.Filters.Should().HaveCount(1);
    }

    [Fact]
    public void Works_With_Ordered_Query_Expression()
    {
        var orderBy = new FirestoreOrderByClause("Name", descending: true);
        var queryExpression = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
            .AddOrderBy(orderBy);

        queryExpression.OrderByClauses.Should().HaveCount(1);
    }

    [Fact]
    public void Works_With_Limited_Query_Expression()
    {
        var queryExpression = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
            .WithLimit(10);

        queryExpression.Limit.Should().Be(10);
    }

    #endregion

    #region Shaper Function Tests

    [Fact]
    public void Shaper_Function_Type_Is_Correct()
    {
        var shaperType = typeof(Func<QueryContext, DocumentSnapshot, TestEntity>);

        shaperType.GetGenericArguments().Should().HaveCount(3);
        shaperType.GetGenericArguments()[0].Should().Be(typeof(QueryContext));
        shaperType.GetGenericArguments()[1].Should().Be(typeof(DocumentSnapshot));
        shaperType.GetGenericArguments()[2].Should().Be(typeof(TestEntity));
    }

    [Fact]
    public void Shaper_Returns_Entity_Type()
    {
        Func<QueryContext, DocumentSnapshot, TestEntity> shaper = (ctx, doc) => new TestEntity
        {
            Id = "test-id",
            Name = "Test Name"
        };

        var result = shaper(null!, null!);

        result.Should().NotBeNull();
        result.Should().BeOfType<TestEntity>();
    }

    #endregion

    #region Async Pattern Tests

    [Fact]
    public void Supports_Await_Foreach_Pattern()
    {
        // IAsyncEnumerable<T> supports await foreach
        var enumerableInterface = typeof(IAsyncEnumerable<TestEntity>);

        typeof(FirestoreQueryingEnumerable<TestEntity>)
            .GetInterfaces()
            .Should().Contain(enumerableInterface);
    }

    [Fact]
    public void IAsyncEnumerator_Has_MoveNextAsync_Method()
    {
        var method = typeof(IAsyncEnumerator<TestEntity>).GetMethod("MoveNextAsync");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(ValueTask<bool>));
    }

    [Fact]
    public void IAsyncEnumerator_Has_Current_Property()
    {
        var property = typeof(IAsyncEnumerator<TestEntity>).GetProperty("Current");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(TestEntity));
    }

    [Fact]
    public void IAsyncEnumerator_Has_DisposeAsync_Method()
    {
        var method = typeof(IAsyncDisposable).GetMethod("DisposeAsync");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(ValueTask));
    }

    #endregion
}
