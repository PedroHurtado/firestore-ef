using Fudie.Firestore.EntityFrameworkCore.Query.Ast;
using Fudie.Firestore.EntityFrameworkCore.Query.Projections;

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
            .WithIdValueExpression(Expression.Constant("doc-123"));

        query.IsIdOnlyQuery.Should().BeTrue();
    }

    #endregion

    #region AddFilter Tests

    [Fact]
    public void AddFilter_Adds_Filter_To_Query()
    {
        var query = new FirestoreQueryExpression(_entityTypeMock.Object, "products");
        var filter = new FirestoreWhereClause("Name", FirestoreOperator.EqualTo, Expression.Constant("Test"));

        query.AddFilter(filter);

        query.Filters.Should().HaveCount(1);
        query.Filters[0].Should().Be(filter);
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
    public void AddOrderBy_Adds_OrderBy_To_Query()
    {
        var query = new FirestoreQueryExpression(_entityTypeMock.Object, "products");
        var orderBy = new FirestoreOrderByClause("Name");

        query.AddOrderBy(orderBy);

        query.OrderByClauses.Should().HaveCount(1);
        query.OrderByClauses[0].Should().Be(orderBy);
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
    public void WithLimit_Sets_Limit()
    {
        var query = new FirestoreQueryExpression(_entityTypeMock.Object, "products");

        query.WithLimit(10);

        query.Limit.Should().Be(10);
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

    #region Fluent API Tests

    [Fact]
    public void Commands_Return_This_For_Fluent_API()
    {
        var query = new FirestoreQueryExpression(_entityTypeMock.Object, "products");

        var result = query.WithLimit(20);

        result.Should().BeSameAs(query);
    }

    [Fact]
    public void Chained_Commands_Work_On_Same_Instance()
    {
        var filter = new FirestoreWhereClause("Name", FirestoreOperator.EqualTo, Expression.Constant("Test"));
        var query = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
            .AddFilter(filter)
            .WithLimit(5)
            .WithSkip(10);

        query.CollectionName.Should().Be("products");
        query.Limit.Should().Be(5);
        query.Skip.Should().Be(10);
        query.Filters.Should().HaveCount(1);
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

    #region Projection Tests

    [Fact]
    public void Projection_Is_Null_By_Default()
    {
        var query = new FirestoreQueryExpression(_entityTypeMock.Object, "products");

        query.Projection.Should().BeNull();
    }

    [Fact]
    public void HasProjection_Returns_False_When_Projection_Is_Null()
    {
        var query = new FirestoreQueryExpression(_entityTypeMock.Object, "products");

        query.HasProjection.Should().BeFalse();
    }

    [Fact]
    public void HasProjection_Returns_True_When_Projection_Is_Set()
    {
        var projection = FirestoreProjectionDefinition.CreateEntityProjection(typeof(TestEntity));
        var query = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
            .WithProjection(projection);

        query.HasProjection.Should().BeTrue();
    }

    [Fact]
    public void WithProjection_Sets_Projection()
    {
        var original = new FirestoreQueryExpression(_entityTypeMock.Object, "products");
        var projection = FirestoreProjectionDefinition.CreateEntityProjection(typeof(TestEntity));

        original.WithProjection(projection);

        original.Projection.Should().Be(projection);
    }

    [Fact]
    public void WithProjection_Preserves_Other_Properties()
    {
        var filter = new FirestoreWhereClause("Name", FirestoreOperator.EqualTo, Expression.Constant("Test"));
        var original = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
            .AddFilter(filter)
            .WithLimit(10);

        var projection = FirestoreProjectionDefinition.CreateEntityProjection(typeof(TestEntity));
        var updated = original.WithProjection(projection);

        updated.Filters.Should().HaveCount(1);
        updated.Limit.Should().Be(10);
        updated.Projection.Should().Be(projection);
    }

    [Fact]
    public void Commands_Preserve_Projection()
    {
        var projection = FirestoreProjectionDefinition.CreateEntityProjection(typeof(TestEntity));
        var original = new FirestoreQueryExpression(_entityTypeMock.Object, "products")
            .WithProjection(projection);

        original.WithLimit(5);

        original.Projection.Should().Be(projection);
    }

    #endregion
}
