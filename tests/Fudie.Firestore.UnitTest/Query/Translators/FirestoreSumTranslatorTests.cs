using System.Linq.Expressions;
using Firestore.EntityFrameworkCore.Query.Translators;
using FluentAssertions;

namespace Fudie.Firestore.UnitTest.Query.Translators;

/// <summary>
/// Tests for FirestoreSumTranslator.
/// Translates Sum selectors to property names for Firestore aggregation.
/// </summary>
public class FirestoreSumTranslatorTests
{
    private readonly FirestoreSumTranslator _translator;

    public FirestoreSumTranslatorTests()
    {
        _translator = new FirestoreSumTranslator();
    }

    private class TestEntity
    {
        public string Id { get; set; } = default!;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public double Weight { get; set; }
        public long Total { get; set; }
        public Address Address { get; set; } = default!;
    }

    private class Address
    {
        public int Floor { get; set; }
        public decimal Rent { get; set; }
    }

    #region Translate - Valid Selectors

    [Fact]
    public void Translate_IntProperty_ReturnsPropertyName()
    {
        Expression<Func<TestEntity, int>> selector = e => e.Quantity;

        var result = _translator.Translate(selector);

        result.Should().Be("Quantity");
    }

    [Fact]
    public void Translate_DecimalProperty_ReturnsPropertyName()
    {
        Expression<Func<TestEntity, decimal>> selector = e => e.Price;

        var result = _translator.Translate(selector);

        result.Should().Be("Price");
    }

    [Fact]
    public void Translate_DoubleProperty_ReturnsPropertyName()
    {
        Expression<Func<TestEntity, double>> selector = e => e.Weight;

        var result = _translator.Translate(selector);

        result.Should().Be("Weight");
    }

    [Fact]
    public void Translate_LongProperty_ReturnsPropertyName()
    {
        Expression<Func<TestEntity, long>> selector = e => e.Total;

        var result = _translator.Translate(selector);

        result.Should().Be("Total");
    }

    #endregion

    #region Translate - Nested Properties

    [Fact]
    public void Translate_NestedIntProperty_ReturnsDottedPath()
    {
        Expression<Func<TestEntity, int>> selector = e => e.Address.Floor;

        var result = _translator.Translate(selector);

        result.Should().Be("Address.Floor");
    }

    [Fact]
    public void Translate_NestedDecimalProperty_ReturnsDottedPath()
    {
        Expression<Func<TestEntity, decimal>> selector = e => e.Address.Rent;

        var result = _translator.Translate(selector);

        result.Should().Be("Address.Rent");
    }

    #endregion

    #region Translate - Convert Expressions

    [Fact]
    public void Translate_WithConvertExpression_UnwrapsAndReturnsPropertyName()
    {
        // Simulate: (object)e.Quantity
        var parameter = Expression.Parameter(typeof(TestEntity), "e");
        var property = Expression.Property(parameter, "Quantity");
        var convert = Expression.Convert(property, typeof(object));
        var lambda = Expression.Lambda(convert, parameter);

        var result = _translator.Translate(lambda);

        result.Should().Be("Quantity");
    }

    #endregion

    #region Translate - Invalid Cases

    [Fact]
    public void Translate_NullSelector_ReturnsNull()
    {
        var result = _translator.Translate(null);

        result.Should().BeNull();
    }

    [Fact]
    public void Translate_ConstantExpression_ReturnsNull()
    {
        var parameter = Expression.Parameter(typeof(TestEntity), "e");
        var constant = Expression.Constant(100);
        var lambda = Expression.Lambda(constant, parameter);

        var result = _translator.Translate(lambda);

        result.Should().BeNull();
    }

    [Fact]
    public void Translate_MethodCallExpression_ReturnsNull()
    {
        Expression<Func<TestEntity, int>> selector = e => e.Quantity + 1;

        var result = _translator.Translate(selector);

        result.Should().BeNull();
    }

    #endregion
}
