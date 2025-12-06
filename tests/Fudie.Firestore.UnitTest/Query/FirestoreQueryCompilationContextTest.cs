namespace Fudie.Firestore.UnitTest.Query;

public class FirestoreQueryCompilationContextFactoryTest
{
    #region Interface Implementation Tests

    [Fact]
    public void FirestoreQueryCompilationContextFactory_Implements_IQueryCompilationContextFactory()
    {
        typeof(FirestoreQueryCompilationContextFactory)
            .Should().Implement<IQueryCompilationContextFactory>();
    }

    [Fact]
    public void FirestoreQueryCompilationContext_Inherits_From_QueryCompilationContext()
    {
        typeof(FirestoreQueryCompilationContext)
            .Should().BeDerivedFrom<QueryCompilationContext>();
    }

    #endregion

    #region Constructor Signature Tests

    [Fact]
    public void FirestoreQueryCompilationContextFactory_Has_Constructor_With_Dependencies()
    {
        var constructors = typeof(FirestoreQueryCompilationContextFactory).GetConstructors();

        constructors.Should().HaveCount(1);
        constructors[0].GetParameters().Should().HaveCount(1);
        constructors[0].GetParameters()[0].ParameterType.Should().Be(typeof(QueryCompilationContextDependencies));
    }

    [Fact]
    public void FirestoreQueryCompilationContext_Has_Constructor_With_Dependencies_And_Async()
    {
        var constructors = typeof(FirestoreQueryCompilationContext).GetConstructors();

        constructors.Should().HaveCount(1);
        constructors[0].GetParameters().Should().HaveCount(2);
        constructors[0].GetParameters()[0].ParameterType.Should().Be(typeof(QueryCompilationContextDependencies));
        constructors[0].GetParameters()[1].ParameterType.Should().Be(typeof(bool));
    }

    #endregion

    #region Method Signature Tests

    [Fact]
    public void FirestoreQueryCompilationContextFactory_Create_Method_Returns_QueryCompilationContext()
    {
        var createMethod = typeof(FirestoreQueryCompilationContextFactory).GetMethod("Create");

        createMethod.Should().NotBeNull();
        createMethod!.ReturnType.Should().Be(typeof(QueryCompilationContext));
        createMethod.GetParameters().Should().HaveCount(1);
        createMethod.GetParameters()[0].ParameterType.Should().Be(typeof(bool));
    }

    [Fact]
    public void FirestoreQueryCompilationContextFactory_Create_Method_Has_Async_Parameter()
    {
        var createMethod = typeof(FirestoreQueryCompilationContextFactory).GetMethod("Create");

        createMethod.Should().NotBeNull();
        createMethod!.GetParameters()[0].Name.Should().Be("async");
    }

    #endregion
}
