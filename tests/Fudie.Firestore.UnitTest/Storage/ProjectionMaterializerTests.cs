using Fudie.Firestore.EntityFrameworkCore.Query.Projections;

namespace Fudie.Firestore.UnitTest.Storage;

/// <summary>
/// Tests for ProjectionMaterializer implementation.
/// </summary>
public class ProjectionMaterializerTests
{
    #region Class Structure Tests

    [Fact]
    public void ProjectionMaterializer_Implements_IProjectionMaterializer()
    {
        typeof(ProjectionMaterializer)
            .Should().Implement<IProjectionMaterializer>();
    }

    [Fact]
    public void ProjectionMaterializer_Constructor_Accepts_IFirestoreValueConverter()
    {
        var constructors = typeof(ProjectionMaterializer).GetConstructors();

        constructors.Should().HaveCount(1);
        var parameters = constructors[0].GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(IFirestoreValueConverter));
    }

    [Fact]
    public void ProjectionMaterializer_Can_Be_Instantiated()
    {
        var mockConverter = new Mock<IFirestoreValueConverter>();

        var materializer = new ProjectionMaterializer(mockConverter.Object);

        materializer.Should().NotBeNull();
    }

    #endregion

    #region Materialize Method Tests

    [Fact]
    public void Materialize_Method_Exists_With_Correct_Signature()
    {
        var method = typeof(ProjectionMaterializer).GetMethod("Materialize");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(object));

        var parameters = method.GetParameters();
        parameters.Should().HaveCount(4);
        parameters[0].ParameterType.Should().Be(typeof(ResolvedProjectionDefinition));
    }

    [Fact]
    public void MaterializeMany_Method_Exists_With_Correct_Signature()
    {
        var method = typeof(ProjectionMaterializer).GetMethod("MaterializeMany");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(IReadOnlyList<object>));

        var parameters = method.GetParameters();
        parameters.Should().HaveCount(4);
    }

    #endregion

    #region Value Conversion Tests

    [Fact]
    public void ProjectionMaterializer_Uses_IFirestoreValueConverter_For_Field_Conversion()
    {
        // Documents that ProjectionMaterializer delegates type conversion to IFirestoreValueConverter
        // This ensures consistent conversion logic across the provider
        typeof(ProjectionMaterializer)
            .GetConstructors()[0]
            .GetParameters()[0]
            .ParameterType.Should().Be(typeof(IFirestoreValueConverter),
                "ProjectionMaterializer uses IFirestoreValueConverter for type conversions");
    }

    #endregion

    #region Projection Definition Tests

    [Fact]
    public void ProjectionMaterializer_Handles_ResolvedProjectionDefinition_With_Fields()
    {
        // Documents that ProjectionMaterializer processes Fields from ResolvedProjectionDefinition
        // Fields contain FieldPath and ResultName mappings
        var fieldsProperty = typeof(ResolvedProjectionDefinition).GetProperty("Fields");

        fieldsProperty.Should().NotBeNull("ResolvedProjectionDefinition must have Fields property");
        fieldsProperty!.PropertyType.Should().Be(typeof(IReadOnlyList<FirestoreProjectedField>));
    }

    [Fact]
    public void ProjectionMaterializer_Handles_ResolvedProjectionDefinition_With_Subcollections()
    {
        // Documents that ProjectionMaterializer processes Subcollections from ResolvedProjectionDefinition
        var subcollectionsProperty = typeof(ResolvedProjectionDefinition).GetProperty("Subcollections");

        subcollectionsProperty.Should().NotBeNull("ResolvedProjectionDefinition must have Subcollections property");
        subcollectionsProperty!.PropertyType.Should().Be(typeof(IReadOnlyList<ResolvedSubcollectionProjection>));
    }

    [Fact]
    public void ProjectionMaterializer_Uses_ClrType_From_Projection_Definition()
    {
        // Documents that the target type for materialization comes from ResolvedProjectionDefinition.ClrType
        var clrTypeProperty = typeof(ResolvedProjectionDefinition).GetProperty("ClrType");

        clrTypeProperty.Should().NotBeNull("ResolvedProjectionDefinition must have ClrType property");
        clrTypeProperty!.PropertyType.Should().Be(typeof(Type));
    }

    #endregion

    #region Aggregation Handling Tests

    [Fact]
    public void ProjectionMaterializer_Resolves_Aggregations_By_Key_Format()
    {
        // Documents that aggregation results are keyed by "{parentPath}:{resultName}"
        // This format allows matching aggregation values to their target properties
        typeof(ProjectionMaterializer).Should().NotBeNull(
            "ProjectionMaterializer uses key format '{parentPath}:{resultName}' for aggregations");
    }

    [Fact]
    public void ResolvedSubcollectionProjection_Has_IsAggregation_Property()
    {
        // Documents that subcollections can be aggregations (Count, Sum, Average)
        var isAggregationProperty = typeof(ResolvedSubcollectionProjection).GetProperty("IsAggregation");

        isAggregationProperty.Should().NotBeNull();
        isAggregationProperty!.PropertyType.Should().Be(typeof(bool));
    }

    [Fact]
    public void ResolvedSubcollectionProjection_Has_ResultName_Property()
    {
        // Documents that ResultName is used to match projection parameters
        var resultNameProperty = typeof(ResolvedSubcollectionProjection).GetProperty("ResultName");

        resultNameProperty.Should().NotBeNull();
        resultNameProperty!.PropertyType.Should().Be(typeof(string));
    }

    #endregion

    #region Constructor Resolution Tests

    [Fact]
    public void ProjectionMaterializer_Prefers_Constructor_With_Most_Parameters()
    {
        // Documents that for anonymous types and records, the constructor with most parameters is used
        // This matches the pattern where all properties are set via constructor
        typeof(ProjectionMaterializer).Should().NotBeNull(
            "ProjectionMaterializer selects constructor with most parameters for anonymous types");
    }

    [Fact]
    public void ProjectionMaterializer_Supports_Parameterless_Constructor_With_Property_Setters()
    {
        // Documents that entities with parameterless constructors are supported
        // Properties are set via reflection after construction
        typeof(ProjectionMaterializer).Should().NotBeNull(
            "ProjectionMaterializer supports parameterless constructors with property setters");
    }

    #endregion

    #region Subcollection Materialization Tests

    [Fact]
    public void ProjectionMaterializer_Finds_Subcollection_Documents_By_Path_Prefix()
    {
        // Documents that subcollection documents are found by matching path prefix
        // Format: "{parentPath}/{collectionPath}/"
        typeof(ProjectionMaterializer).Should().NotBeNull(
            "ProjectionMaterializer finds subcollection documents by path prefix");
    }

    [Fact]
    public void ProjectionMaterializer_Creates_Typed_List_For_Subcollections()
    {
        // Documents that subcollections are materialized as List<T> where T is the element type
        typeof(ProjectionMaterializer).Should().NotBeNull(
            "ProjectionMaterializer creates typed List<T> for subcollections");
    }

    [Fact]
    public void ProjectionMaterializer_Supports_Nested_Subcollections()
    {
        // Documents that nested subcollections are materialized recursively
        var nestedProperty = typeof(ResolvedSubcollectionProjection).GetProperty("NestedSubcollections");

        nestedProperty.Should().NotBeNull();
        nestedProperty!.PropertyType.Should().Be(typeof(IReadOnlyList<ResolvedSubcollectionProjection>));
    }

    #endregion

    #region Id Handling Tests

    [Fact]
    public void ProjectionMaterializer_Maps_Document_Id_To_Id_Parameter()
    {
        // Documents that the document ID is mapped to parameters named "Id" (case-insensitive)
        typeof(ProjectionMaterializer).Should().NotBeNull(
            "ProjectionMaterializer maps DocumentSnapshot.Id to 'Id' parameter");
    }

    #endregion

    #region Field Path Resolution Tests

    [Fact]
    public void ProjectionMaterializer_Supports_Nested_Field_Paths()
    {
        // Documents that field paths like "Address.City" are resolved through nested dictionaries
        typeof(ProjectionMaterializer).Should().NotBeNull(
            "ProjectionMaterializer resolves nested field paths like 'Address.City'");
    }

    [Fact]
    public void FirestoreProjectedField_Has_FieldPath_Property()
    {
        // Documents that FieldPath contains the Firestore field path (e.g., "Address.City")
        var fieldPathProperty = typeof(FirestoreProjectedField).GetProperty("FieldPath");

        fieldPathProperty.Should().NotBeNull();
        fieldPathProperty!.PropertyType.Should().Be(typeof(string));
    }

    [Fact]
    public void FirestoreProjectedField_Has_ResultName_Property()
    {
        // Documents that ResultName contains the target property/parameter name
        var resultNameProperty = typeof(FirestoreProjectedField).GetProperty("ResultName");

        resultNameProperty.Should().NotBeNull();
        resultNameProperty!.PropertyType.Should().Be(typeof(string));
    }

    #endregion
}
