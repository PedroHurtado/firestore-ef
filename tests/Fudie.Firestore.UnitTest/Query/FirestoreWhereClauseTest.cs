namespace Fudie.Firestore.UnitTest.Query;

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
