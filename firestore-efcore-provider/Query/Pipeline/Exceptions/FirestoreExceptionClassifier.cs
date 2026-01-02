using Grpc.Core;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Classifies Firestore/gRPC exceptions as transient or permanent.
/// Used by ErrorHandlingHandler to determine if a retry should be attempted.
/// </summary>
public static class FirestoreExceptionClassifier
{
    /// <summary>
    /// Determines if an RpcException is transient and can be retried.
    /// </summary>
    /// <param name="exception">The RPC exception to classify.</param>
    /// <returns>True if the error is transient, false otherwise.</returns>
    public static bool IsTransient(RpcException exception)
    {
        return exception.StatusCode switch
        {
            StatusCode.Unavailable => true,        // Service temporarily unavailable
            StatusCode.DeadlineExceeded => true,   // Request timeout
            StatusCode.ResourceExhausted => true,  // Rate limiting
            StatusCode.Aborted => true,            // Transaction conflict
            _ => false
        };
    }
}
