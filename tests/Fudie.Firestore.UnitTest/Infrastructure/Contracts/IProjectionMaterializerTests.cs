namespace Fudie.Firestore.UnitTest.Infrastructure.Contracts;

/// <summary>
/// Tests for the IProjectionMaterializer interface contract.
/// Documents the expected behavior that any implementation must provide.
/// </summary>
public class IProjectionMaterializerTests
{
    #region Interface Contract Tests

    [Fact]
    public void IProjectionMaterializer_Should_Be_Interface()
    {
        typeof(IProjectionMaterializer).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void IProjectionMaterializer_Should_Have_Materialize_Method()
    {
        var method = typeof(IProjectionMaterializer).GetMethod("Materialize");

        method.Should().NotBeNull("IProjectionMaterializer must have Materialize method");
        method!.ReturnType.Should().Be(typeof(object), "Materialize returns object");
        method.GetParameters().Should().HaveCount(4, "Materialize takes projection, rootSnapshot, allSnapshots, aggregations");
    }

    [Fact]
    public void IProjectionMaterializer_Should_Have_MaterializeMany_Method()
    {
        var method = typeof(IProjectionMaterializer).GetMethod("MaterializeMany");

        method.Should().NotBeNull("IProjectionMaterializer must have MaterializeMany method");
        method!.ReturnType.Should().Be(typeof(IReadOnlyList<object>), "MaterializeMany returns IReadOnlyList<object>");
        method.GetParameters().Should().HaveCount(4, "MaterializeMany takes projection, rootSnapshots, allSnapshots, aggregations");
    }

    [Fact]
    public void Materialize_First_Parameter_Should_Be_ResolvedProjectionDefinition()
    {
        var method = typeof(IProjectionMaterializer).GetMethod("Materialize");
        var parameters = method!.GetParameters();

        parameters[0].ParameterType.Should().Be(typeof(ResolvedProjectionDefinition));
        parameters[0].Name.Should().Be("projection");
    }

    [Fact]
    public void Materialize_Second_Parameter_Should_Be_DocumentSnapshot()
    {
        var method = typeof(IProjectionMaterializer).GetMethod("Materialize");
        var parameters = method!.GetParameters();

        parameters[1].ParameterType.Should().Be(typeof(Google.Cloud.Firestore.DocumentSnapshot));
        parameters[1].Name.Should().Be("rootSnapshot");
    }

    [Fact]
    public void Materialize_Third_Parameter_Should_Be_Dictionary_Of_Snapshots()
    {
        var method = typeof(IProjectionMaterializer).GetMethod("Materialize");
        var parameters = method!.GetParameters();

        parameters[2].ParameterType.Should().Be(typeof(IReadOnlyDictionary<string, Google.Cloud.Firestore.DocumentSnapshot>));
        parameters[2].Name.Should().Be("allSnapshots");
    }

    [Fact]
    public void Materialize_Fourth_Parameter_Should_Be_Dictionary_Of_Aggregations()
    {
        var method = typeof(IProjectionMaterializer).GetMethod("Materialize");
        var parameters = method!.GetParameters();

        parameters[3].ParameterType.Should().Be(typeof(IReadOnlyDictionary<string, object>));
        parameters[3].Name.Should().Be("aggregations");
    }

    [Fact]
    public void MaterializeMany_Second_Parameter_Should_Be_Enumerable_Of_DocumentSnapshot()
    {
        var method = typeof(IProjectionMaterializer).GetMethod("MaterializeMany");
        var parameters = method!.GetParameters();

        parameters[1].ParameterType.Should().Be(typeof(IEnumerable<Google.Cloud.Firestore.DocumentSnapshot>));
        parameters[1].Name.Should().Be("rootSnapshots");
    }

    #endregion
}
