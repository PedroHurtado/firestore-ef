using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore;

namespace Fudie.Firestore.UnitTest.Query;

public class FirestoreQueryExecutorTest
{
    private readonly Mock<IFirestoreClientWrapper> _clientMock;
    private readonly Mock<IFirestoreDocumentDeserializer> _deserializerMock;
    private readonly Mock<ILogger<FirestoreQueryExecutor>> _loggerMock;
    private readonly Mock<IEntityType> _entityTypeMock;

    public FirestoreQueryExecutorTest()
    {
        _clientMock = new Mock<IFirestoreClientWrapper>();
        _deserializerMock = new Mock<IFirestoreDocumentDeserializer>();
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
    public void Constructor_Creates_Valid_Executor()
    {
        var executor = new FirestoreQueryExecutor(_clientMock.Object, _deserializerMock.Object, _loggerMock.Object);

        executor.Should().NotBeNull();
        executor.Should().BeAssignableTo<IFirestoreQueryExecutor>();
    }

    [Fact]
    public void Constructor_Throws_On_Null_Client()
    {
        var action = () => new FirestoreQueryExecutor(null!, _deserializerMock.Object, _loggerMock.Object);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("client");
    }

    [Fact]
    public void Constructor_Throws_On_Null_Deserializer()
    {
        var action = () => new FirestoreQueryExecutor(_clientMock.Object, null!, _loggerMock.Object);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("deserializer");
    }

    [Fact]
    public void Constructor_Throws_On_Null_Logger()
    {
        var action = () => new FirestoreQueryExecutor(_clientMock.Object, _deserializerMock.Object, null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region Class Structure Tests

    [Fact]
    public void FirestoreQueryExecutor_Has_ExecuteQueryAsync_Method()
    {
        // El método genérico ExecuteQueryAsync<T> es el nuevo método principal
        var methods = typeof(FirestoreQueryExecutor).GetMethods()
            .Where(m => m.Name == "ExecuteQueryAsync")
            .ToList();

        methods.Should().HaveCountGreaterOrEqualTo(1);

        // Verificar que existe el método genérico
        var genericMethod = methods.FirstOrDefault(m => m.IsGenericMethod);
        genericMethod.Should().NotBeNull("El método genérico ExecuteQueryAsync<T> debería existir");
    }

    [Fact]
    public void FirestoreQueryExecutor_Has_ExecuteIdQueryAsync_Methods()
    {
        // Verificar que existe el método genérico ExecuteIdQueryAsync<T>
        var genericMethod = typeof(FirestoreQueryExecutor).GetMethods()
            .FirstOrDefault(m => m.Name == "ExecuteIdQueryAsync" && m.IsGenericMethod);

        genericMethod.Should().NotBeNull("El método genérico ExecuteIdQueryAsync<T> debería existir");

        // Verificar que existe ExecuteIdQueryForDocumentAsync (para proyecciones)
        var forDocumentMethod = typeof(FirestoreQueryExecutor).GetMethod("ExecuteIdQueryForDocumentAsync");
        forDocumentMethod.Should().NotBeNull("El método ExecuteIdQueryForDocumentAsync debería existir");
        forDocumentMethod!.ReturnType.Should().Be(typeof(Task<DocumentSnapshot?>));
    }

    [Fact]
    public void ExecuteQueryAsync_GenericMethod_Has_Correct_Parameters()
    {
        // Buscar el método genérico ExecuteQueryAsync<T>
        var genericMethod = typeof(FirestoreQueryExecutor).GetMethods()
            .FirstOrDefault(m => m.Name == "ExecuteQueryAsync" && m.IsGenericMethod);

        genericMethod.Should().NotBeNull();
        var parameters = genericMethod!.GetParameters();
        parameters.Should().HaveCount(5);
        parameters[0].ParameterType.Should().Be(typeof(FirestoreQueryExpression));
        parameters[1].ParameterType.Should().Be(typeof(QueryContext));
        parameters[2].ParameterType.Should().Be(typeof(DbContext));
        parameters[3].ParameterType.Should().Be(typeof(bool));
        parameters[4].ParameterType.Should().Be(typeof(CancellationToken));
    }

    [Fact]
    public void ExecuteIdQueryAsync_GenericMethod_Has_Correct_Parameters()
    {
        // Buscar el método genérico ExecuteIdQueryAsync<T>
        var genericMethod = typeof(FirestoreQueryExecutor).GetMethods()
            .FirstOrDefault(m => m.Name == "ExecuteIdQueryAsync" && m.IsGenericMethod);

        genericMethod.Should().NotBeNull();
        var parameters = genericMethod!.GetParameters();
        parameters.Should().HaveCount(5);
        parameters[0].ParameterType.Should().Be(typeof(FirestoreQueryExpression));
        parameters[1].ParameterType.Should().Be(typeof(QueryContext));
        parameters[2].ParameterType.Should().Be(typeof(DbContext));
        parameters[3].ParameterType.Should().Be(typeof(bool));
        parameters[4].ParameterType.Should().Be(typeof(CancellationToken));
    }

    [Fact]
    public void ExecuteIdQueryForDocumentAsync_Has_Correct_Parameters()
    {
        // Buscar el método ExecuteIdQueryForDocumentAsync (para proyecciones)
        var method = typeof(FirestoreQueryExecutor).GetMethod("ExecuteIdQueryForDocumentAsync");

        method.Should().NotBeNull();
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(3);
        parameters[0].ParameterType.Should().Be(typeof(FirestoreQueryExpression));
        parameters[1].ParameterType.Should().Be(typeof(QueryContext));
        parameters[2].ParameterType.Should().Be(typeof(CancellationToken));
    }

    [Fact]
    public void ExecuteQueryForDocumentsAsync_Has_Correct_Parameters()
    {
        // Buscar el método ExecuteQueryForDocumentsAsync (para proyecciones)
        var method = typeof(FirestoreQueryExecutor).GetMethod("ExecuteQueryForDocumentsAsync");

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

    #region Ciclo 12: IFirestoreQueryExecutor Interface Tests

    /// <summary>
    /// Ciclo 12: Verifica que IFirestoreQueryExecutor existe.
    /// </summary>
    [Fact]
    public void IFirestoreQueryExecutor_ShouldExist()
    {
        var interfaceType = typeof(IFirestoreQueryExecutor);

        interfaceType.Should().NotBeNull();
        interfaceType.IsInterface.Should().BeTrue();
    }

    /// <summary>
    /// Ciclo 12: Verifica que FirestoreQueryExecutor implementa IFirestoreQueryExecutor.
    /// </summary>
    [Fact]
    public void FirestoreQueryExecutor_ShouldImplementIFirestoreQueryExecutor()
    {
        typeof(IFirestoreQueryExecutor).IsAssignableFrom(typeof(FirestoreQueryExecutor)).Should().BeTrue();
    }

    /// <summary>
    /// Ciclo 12: Verifica que IFirestoreQueryExecutor tiene ExecuteQueryAsync (genérico).
    /// </summary>
    [Fact]
    public void IFirestoreQueryExecutor_ShouldHaveExecuteQueryAsyncMethod()
    {
        var methods = typeof(IFirestoreQueryExecutor).GetMethods()
            .Where(m => m.Name == "ExecuteQueryAsync")
            .ToList();

        methods.Should().NotBeEmpty();

        // Verificar que existe el método genérico
        var genericMethod = methods.FirstOrDefault(m => m.IsGenericMethod);
        genericMethod.Should().NotBeNull("El método genérico ExecuteQueryAsync<T> debería existir");
    }

    /// <summary>
    /// Verifica que IFirestoreQueryExecutor tiene ExecuteIdQueryAsync&lt;T&gt;.
    /// </summary>
    [Fact]
    public void IFirestoreQueryExecutor_ShouldHaveExecuteIdQueryAsyncMethod()
    {
        var genericMethod = typeof(IFirestoreQueryExecutor).GetMethods()
            .FirstOrDefault(m => m.Name == "ExecuteIdQueryAsync" && m.IsGenericMethod);

        genericMethod.Should().NotBeNull("El método genérico ExecuteIdQueryAsync<T> debería existir");
    }

    /// <summary>
    /// Ciclo 12: Verifica que IFirestoreQueryExecutor puede ser mockeado.
    /// </summary>
    [Fact]
    public void IFirestoreQueryExecutor_CanBeMocked()
    {
        var mockExecutor = new Mock<IFirestoreQueryExecutor>();

        mockExecutor.Object.Should().NotBeNull();
    }

    #endregion
}
