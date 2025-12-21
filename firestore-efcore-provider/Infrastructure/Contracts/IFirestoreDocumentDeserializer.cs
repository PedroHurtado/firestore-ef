using Google.Cloud.Firestore;
using System.Collections.Generic;

namespace Firestore.EntityFrameworkCore.Infrastructure
{
    /// <summary>
    /// Define el contrato para deserializar DocumentSnapshots de Firestore a entidades C#.
    /// Es el proceso inverso de IFirestoreDocumentSerializer.
    /// </summary>
    public interface IFirestoreDocumentDeserializer
    {
        /// <summary>
        /// Deserializa un DocumentSnapshot a una entidad del tipo especificado
        /// </summary>
        T DeserializeEntity<T>(DocumentSnapshot document) where T : class, new();

        /// <summary>
        /// Deserializa un DocumentSnapshot en una instancia de entidad existente.
        /// Útil para poblar proxies de lazy loading.
        /// </summary>
        T DeserializeIntoEntity<T>(DocumentSnapshot document, T entity) where T : class;

        /// <summary>
        /// Deserializa múltiples documentos
        /// </summary>
        List<T> DeserializeEntities<T>(IEnumerable<DocumentSnapshot> documents) where T : class, new();
    }
}
