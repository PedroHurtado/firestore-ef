namespace Fudie.Firestore.UnitTest.Query.Pipeline;

public class QueryKindTests
{
    [Fact]
    public void QueryKind_Has_Entity_Value()
    {
        Enum.IsDefined(typeof(QueryKind), "Entity").Should().BeTrue();
    }

    [Fact]
    public void QueryKind_Has_Aggregation_Value()
    {
        Enum.IsDefined(typeof(QueryKind), "Aggregation").Should().BeTrue();
    }

    [Fact]
    public void QueryKind_Has_Projection_Value()
    {
        Enum.IsDefined(typeof(QueryKind), "Projection").Should().BeTrue();
    }

    [Fact]
    public void QueryKind_Has_Predicate_Value()
    {
        Enum.IsDefined(typeof(QueryKind), "Predicate").Should().BeTrue();
    }

    [Fact]
    public void QueryKind_Has_Exactly_Four_Values()
    {
        Enum.GetValues<QueryKind>().Should().HaveCount(4);
    }
}
