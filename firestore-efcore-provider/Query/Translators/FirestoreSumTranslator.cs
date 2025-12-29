using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Translators
{
    /// <summary>
    /// Translates Sum selector expressions to property names for Firestore aggregation.
    /// </summary>
    internal class FirestoreSumTranslator
    {
        /// <summary>
        /// Translates a Sum selector to a property name.
        /// </summary>
        /// <param name="selector">Lambda expression (e.g., e => e.Price)</param>
        /// <returns>Property name or null if not translatable</returns>
        public string? Translate(LambdaExpression? selector)
        {
            return PropertyPathExtractor.ExtractFromLambda(selector);
        }
    }
}
