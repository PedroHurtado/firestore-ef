using Google.Cloud.Firestore;
using System;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Deserializes Firestore DocumentSnapshots into CLR entity instances.
/// </summary>
public interface IDocumentDeserializer
{
    /// <summary>
    /// Deserializes a document snapshot into an entity of the specified type.
    /// </summary>
    /// <param name="document">The Firestore document snapshot.</param>
    /// <param name="entityType">The target entity type.</param>
    /// <returns>The deserialized entity instance.</returns>
    object Deserialize(DocumentSnapshot document, Type entityType);
}
