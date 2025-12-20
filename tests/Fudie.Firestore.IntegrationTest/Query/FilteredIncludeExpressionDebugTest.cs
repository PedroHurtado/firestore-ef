using Firestore.EntityFrameworkCore.Query;
using Firestore.EntityFrameworkCore.Query.Visitors;
using FluentAssertions;
using Fudie.Firestore.IntegrationTest.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;
using Xunit.Abstractions;

namespace Fudie.Firestore.IntegrationTest.Query;

/// <summary>
/// Debug test to explore EF Core Filtered Include expression structure
/// </summary>
[Collection(nameof(FirestoreTestCollection))]
public class FilteredIncludeExpressionDebugTest
{
    private readonly FirestoreTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public FilteredIncludeExpressionDebugTest(FirestoreTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public void Debug_FilteredInclude_ExpressionTree()
    {
        using var context = _fixture.CreateContext<TestDbContext>();

        // Create the expression tree for a filtered include query
        var query = context.Clientes
            .Include(c => c.Pedidos.Where(p => p.Estado == EstadoPedido.Confirmado))
                .ThenInclude(p => p.Lineas.Where(l => l.Cantidad >= 3));

        // Get the expression
        var expression = query.Expression;

        _output.WriteLine("=== Filtered Include Expression Tree ===");
        _output.WriteLine($"Expression Type: {expression.GetType().Name}");
        _output.WriteLine($"Expression: {expression}");
        _output.WriteLine("");

        // Walk the tree
        WalkExpression(expression, 0);
    }

    [Fact]
    public void Debug_FilteredIncludeExtractor_ShouldExtractFilters()
    {
        using var context = _fixture.CreateContext<TestDbContext>();

        // Create the expression tree for a filtered include query
        var query = context.Clientes
            .Include(c => c.Pedidos.Where(p => p.Estado == EstadoPedido.Confirmado))
                .ThenInclude(p => p.Lineas.Where(l => l.Cantidad >= 3));

        // Get the expression
        var expression = query.Expression;

        // Manually test the extraction logic
        var extractedFilters = new Dictionary<string, IncludeInfo>();

        // Walk the expression tree to find Include calls
        ExtractFiltersFromExpression(expression, extractedFilters);

        _output.WriteLine($"Extracted filters count: {extractedFilters.Count}");
        foreach (var kvp in extractedFilters)
        {
            _output.WriteLine($"  Navigation: {kvp.Key}");
            _output.WriteLine($"  HasFilter: {kvp.Value.FilterExpression != null}");
            if (kvp.Value.FilterExpression != null)
            {
                _output.WriteLine($"  Filter: {kvp.Value.FilterExpression}");
            }
        }

        // Assert we found both filters
        extractedFilters.Should().ContainKey("Pedidos");
        extractedFilters.Should().ContainKey("Lineas");
        extractedFilters["Pedidos"].FilterExpression.Should().NotBeNull();
        extractedFilters["Lineas"].FilterExpression.Should().NotBeNull();
    }

    private void ExtractFiltersFromExpression(Expression expr, Dictionary<string, IncludeInfo> filters)
    {
        if (expr is MethodCallExpression methodCall)
        {
            // Check if this is Include or ThenInclude
            if ((methodCall.Method.Name == "Include" || methodCall.Method.Name == "ThenInclude") &&
                methodCall.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions))
            {
                _output.WriteLine($"Found {methodCall.Method.Name} call");

                // Get the lambda (second argument)
                if (methodCall.Arguments.Count >= 2)
                {
                    var lambdaArg = methodCall.Arguments[1];
                    _output.WriteLine($"Lambda arg type: {lambdaArg.GetType().Name}");

                    LambdaExpression? lambda = null;
                    if (lambdaArg is UnaryExpression unary && unary.Operand is LambdaExpression l1)
                    {
                        lambda = l1;
                    }
                    else if (lambdaArg is LambdaExpression l2)
                    {
                        lambda = l2;
                    }

                    if (lambda != null)
                    {
                        _output.WriteLine($"Lambda body: {lambda.Body}");
                        _output.WriteLine($"Lambda body type: {lambda.Body.GetType().Name}");

                        // Check if body is a MethodCallExpression (e.g., Pedidos.Where(...))
                        if (lambda.Body is MethodCallExpression whereCall && whereCall.Method.Name == "Where")
                        {
                            _output.WriteLine($"Found Where call in lambda body");
                            _output.WriteLine($"Where method: {whereCall.Method.DeclaringType?.Name}.{whereCall.Method.Name}");
                            _output.WriteLine($"Where args count: {whereCall.Arguments.Count}");

                            // Get navigation name from first argument
                            if (whereCall.Arguments[0] is MemberExpression memberExpr)
                            {
                                var navigationName = memberExpr.Member.Name;
                                _output.WriteLine($"Navigation name: {navigationName}");

                                // Get filter predicate from second argument
                                if (whereCall.Arguments.Count >= 2)
                                {
                                    var predicateArg = whereCall.Arguments[1];
                                    _output.WriteLine($"Predicate arg type: {predicateArg.GetType().Name}");

                                    LambdaExpression? predicate = null;
                                    if (predicateArg is LambdaExpression pl)
                                    {
                                        predicate = pl;
                                    }

                                    if (predicate != null)
                                    {
                                        _output.WriteLine($"Predicate: {predicate}");
                                        var includeInfo = new IncludeInfo(navigationName)
                                        {
                                            FilterExpression = predicate
                                        };
                                        filters[navigationName] = includeInfo;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Recurse into arguments
            foreach (var arg in methodCall.Arguments)
            {
                ExtractFiltersFromExpression(arg, filters);
            }
        }
    }

    private void WalkExpression(Expression expr, int depth)
    {
        var indent = new string(' ', depth * 2);
        _output.WriteLine($"{indent}Node: {expr.GetType().Name}");
        _output.WriteLine($"{indent}  Type: {expr.Type}");
        _output.WriteLine($"{indent}  NodeType: {expr.NodeType}");

        switch (expr)
        {
            case MethodCallExpression methodCall:
                _output.WriteLine($"{indent}  Method: {methodCall.Method.DeclaringType?.Name}.{methodCall.Method.Name}");
                _output.WriteLine($"{indent}  Arguments ({methodCall.Arguments.Count}):");
                for (int i = 0; i < methodCall.Arguments.Count; i++)
                {
                    _output.WriteLine($"{indent}  Arg[{i}]:");
                    WalkExpression(methodCall.Arguments[i], depth + 2);
                }
                break;

            case UnaryExpression unary:
                _output.WriteLine($"{indent}  UnaryType: {unary.NodeType}");
                WalkExpression(unary.Operand, depth + 1);
                break;

            case LambdaExpression lambda:
                _output.WriteLine($"{indent}  Lambda Body Type: {lambda.Body.GetType().Name}");
                _output.WriteLine($"{indent}  Lambda Body: {lambda.Body}");
                WalkExpression(lambda.Body, depth + 1);
                break;

            case MemberExpression member:
                _output.WriteLine($"{indent}  Member: {member.Member.Name}");
                if (member.Expression != null)
                {
                    WalkExpression(member.Expression, depth + 1);
                }
                break;

            case BinaryExpression binary:
                _output.WriteLine($"{indent}  Operator: {binary.NodeType}");
                WalkExpression(binary.Left, depth + 1);
                WalkExpression(binary.Right, depth + 1);
                break;

            case ConstantExpression constant:
                _output.WriteLine($"{indent}  Value: {constant.Value}");
                break;

            case ParameterExpression param:
                _output.WriteLine($"{indent}  Parameter: {param.Name} ({param.Type})");
                break;
        }
    }
}
