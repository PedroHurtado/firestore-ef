using System;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Base exception for pipeline errors.
/// Contains the pipeline context for debugging and logging purposes.
/// </summary>
public abstract class FirestorePipelineException : Exception
{
    /// <summary>
    /// The pipeline context at the time of the error.
    /// </summary>
    public PipelineContext Context { get; }

    /// <summary>
    /// Creates a new pipeline exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="context">The pipeline context.</param>
    /// <param name="innerException">The inner exception, if any.</param>
    protected FirestorePipelineException(
        string message,
        PipelineContext context,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Context = context;
    }
}
