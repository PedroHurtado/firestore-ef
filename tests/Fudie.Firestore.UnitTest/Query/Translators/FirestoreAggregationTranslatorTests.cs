using System.Linq.Expressions;
using Firestore.EntityFrameworkCore.Query.Translators;
using FluentAssertions;

namespace Fudie.Firestore.UnitTest.Query.Translators;

/// <summary>
/// Tests for FirestoreAggregationTranslator base class.
/// All aggregation translators (Sum, Average, Min, Max) inherit from this.
/// </summary>
public class FirestoreAggregationTranslatorTests
{
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

    #region Sum Translator

    [Fact]
    public void SumTranslator_InheritsFromAggregationTranslator()
    {
        var translator = new FirestoreSumTranslator();
        translator.Should().BeAssignableTo<FirestoreAggregationTranslator>();
    }

    [Fact]
    public void SumTranslator_TranslatesIntProperty()
    {
        var translator = new FirestoreSumTranslator();
        Expression<Func<TestEntity, int>> selector = e => e.Quantity;

        var result = translator.Translate(selector);

        result.Should().Be("Quantity");
    }

    [Fact]
    public void SumTranslator_TranslatesNestedProperty()
    {
        var translator = new FirestoreSumTranslator();
        Expression<Func<TestEntity, decimal>> selector = e => e.Address.Rent;

        var result = translator.Translate(selector);

        result.Should().Be("Address.Rent");
    }

    [Fact]
    public void SumTranslator_NullSelector_ReturnsNull()
    {
        var translator = new FirestoreSumTranslator();

        var result = translator.Translate(null);

        result.Should().BeNull();
    }

    #endregion

    #region Average Translator

    [Fact]
    public void AverageTranslator_InheritsFromAggregationTranslator()
    {
        var translator = new FirestoreAverageTranslator();
        translator.Should().BeAssignableTo<FirestoreAggregationTranslator>();
    }

    [Fact]
    public void AverageTranslator_TranslatesDecimalProperty()
    {
        var translator = new FirestoreAverageTranslator();
        Expression<Func<TestEntity, decimal>> selector = e => e.Price;

        var result = translator.Translate(selector);

        result.Should().Be("Price");
    }

    [Fact]
    public void AverageTranslator_TranslatesNestedProperty()
    {
        var translator = new FirestoreAverageTranslator();
        Expression<Func<TestEntity, int>> selector = e => e.Address.Floor;

        var result = translator.Translate(selector);

        result.Should().Be("Address.Floor");
    }

    [Fact]
    public void AverageTranslator_NullSelector_ReturnsNull()
    {
        var translator = new FirestoreAverageTranslator();

        var result = translator.Translate(null);

        result.Should().BeNull();
    }

    #endregion

    #region Min Translator

    [Fact]
    public void MinTranslator_InheritsFromAggregationTranslator()
    {
        var translator = new FirestoreMinTranslator();
        translator.Should().BeAssignableTo<FirestoreAggregationTranslator>();
    }

    [Fact]
    public void MinTranslator_TranslatesDoubleProperty()
    {
        var translator = new FirestoreMinTranslator();
        Expression<Func<TestEntity, double>> selector = e => e.Weight;

        var result = translator.Translate(selector);

        result.Should().Be("Weight");
    }

    [Fact]
    public void MinTranslator_TranslatesNestedProperty()
    {
        var translator = new FirestoreMinTranslator();
        Expression<Func<TestEntity, decimal>> selector = e => e.Address.Rent;

        var result = translator.Translate(selector);

        result.Should().Be("Address.Rent");
    }

    [Fact]
    public void MinTranslator_NullSelector_ReturnsNull()
    {
        var translator = new FirestoreMinTranslator();

        var result = translator.Translate(null);

        result.Should().BeNull();
    }

    #endregion

    #region Max Translator

    [Fact]
    public void MaxTranslator_InheritsFromAggregationTranslator()
    {
        var translator = new FirestoreMaxTranslator();
        translator.Should().BeAssignableTo<FirestoreAggregationTranslator>();
    }

    [Fact]
    public void MaxTranslator_TranslatesLongProperty()
    {
        var translator = new FirestoreMaxTranslator();
        Expression<Func<TestEntity, long>> selector = e => e.Total;

        var result = translator.Translate(selector);

        result.Should().Be("Total");
    }

    [Fact]
    public void MaxTranslator_TranslatesNestedProperty()
    {
        var translator = new FirestoreMaxTranslator();
        Expression<Func<TestEntity, int>> selector = e => e.Address.Floor;

        var result = translator.Translate(selector);

        result.Should().Be("Address.Floor");
    }

    [Fact]
    public void MaxTranslator_NullSelector_ReturnsNull()
    {
        var translator = new FirestoreMaxTranslator();

        var result = translator.Translate(null);

        result.Should().BeNull();
    }

    #endregion

    #region Invalid Cases (shared behavior)

    [Fact]
    public void AggregationTranslator_MethodCallExpression_ReturnsNull()
    {
        var translator = new FirestoreSumTranslator();
        Expression<Func<TestEntity, int>> selector = e => e.Quantity + 1;

        var result = translator.Translate(selector);

        result.Should().BeNull();
    }

    [Fact]
    public void AggregationTranslator_ConstantExpression_ReturnsNull()
    {
        var translator = new FirestoreAverageTranslator();
        var parameter = Expression.Parameter(typeof(TestEntity), "e");
        var constant = Expression.Constant(100);
        var lambda = Expression.Lambda(constant, parameter);

        var result = translator.Translate(lambda);

        result.Should().BeNull();
    }

    [Fact]
    public void AggregationTranslator_WithConvertExpression_UnwrapsAndReturnsPropertyName()
    {
        var translator = new FirestoreMinTranslator();
        var parameter = Expression.Parameter(typeof(TestEntity), "e");
        var property = Expression.Property(parameter, "Quantity");
        var convert = Expression.Convert(property, typeof(object));
        var lambda = Expression.Lambda(convert, parameter);

        var result = translator.Translate(lambda);

        result.Should().Be("Quantity");
    }

    #endregion
}
