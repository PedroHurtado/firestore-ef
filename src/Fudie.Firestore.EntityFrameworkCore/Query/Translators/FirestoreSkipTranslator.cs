using System.Linq.Expressions;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Translators
{
    /// <summary>
    /// Traduce expresiones de Skip a valores enteros.
    /// Hereda de FirestoreLimitTranslator ya que la lógica de extracción es idéntica.
    /// </summary>
    internal class FirestoreSkipTranslator : FirestoreLimitTranslator
    {
    }
}
