using System.Linq.Expressions;
using Fudie.Firestore.EntityFrameworkCore.Query.Translators;
using FluentAssertions;

namespace Fudie.Firestore.UnitTest.Query.Translators;

/// <summary>
/// Tests for PropertyPathExtractor helper class.
/// Extracts property paths from expressions for OrderBy, Aggregations, etc.
/// </summary>
public class PropertyPathExtractorTests
{
    private class TestEntity
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public int Age { get; set; }
        public decimal Price { get; set; }
        public DateTime CreatedAt { get; set; }
        public Address Address { get; set; } = default!;
    }

    private class Address
    {
        public string City { get; set; } = default!;
        public string Street { get; set; } = default!;
        public Coordinates Coordinates { get; set; } = default!;
    }

    private class Coordinates
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    #region ExtractFromLambda - Simple Properties

    [Fact]
    public void ExtractFromLambda_SimpleStringProperty_ReturnsPropertyName()
    {
        Expression<Func<TestEntity, string>> selector = e => e.Name;

        var result = PropertyPathExtractor.ExtractFromLambda(selector);

        result.Should().Be("Name");
    }

    [Fact]
    public void ExtractFromLambda_SimpleIntProperty_ReturnsPropertyName()
    {
        Expression<Func<TestEntity, int>> selector = e => e.Age;

        var result = PropertyPathExtractor.ExtractFromLambda(selector);

        result.Should().Be("Age");
    }

    [Fact]
    public void ExtractFromLambda_SimpleDecimalProperty_ReturnsPropertyName()
    {
        Expression<Func<TestEntity, decimal>> selector = e => e.Price;

        var result = PropertyPathExtractor.ExtractFromLambda(selector);

        result.Should().Be("Price");
    }

    [Fact]
    public void ExtractFromLambda_SimpleDateTimeProperty_ReturnsPropertyName()
    {
        Expression<Func<TestEntity, DateTime>> selector = e => e.CreatedAt;

        var result = PropertyPathExtractor.ExtractFromLambda(selector);

        result.Should().Be("CreatedAt");
    }

    #endregion

    #region ExtractFromLambda - Nested Properties

    [Fact]
    public void ExtractFromLambda_NestedProperty_ReturnsDottedPath()
    {
        Expression<Func<TestEntity, string>> selector = e => e.Address.City;

        var result = PropertyPathExtractor.ExtractFromLambda(selector);

        result.Should().Be("Address.City");
    }

    [Fact]
    public void ExtractFromLambda_DeepNestedProperty_ReturnsFullPath()
    {
        Expression<Func<TestEntity, double>> selector = e => e.Address.Coordinates.Latitude;

        var result = PropertyPathExtractor.ExtractFromLambda(selector);

        result.Should().Be("Address.Coordinates.Latitude");
    }

    #endregion

    #region ExtractFromLambda - Convert Expressions (Value Types)

    [Fact]
    public void ExtractFromLambda_WithConvertExpression_UnwrapsAndReturnsPropertyName()
    {
        // Simulate: (object)e.Age - common when EF Core wraps value types
        var parameter = Expression.Parameter(typeof(TestEntity), "e");
        var property = Expression.Property(parameter, "Age");
        var convert = Expression.Convert(property, typeof(object));
        var lambda = Expression.Lambda(convert, parameter);

        var result = PropertyPathExtractor.ExtractFromLambda(lambda);

        result.Should().Be("Age");
    }

    [Fact]
    public void ExtractFromLambda_WithConvertCheckedExpression_UnwrapsAndReturnsPropertyName()
    {
        var parameter = Expression.Parameter(typeof(TestEntity), "e");
        var property = Expression.Property(parameter, "Price");
        var convert = Expression.ConvertChecked(property, typeof(object));
        var lambda = Expression.Lambda(convert, parameter);

        var result = PropertyPathExtractor.ExtractFromLambda(lambda);

        result.Should().Be("Price");
    }

    #endregion

    #region ExtractFromLambda - Invalid Cases

    [Fact]
    public void ExtractFromLambda_ConstantExpression_ReturnsNull()
    {
        var parameter = Expression.Parameter(typeof(TestEntity), "e");
        var constant = Expression.Constant("test");
        var lambda = Expression.Lambda(constant, parameter);

        var result = PropertyPathExtractor.ExtractFromLambda(lambda);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractFromLambda_MethodCallExpression_ReturnsNull()
    {
        Expression<Func<TestEntity, string>> selector = e => e.Name.ToUpper();

        var result = PropertyPathExtractor.ExtractFromLambda(selector);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractFromLambda_BinaryExpression_ReturnsNull()
    {
        Expression<Func<TestEntity, int>> selector = e => e.Age + 1;

        var result = PropertyPathExtractor.ExtractFromLambda(selector);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractFromLambda_NullLambda_ReturnsNull()
    {
        var result = PropertyPathExtractor.ExtractFromLambda(null!);

        result.Should().BeNull();
    }

    #endregion

    #region ExtractFromMemberExpression - Direct Usage

    [Fact]
    public void ExtractFromMemberExpression_SimpleMember_ReturnsPropertyName()
    {
        var parameter = Expression.Parameter(typeof(TestEntity), "e");
        var memberExpr = Expression.Property(parameter, "Name");

        var result = PropertyPathExtractor.ExtractFromMemberExpression(memberExpr);

        result.Should().Be("Name");
    }

    [Fact]
    public void ExtractFromMemberExpression_NestedMember_ReturnsDottedPath()
    {
        var parameter = Expression.Parameter(typeof(TestEntity), "e");
        var address = Expression.Property(parameter, "Address");
        var city = Expression.Property(address, "City");

        var result = PropertyPathExtractor.ExtractFromMemberExpression(city);

        result.Should().Be("Address.City");
    }

    [Fact]
    public void ExtractFromMemberExpression_Null_ReturnsNull()
    {
        var result = PropertyPathExtractor.ExtractFromMemberExpression(null!);

        result.Should().BeNull();
    }

    #endregion
}