using System.Text;
using Fudie.Firestore.EntityFrameworkCore.Update;

namespace Fudie.Firestore.UnitTest.Update;

public class FirestoreUpdateSqlGeneratorTest
{
    private static readonly Type[] AppendOperationParameterTypes = new[]
    {
        typeof(StringBuilder),
        typeof(IReadOnlyModificationCommand),
        typeof(int)
    };

    #region Inheritance Tests

    [Fact]
    public void FirestoreUpdateSqlGenerator_Inherits_From_UpdateSqlGenerator()
    {
        typeof(FirestoreUpdateSqlGenerator)
            .Should().BeDerivedFrom<UpdateSqlGenerator>();
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_Has_Dependencies_Parameter()
    {
        var constructors = typeof(FirestoreUpdateSqlGenerator).GetConstructors();

        constructors.Should().HaveCount(1);
        constructors[0].GetParameters().Should().HaveCount(1);
        constructors[0].GetParameters()[0].ParameterType.Should().Be(typeof(UpdateSqlGeneratorDependencies));
    }

    #endregion

    #region AppendInsertOperation Tests

    [Fact]
    public void AppendInsertOperation_Method_Exists()
    {
        var method = typeof(FirestoreUpdateSqlGenerator).GetMethod(
            "AppendInsertOperation",
            AppendOperationParameterTypes);

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(ResultSetMapping));
    }

    [Fact]
    public void AppendInsertOperation_Has_Correct_Parameters()
    {
        var method = typeof(FirestoreUpdateSqlGenerator).GetMethod(
            "AppendInsertOperation",
            AppendOperationParameterTypes);

        method.Should().NotBeNull();
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(3);
        parameters[0].ParameterType.Should().Be(typeof(StringBuilder));
        parameters[1].ParameterType.Should().Be(typeof(IReadOnlyModificationCommand));
        parameters[2].ParameterType.Should().Be(typeof(int));
    }

    [Fact]
    public void AppendInsertOperation_Is_Override()
    {
        var method = typeof(FirestoreUpdateSqlGenerator).GetMethod(
            "AppendInsertOperation",
            AppendOperationParameterTypes);

        method.Should().NotBeNull();
        method!.GetBaseDefinition().DeclaringType.Should().Be(typeof(UpdateSqlGenerator));
    }

    #endregion

    #region AppendUpdateOperation Tests

    [Fact]
    public void AppendUpdateOperation_Method_Exists()
    {
        var method = typeof(FirestoreUpdateSqlGenerator).GetMethod(
            "AppendUpdateOperation",
            AppendOperationParameterTypes);

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(ResultSetMapping));
    }

    [Fact]
    public void AppendUpdateOperation_Has_Correct_Parameters()
    {
        var method = typeof(FirestoreUpdateSqlGenerator).GetMethod(
            "AppendUpdateOperation",
            AppendOperationParameterTypes);

        method.Should().NotBeNull();
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(3);
        parameters[0].ParameterType.Should().Be(typeof(StringBuilder));
        parameters[1].ParameterType.Should().Be(typeof(IReadOnlyModificationCommand));
        parameters[2].ParameterType.Should().Be(typeof(int));
    }

    [Fact]
    public void AppendUpdateOperation_Is_Override()
    {
        var method = typeof(FirestoreUpdateSqlGenerator).GetMethod(
            "AppendUpdateOperation",
            AppendOperationParameterTypes);

        method.Should().NotBeNull();
        method!.GetBaseDefinition().DeclaringType.Should().Be(typeof(UpdateSqlGenerator));
    }

    #endregion

    #region AppendDeleteOperation Tests

    [Fact]
    public void AppendDeleteOperation_Method_Exists()
    {
        var method = typeof(FirestoreUpdateSqlGenerator).GetMethod(
            "AppendDeleteOperation",
            AppendOperationParameterTypes);

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(ResultSetMapping));
    }

    [Fact]
    public void AppendDeleteOperation_Has_Correct_Parameters()
    {
        var method = typeof(FirestoreUpdateSqlGenerator).GetMethod(
            "AppendDeleteOperation",
            AppendOperationParameterTypes);

        method.Should().NotBeNull();
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(3);
        parameters[0].ParameterType.Should().Be(typeof(StringBuilder));
        parameters[1].ParameterType.Should().Be(typeof(IReadOnlyModificationCommand));
        parameters[2].ParameterType.Should().Be(typeof(int));
    }

    [Fact]
    public void AppendDeleteOperation_Is_Override()
    {
        var method = typeof(FirestoreUpdateSqlGenerator).GetMethod(
            "AppendDeleteOperation",
            AppendOperationParameterTypes);

        method.Should().NotBeNull();
        method!.GetBaseDefinition().DeclaringType.Should().Be(typeof(UpdateSqlGenerator));
    }

    #endregion

    #region ResultSetMapping Tests

    [Fact]
    public void ResultSetMapping_NoResults_Is_Valid_Value()
    {
        var noResults = ResultSetMapping.NoResults;

        noResults.Should().Be(ResultSetMapping.NoResults);
    }

    [Theory]
    [InlineData("AppendInsertOperation")]
    [InlineData("AppendUpdateOperation")]
    [InlineData("AppendDeleteOperation")]
    public void All_Append_Methods_Return_ResultSetMapping(string methodName)
    {
        var method = typeof(FirestoreUpdateSqlGenerator).GetMethod(
            methodName,
            AppendOperationParameterTypes);

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(ResultSetMapping));
    }

    #endregion
}
