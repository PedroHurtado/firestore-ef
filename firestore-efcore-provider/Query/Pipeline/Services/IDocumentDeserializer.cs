using Google.Cloud.Firestore;
using System;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Deserializes Firestore DocumentSnapshots into CLR entity instances.
/// </summary>
public interface IDocumentDeserializer
{
    /// <summary>
    /// Deserializes a document snapshot into a new entity of the specified type.
    /// </summary>
    /// <param name="document">The Firestore document snapshot.</param>
    /// <param name="entityType">The target entity type.</param>
    /// <returns>The deserialized entity instance.</returns>
    object Deserialize(DocumentSnapshot document, Type entityType);

    /// <summary>
    /// Deserializes a document snapshot into an existing entity instance.
    /// Used for populating proxy instances created by IProxyFactory.
    /// </summary>
    /// <param name="document">The Firestore document snapshot.</param>
    /// <param name="entity">The existing entity instance to populate.</param>
    void DeserializeInto(DocumentSnapshot document, object entity);
}
