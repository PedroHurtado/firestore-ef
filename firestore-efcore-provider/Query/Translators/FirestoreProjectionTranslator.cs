using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Query.Projections;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Translators
{
    /// <summary>
    /// Translates a Select LambdaExpression to a FirestoreProjectionDefinition.
    /// Coordinates with ProjectionExtractionVisitor for the actual extraction logic.
    /// </summary>
    internal class FirestoreProjectionTranslator
    {
        private readonly IFirestoreCollectionManager _collectionManager;
        private readonly IEntityType? _entityType;

        /// <summary>
        /// Creates a new FirestoreProjectionTranslator with the required dependencies.
        /// </summary>
        /// <param name="collectionManager">Manager for resolving Firestore collection names.</param>
        /// <param name="entityType">The source entity type for navigation resolution.</param>
        public FirestoreProjectionTranslator(IFirestoreCollectionManager collectionManager, IEntityType? entityType = null)
        {
            _collectionManager = collectionManager;
            _entityType = entityType;
        }

        /// <summary>
        /// Translates a Select selector expression to a FirestoreProjectionDefinition.
        /// </summary>
        /// <param name="selector">The Select lambda expression (e.g., e => new { e.Id, e.Name })</param>
        /// <returns>
        /// FirestoreProjectionDefinition if projection is needed,
        /// null if no projection is needed (identity or type conversion).
        /// </returns>
        public FirestoreProjectionDefinition? Translate(LambdaExpression? selector)
        {
            if (selector == null)
                return null;

            var visitor = new ProjectionExtractionVisitor(_collectionManager, _entityType);
            return visitor.Extract(selector);
        }
    }
}
