using Firestore.EntityFrameworkCore.Infrastructure;

namespace Fudie.Firestore.UnitTest.Query;

public class FirestoreWhereTranslatorTest
{
    private readonly FirestoreWhereTranslator _translator;

    public FirestoreWhereTranslatorTest()
    {
        _translator = new FirestoreWhereTranslator();
    }

    private class TestEntity
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
        public int Age { get; set; }
        public decimal Price { get; set; }
        public bool IsActive { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    #region Binary Expression Tests - Equal

    [Fact]
    public void Translate_Equal_Returns_EqualTo_Operator()
    {
        // p => p.Name == "Test"
        Expression<Func<TestEntity, bool>> expr = p => p.Name == "Test";
        var body = expr.Body;

        var result = _translator.Translate(body);

        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("Name");
        result.Operator.Should().Be(FirestoreOperator.EqualTo);
    }

    [Fact]
    public void Translate_Equal_With_Int_Returns_Correct_Clause()
    {
        // p => p.Age == 25
        Expression<Func<TestEntity, bool>> expr = p => p.Age == 25;
        var body = expr.Body;

        var result = _translator.Translate(body);

        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("Age");
        result.Operator.Should().Be(FirestoreOperator.EqualTo);
    }

    [Fact]
    public void Translate_Equal_With_Bool_Returns_Correct_Clause()
    {
        // p => p.IsActive == true
        Expression<Func<TestEntity, bool>> expr = p => p.IsActive == true;
        var body = expr.Body;

        var result = _translator.Translate(body);

        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("IsActive");
        result.Operator.Should().Be(FirestoreOperator.EqualTo);
    }

    #endregion

    #region Binary Expression Tests - NotEqual

    [Fact]
    public void Translate_NotEqual_Returns_NotEqualTo_Operator()
    {
        // p => p.Name != "Test"
        Expression<Func<TestEntity, bool>> expr = p => p.Name != "Test";
        var body = expr.Body;

        var result = _translator.Translate(body);

        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("Name");
        result.Operator.Should().Be(FirestoreOperator.NotEqualTo);
    }

    #endregion

    #region Binary Expression Tests - Comparison Operators

    [Fact]
    public void Translate_LessThan_Returns_LessThan_Operator()
    {
        // p => p.Age < 30
        Expression<Func<TestEntity, bool>> expr = p => p.Age < 30;
        var body = expr.Body;

        var result = _translator.Translate(body);

        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("Age");
        result.Operator.Should().Be(FirestoreOperator.LessThan);
    }

    [Fact]
    public void Translate_LessThanOrEqual_Returns_LessThanOrEqualTo_Operator()
    {
        // p => p.Age <= 30
        Expression<Func<TestEntity, bool>> expr = p => p.Age <= 30;
        var body = expr.Body;

        var result = _translator.Translate(body);

        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("Age");
        result.Operator.Should().Be(FirestoreOperator.LessThanOrEqualTo);
    }

    [Fact]
    public void Translate_GreaterThan_Returns_GreaterThan_Operator()
    {
        // p => p.Price > 100
        Expression<Func<TestEntity, bool>> expr = p => p.Price > 100;
        var body = expr.Body;

        var result = _translator.Translate(body);

        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("Price");
        result.Operator.Should().Be(FirestoreOperator.GreaterThan);
    }

    [Fact]
    public void Translate_GreaterThanOrEqual_Returns_GreaterThanOrEqualTo_Operator()
    {
        // p => p.Price >= 100
        Expression<Func<TestEntity, bool>> expr = p => p.Price >= 100;
        var body = expr.Body;

        var result = _translator.Translate(body);

        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("Price");
        result.Operator.Should().Be(FirestoreOperator.GreaterThanOrEqualTo);
    }

    #endregion

    #region Reversed Comparison Tests

    [Fact]
    public void Translate_Reversed_Equal_Extracts_PropertyName_From_Right()
    {
        // p => "Test" == p.Name (value on left, property on right)
        var parameter = Expression.Parameter(typeof(TestEntity), "p");
        var property = Expression.Property(parameter, "Name");
        var constant = Expression.Constant("Test");
        var binary = Expression.Equal(constant, property);

        var result = _translator.Translate(binary);

        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("Name");
        result.Operator.Should().Be(FirestoreOperator.EqualTo);
    }

    #endregion

    #region Method Call Tests - Contains

    [Fact]
    public void Translate_List_Contains_Returns_In_Operator()
    {
        // ids.Contains(p.Id) where ids is a list
        var ids = new List<string> { "1", "2", "3" };
        var parameter = Expression.Parameter(typeof(TestEntity), "p");
        var property = Expression.Property(parameter, "Id");
        var listConstant = Expression.Constant(ids);
        var containsMethod = typeof(List<string>).GetMethod("Contains", new[] { typeof(string) })!;
        var methodCall = Expression.Call(listConstant, containsMethod, property);

        var result = _translator.Translate(methodCall);

        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("Id");
        result.Operator.Should().Be(FirestoreOperator.In);
    }

    [Fact]
    public void Translate_Property_Contains_Returns_ArrayContains_Operator()
    {
        // p => p.Tags.Contains("tag1")
        var parameter = Expression.Parameter(typeof(TestEntity), "p");
        var property = Expression.Property(parameter, "Tags");
        var constant = Expression.Constant("tag1");
        var containsMethod = typeof(List<string>).GetMethod("Contains", new[] { typeof(string) })!;
        var methodCall = Expression.Call(property, containsMethod, constant);

        var result = _translator.Translate(methodCall);

        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("Tags");
        result.Operator.Should().Be(FirestoreOperator.ArrayContains);
    }

    #endregion

    #region Unsupported Expression Tests

    [Fact]
    public void Translate_Returns_Null_For_Unsupported_Expression()
    {
        // Unsupported: unary expression
        var constant = Expression.Constant(true);
        var unary = Expression.Not(constant);

        var result = _translator.Translate(unary);

        result.Should().BeNull();
    }

    [Fact]
    public void Translate_Returns_Null_For_Unsupported_Binary_Operator()
    {
        // Unsupported: modulo operator
        var left = Expression.Constant(10);
        var right = Expression.Constant(3);
        var modulo = Expression.Modulo(left, right);

        var result = _translator.Translate(modulo);

        result.Should().BeNull();
    }

    #endregion

    #region Value Expression Tests

    [Fact]
    public void Translate_Captures_Constant_Value_Expression()
    {
        // p => p.Name == "Test"
        Expression<Func<TestEntity, bool>> expr = p => p.Name == "Test";
        var body = expr.Body;

        var result = _translator.Translate(body);

        result.Should().NotBeNull();
        result!.ValueExpression.Should().BeOfType<ConstantExpression>();
        ((ConstantExpression)result.ValueExpression).Value.Should().Be("Test");
    }

    [Fact]
    public void Translate_Captures_Numeric_Value_Expression()
    {
        // p => p.Age > 25
        Expression<Func<TestEntity, bool>> expr = p => p.Age > 25;
        var body = expr.Body;

        var result = _translator.Translate(body);

        result.Should().NotBeNull();
        result!.ValueExpression.Should().BeOfType<ConstantExpression>();
        ((ConstantExpression)result.ValueExpression).Value.Should().Be(25);
    }

    #endregion
}
