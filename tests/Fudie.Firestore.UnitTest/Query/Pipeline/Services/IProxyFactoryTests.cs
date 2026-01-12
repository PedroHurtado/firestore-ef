using Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using Xunit;

namespace Fudie.Firestore.UnitTest.Query.Pipeline.Services;

public class IProxyFactoryTests
{
    #region Interface Structure Tests

    [Fact]
    public void IProxyFactory_Is_Interface()
    {
        typeof(IProxyFactory).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void IProxyFactory_Has_GetProxyType_Method()
    {
        var method = typeof(IProxyFactory).GetMethod("GetProxyType");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Type));
    }

    [Fact]
    public void GetProxyType_Accepts_IEntityType_Parameter()
    {
        var method = typeof(IProxyFactory).GetMethod("GetProxyType");
        var parameters = method!.GetParameters();

        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(IEntityType));
        parameters[0].Name.Should().Be("entityType");
    }

    [Fact]
    public void IProxyFactory_Has_CreateProxy_Method()
    {
        var method = typeof(IProxyFactory).GetMethod("CreateProxy");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(object));
    }

    [Fact]
    public void CreateProxy_Accepts_IEntityType_Parameter()
    {
        var method = typeof(IProxyFactory).GetMethod("CreateProxy");
        var parameters = method!.GetParameters();

        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(IEntityType));
        parameters[0].Name.Should().Be("entityType");
    }

    #endregion

    #region Design Documentation Tests

    [Fact]
    public void IProxyFactory_Abstracts_EFCore_Proxies_Package()
    {
        // Documents that IProxyFactory is an abstraction over the optional
        // Microsoft.EntityFrameworkCore.Proxies package.
        // This allows the pipeline to work with or without lazy loading.

        typeof(IProxyFactory).IsInterface.Should().BeTrue(
            "IProxyFactory abstracts the optional Proxies package");
    }

    [Fact]
    public void GetProxyType_Returns_DynamicType_For_LazyLoading()
    {
        // Documents that GetProxyType returns a dynamically generated type
        // that inherits from the entity and intercepts navigation property access.

        var method = typeof(IProxyFactory).GetMethod("GetProxyType");

        method!.ReturnType.Should().Be(typeof(Type),
            "GetProxyType returns the proxy Type for instantiation");
    }

    [Fact]
    public void CreateProxy_Returns_Empty_Instance_For_Deserialization()
    {
        // Documents that CreateProxy returns an empty instance.
        // The deserializer will populate properties after creation.
        // This is different from the EF Core Proxies approach which wraps
        // an existing entity.

        var method = typeof(IProxyFactory).GetMethod("CreateProxy");

        method!.ReturnType.Should().Be(typeof(object),
            "CreateProxy returns empty instance to be populated by deserializer");
    }

    #endregion
}
