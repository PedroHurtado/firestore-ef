namespace Fudie.Firestore.UnitTest.Query;

public class FirestoreQueryExpressionTest
{
    private readonly Mock<IEntityType> _entityTypeMock;

    public FirestoreQueryExpressionTest()
    {
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
    public void Constructor_Sets_EntityType_And_CollectionName()
    {
        var query = new FirestoreQueryExpression(_entityTypeMock.Object, "products");

        query.EntityType.Should().Be(_entityTypeMock.Object);
        query.CollectionName.Should().Be("products");
    }

    [Fact]
    public void Constructor_Initializes_Empty_Lists()
    {
        var query = new FirestoreQueryExpression(_entityTypeMock.Object, "products");

        query.Filters.Should().BeEmpty();
        query.OrderByClauses.Should().BeEmpty();
        query.PendingIncludes.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_Sets_Null_Defaults()
    {
        var query = new FirestoreQueryExpression(_entityTypeMock.Object, "products");

        query.Limit.Should().BeNull();
        query.StartAfterCursor.Should().BeNull();
        query.IdValueExpression.Should().BeNull();
    }

    [Fact]
    public void Constructor_Throws_On_Null_EntityType()
    {
        var action = () => new FirestoreQueryExpression(null!, "products");

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("entityType");
    }

    [Fact]
    public void Constructor_Throws_On_Null_CollectionName()
    {
        var action = () => new FirestoreQueryExpression(_entityTypeMock.Object, null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("collectionName");
    }

    #endregion

    #region IsIdOnlyQuery Tests

    [Fact]
    public void IsIdOnlyQuery_Returns_False_When_IdValueExpression_Is_Null()
    {
        var query = new FirestoreQueryExpression(_entityTypeMock.Object, "products");

        query.IsIdOnlyQuery.Should().BeFalse();
    }

    [Fact]
    public void IsIdOnlyQuery_Returns_True_When_IdValueExpression_Is_Set()
    {
        var query = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
        {
            IdValueExpression = Expression.Constant("doc-123")
        };

        query.IsIdOnlyQuery.Should().BeTrue();
    }

    #endregion

    #region AddFilter Tests

    [Fact]
    public void AddFilter_Creates_New_Query_With_Filter()
    {
        var original = new FirestoreQueryExpression(_entityTypeMock.Object, "products");
        var filter = new FirestoreWhereClause("Name", FirestoreOperator.EqualTo, Expression.Constant("Test"));

        var updated = original.AddFilter(filter);

        updated.Filters.Should().HaveCount(1);
        updated.Filters[0].Should().Be(filter);
        original.Filters.Should().BeEmpty("original should be unchanged");
    }

    [Fact]
    public void AddFilter_Preserves_Existing_Filters()
    {
        var filter1 = new FirestoreWhereClause("Name", FirestoreOperator.EqualTo, Expression.Constant("Test"));
        var filter2 = new FirestoreWhereClause("Price", FirestoreOperator.GreaterThan, Expression.Constant(100m));

        var query = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
            .AddFilter(filter1)
            .AddFilter(filter2);

        query.Filters.Should().HaveCount(2);
        query.Filters.Should().Contain(filter1);
        query.Filters.Should().Contain(filter2);
    }

    #endregion

    #region AddOrderBy Tests

    [Fact]
    public void AddOrderBy_Creates_New_Query_With_OrderBy()
    {
        var original = new FirestoreQueryExpression(_entityTypeMock.Object, "products");
        var orderBy = new FirestoreOrderByClause("Name");

        var updated = original.AddOrderBy(orderBy);

        updated.OrderByClauses.Should().HaveCount(1);
        updated.OrderByClauses[0].Should().Be(orderBy);
        original.OrderByClauses.Should().BeEmpty("original should be unchanged");
    }

    [Fact]
    public void AddOrderBy_Preserves_Existing_OrderBys()
    {
        var orderBy1 = new FirestoreOrderByClause("Name");
        var orderBy2 = new FirestoreOrderByClause("Price", descending: true);

        var query = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
            .AddOrderBy(orderBy1)
            .AddOrderBy(orderBy2);

        query.OrderByClauses.Should().HaveCount(2);
    }

    #endregion

    #region WithLimit Tests

    [Fact]
    public void WithLimit_Creates_New_Query_With_Limit()
    {
        var original = new FirestoreQueryExpression(_entityTypeMock.Object, "products");

        var updated = original.WithLimit(10);

        updated.Limit.Should().Be(10);
        original.Limit.Should().BeNull("original should be unchanged");
    }

    [Fact]
    public void WithLimit_Can_Be_Chained()
    {
        var query = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
            .WithLimit(5)
            .WithLimit(10);

        query.Limit.Should().Be(10);
    }

    #endregion

    #region Update Tests

    [Fact]
    public void Update_Creates_Copy_With_New_Values()
    {
        var original = new FirestoreQueryExpression(_entityTypeMock.Object, "products");

        var updated = original.Update(collectionName: "new_collection", limit: 20);

        updated.CollectionName.Should().Be("new_collection");
        updated.Limit.Should().Be(20);
        original.CollectionName.Should().Be("products");
        original.Limit.Should().BeNull();
    }

    [Fact]
    public void Update_Preserves_Values_When_Null_Passed()
    {
        var filter = new FirestoreWhereClause("Name", FirestoreOperator.EqualTo, Expression.Constant("Test"));
        var original = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
            .AddFilter(filter)
            .WithLimit(5);

        var updated = original.Update();

        updated.CollectionName.Should().Be("products");
        updated.Limit.Should().Be(5);
        updated.Filters.Should().HaveCount(1);
    }

    #endregion

    #region Expression Properties Tests

    [Fact]
    public void NodeType_Returns_Extension()
    {
        var query = new FirestoreQueryExpression(_entityTypeMock.Object, "products");

        query.NodeType.Should().Be(ExpressionType.Extension);
    }

    [Fact]
    public void Type_Returns_IAsyncEnumerable_Of_EntityType()
    {
        var query = new FirestoreQueryExpression(_entityTypeMock.Object, "products");

        query.Type.Should().Be(typeof(IAsyncEnumerable<TestEntity>));
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_Includes_CollectionName()
    {
        var query = new FirestoreQueryExpression(_entityTypeMock.Object, "products");

        query.ToString().Should().Contain("Collection: products");
    }

    [Fact]
    public void ToString_Includes_Limit_When_Set()
    {
        var query = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
            .WithLimit(10);

        query.ToString().Should().Contain("Limit: 10");
    }

    [Fact]
    public void ToString_Includes_Filters_When_Present()
    {
        var query = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
            .AddFilter(new FirestoreWhereClause("Name", FirestoreOperator.EqualTo, Expression.Constant("Test")));

        query.ToString().Should().Contain("Filters:");
    }

    #endregion
}

public class FirestoreWhereClauseTest
{
    [Fact]
    public void Constructor_Sets_Properties()
    {
        var valueExpr = Expression.Constant("Test");

        var clause = new FirestoreWhereClause("Name", FirestoreOperator.EqualTo, valueExpr);

        clause.PropertyName.Should().Be("Name");
        clause.Operator.Should().Be(FirestoreOperator.EqualTo);
        clause.ValueExpression.Should().Be(valueExpr);
    }

    [Fact]
    public void Constructor_Throws_On_Null_PropertyName()
    {
        var action = () => new FirestoreWhereClause(null!, FirestoreOperator.EqualTo, Expression.Constant("Test"));

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("propertyName");
    }

    [Fact]
    public void Constructor_Throws_On_Null_ValueExpression()
    {
        var action = () => new FirestoreWhereClause("Name", FirestoreOperator.EqualTo, null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("valueExpression");
    }

    [Theory]
    [InlineData(FirestoreOperator.EqualTo, "==")]
    [InlineData(FirestoreOperator.NotEqualTo, "!=")]
    [InlineData(FirestoreOperator.LessThan, "<")]
    [InlineData(FirestoreOperator.LessThanOrEqualTo, "<=")]
    [InlineData(FirestoreOperator.GreaterThan, ">")]
    [InlineData(FirestoreOperator.GreaterThanOrEqualTo, ">=")]
    [InlineData(FirestoreOperator.ArrayContains, "array-contains")]
    [InlineData(FirestoreOperator.In, "in")]
    [InlineData(FirestoreOperator.ArrayContainsAny, "array-contains-any")]
    [InlineData(FirestoreOperator.NotIn, "not-in")]
    public void ToString_Shows_Correct_Operator_Symbol(FirestoreOperator op, string expectedSymbol)
    {
        var clause = new FirestoreWhereClause("Field", op, Expression.Constant("value"));

        clause.ToString().Should().Contain(expectedSymbol);
    }
}

public class FirestoreOrderByClauseTest
{
    [Fact]
    public void Constructor_Sets_Properties_With_Defaults()
    {
        var clause = new FirestoreOrderByClause("Name");

        clause.PropertyName.Should().Be("Name");
        clause.Descending.Should().BeFalse();
    }

    [Fact]
    public void Constructor_Sets_Descending_When_Specified()
    {
        var clause = new FirestoreOrderByClause("Price", descending: true);

        clause.PropertyName.Should().Be("Price");
        clause.Descending.Should().BeTrue();
    }

    [Fact]
    public void Constructor_Throws_On_Null_PropertyName()
    {
        var action = () => new FirestoreOrderByClause(null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("propertyName");
    }

    [Fact]
    public void ToString_Shows_ASC_For_Ascending()
    {
        var clause = new FirestoreOrderByClause("Name", descending: false);

        clause.ToString().Should().Be("Name ASC");
    }

    [Fact]
    public void ToString_Shows_DESC_For_Descending()
    {
        var clause = new FirestoreOrderByClause("Price", descending: true);

        clause.ToString().Should().Be("Price DESC");
    }
}
