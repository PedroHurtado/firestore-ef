using System;

namespace Firestore.EntityFrameworkCore.Query.Ast
{
    /// <summary>
    /// Representa una cláusula ORDER BY en una query de Firestore
    /// </summary>
    public class FirestoreOrderByClause
    {
        /// <summary>
        /// Nombre de la propiedad/campo por el cual ordenar (ej: "Nombre", "Precio")
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// Si es orden descendente (true) o ascendente (false)
        /// </summary>
        public bool Descending { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public FirestoreOrderByClause(string propertyName, bool descending = false)
        {
            PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            Descending = descending;
        }

        /// <summary>
        /// Representación en string para debugging
        /// </summary>
        public override string ToString()
        {
            return $"{PropertyName} {(Descending ? "DESC" : "ASC")}";
        }
    }
}
