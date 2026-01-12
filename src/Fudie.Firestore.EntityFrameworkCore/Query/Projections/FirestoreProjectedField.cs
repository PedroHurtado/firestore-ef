using System;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Projections
{
    /// <summary>
    /// Represents a field to be projected from a Firestore document.
    /// Contains the source field path in Firestore and the target name in the result.
    /// </summary>
    public class FirestoreProjectedField
    {
        /// <summary>
        /// Path of the field in Firestore document.
        /// Supports nested paths like "Direccion.Ciudad" or "Direccion.Coordenadas.Latitud".
        /// </summary>
        public string FieldPath { get; }

        /// <summary>
        /// Name of the field in the projection result.
        /// May differ from FieldPath when using aliases (e.g., new { Ciudad = e.Direccion.Ciudad }).
        /// </summary>
        public string ResultName { get; }

        /// <summary>
        /// CLR type of the field.
        /// </summary>
        public Type FieldType { get; }

        /// <summary>
        /// Index of this field in the constructor for record types.
        /// Value is -1 for property assignments (DTOs, anonymous types).
        /// </summary>
        public int ConstructorParameterIndex { get; }

        /// <summary>
        /// Indicates whether this field is passed via constructor (for records).
        /// </summary>
        public bool IsConstructorParameter => ConstructorParameterIndex >= 0;

        /// <summary>
        /// Creates a new projected field definition.
        /// </summary>
        /// <param name="fieldPath">Path of the field in Firestore document.</param>
        /// <param name="resultName">Name of the field in the projection result.</param>
        /// <param name="fieldType">CLR type of the field.</param>
        /// <param name="constructorParameterIndex">Index in constructor for records, or -1 for property assignment.</param>
        public FirestoreProjectedField(
            string fieldPath,
            string resultName,
            Type fieldType,
            int constructorParameterIndex = -1)
        {
            FieldPath = fieldPath ?? throw new ArgumentNullException(nameof(fieldPath));
            ResultName = resultName ?? throw new ArgumentNullException(nameof(resultName));
            FieldType = fieldType ?? throw new ArgumentNullException(nameof(fieldType));
            ConstructorParameterIndex = constructorParameterIndex;
        }
    }
}
