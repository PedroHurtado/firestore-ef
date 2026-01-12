using System;
using System.Collections.Generic;

namespace Fudie.Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Representa un cursor de paginación para queries de Firestore.
    /// Encapsula los valores necesarios para usar StartAfter/StartAt/EndBefore/EndAt
    /// sin exponer tipos del SDK de Google (DocumentSnapshot).
    /// </summary>
    public class FirestoreCursor
    {
        /// <summary>
        /// ID del documento desde el cual continuar la paginación.
        /// </summary>
        public string DocumentId { get; }

        /// <summary>
        /// Valores de los campos ordenados, en el mismo orden que los OrderBy.
        /// Firestore requiere estos valores para posicionar el cursor correctamente
        /// cuando hay múltiples OrderBy clauses.
        /// </summary>
        public IReadOnlyList<object?> OrderByValues { get; }

        /// <summary>
        /// Crea un cursor con solo el ID del documento.
        /// Útil cuando la query ordena solo por __name__ (el ID).
        /// </summary>
        public FirestoreCursor(string documentId)
        {
            DocumentId = documentId ?? throw new ArgumentNullException(nameof(documentId));
            OrderByValues = Array.Empty<object?>();
        }

        /// <summary>
        /// Crea un cursor con el ID del documento y los valores de los campos ordenados.
        /// Los valores deben estar en el mismo orden que los OrderBy clauses de la query.
        /// </summary>
        public FirestoreCursor(string documentId, params object?[] orderByValues)
        {
            DocumentId = documentId ?? throw new ArgumentNullException(nameof(documentId));
            OrderByValues = orderByValues ?? Array.Empty<object?>();
        }

        public override string ToString()
        {
            if (OrderByValues.Count == 0)
            {
                return $"Cursor({DocumentId})";
            }
            return $"Cursor({DocumentId}, [{string.Join(", ", OrderByValues)}])";
        }
    }
}
