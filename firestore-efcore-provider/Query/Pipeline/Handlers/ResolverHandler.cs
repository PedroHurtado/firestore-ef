using Firestore.EntityFrameworkCore.Query.Resolved;
using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Handler that resolves the AST into a ResolvedFirestoreQuery.
/// This transforms the expression tree into concrete values ready for execution.
/// </summary>
public class ResolverHandler : IQueryPipelineHandler
{
    private readonly IFirestoreAstResolver _resolver;

    /// <summary>
    /// Creates a new resolver handler.
    /// </summary>
    /// <param name="resolver">The AST resolver.</param>
    public ResolverHandler(IFirestoreAstResolver resolver)
    {
        _resolver = resolver;
    }

    /// <inheritdoc />
    public async Task<PipelineResult> HandleAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken cancellationToken)
    {
        var resolved = _resolver.Resolve(context.Ast);

        var newContext = context with { ResolvedQuery = resolved };

        return await next(newContext, cancellationToken);
    }
}
