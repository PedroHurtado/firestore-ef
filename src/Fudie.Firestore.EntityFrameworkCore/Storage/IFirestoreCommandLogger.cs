using System;

namespace Fudie.Firestore.EntityFrameworkCore.Storage;

/// <summary>
/// Contract for logging Firestore database commands (Insert, Update, Delete).
/// </summary>
public interface IFirestoreCommandLogger
{
    /// <summary>
    /// Logs a document insert operation.
    /// </summary>
    /// <param name="collectionPath">Full path to the collection/document</param>
    /// <param name="documentId">Document ID</param>
    /// <param name="entityType">CLR type of the entity</param>
    /// <param name="elapsed">Time elapsed for the operation</param>
    void LogInsert(string collectionPath, string documentId, Type entityType, TimeSpan elapsed);

    /// <summary>
    /// Logs a document update operation.
    /// </summary>
    /// <param name="collectionPath">Full path to the collection/document</param>
    /// <param name="documentId">Document ID</param>
    /// <param name="entityType">CLR type of the entity</param>
    /// <param name="elapsed">Time elapsed for the operation</param>
    void LogUpdate(string collectionPath, string documentId, Type entityType, TimeSpan elapsed);

    /// <summary>
    /// Logs a document delete operation.
    /// </summary>
    /// <param name="collectionPath">Full path to the collection/document</param>
    /// <param name="documentId">Document ID</param>
    /// <param name="entityType">CLR type of the entity</param>
    /// <param name="elapsed">Time elapsed for the operation</param>
    void LogDelete(string collectionPath, string documentId, Type entityType, TimeSpan elapsed);
}
