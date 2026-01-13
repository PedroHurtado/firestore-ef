using System;
using System.Collections.Generic;

namespace Fudie.Firestore.EntityFrameworkCore.Storage;

/// <summary>
/// Contract for logging Firestore database commands (Insert, Update, Delete).
/// </summary>
public interface IFirestoreCommandLogger
{
    /// <summary>
    /// Logs a document insert operation.
    /// </summary>
    /// <param name="collectionPath">Relative path to the collection</param>
    /// <param name="documentId">Document ID</param>
    /// <param name="entityType">CLR type of the entity</param>
    /// <param name="elapsed">Time elapsed for the operation</param>
    /// <param name="data">Document data being inserted</param>
    void LogInsert(string collectionPath, string documentId, Type entityType, TimeSpan elapsed, Dictionary<string, object>? data = null);

    /// <summary>
    /// Logs a document update operation.
    /// </summary>
    /// <param name="collectionPath">Relative path to the collection</param>
    /// <param name="documentId">Document ID</param>
    /// <param name="entityType">CLR type of the entity</param>
    /// <param name="elapsed">Time elapsed for the operation</param>
    /// <param name="data">Document data being updated</param>
    void LogUpdate(string collectionPath, string documentId, Type entityType, TimeSpan elapsed, Dictionary<string, object>? data = null);

    /// <summary>
    /// Logs a document delete operation.
    /// </summary>
    /// <param name="collectionPath">Relative path to the collection</param>
    /// <param name="documentId">Document ID</param>
    /// <param name="entityType">CLR type of the entity</param>
    /// <param name="elapsed">Time elapsed for the operation</param>
    void LogDelete(string collectionPath, string documentId, Type entityType, TimeSpan elapsed);
}
