using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using FluentAssertions;
using Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;
using Microsoft.EntityFrameworkCore.Query;

namespace Fudie.Firestore.UnitTest.Query.Pipeline;

public class FirestorePipelineQueryingEnumerableTests
{
    private class TestEntity
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
    }

    #region Interface Implementation Tests

    [Fact]
    public void FirestorePipelineQueryingEnumerable_Implements_IAsyncEnumerable()
    {
        typeof(FirestorePipelineQueryingEnumerable<TestEntity>)
            .Should().Implement<IAsyncEnumerable<TestEntity>>();
    }

    [Fact]
    public void FirestorePipelineQueryingEnumerable_Implements_IEnumerable()
    {
        typeof(FirestorePipelineQueryingEnumerable<TestEntity>)
            .Should().Implement<IEnumerable<TestEntity>>();
    }

    [Fact]
    public void FirestorePipelineQueryingEnumerable_Is_Generic_Type()
    {
        typeof(FirestorePipelineQueryingEnumerable<>).IsGenericTypeDefinition.Should().BeTrue();
    }

    #endregion

    #region Constructor Signature Tests

    [Fact]
    public void Constructor_Has_Required_Parameters()
    {
        var constructors = typeof(FirestorePipelineQueryingEnumerable<TestEntity>).GetConstructors();

        constructors.Should().HaveCount(1);
    }

    [Fact]
    public void Constructor_Has_Mediator_Parameter()
    {
        var constructor = typeof(FirestorePipelineQueryingEnumerable<TestEntity>).GetConstructors()[0];
        var parameters = constructor.GetParameters();

        parameters.Should().Contain(p => p.ParameterType == typeof(IQueryPipelineMediator));
    }

    [Fact]
    public void Constructor_Has_PipelineContext_Parameter()
    {
        var constructor = typeof(FirestorePipelineQueryingEnumerable<TestEntity>).GetConstructors()[0];
        var parameters = constructor.GetParameters();

        parameters.Should().Contain(p => p.ParameterType == typeof(PipelineContext));
    }

    #endregion

    #region GetAsyncEnumerator Tests

    [Fact]
    public void GetAsyncEnumerator_Method_Exists()
    {
        var method = typeof(FirestorePipelineQueryingEnumerable<TestEntity>).GetMethod("GetAsyncEnumerator");

        method.Should().NotBeNull();
    }

    [Fact]
    public void GetAsyncEnumerator_Returns_IAsyncEnumerator()
    {
        var method = typeof(FirestorePipelineQueryingEnumerable<TestEntity>).GetMethod("GetAsyncEnumerator");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(IAsyncEnumerator<TestEntity>));
    }

    [Fact]
    public void GetAsyncEnumerator_Accepts_CancellationToken()
    {
        var method = typeof(FirestorePipelineQueryingEnumerable<TestEntity>).GetMethod("GetAsyncEnumerator");

        method.Should().NotBeNull();
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(CancellationToken));
    }

    #endregion

    #region GetEnumerator Tests

    [Fact]
    public void GetEnumerator_Method_Exists()
    {
        var method = typeof(FirestorePipelineQueryingEnumerable<TestEntity>).GetMethod(
            "GetEnumerator",
            Type.EmptyTypes);

        method.Should().NotBeNull();
    }

    [Fact]
    public void GetEnumerator_Returns_IEnumerator_Of_T()
    {
        var method = typeof(FirestorePipelineQueryingEnumerable<TestEntity>).GetMethod(
            "GetEnumerator",
            Type.EmptyTypes);

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(IEnumerator<TestEntity>));
    }

    #endregion

    #region Generic Type Tests

    [Fact]
    public void Can_Be_Created_With_Any_Type()
    {
        var enumerableType = typeof(FirestorePipelineQueryingEnumerable<>);

        var closedWithInt = enumerableType.MakeGenericType(typeof(int));
        var closedWithString = enumerableType.MakeGenericType(typeof(string));
        var closedWithObject = enumerableType.MakeGenericType(typeof(object));

        closedWithInt.Should().NotBeNull();
        closedWithString.Should().NotBeNull();
        closedWithObject.Should().NotBeNull();
    }

    [Fact]
    public void Different_Types_Create_Different_Closed_Types()
    {
        var type1 = typeof(FirestorePipelineQueryingEnumerable<TestEntity>);
        var type2 = typeof(FirestorePipelineQueryingEnumerable<AnotherEntity>);

        type1.Should().NotBe(type2);
    }

    private class AnotherEntity
    {
        public string Id { get; set; } = default!;
    }

    #endregion

    #region No Class Constraint Tests

    [Fact]
    public void Can_Be_Used_With_Value_Types()
    {
        // A diferencia del Enumerable antiguo, este no tiene restricción "where T : class"
        // porque las agregaciones retornan int, long, decimal, etc.
        var type = typeof(FirestorePipelineQueryingEnumerable<int>);

        type.Should().NotBeNull();
    }

    [Fact]
    public void Can_Be_Used_With_Decimal_For_Aggregations()
    {
        var type = typeof(FirestorePipelineQueryingEnumerable<decimal>);

        type.Should().NotBeNull();
    }

    [Fact]
    public void Can_Be_Used_With_Bool_For_Any_All()
    {
        var type = typeof(FirestorePipelineQueryingEnumerable<bool>);

        type.Should().NotBeNull();
    }

    #endregion
}
