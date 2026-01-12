using System.Linq.Expressions;
using System.Reflection;
using Fudie.Firestore.EntityFrameworkCore.Query.Ast;
using Fudie.Firestore.EntityFrameworkCore.Query.Translators;
using FluentAssertions;

namespace Fudie.Firestore.UnitTest.Query.Translators;

public class FirestoreOrderByTranslatorTests
{
    private readonly FirestoreOrderByTranslator _translator;

    public FirestoreOrderByTranslatorTests()
    {
        _translator = new FirestoreOrderByTranslator();
    }

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

    #region Simple Property Tests

    [Fact]
    public void Translate_SimpleProperty_Ascending_Returns_Correct_Clause()
    {
        // e => e.Name (ascending)
        Expression<Func<TestEntity, string>> keySelector = e => e.Name;

        var result = _translator.Translate(keySelector, ascending: true);

        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("Name");
        result.Descending.Should().BeFalse();
    }

    [Fact]
    public void Translate_SimpleProperty_Descending_Returns_Correct_Clause()
    {
        // e => e.Name (descending)
        Expression<Func<TestEntity, string>> keySelector = e => e.Name;

        var result = _translator.Translate(keySelector, ascending: false);

        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("Name");
        result.Descending.Should().BeTrue();
    }

    [Fact]
    public void Translate_IntProperty_Returns_Correct_Clause()
    {
        // e => e.Age
        Expression<Func<TestEntity, int>> keySelector = e => e.Age;

        var result = _translator.Translate(keySelector, ascending: true);

        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("Age");
        result.Descending.Should().BeFalse();
    }

    [Fact]
    public void Translate_DecimalProperty_Returns_Correct_Clause()
    {
        // e => e.Price
        Expression<Func<TestEntity, decimal>> keySelector = e => e.Price;

        var result = _translator.Translate(keySelector, ascending: false);

        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("Price");
        result.Descending.Should().BeTrue();
    }

    [Fact]
    public void Translate_DateTimeProperty_Returns_Correct_Clause()
    {
        // e => e.CreatedAt
        Expression<Func<TestEntity, DateTime>> keySelector = e => e.CreatedAt;

        var result = _translator.Translate(keySelector, ascending: true);

        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("CreatedAt");
        result.Descending.Should().BeFalse();
    }

    #endregion

    #region Nested Property Tests

    [Fact]
    public void Translate_NestedProperty_Returns_Dotted_Path()
    {
        // e => e.Address.City
        Expression<Func<TestEntity, string>> keySelector = e => e.Address.City;

        var result = _translator.Translate(keySelector, ascending: true);

        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("Address.City");
        result.Descending.Should().BeFalse();
    }

    [Fact]
    public void Translate_DeepNestedProperty_Returns_Full_Path()
    {
        // e => e.Address.Coordinates.Latitude
        Expression<Func<TestEntity, double>> keySelector = e => e.Address.Coordinates.Latitude;

        var result = _translator.Translate(keySelector, ascending: false);

        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("Address.Coordinates.Latitude");
        result.Descending.Should().BeTrue();
    }

    #endregion

    #region Convert Expression Tests (Value Types)

    [Fact]
    public void Translate_WithConvertExpression_Unwraps_And_Returns_Correct_Clause()
    {
        // When EF Core wraps value types in Convert expressions
        // Simulate: (object)e.Age
        var parameter = Expression.Parameter(typeof(TestEntity), "e");
        var property = Expression.Property(parameter, "Age");
        var convert = Expression.Convert(property, typeof(object));
        var lambda = Expression.Lambda(convert, parameter);

        var result = _translator.Translate(lambda, ascending: true);

        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("Age");
        result.Descending.Should().BeFalse();
    }

    #endregion

    #region Multiple OrderBy Scenarios

    [Fact]
    public void Translate_CanBeUsed_For_OrderBy_ThenByDescending_Scenario()
    {
        // Simula: .OrderBy(p => p.Categoria).ThenByDescending(p => p.Precio)
        // El Visitor llama al Translator dos veces, una por cada ordenamiento

        Expression<Func<TestEntity, string>> orderBySelector = p => p.Name;
        Expression<Func<TestEntity, decimal>> thenBySelector = p => p.Price;

        var orderByResult = _translator.Translate(orderBySelector, ascending: true);
        var thenByResult = _translator.Translate(thenBySelector, ascending: false);

        // OrderBy
        orderByResult.Should().NotBeNull();
        orderByResult!.PropertyName.Should().Be("Name");
        orderByResult.Descending.Should().BeFalse();

        // ThenByDescending
        thenByResult.Should().NotBeNull();
        thenByResult!.PropertyName.Should().Be("Price");
        thenByResult.Descending.Should().BeTrue();
    }

    [Fact]
    public void Translate_CanBeUsed_For_OrderByDescending_ThenBy_Scenario()
    {
        // Simula: .OrderByDescending(p => p.FechaCreacion).ThenBy(p => p.Nombre)

        Expression<Func<TestEntity, DateTime>> orderBySelector = p => p.CreatedAt;
        Expression<Func<TestEntity, string>> thenBySelector = p => p.Name;

        var orderByResult = _translator.Translate(orderBySelector, ascending: false);
        var thenByResult = _translator.Translate(thenBySelector, ascending: true);

        // OrderByDescending
        orderByResult.Should().NotBeNull();
        orderByResult!.PropertyName.Should().Be("CreatedAt");
        orderByResult.Descending.Should().BeTrue();

        // ThenBy
        thenByResult.Should().NotBeNull();
        thenByResult!.PropertyName.Should().Be("Name");
        thenByResult.Descending.Should().BeFalse();
    }

    #endregion

    #region Null/Invalid Cases

    [Fact]
    public void Translate_ConstantExpression_Returns_Null()
    {
        // e => "constant" (not a property)
        var parameter = Expression.Parameter(typeof(TestEntity), "e");
        var constant = Expression.Constant("test");
        var lambda = Expression.Lambda(constant, parameter);

        var result = _translator.Translate(lambda, ascending: true);

        result.Should().BeNull();
    }

    [Fact]
    public void Translate_MethodCallExpression_Returns_Null()
    {
        // e => e.Name.ToUpper() (method call, not supported)
        Expression<Func<TestEntity, string>> keySelector = e => e.Name.ToUpper();

        var result = _translator.Translate(keySelector, ascending: true);

        result.Should().BeNull();
    }

    #endregion
}
