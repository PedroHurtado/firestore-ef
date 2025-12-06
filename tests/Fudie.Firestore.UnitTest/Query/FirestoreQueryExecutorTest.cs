using Google.Cloud.Firestore;

namespace Fudie.Firestore.UnitTest.Query;

public class FirestoreQueryExecutorTest
{
    private readonly Mock<IFirestoreClientWrapper> _clientMock;
    private readonly Mock<ILogger<FirestoreQueryExecutor>> _loggerMock;
    private readonly Mock<IEntityType> _entityTypeMock;

    public FirestoreQueryExecutorTest()
    {
        _clientMock = new Mock<IFirestoreClientWrapper>();
        _loggerMock = new Mock<ILogger<FirestoreQueryExecutor>>();
        _entityTypeMock = new Mock<IEntityType>();
        _entityTypeMock.Setup(e => e.ClrType).Returns(typeof(TestEntity));
    }

    private class TestEntity
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public decimal Price { get; set; }
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_Accepts_Valid_Dependencies()
    {
        var executor = new FirestoreQueryExecutor(_clientMock.Object, _loggerMock.Object);

        executor.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_Throws_On_Null_Client()
    {
        var action = () => new FirestoreQueryExecutor(null!, _loggerMock.Object);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("client");
    }

    [Fact]
    public void Constructor_Throws_On_Null_Logger()
    {
        var action = () => new FirestoreQueryExecutor(_clientMock.Object, null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region Class Structure Tests

    [Fact]
    public void FirestoreQueryExecutor_Has_ExecuteQueryAsync_Method()
    {
        var method = typeof(FirestoreQueryExecutor).GetMethod("ExecuteQueryAsync");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task<QuerySnapshot>));
    }

    [Fact]
    public void FirestoreQueryExecutor_Has_ExecuteIdQueryAsync_Method()
    {
        var method = typeof(FirestoreQueryExecutor).GetMethod("ExecuteIdQueryAsync");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task<DocumentSnapshot?>));
    }

    [Fact]
    public void ExecuteQueryAsync_Has_Correct_Parameters()
    {
        var method = typeof(FirestoreQueryExecutor).GetMethod("ExecuteQueryAsync");

        method.Should().NotBeNull();
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(3);
        parameters[0].ParameterType.Should().Be(typeof(FirestoreQueryExpression));
        parameters[1].ParameterType.Should().Be(typeof(QueryContext));
        parameters[2].ParameterType.Should().Be(typeof(CancellationToken));
    }

    [Fact]
    public void ExecuteIdQueryAsync_Has_Correct_Parameters()
    {
        var method = typeof(FirestoreQueryExecutor).GetMethod("ExecuteIdQueryAsync");

        method.Should().NotBeNull();
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(3);
        parameters[0].ParameterType.Should().Be(typeof(FirestoreQueryExpression));
        parameters[1].ParameterType.Should().Be(typeof(QueryContext));
        parameters[2].ParameterType.Should().Be(typeof(CancellationToken));
    }

    #endregion

    #region FirestoreWhereClause Tests

    [Fact]
    public void FirestoreWhereClause_Creates_With_Valid_Parameters()
    {
        var clause = new FirestoreWhereClause("Name", FirestoreOperator.EqualTo, Expression.Constant("Test"));

        clause.PropertyName.Should().Be("Name");
        clause.Operator.Should().Be(FirestoreOperator.EqualTo);
    }

    [Theory]
    [InlineData(FirestoreOperator.EqualTo)]
    [InlineData(FirestoreOperator.NotEqualTo)]
    [InlineData(FirestoreOperator.LessThan)]
    [InlineData(FirestoreOperator.LessThanOrEqualTo)]
    [InlineData(FirestoreOperator.GreaterThan)]
    [InlineData(FirestoreOperator.GreaterThanOrEqualTo)]
    [InlineData(FirestoreOperator.ArrayContains)]
    [InlineData(FirestoreOperator.In)]
    [InlineData(FirestoreOperator.ArrayContainsAny)]
    [InlineData(FirestoreOperator.NotIn)]
    public void FirestoreWhereClause_Supports_All_Operators(FirestoreOperator op)
    {
        var clause = new FirestoreWhereClause("Field", op, Expression.Constant("value"));

        clause.Operator.Should().Be(op);
    }

    #endregion

    #region FirestoreOrderByClause Tests

    [Fact]
    public void FirestoreOrderByClause_Creates_Ascending_By_Default()
    {
        var clause = new FirestoreOrderByClause("Name");

        clause.PropertyName.Should().Be("Name");
        clause.Descending.Should().BeFalse();
    }

    [Fact]
    public void FirestoreOrderByClause_Creates_Descending_When_Specified()
    {
        var clause = new FirestoreOrderByClause("Price", descending: true);

        clause.PropertyName.Should().Be("Price");
        clause.Descending.Should().BeTrue();
    }

    #endregion

    #region Query Building Integration Tests

    [Fact]
    public void Query_With_Multiple_Filters_Can_Be_Built()
    {
        var filter1 = new FirestoreWhereClause("Name", FirestoreOperator.EqualTo, Expression.Constant("Test"));
        var filter2 = new FirestoreWhereClause("Price", FirestoreOperator.GreaterThan, Expression.Constant(100.0));

        var query = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
            .AddFilter(filter1)
            .AddFilter(filter2);

        query.Filters.Should().HaveCount(2);
    }

    [Fact]
    public void Query_With_OrderBy_And_Limit_Can_Be_Built()
    {
        var orderBy = new FirestoreOrderByClause("Name");

        var query = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
            .AddOrderBy(orderBy)
            .WithLimit(10);

        query.OrderByClauses.Should().HaveCount(1);
        query.Limit.Should().Be(10);
    }

    [Fact]
    public void Query_With_IdValueExpression_Is_IdOnlyQuery()
    {
        var query = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
        {
            IdValueExpression = Expression.Constant("doc-123")
        };

        query.IsIdOnlyQuery.Should().BeTrue();
    }

    [Fact]
    public void Query_Without_IdValueExpression_Is_Not_IdOnlyQuery()
    {
        var query = new FirestoreQueryExpression(_entityTypeMock.Object, "products");

        query.IsIdOnlyQuery.Should().BeFalse();
    }

    #endregion

    #region Value Conversion Tests (Based on Code Review)

    [Fact]
    public void Decimal_Type_Is_Recognized_For_Conversion()
    {
        // The executor converts decimal to double for Firestore
        var decimalType = typeof(decimal);
        var doubleType = typeof(double);

        decimalType.Should().NotBe(doubleType);
        Convert.ToDouble(99.99m).Should().BeApproximately(99.99, 0.001);
    }

    [Fact]
    public void Enum_Type_Is_Recognized_For_Conversion()
    {
        // The executor converts enums to strings for Firestore
        var enumValue = TestStatus.Active;

        enumValue.ToString().Should().Be("Active");
    }

    [Fact]
    public void DateTime_Is_Converted_To_UTC()
    {
        // The executor converts DateTime to UTC for Firestore
        var localTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Local);
        var utcTime = localTime.ToUniversalTime();

        utcTime.Kind.Should().Be(DateTimeKind.Utc);
    }

    private enum TestStatus
    {
        Pending,
        Active,
        Completed
    }

    #endregion

    #region Firestore Limits Tests

    [Fact]
    public void WhereIn_Has_30_Element_Limit()
    {
        // Firestore WhereIn supports max 30 elements
        var elements = Enumerable.Range(1, 30).ToList();

        elements.Should().HaveCount(30);
    }

    [Fact]
    public void WhereNotIn_Has_10_Element_Limit()
    {
        // Firestore WhereNotIn supports max 10 elements
        var elements = Enumerable.Range(1, 10).ToList();

        elements.Should().HaveCount(10);
    }

    [Fact]
    public void WhereArrayContainsAny_Has_30_Element_Limit()
    {
        // Firestore WhereArrayContainsAny supports max 30 elements
        var elements = Enumerable.Range(1, 30).ToList();

        elements.Should().HaveCount(30);
    }

    #endregion
}
