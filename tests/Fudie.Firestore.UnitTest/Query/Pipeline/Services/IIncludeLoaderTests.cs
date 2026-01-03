using Firestore.EntityFrameworkCore.Query.Pipeline;
using Firestore.EntityFrameworkCore.Query.Resolved;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Fudie.Firestore.UnitTest.Query.Pipeline.Services;

public class IIncludeLoaderTests
{
    #region Interface Structure Tests

    [Fact]
    public void IIncludeLoader_Is_Interface()
    {
        typeof(IIncludeLoader).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void IIncludeLoader_Has_LoadIncludeAsync_Method()
    {
        var method = typeof(IIncludeLoader).GetMethod("LoadIncludeAsync");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task));
    }

    [Fact]
    public void LoadIncludeAsync_Has_Entity_Parameter()
    {
        var method = typeof(IIncludeLoader).GetMethod("LoadIncludeAsync");
        var parameters = method!.GetParameters();

        parameters[0].ParameterType.Should().Be(typeof(object));
        parameters[0].Name.Should().Be("entity");
    }

    [Fact]
    public void LoadIncludeAsync_Has_EntityType_Parameter()
    {
        var method = typeof(IIncludeLoader).GetMethod("LoadIncludeAsync");
        var parameters = method!.GetParameters();

        parameters[1].ParameterType.Should().Be(typeof(IEntityType));
        parameters[1].Name.Should().Be("entityType");
    }

    [Fact]
    public void LoadIncludeAsync_Has_ResolvedInclude_Parameter()
    {
        var method = typeof(IIncludeLoader).GetMethod("LoadIncludeAsync");
        var parameters = method!.GetParameters();

        parameters[2].ParameterType.Should().Be(typeof(ResolvedInclude));
        parameters[2].Name.Should().Be("resolvedInclude");
    }

    [Fact]
    public void LoadIncludeAsync_Has_CancellationToken_Parameter()
    {
        var method = typeof(IIncludeLoader).GetMethod("LoadIncludeAsync");
        var parameters = method!.GetParameters();

        parameters[3].ParameterType.Should().Be(typeof(CancellationToken));
        parameters[3].Name.Should().Be("cancellationToken");
    }

    [Fact]
    public void LoadIncludeAsync_Has_Four_Parameters()
    {
        var method = typeof(IIncludeLoader).GetMethod("LoadIncludeAsync");

        method!.GetParameters().Should().HaveCount(4);
    }

    #endregion
}
