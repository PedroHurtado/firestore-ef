using System.Linq.Expressions;
using Firestore.EntityFrameworkCore.Query.Translators;
using FluentAssertions;

namespace Fudie.Firestore.UnitTest.Query.Translator;

/// <summary>
/// Tests for FirestoreSkipTranslator.
/// FirestoreSkipTranslator inherits from FirestoreLimitTranslator, reusing the same
/// ExtractIntConstant logic for evaluating Skip expressions.
/// </summary>
public class FirestoreSkipTranslatorTests
{
    private readonly FirestoreSkipTranslator _translator = new();

    #region Constant Expressions

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void Translate_WithConstantExpression_ReturnsValue(int value)
    {
        var expression = Expression.Constant(value);

        var result = _translator.Translate(expression);

        result.Should().Be(value);
    }

    #endregion

    #region Closure/Captured Variables

    [Fact]
    public void Translate_WithCapturedVariable_ReturnsValue()
    {
        int capturedValue = 25;
        Expression<Func<int>> lambda = () => capturedValue;

        var result = _translator.Translate(lambda.Body);

        result.Should().Be(25);
    }

    [Fact]
    public void Translate_WithComplexClosure_ReturnsComputedValue()
    {
        var obj = new { PageSize = 10, PageNumber = 3 };
        Expression<Func<int>> lambda = () => obj.PageSize * obj.PageNumber;

        var result = _translator.Translate(lambda.Body);

        result.Should().Be(30);
    }

    #endregion

    #region Type Conversion

    public static IEnumerable<object[]> ConversionTestData =>
        new List<object[]>
        {
            new object[] { Expression.Convert(Expression.Constant(50L), typeof(int)), 50 },
            new object[] { Expression.Convert(Expression.Constant((short)15), typeof(int)), 15 },
            new object[] { Expression.Convert(Expression.Constant((byte)5), typeof(int)), 5 },
        };

    [Theory]
    [MemberData(nameof(ConversionTestData))]
    public void Translate_WithTypeConversion_ReturnsValue(Expression expression, int expected)
    {
        var result = _translator.Translate(expression);

        result.Should().Be(expected);
    }

    #endregion

    #region Arithmetic Expressions

    public static IEnumerable<object[]> ArithmeticTestData =>
        new List<object[]>
        {
            new object[] { Expression.Add(Expression.Constant(5), Expression.Constant(5)), 10 },
            new object[] { Expression.Multiply(Expression.Constant(5), Expression.Constant(4)), 20 },
            new object[] { Expression.Subtract(Expression.Constant(100), Expression.Constant(25)), 75 },
        };

    [Theory]
    [MemberData(nameof(ArithmeticTestData))]
    public void Translate_WithArithmeticExpression_ReturnsComputedValue(Expression expression, int expected)
    {
        var result = _translator.Translate(expression);

        result.Should().Be(expected);
    }

    #endregion

    #region Non-Evaluable Expressions

    [Fact]
    public void Translate_WithParameterExpression_ReturnsNull()
    {
        var paramExpr = Expression.Parameter(typeof(int), "skip");

        var result = _translator.Translate(paramExpr);

        result.Should().BeNull();
    }

    #endregion
}
