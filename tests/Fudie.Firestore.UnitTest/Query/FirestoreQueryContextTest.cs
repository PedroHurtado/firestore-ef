namespace Fudie.Firestore.UnitTest.Query;

public class FirestoreQueryContextFactoryTest
{
    #region Interface Implementation Tests

    [Fact]
    public void FirestoreQueryContextFactory_Implements_IQueryContextFactory()
    {
        typeof(FirestoreQueryContextFactory)
            .Should().Implement<IQueryContextFactory>();
    }

    [Fact]
    public void FirestoreQueryContext_Inherits_From_QueryContext()
    {
        typeof(FirestoreQueryContext)
            .Should().BeDerivedFrom<QueryContext>();
    }

    #endregion

    #region Constructor Signature Tests

    [Fact]
    public void FirestoreQueryContextFactory_Has_Constructor_With_Dependencies()
    {
        var constructors = typeof(FirestoreQueryContextFactory).GetConstructors();

        constructors.Should().HaveCount(1);
        constructors[0].GetParameters().Should().HaveCount(1);
        constructors[0].GetParameters()[0].ParameterType.Should().Be(typeof(QueryContextDependencies));
    }

    [Fact]
    public void FirestoreQueryContext_Has_Constructor_With_Dependencies()
    {
        var constructors = typeof(FirestoreQueryContext).GetConstructors();

        constructors.Should().HaveCount(1);
        constructors[0].GetParameters().Should().HaveCount(1);
        constructors[0].GetParameters()[0].ParameterType.Should().Be(typeof(QueryContextDependencies));
    }

    #endregion

    #region Method Signature Tests

    [Fact]
    public void FirestoreQueryContextFactory_Create_Method_Returns_QueryContext()
    {
        var createMethod = typeof(FirestoreQueryContextFactory).GetMethod("Create");

        createMethod.Should().NotBeNull();
        createMethod!.ReturnType.Should().Be(typeof(QueryContext));
        createMethod.GetParameters().Should().BeEmpty();
    }

    #endregion
}
