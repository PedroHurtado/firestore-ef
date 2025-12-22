using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore;
using System;
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
        /// Deserializa un DocumentSnapshot a una entidad del tipo especificado.
        /// Si se proporciona DbContext y lazy loading está habilitado, crea un proxy.
        /// </summary>
        /// <param name="document">El documento de Firestore a deserializar</param>
        /// <param name="dbContext">DbContext para crear lazy loading proxies (opcional)</param>
        /// <param name="serviceProvider">ServiceProvider para resolver dependencias de proxy (opcional)</param>
        T DeserializeEntity<T>(DocumentSnapshot document, DbContext? dbContext, IServiceProvider? serviceProvider) where T : class;

        /// <summary>
        /// Deserializa un DocumentSnapshot en una instancia de entidad existente.
        /// Útil para poblar proxies de lazy loading.
        /// </summary>
        T DeserializeIntoEntity<T>(DocumentSnapshot document, T entity) where T : class;

        /// <summary>
        /// Deserializa múltiples documentos
        /// </summary>
        List<T> DeserializeEntities<T>(IEnumerable<DocumentSnapshot> documents) where T : class;
    }
}
