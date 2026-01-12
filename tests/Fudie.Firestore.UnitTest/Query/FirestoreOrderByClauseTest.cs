using Fudie.Firestore.EntityFrameworkCore.Query.Ast;

namespace Fudie.Firestore.UnitTest.Query;

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
