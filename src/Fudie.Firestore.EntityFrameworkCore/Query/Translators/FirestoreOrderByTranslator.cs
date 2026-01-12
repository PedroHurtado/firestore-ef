using Fudie.Firestore.EntityFrameworkCore.Query.Ast;
using System.Linq.Expressions;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Translators
{
    /// <summary>
    /// Traduce expresiones OrderBy/ThenBy a FirestoreOrderByClause.
    /// </summary>
    internal class FirestoreOrderByTranslator
    {
        /// <summary>
        /// Traduce un keySelector de OrderBy/ThenBy a un FirestoreOrderByClause.
        /// </summary>
        /// <param name="keySelector">Lambda expression del selector (ej: e => e.Name)</param>
        /// <param name="ascending">True para ascendente, false para descendente</param>
        /// <returns>FirestoreOrderByClause o null si no se puede traducir</returns>
        public FirestoreOrderByClause? Translate(LambdaExpression keySelector, bool ascending)
        {
            var propertyName = PropertyPathExtractor.ExtractFromLambda(keySelector);
            if (propertyName == null)
            {
                return null;
            }

            return new FirestoreOrderByClause(propertyName, descending: !ascending);
        }
    }
}
