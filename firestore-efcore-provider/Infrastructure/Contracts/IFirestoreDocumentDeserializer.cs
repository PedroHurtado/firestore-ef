using Google.Cloud.Firestore;
using System.Collections.Generic;

namespace Firestore.EntityFrameworkCore.Infrastructure
{
    /// <summary>
    /// Define el contrato para deserializar DocumentSnapshots de Firestore a entidades C#.
    /// Es el proceso inverso de IFirestoreDocumentSerializer.
    /// Soporta entidades con constructores sin parámetros, con parámetros, y records.
    /// </summary>
    public interface IFirestoreDocumentDeserializer
    {
        /// <summary>
        /// Deserializa un DocumentSnapshot a una entidad del tipo especificado.
        /// Soporta:
        /// - Constructor sin parámetros (new() + property setters)
        /// - Constructor con parámetros que coinciden con propiedades
        /// - Constructor parcial (algunos parámetros + property setters para el resto)
        /// - Records (constructor con todos los parámetros)
        /// </summary>
        T DeserializeEntity<T>(DocumentSnapshot document) where T : class;

        /// <summary>
        /// Deserializa un DocumentSnapshot a una entidad del tipo especificado,
        /// usando entidades relacionadas ya deserializadas para navegaciones (FK y SubCollections).
        /// </summary>
        /// <param name="document">El documento de Firestore a deserializar</param>
        /// <param name="relatedEntities">
        /// Diccionario de entidades ya deserializadas, indexadas por path de documento.
        /// Se usa para:
        /// - Inyectar FK en constructores que las requieran
        /// - Asignar navegaciones a propiedades
        /// - Asignar SubCollections a propiedades de colección
        /// </param>
        T DeserializeEntity<T>(DocumentSnapshot document, IReadOnlyDictionary<string, object> relatedEntities) where T : class;
    }
}