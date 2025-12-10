namespace Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Operadores de comparación soportados por Firestore
    /// </summary>
    public enum FirestoreOperator
    {
        /// <summary>
        /// Igual a (==) - WhereEqualTo
        /// </summary>
        EqualTo,

        /// <summary>
        /// No igual a (!=) - WhereNotEqualTo
        /// </summary>
        NotEqualTo,

        /// <summary>
        /// Menor que (&lt;) - WhereLessThan
        /// </summary>
        LessThan,

        /// <summary>
        /// Menor o igual que (&lt;=) - WhereLessThanOrEqualTo
        /// </summary>
        LessThanOrEqualTo,

        /// <summary>
        /// Mayor que (&gt;) - WhereGreaterThan
        /// </summary>
        GreaterThan,

        /// <summary>
        /// Mayor o igual que (&gt;=) - WhereGreaterThanOrEqualTo
        /// </summary>
        GreaterThanOrEqualTo,

        /// <summary>
        /// Array contiene (array-contains) - WhereArrayContains
        /// Ejemplo: p.Tags.Contains("nuevo")
        /// </summary>
        ArrayContains,

        /// <summary>
        /// Valor en lista (in) - WhereIn
        /// Ejemplo: ids.Contains(p.Id)
        /// NOTA: Firestore limita a 30 elementos máximo
        /// </summary>
        In,

        /// <summary>
        /// Array contiene cualquiera (array-contains-any) - WhereArrayContainsAny
        /// Ejemplo: p.Tags intersecta con ["nuevo", "destacado"]
        /// NOTA: Firestore limita a 30 elementos máximo
        /// </summary>
        ArrayContainsAny,

        /// <summary>
        /// Valor NO en lista (not-in) - WhereNotIn
        /// NOTA: Firestore limita a 10 elementos máximo
        /// </summary>
        NotIn
    }
}
