using System;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Exception thrown when document deserialization fails.
/// </summary>
public class FirestoreDeserializationException : FirestorePipelineException
{
    /// <summary>
    /// The ID of the document that failed to deserialize.
    /// </summary>
    public string DocumentId { get; }

    /// <summary>
    /// The target type that deserialization was attempting to create.
    /// </summary>
    public Type TargetType { get; }

    /// <summary>
    /// Creates a new deserialization exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="context">The pipeline context.</param>
    /// <param name="documentId">The document ID.</param>
    /// <param name="targetType">The target deserialization type.</param>
    /// <param name="innerException">The inner exception, if any.</param>
    public FirestoreDeserializationException(
        string message,
        PipelineContext context,
        string documentId,
        Type targetType,
        Exception? innerException = null)
        : base(message, context, innerException)
    {
        DocumentId = documentId;
        TargetType = targetType;
    }
}
