using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Delegate that represents the next handler in the pipeline.
/// Similar to ASP.NET Core's RequestDelegate pattern.
/// </summary>
public delegate Task<PipelineResult> PipelineDelegate(
    PipelineContext context,
    CancellationToken cancellationToken);
