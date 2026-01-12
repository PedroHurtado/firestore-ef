using System;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Configuration options for error handling in the query pipeline.
/// </summary>
public class FirestoreErrorHandlingOptions
{
    /// <summary>
    /// Maximum number of retry attempts for transient errors.
    /// Default is 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial delay before the first retry.
    /// Subsequent retries use exponential backoff.
    /// Default is 100ms.
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Calculates the delay for a specific retry attempt using exponential backoff.
    /// </summary>
    /// <param name="attempt">The retry attempt number (1-based).</param>
    /// <returns>The delay to wait before retrying.</returns>
    public TimeSpan GetDelay(int attempt)
    {
        // Exponential backoff: delay * 2^(attempt-1)
        var multiplier = Math.Pow(2, attempt - 1);
        return TimeSpan.FromMilliseconds(InitialDelay.TotalMilliseconds * multiplier);
    }
}
