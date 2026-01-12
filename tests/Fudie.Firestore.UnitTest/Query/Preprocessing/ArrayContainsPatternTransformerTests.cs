using System.Linq.Expressions;
using Fudie.Firestore.EntityFrameworkCore.Query.Preprocessing;
using Fudie.Firestore.EntityFrameworkCore.Query.Visitors;
using FluentAssertions;

namespace Fudie.Firestore.UnitTest.Query.Preprocessing;

/// <summary>
/// Tests for ArrayContainsPatternTransformer.
/// Transforms array Contains patterns into FirestoreArrayContainsExpression markers.
/// </summary>
public class ArrayContainsPatternTransformerTests
{
    #region Transform - No Changes

    [Fact]
    public void Transform_WithSimpleComparison_ReturnsUnchanged()
    {
        Expression<Func<TestEntity, bool>> expr = e => e.Name == "test";

        var result = ArrayContainsPatternTransformer.Transform(expr.Body);

        result.Should().Be(expr.Body);
    }

    [Fact]
    public void Transform_WithNull_ReturnsNull()
    {
        var result = ArrayContainsPatternTransformer.Transform(null!);

        result.Should().BeNull();
    }

    #endregion

    #region Transform - ArrayContains Pattern

    [Fact]
    public void Transform_WithArrayContainsExpression_ReturnsFirestoreArrayContainsExpression()
    {
        // Simulates: e.Tags.AsQueryable().Contains("value")
        // which EF Core transforms from e.Tags.Contains("value")
        var parameter = Expression.Parameter(typeof(TestEntity), "e");
        var tagsProperty = Expression.Property(parameter, nameof(TestEntity.Tags));

        // EF.Property<List<string>>(e, "Tags")
        var efPropertyMethod = typeof(Microsoft.EntityFrameworkCore.EF)
            .GetMethod("Property")!
            .MakeGenericMethod(typeof(List<string>));
        var efPropertyCall = Expression.Call(efPropertyMethod, parameter, Expression.Constant("Tags"));

        // .AsQueryable()
        var asQueryableMethod = typeof(Queryable)
            .GetMethods()
            .First(m => m.Name == "AsQueryable" && m.GetParameters().Length == 1)
            .MakeGenericMethod(typeof(string));
        var asQueryableCall = Expression.Call(asQueryableMethod, efPropertyCall);

        // .Contains("searchValue")
        var containsMethod = typeof(Queryable)
            .GetMethods()
            .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(string));
        var containsCall = Expression.Call(containsMethod, asQueryableCall, Expression.Constant("searchValue"));

        var result = ArrayContainsPatternTransformer.Transform(containsCall);

        result.Should().BeOfType<FirestoreArrayContainsExpression>();
        var arrayContainsExpr = (FirestoreArrayContainsExpression)result;
        arrayContainsExpr.PropertyName.Should().Be("Tags");
    }

    #endregion

    #region Transform - ArrayContainsAny Pattern

    [Fact]
    public void Transform_WithAnyContainsPattern_ReturnsFirestoreArrayContainsAnyExpression()
    {
        // Simulates: e.Tags.Any(t => searchTags.Contains(t))
        var parameter = Expression.Parameter(typeof(TestEntity), "e");
        var tagsProperty = Expression.Property(parameter, nameof(TestEntity.Tags));

        // EF.Property<List<string>>(e, "Tags")
        var efPropertyMethod = typeof(Microsoft.EntityFrameworkCore.EF)
            .GetMethod("Property")!
            .MakeGenericMethod(typeof(List<string>));
        var efPropertyCall = Expression.Call(efPropertyMethod, parameter, Expression.Constant("Tags"));

        // .AsQueryable()
        var asQueryableMethod = typeof(Queryable)
            .GetMethods()
            .First(m => m.Name == "AsQueryable" && m.GetParameters().Length == 1)
            .MakeGenericMethod(typeof(string));
        var asQueryableCall = Expression.Call(asQueryableMethod, efPropertyCall);

        // Predicate: t => searchList.Contains(t)
        var searchList = new List<string> { "a", "b" };
        var searchListExpr = Expression.Constant(searchList);
        var tParam = Expression.Parameter(typeof(string), "t");
        var listContainsMethod = typeof(List<string>).GetMethod("Contains", new[] { typeof(string) })!;
        var listContainsCall = Expression.Call(searchListExpr, listContainsMethod, tParam);
        var predicate = Expression.Lambda<Func<string, bool>>(listContainsCall, tParam);

        // .Any(t => searchList.Contains(t))
        var anyMethod = typeof(Queryable)
            .GetMethods()
            .First(m => m.Name == "Any" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(string));
        var quotedPredicate = Expression.Quote(predicate);
        var anyCall = Expression.Call(anyMethod, asQueryableCall, quotedPredicate);

        var result = ArrayContainsPatternTransformer.Transform(anyCall);

        result.Should().BeOfType<FirestoreArrayContainsAnyExpression>();
        var arrayContainsAnyExpr = (FirestoreArrayContainsAnyExpression)result;
        arrayContainsAnyExpr.PropertyName.Should().Be("Tags");
    }

    #endregion

    #region Transform - Nested in Binary Expressions

    [Fact]
    public void Transform_WithArrayContainsInAndExpression_TransformsBothSides()
    {
        // Simulates: e.Name == "test" && e.Tags.Contains("value")
        var parameter = Expression.Parameter(typeof(TestEntity), "e");

        // Left side: e.Name == "test"
        var nameProperty = Expression.Property(parameter, nameof(TestEntity.Name));
        var leftComparison = Expression.Equal(nameProperty, Expression.Constant("test"));

        // Right side: Build Contains pattern
        var efPropertyMethod = typeof(Microsoft.EntityFrameworkCore.EF)
            .GetMethod("Property")!
            .MakeGenericMethod(typeof(List<string>));
        var efPropertyCall = Expression.Call(efPropertyMethod, parameter, Expression.Constant("Tags"));
        var asQueryableMethod = typeof(Queryable)
            .GetMethods()
            .First(m => m.Name == "AsQueryable" && m.GetParameters().Length == 1)
            .MakeGenericMethod(typeof(string));
        var asQueryableCall = Expression.Call(asQueryableMethod, efPropertyCall);
        var containsMethod = typeof(Queryable)
            .GetMethods()
            .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(string));
        var containsCall = Expression.Call(containsMethod, asQueryableCall, Expression.Constant("searchValue"));

        // AND expression
        var andExpression = Expression.AndAlso(leftComparison, containsCall);

        var result = ArrayContainsPatternTransformer.Transform(andExpression);

        result.Should().BeAssignableTo<BinaryExpression>();
        var binaryResult = (BinaryExpression)result;
        binaryResult.Left.Should().Be(leftComparison); // Unchanged
        binaryResult.Right.Should().BeOfType<FirestoreArrayContainsExpression>(); // Transformed
    }

    #endregion

    #region Test Entity

    private class TestEntity
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public List<string> Tags { get; set; } = new();
    }

    #endregion
}
