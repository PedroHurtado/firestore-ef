using Google.Cloud.Firestore;

namespace Fudie.Firestore.UnitTest.Query;

public class FirestoreProjectionQueryingEnumerableTest
{
    private readonly Mock<IEntityType> _entityTypeMock;

    public FirestoreProjectionQueryingEnumerableTest()
    {
        _entityTypeMock = new Mock<IEntityType>();
        _entityTypeMock.Setup(e => e.ClrType).Returns(typeof(TestEntity));
    }

    private class TestEntity
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
    }

    // DTO para proyecciones
    private class TestDto
    {
        public string Id { get; set; } = default!;
        public string DisplayName { get; set; } = default!;
    }

    #region Interface Implementation Tests

    [Fact]
    public void FirestoreProjectionQueryingEnumerable_Implements_IAsyncEnumerable()
    {
        typeof(FirestoreProjectionQueryingEnumerable<TestDto>)
            .Should().Implement<IAsyncEnumerable<TestDto>>();
    }

    [Fact]
    public void FirestoreProjectionQueryingEnumerable_Implements_IEnumerable()
    {
        typeof(FirestoreProjectionQueryingEnumerable<TestDto>)
            .Should().Implement<IEnumerable<TestDto>>();
    }

    [Fact]
    public void FirestoreProjectionQueryingEnumerable_Is_Generic_Type()
    {
        typeof(FirestoreProjectionQueryingEnumerable<>).IsGenericTypeDefinition.Should().BeTrue();
    }

    #endregion

    #region Constructor Signature Tests

    [Fact]
    public void Constructor_Has_Six_Parameters()
    {
        // El constructor tiene 6 parámetros:
        // QueryContext, FirestoreQueryExpression, Func<...> shaper, Type entityType, bool isTracking, IFirestoreQueryExecutor
        var constructors = typeof(FirestoreProjectionQueryingEnumerable<TestDto>).GetConstructors();

        constructors.Should().HaveCount(1);
        constructors[0].GetParameters().Should().HaveCount(6);
    }

    [Fact]
    public void Constructor_First_Parameter_Is_QueryContext()
    {
        var constructor = typeof(FirestoreProjectionQueryingEnumerable<TestDto>).GetConstructors()[0];

        constructor.GetParameters()[0].ParameterType.Should().Be(typeof(QueryContext));
        constructor.GetParameters()[0].Name.Should().Be("queryContext");
    }

    [Fact]
    public void Constructor_Second_Parameter_Is_FirestoreQueryExpression()
    {
        var constructor = typeof(FirestoreProjectionQueryingEnumerable<TestDto>).GetConstructors()[0];

        constructor.GetParameters()[1].ParameterType.Should().Be(typeof(FirestoreQueryExpression));
        constructor.GetParameters()[1].Name.Should().Be("queryExpression");
    }

    [Fact]
    public void Constructor_Third_Parameter_Is_Shaper_Func()
    {
        var constructor = typeof(FirestoreProjectionQueryingEnumerable<TestDto>).GetConstructors()[0];
        // Shaper: Func<QueryContext, DocumentSnapshot, bool, T>
        var shaperType = typeof(Func<QueryContext, DocumentSnapshot, bool, TestDto>);

        constructor.GetParameters()[2].ParameterType.Should().Be(shaperType);
        constructor.GetParameters()[2].Name.Should().Be("shaper");
    }

    [Fact]
    public void Constructor_Fourth_Parameter_Is_Type()
    {
        var constructor = typeof(FirestoreProjectionQueryingEnumerable<TestDto>).GetConstructors()[0];

        constructor.GetParameters()[3].ParameterType.Should().Be(typeof(Type));
        constructor.GetParameters()[3].Name.Should().Be("entityType");
    }

    [Fact]
    public void Constructor_Fifth_Parameter_Is_IsTracking_Bool()
    {
        var constructor = typeof(FirestoreProjectionQueryingEnumerable<TestDto>).GetConstructors()[0];

        constructor.GetParameters()[4].ParameterType.Should().Be(typeof(bool));
        constructor.GetParameters()[4].Name.Should().Be("isTracking");
    }

    [Fact]
    public void Constructor_Sixth_Parameter_Is_Executor()
    {
        var constructor = typeof(FirestoreProjectionQueryingEnumerable<TestDto>).GetConstructors()[0];

        constructor.GetParameters()[5].ParameterType.Should().Be(typeof(IFirestoreQueryExecutor));
        constructor.GetParameters()[5].Name.Should().Be("executor");
    }

    #endregion

    #region GetAsyncEnumerator Tests

    [Fact]
    public void GetAsyncEnumerator_Method_Exists()
    {
        var method = typeof(FirestoreProjectionQueryingEnumerable<TestDto>).GetMethod("GetAsyncEnumerator");

        method.Should().NotBeNull();
    }

    [Fact]
    public void GetAsyncEnumerator_Returns_IAsyncEnumerator()
    {
        var method = typeof(FirestoreProjectionQueryingEnumerable<TestDto>).GetMethod("GetAsyncEnumerator");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(IAsyncEnumerator<TestDto>));
    }

    [Fact]
    public void GetAsyncEnumerator_Accepts_CancellationToken()
    {
        var method = typeof(FirestoreProjectionQueryingEnumerable<TestDto>).GetMethod("GetAsyncEnumerator");

        method.Should().NotBeNull();
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(CancellationToken));
    }

    #endregion

    #region GetEnumerator Tests

    [Fact]
    public void GetEnumerator_Method_Exists()
    {
        var method = typeof(FirestoreProjectionQueryingEnumerable<TestDto>).GetMethod("GetEnumerator");

        method.Should().NotBeNull();
    }

    [Fact]
    public void GetEnumerator_Returns_IEnumerator()
    {
        var method = typeof(FirestoreProjectionQueryingEnumerable<TestDto>).GetMethod("GetEnumerator");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(IEnumerator<TestDto>));
    }

    #endregion

    #region Generic Type Tests

    [Fact]
    public void Can_Be_Created_With_Any_Type()
    {
        var enumerableType = typeof(FirestoreProjectionQueryingEnumerable<>);

        // Verify it can be closed with different types (no class constraint)
        var closedWithDto = enumerableType.MakeGenericType(typeof(TestDto));
        var closedWithString = enumerableType.MakeGenericType(typeof(string));
        var closedWithObject = enumerableType.MakeGenericType(typeof(object));

        closedWithDto.Should().NotBeNull();
        closedWithString.Should().NotBeNull();
        closedWithObject.Should().NotBeNull();
    }

    [Fact]
    public void Does_Not_Have_Class_Constraint()
    {
        // FirestoreProjectionQueryingEnumerable no tiene restricción where T : class
        // porque las proyecciones pueden ser tipos anónimos o DTOs
        var genericArgument = typeof(FirestoreProjectionQueryingEnumerable<>).GetGenericArguments()[0];
        var constraints = genericArgument.GetGenericParameterConstraints();

        constraints.Should().BeEmpty("No debe tener restricciones de tipo");
    }

    [Fact]
    public void Different_Projection_Types_Create_Different_Closed_Types()
    {
        var type1 = typeof(FirestoreProjectionQueryingEnumerable<TestDto>);
        var type2 = typeof(FirestoreProjectionQueryingEnumerable<AnotherDto>);

        type1.Should().NotBe(type2);
    }

    private class AnotherDto
    {
        public int Value { get; set; }
    }

    #endregion

    #region Shaper Function Tests

    [Fact]
    public void Shaper_Function_Type_Is_Correct()
    {
        // El shaper incluye bool isTracking: Func<QueryContext, DocumentSnapshot, bool, T>
        var shaperType = typeof(Func<QueryContext, DocumentSnapshot, bool, TestDto>);

        shaperType.GetGenericArguments().Should().HaveCount(4);
        shaperType.GetGenericArguments()[0].Should().Be(typeof(QueryContext));
        shaperType.GetGenericArguments()[1].Should().Be(typeof(DocumentSnapshot));
        shaperType.GetGenericArguments()[2].Should().Be(typeof(bool));
        shaperType.GetGenericArguments()[3].Should().Be(typeof(TestDto));
    }

    [Fact]
    public void Shaper_Returns_Projection_Type()
    {
        Func<QueryContext, DocumentSnapshot, bool, TestDto> shaper = (ctx, doc, tracking) => new TestDto
        {
            Id = "test-id",
            DisplayName = "Test Display Name"
        };

        var result = shaper(null!, null!, false);

        result.Should().NotBeNull();
        result.Should().BeOfType<TestDto>();
        result.DisplayName.Should().Be("Test Display Name");
    }

    #endregion

    #region Async Pattern Tests

    [Fact]
    public void Supports_Await_Foreach_Pattern()
    {
        var enumerableInterface = typeof(IAsyncEnumerable<TestDto>);

        typeof(FirestoreProjectionQueryingEnumerable<TestDto>)
            .GetInterfaces()
            .Should().Contain(enumerableInterface);
    }

    [Fact]
    public void IAsyncEnumerator_Has_MoveNextAsync_Method()
    {
        var method = typeof(IAsyncEnumerator<TestDto>).GetMethod("MoveNextAsync");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(ValueTask<bool>));
    }

    [Fact]
    public void IAsyncEnumerator_Has_Current_Property()
    {
        var property = typeof(IAsyncEnumerator<TestDto>).GetProperty("Current");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(TestDto));
    }

    #endregion

    #region Difference from FirestoreQueryingEnumerable Tests

    [Fact]
    public void FirestoreQueryingEnumerable_Has_Class_Constraint()
    {
        // FirestoreQueryingEnumerable SÍ tiene restricción where T : class
        var genericArgument = typeof(FirestoreQueryingEnumerable<>).GetGenericArguments()[0];
        var constraints = genericArgument.GetGenericParameterConstraints();

        // La restricción class se manifiesta como GenericParameterAttributes
        var attributes = genericArgument.GenericParameterAttributes;
        attributes.HasFlag(System.Reflection.GenericParameterAttributes.ReferenceTypeConstraint)
            .Should().BeTrue("FirestoreQueryingEnumerable debe tener where T : class");
    }

    [Fact]
    public void FirestoreProjectionQueryingEnumerable_Uses_Shaper()
    {
        // Projection enumerable usa shaper (3er parámetro)
        var projectionConstructor = typeof(FirestoreProjectionQueryingEnumerable<TestDto>).GetConstructors()[0];
        var shaperParam = projectionConstructor.GetParameters()[2];

        shaperParam.Name.Should().Be("shaper");
        shaperParam.ParameterType.Name.Should().StartWith("Func");
    }

    [Fact]
    public void FirestoreQueryingEnumerable_Uses_DbContext()
    {
        // Query enumerable usa DbContext (3er parámetro, no shaper)
        var queryConstructor = typeof(FirestoreQueryingEnumerable<TestEntity>).GetConstructors()[0];
        var dbContextParam = queryConstructor.GetParameters()[2];

        dbContextParam.Name.Should().Be("dbContext");
        dbContextParam.ParameterType.Should().Be(typeof(DbContext));
    }

    #endregion
}
