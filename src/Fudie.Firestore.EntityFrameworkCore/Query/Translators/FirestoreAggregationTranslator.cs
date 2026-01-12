using System.Linq.Expressions;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Translators
{
    /// <summary>
    /// Base class for aggregation translators (Sum, Average, Min, Max).
    /// Translates selector expressions to property names.
    /// </summary>
    internal abstract class FirestoreAggregationTranslator
    {
        /// <summary>
        /// Translates an aggregation selector to a property name.
        /// </summary>
        /// <param name="selector">Lambda expression (e.g., e => e.Price)</param>
        /// <returns>Property name or null if not translatable</returns>
        public string? Translate(LambdaExpression? selector)
        {
            return PropertyPathExtractor.ExtractFromLambda(selector);
        }
    }
}
