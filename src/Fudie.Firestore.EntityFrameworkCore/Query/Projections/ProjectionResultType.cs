namespace Fudie.Firestore.EntityFrameworkCore.Query.Projections
{
    /// <summary>
    /// Defines the type of projection result for a Select clause.
    /// Used to determine how to construct the result object.
    /// </summary>
    public enum ProjectionResultType
    {
        /// <summary>
        /// Returns the entire entity (e => e).
        /// No specific fields are projected.
        /// </summary>
        Entity = 0,

        /// <summary>
        /// Returns a single field value (e => e.Name).
        /// The result type is the field's type.
        /// </summary>
        SingleField = 1,

        /// <summary>
        /// Returns an anonymous type (e => new { e.Id, e.Name }).
        /// Fields are assigned by property name.
        /// </summary>
        AnonymousType = 2,

        /// <summary>
        /// Returns a DTO class with property assignments (e => new Dto { Id = e.Id }).
        /// Uses MemberInit expression pattern.
        /// </summary>
        DtoClass = 3,

        /// <summary>
        /// Returns a record with constructor parameters (e => new Record(e.Id, e.Name)).
        /// Fields are passed via constructor in order.
        /// </summary>
        Record = 4
    }
}
