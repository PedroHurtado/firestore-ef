using System;
using System.Collections.Generic;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Projections
{
    /// <summary>
    /// Represents a complete projection definition for a Select clause.
    /// Contains all information needed to project fields from Firestore documents.
    /// This is the structured representation that replaces LambdaExpression in the AST.
    /// </summary>
    public class FirestoreProjectionDefinition
    {
        /// <summary>
        /// Type of projection result (Entity, SingleField, AnonymousType, DtoClass, Record).
        /// </summary>
        public ProjectionResultType ResultType { get; }

        /// <summary>
        /// CLR type of the projection result.
        /// </summary>
        public Type ClrType { get; }

        /// <summary>
        /// Fields to project from the root entity.
        /// Null for Entity projections (all fields).
        /// </summary>
        public List<FirestoreProjectedField>? Fields { get; }

        /// <summary>
        /// Subcollections included in the projection.
        /// </summary>
        public List<FirestoreSubcollectionProjection> Subcollections { get; }

        /// <summary>
        /// Private constructor. Use factory methods to create instances.
        /// </summary>
        private FirestoreProjectionDefinition(
            ProjectionResultType resultType,
            Type clrType,
            List<FirestoreProjectedField>? fields)
        {
            ResultType = resultType;
            ClrType = clrType ?? throw new ArgumentNullException(nameof(clrType));
            Fields = fields;
            Subcollections = new List<FirestoreSubcollectionProjection>();
        }

        /// <summary>
        /// Creates an entity projection (e => e).
        /// Returns the entire entity without field filtering.
        /// </summary>
        /// <param name="entityType">CLR type of the entity.</param>
        /// <returns>A new projection definition for entity projection.</returns>
        public static FirestoreProjectionDefinition CreateEntityProjection(Type entityType)
        {
            return new FirestoreProjectionDefinition(
                ProjectionResultType.Entity,
                entityType,
                fields: null);
        }

        /// <summary>
        /// Creates a single field projection (e => e.Name).
        /// </summary>
        /// <param name="fieldType">CLR type of the field.</param>
        /// <param name="field">The field to project.</param>
        /// <returns>A new projection definition for single field projection.</returns>
        public static FirestoreProjectionDefinition CreateSingleFieldProjection(
            Type fieldType,
            FirestoreProjectedField field)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));

            return new FirestoreProjectionDefinition(
                ProjectionResultType.SingleField,
                fieldType,
                new List<FirestoreProjectedField> { field });
        }

        /// <summary>
        /// Creates an anonymous type projection (e => new { e.Id, e.Name }).
        /// </summary>
        /// <param name="anonymousType">CLR type of the anonymous type.</param>
        /// <param name="fields">Fields to include in the projection.</param>
        /// <returns>A new projection definition for anonymous type projection.</returns>
        public static FirestoreProjectionDefinition CreateAnonymousTypeProjection(
            Type anonymousType,
            List<FirestoreProjectedField> fields)
        {
            if (fields == null) throw new ArgumentNullException(nameof(fields));

            return new FirestoreProjectionDefinition(
                ProjectionResultType.AnonymousType,
                anonymousType,
                fields);
        }

        /// <summary>
        /// Creates a DTO class projection (e => new Dto { Id = e.Id }).
        /// </summary>
        /// <param name="dtoType">CLR type of the DTO class.</param>
        /// <param name="fields">Fields to include in the projection.</param>
        /// <returns>A new projection definition for DTO class projection.</returns>
        public static FirestoreProjectionDefinition CreateDtoClassProjection(
            Type dtoType,
            List<FirestoreProjectedField> fields)
        {
            if (fields == null) throw new ArgumentNullException(nameof(fields));

            return new FirestoreProjectionDefinition(
                ProjectionResultType.DtoClass,
                dtoType,
                fields);
        }

        /// <summary>
        /// Creates a record projection (e => new Record(e.Id, e.Name)).
        /// Fields must have ConstructorParameterIndex set.
        /// </summary>
        /// <param name="recordType">CLR type of the record.</param>
        /// <param name="fields">Fields to include in the projection with constructor indices.</param>
        /// <returns>A new projection definition for record projection.</returns>
        public static FirestoreProjectionDefinition CreateRecordProjection(
            Type recordType,
            List<FirestoreProjectedField> fields)
        {
            if (fields == null) throw new ArgumentNullException(nameof(fields));

            return new FirestoreProjectionDefinition(
                ProjectionResultType.Record,
                recordType,
                fields);
        }
    }
}
