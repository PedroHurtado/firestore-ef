using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Storage.Contracts
{
    /// <summary>
    /// Servicio para cargar propiedades de navegación (subcollections y references).
    /// Centraliza la carga de navegaciones usando IFirestoreClientWrapper como único punto de I/O.
    /// </summary>
    public interface INavigationLoader
    {
        /// <summary>
        /// Carga una subcollection en la entidad padre.
        /// </summary>
        /// <typeparam name="TParent">Tipo de la entidad padre.</typeparam>
        /// <typeparam name="TChild">Tipo de las entidades hijas.</typeparam>
        /// <param name="parentEntity">Entidad padre donde se cargará la colección.</param>
        /// <param name="parentDoc">DocumentSnapshot del padre.</param>
        /// <param name="navigation">Metadatos de la navegación.</param>
        /// <param name="dbContext">DbContext para lazy loading proxies.</param>
        /// <param name="cancellationToken">Token de cancelación.</param>
        Task LoadSubCollectionAsync<TParent, TChild>(
            TParent parentEntity,
            DocumentSnapshot parentDoc,
            IReadOnlyNavigation navigation,
            DbContext? dbContext = null,
            CancellationToken cancellationToken = default)
            where TParent : class
            where TChild : class;

        /// <summary>
        /// Carga una referencia en la entidad padre.
        /// </summary>
        /// <typeparam name="TParent">Tipo de la entidad padre.</typeparam>
        /// <typeparam name="TChild">Tipo de la entidad referenciada.</typeparam>
        /// <param name="parentEntity">Entidad padre donde se cargará la referencia.</param>
        /// <param name="parentDoc">DocumentSnapshot del padre.</param>
        /// <param name="navigation">Metadatos de la navegación.</param>
        /// <param name="dbContext">DbContext para lazy loading proxies.</param>
        /// <param name="cancellationToken">Token de cancelación.</param>
        Task LoadReferenceAsync<TParent, TChild>(
            TParent parentEntity,
            DocumentSnapshot parentDoc,
            IReadOnlyNavigation navigation,
            DbContext? dbContext = null,
            CancellationToken cancellationToken = default)
            where TParent : class
            where TChild : class;
    }
}
