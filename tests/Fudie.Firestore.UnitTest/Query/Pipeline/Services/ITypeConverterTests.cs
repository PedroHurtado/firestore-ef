using Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;
using FluentAssertions;
using System;
using Xunit;

namespace Fudie.Firestore.UnitTest.Query.Pipeline.Services;

public class ITypeConverterTests
{
    #region Interface Structure Tests

    [Fact]
    public void ITypeConverter_Is_Interface()
    {
        typeof(ITypeConverter).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void ITypeConverter_Has_Convert_Method()
    {
        var method = typeof(ITypeConverter).GetMethod("Convert");

        method.Should().NotBeNull();
    }

    [Fact]
    public void Convert_Returns_Nullable_Object()
    {
        var method = typeof(ITypeConverter).GetMethod("Convert");

        // Return type is object? (nullable)
        method!.ReturnType.Should().Be(typeof(object));
    }

    [Fact]
    public void Convert_Has_Value_Parameter()
    {
        var method = typeof(ITypeConverter).GetMethod("Convert");
        var parameters = method!.GetParameters();

        parameters[0].ParameterType.Should().Be(typeof(object));
        parameters[0].Name.Should().Be("value");
    }

    [Fact]
    public void Convert_Has_TargetType_Parameter()
    {
        var method = typeof(ITypeConverter).GetMethod("Convert");
        var parameters = method!.GetParameters();

        parameters[1].ParameterType.Should().Be(typeof(Type));
        parameters[1].Name.Should().Be("targetType");
    }

    [Fact]
    public void Convert_Has_Two_Parameters()
    {
        var method = typeof(ITypeConverter).GetMethod("Convert");

        method!.GetParameters().Should().HaveCount(2);
    }

    #endregion

    #region Design Documentation Tests

    [Fact]
    public void ITypeConverter_Used_For_Aggregation_Results()
    {
        // Documents that ITypeConverter is used by SnapshotShapingHandler
        // to convert aggregation results from Firestore types to CLR types.
        // Example: Firestore returns long for Count, but LINQ expects int.

        typeof(ITypeConverter).IsInterface.Should().BeTrue(
            "ITypeConverter converts Firestore types to CLR types");
    }

    [Fact]
    public void ITypeConverter_Used_For_MinMax_Field_Values()
    {
        // Documents that Min/Max aggregations return Streaming results.
        // The SnapshotShapingHandler extracts the field value and uses ITypeConverter
        // to convert it to the expected CLR type.

        var method = typeof(ITypeConverter).GetMethod("Convert");

        method.Should().NotBeNull(
            "Convert is used for Min/Max field value conversion");
    }

    [Fact]
    public void Convert_Value_Parameter_Can_Be_Null()
    {
        // Documents that the value parameter can be null.
        // The implementation should handle null appropriately based on targetType.

        var method = typeof(ITypeConverter).GetMethod("Convert");
        var valueParam = method!.GetParameters()[0];

        // object type can accept null
        valueParam.ParameterType.Should().Be(typeof(object),
            "Value can be null for empty aggregation results");
    }

    #endregion
}
