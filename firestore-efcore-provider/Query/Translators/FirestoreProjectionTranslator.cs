using Firestore.EntityFrameworkCore.Query.Projections;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Translators
{
    /// <summary>
    /// Translates a Select LambdaExpression to a FirestoreProjectionDefinition.
    /// Coordinates with ProjectionExtractionVisitor for the actual extraction logic.
    /// </summary>
    internal class FirestoreProjectionTranslator
    {
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

            var visitor = new ProjectionExtractionVisitor();
            return visitor.Extract(selector);
        }
    }
}
