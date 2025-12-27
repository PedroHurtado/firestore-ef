using System.Linq.Expressions;
using Firestore.EntityFrameworkCore.Query.Translators;
using FluentAssertions;

namespace Fudie.Firestore.UnitTest.Query.Translators;

/// <summary>
/// Tests for FirestoreLimitTranslator.
/// Tests the Translate method that extracts integer constants from expressions.
/// </summary>
public class FirestoreLimitTranslatorTests
{
    private readonly FirestoreLimitTranslator _translator;

    public FirestoreLimitTranslatorTests()
    {
        _translator = new FirestoreLimitTranslator();
    }

    #region Constant Expression Tests

    [Fact]
    public void Translate_ConstantInt_ReturnsValue()
    {
        var countExpr = Expression.Constant(10);

        var result = _translator.Translate(countExpr);

        result.Should().Be(10);
    }

    [Fact]
    public void Translate_ConstantInt_DifferentValues()
    {
        var result1 = _translator.Translate(Expression.Constant(5));
        var result2 = _translator.Translate(Expression.Constant(100));
        var result3 = _translator.Translate(Expression.Constant(1));

        result1.Should().Be(5);
        result2.Should().Be(100);
        result3.Should().Be(1);
    }

    [Fact]
    public void Translate_ConstantZero_ReturnsZero()
    {
        var countExpr = Expression.Constant(0);

        var result = _translator.Translate(countExpr);

        result.Should().Be(0);
    }

    #endregion

    #region Closure/Captured Variable Tests

    [Fact]
    public void Translate_CapturedVariable_ReturnsValue()
    {
        int capturedValue = 15;
        Expression<Func<int>> lambda = () => capturedValue;
        var countExpr = lambda.Body;

        var result = _translator.Translate(countExpr);

        result.Should().Be(15);
    }

    [Fact]
    public void Translate_CapturedVariable_DifferentValue_ReturnsCorrectValue()
    {
        int pageSize = 25;
        Expression<Func<int>> lambda = () => pageSize;
        var countExpr = lambda.Body;

        var result = _translator.Translate(countExpr);

        result.Should().Be(25);
    }

    #endregion

    #region Convert Expression Tests

    [Fact]
    public void Translate_ConvertFromLong_ReturnsValue()
    {
        var longExpr = Expression.Constant(30L);
        var convertExpr = Expression.Convert(longExpr, typeof(int));

        var result = _translator.Translate(convertExpr);

        result.Should().Be(30);
    }

    [Fact]
    public void Translate_ConvertFromShort_ReturnsValue()
    {
        short shortValue = 20;
        var shortExpr = Expression.Constant(shortValue);
        var convertExpr = Expression.Convert(shortExpr, typeof(int));

        var result = _translator.Translate(convertExpr);

        result.Should().Be(20);
    }

    [Fact]
    public void Translate_ConvertFromByte_ReturnsValue()
    {
        byte byteValue = 50;
        var byteExpr = Expression.Constant(byteValue);
        var convertExpr = Expression.Convert(byteExpr, typeof(int));

        var result = _translator.Translate(convertExpr);

        result.Should().Be(50);
    }

    #endregion

    #region Arithmetic Expression Tests

    [Fact]
    public void Translate_AddExpression_ReturnsComputedValue()
    {
        // 5 + 5 = 10
        var addExpr = Expression.Add(Expression.Constant(5), Expression.Constant(5));

        var result = _translator.Translate(addExpr);

        result.Should().Be(10);
    }

    [Fact]
    public void Translate_MultiplyExpression_ReturnsComputedValue()
    {
        // 4 * 3 = 12
        var multiplyExpr = Expression.Multiply(Expression.Constant(4), Expression.Constant(3));

        var result = _translator.Translate(multiplyExpr);

        result.Should().Be(12);
    }

    [Fact]
    public void Translate_SubtractExpression_ReturnsComputedValue()
    {
        // 20 - 5 = 15
        var subtractExpr = Expression.Subtract(Expression.Constant(20), Expression.Constant(5));

        var result = _translator.Translate(subtractExpr);

        result.Should().Be(15);
    }

    #endregion

    #region Non-Evaluable Expression Tests

    [Fact]
    public void Translate_ParameterExpression_ReturnsNull()
    {
        // A parameter that cannot be evaluated at compile time
        var paramExpr = Expression.Parameter(typeof(int), "count");

        var result = _translator.Translate(paramExpr);

        result.Should().BeNull();
    }

    [Fact]
    public void Translate_UnboundMemberAccess_ReturnsNull()
    {
        // Accessing a member without a bound instance
        var paramExpr = Expression.Parameter(typeof(TestClass), "x");
        var memberExpr = Expression.Property(paramExpr, "Value");

        var result = _translator.Translate(memberExpr);

        result.Should().BeNull();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Translate_NegativeValue_ReturnsNegative()
    {
        var countExpr = Expression.Constant(-5);

        var result = _translator.Translate(countExpr);

        result.Should().Be(-5);
    }

    [Fact]
    public void Translate_MaxIntValue_ReturnsMaxInt()
    {
        var countExpr = Expression.Constant(int.MaxValue);

        var result = _translator.Translate(countExpr);

        result.Should().Be(int.MaxValue);
    }

    #endregion

    private class TestClass
    {
        public int Value { get; set; }
    }
}
