using Fudie.Firestore.EntityFrameworkCore.Storage;
using FluentAssertions;

namespace Fudie.Firestore.UnitTest.Storage;

/// <summary>
/// Tests for the IFirestoreCommandLogger interface contract.
/// Documents the expected behavior that any implementation must provide.
/// </summary>
public class IFirestoreCommandLoggerTests
{
    #region Interface Contract Tests

    [Fact]
    public void IFirestoreCommandLogger_Should_Have_LogInsert_Method()
    {
        // Documents that LogInsert logs document insert operations with optional data
        var method = typeof(IFirestoreCommandLogger).GetMethod("LogInsert");

        method.Should().NotBeNull("IFirestoreCommandLogger must have LogInsert method");
        method!.ReturnType.Should().Be(typeof(void), "LogInsert returns void");
        method.GetParameters().Should().HaveCount(5, "LogInsert takes collectionPath, documentId, entityType, elapsed, and optional data parameters");
    }

    [Fact]
    public void IFirestoreCommandLogger_Should_Have_LogUpdate_Method()
    {
        // Documents that LogUpdate logs document update operations with optional data
        var method = typeof(IFirestoreCommandLogger).GetMethod("LogUpdate");

        method.Should().NotBeNull("IFirestoreCommandLogger must have LogUpdate method");
        method!.ReturnType.Should().Be(typeof(void), "LogUpdate returns void");
        method.GetParameters().Should().HaveCount(5, "LogUpdate takes collectionPath, documentId, entityType, elapsed, and optional data parameters");
    }

    [Fact]
    public void IFirestoreCommandLogger_Should_Have_LogDelete_Method()
    {
        // Documents that LogDelete logs document delete operations
        var method = typeof(IFirestoreCommandLogger).GetMethod("LogDelete");

        method.Should().NotBeNull("IFirestoreCommandLogger must have LogDelete method");
        method!.ReturnType.Should().Be(typeof(void), "LogDelete returns void");
        method.GetParameters().Should().HaveCount(4, "LogDelete takes collectionPath, documentId, entityType, and elapsed parameters");
    }

    [Fact]
    public void IFirestoreCommandLogger_Should_Have_Three_Methods()
    {
        // Documents that IFirestoreCommandLogger logs Insert, Update, and Delete operations
        typeof(IFirestoreCommandLogger).GetMethods()
            .Should().HaveCount(3, "IFirestoreCommandLogger has LogInsert, LogUpdate, and LogDelete methods");
    }

    [Fact]
    public void LogInsert_Parameters_Should_Have_Correct_Types()
    {
        var method = typeof(IFirestoreCommandLogger).GetMethod("LogInsert");
        var parameters = method!.GetParameters();

        parameters[0].ParameterType.Should().Be(typeof(string), "collectionPath is string");
        parameters[1].ParameterType.Should().Be(typeof(string), "documentId is string");
        parameters[2].ParameterType.Should().Be(typeof(Type), "entityType is Type");
        parameters[3].ParameterType.Should().Be(typeof(TimeSpan), "elapsed is TimeSpan");
        parameters[4].ParameterType.Should().Be(typeof(Dictionary<string, object>), "data is Dictionary<string, object>");
        parameters[4].HasDefaultValue.Should().BeTrue("data parameter has default value (optional)");
    }

    [Fact]
    public void LogUpdate_Parameters_Should_Have_Correct_Types()
    {
        var method = typeof(IFirestoreCommandLogger).GetMethod("LogUpdate");
        var parameters = method!.GetParameters();

        parameters[0].ParameterType.Should().Be(typeof(string), "collectionPath is string");
        parameters[1].ParameterType.Should().Be(typeof(string), "documentId is string");
        parameters[2].ParameterType.Should().Be(typeof(Type), "entityType is Type");
        parameters[3].ParameterType.Should().Be(typeof(TimeSpan), "elapsed is TimeSpan");
        parameters[4].ParameterType.Should().Be(typeof(Dictionary<string, object>), "data is Dictionary<string, object>");
        parameters[4].HasDefaultValue.Should().BeTrue("data parameter has default value (optional)");
    }

    [Fact]
    public void LogDelete_Parameters_Should_Have_Correct_Types()
    {
        var method = typeof(IFirestoreCommandLogger).GetMethod("LogDelete");
        var parameters = method!.GetParameters();

        parameters[0].ParameterType.Should().Be(typeof(string), "collectionPath is string");
        parameters[1].ParameterType.Should().Be(typeof(string), "documentId is string");
        parameters[2].ParameterType.Should().Be(typeof(Type), "entityType is Type");
        parameters[3].ParameterType.Should().Be(typeof(TimeSpan), "elapsed is TimeSpan");
    }

    #endregion
}
