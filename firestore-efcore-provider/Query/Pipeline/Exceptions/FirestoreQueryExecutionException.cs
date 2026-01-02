using System;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Exception thrown when query execution fails.
/// Includes information about whether the error is transient (retriable).
/// </summary>
public class FirestoreQueryExecutionException : FirestorePipelineException
{
    /// <summary>
    /// The collection being queried when the error occurred.
    /// </summary>
    public string Collection { get; }

    /// <summary>
    /// Indicates if this error is transient and can be retried.
    /// </summary>
    public bool IsTransient { get; }

    /// <summary>
    /// Creates a new query execution exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="context">The pipeline context.</param>
    /// <param name="collection">The collection being queried.</param>
    /// <param name="isTransient">Whether the error is transient.</param>
    /// <param name="innerException">The inner exception, if any.</param>
    public FirestoreQueryExecutionException(
        string message,
        PipelineContext context,
        string collection,
        bool isTransient = false,
        Exception? innerException = null)
        : base(message, context, innerException)
    {
        Collection = collection;
        IsTransient = isTransient;
    }
}
