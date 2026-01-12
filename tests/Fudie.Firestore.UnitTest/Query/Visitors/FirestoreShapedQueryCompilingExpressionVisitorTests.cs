using System;
using System.Linq.Expressions;
using FluentAssertions;
using Fudie.Firestore.EntityFrameworkCore.Query;
using Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;
using Fudie.Firestore.EntityFrameworkCore.Query.Visitors;
using Microsoft.EntityFrameworkCore.Query;

namespace Fudie.Firestore.UnitTest.Query.Visitors;

public class FirestoreShapedQueryCompilingExpressionVisitorTests
{
    #region Class Structure Tests

    [Fact]
    public void FirestoreShapedQueryCompilingExpressionVisitor_Extends_ShapedQueryCompilingExpressionVisitor()
    {
        typeof(FirestoreShapedQueryCompilingExpressionVisitor)
            .Should().BeAssignableTo<ShapedQueryCompilingExpressionVisitor>();
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_Has_Mediator_Parameter()
    {
        var constructors = typeof(FirestoreShapedQueryCompilingExpressionVisitor).GetConstructors();

        constructors.Should().HaveCount(1);
        var parameters = constructors[0].GetParameters();

        parameters.Should().Contain(p => p.ParameterType == typeof(IQueryPipelineMediator));
    }

    [Fact]
    public void Constructor_Has_Dependencies_Parameter()
    {
        var constructors = typeof(FirestoreShapedQueryCompilingExpressionVisitor).GetConstructors();
        var parameters = constructors[0].GetParameters();

        parameters.Should().Contain(p => p.ParameterType == typeof(ShapedQueryCompilingExpressionVisitorDependencies));
    }

    [Fact]
    public void Constructor_Has_QueryCompilationContext_Parameter()
    {
        var constructors = typeof(FirestoreShapedQueryCompilingExpressionVisitor).GetConstructors();
        var parameters = constructors[0].GetParameters();

        parameters.Should().Contain(p => p.ParameterType == typeof(QueryCompilationContext));
    }

    #endregion

    #region VisitShapedQuery Method Tests

    [Fact]
    public void VisitShapedQuery_Method_Exists()
    {
        var method = typeof(FirestoreShapedQueryCompilingExpressionVisitor).GetMethod(
            "VisitShapedQuery",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        method.Should().NotBeNull();
    }

    [Fact]
    public void VisitShapedQuery_Returns_Expression()
    {
        var method = typeof(FirestoreShapedQueryCompilingExpressionVisitor).GetMethod(
            "VisitShapedQuery",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Expression));
    }

    #endregion
}
