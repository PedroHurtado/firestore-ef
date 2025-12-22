using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Storage.Contracts;
using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Storage
{
    /// <summary>
    /// Implementación de INavigationLoader.
    /// Centraliza la carga de navegaciones usando IFirestoreClientWrapper como único punto de I/O.
    /// </summary>
    public class NavigationLoader : INavigationLoader
    {
        private readonly IFirestoreClientWrapper _clientWrapper;
        private readonly IFirestoreDocumentDeserializer _deserializer;

        public NavigationLoader(
            IFirestoreClientWrapper clientWrapper,
            IFirestoreDocumentDeserializer deserializer)
        {
            _clientWrapper = clientWrapper ?? throw new ArgumentNullException(nameof(clientWrapper));
            _deserializer = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
        }

        /// <inheritdoc />
        public Task LoadSubCollectionAsync<TParent, TChild>(
            TParent parentEntity,
            DocumentSnapshot parentDoc,
            IReadOnlyNavigation navigation,
            DbContext? dbContext = null,
            CancellationToken cancellationToken = default)
            where TParent : class
            where TChild : class
        {
            // Implementación pendiente - se completará en Ciclo 9
            throw new NotImplementedException("LoadSubCollectionAsync será implementado en Ciclo 9");
        }

        /// <inheritdoc />
        public Task LoadReferenceAsync<TParent, TChild>(
            TParent parentEntity,
            DocumentSnapshot parentDoc,
            IReadOnlyNavigation navigation,
            DbContext? dbContext = null,
            CancellationToken cancellationToken = default)
            where TParent : class
            where TChild : class
        {
            // Implementación pendiente - se completará en Ciclo 10
            throw new NotImplementedException("LoadReferenceAsync será implementado en Ciclo 10");
        }
    }
}
