using Firestore.EntityFrameworkCore.Query.Ast;
using Microsoft.EntityFrameworkCore.Query;
using System.Collections.Generic;

namespace Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Custom QueryCompilationContext for Firestore.
    ///
    /// NOTE: FilteredIncludes storage was removed. Filtered Includes are now translated
    /// directly in FirestoreQueryableMethodTranslatingExpressionVisitor.TranslateInclude
    /// using FirestoreIncludeTranslator, which produces IncludeInfo with:
    /// - FirestoreWhereClause (translated by FirestoreWhereTranslator)
    /// - FirestoreOrderByClause (translated by FirestoreOrderByTranslator)
    /// - Take/Skip values or expressions (translated by FirestoreLimitTranslator)
    ///
    /// This ensures consistency with the main query translation pipeline.
    /// </summary>
    public class FirestoreQueryCompilationContext : QueryCompilationContext
    {
        private readonly List<IncludeInfo> _complexTypeIncludes = new();
        private readonly List<IncludeInfo> _arrayOfIncludes = new();

        public FirestoreQueryCompilationContext(
            QueryCompilationContextDependencies dependencies,
            bool async) : base(dependencies, async)
        {
        }

        /// <summary>
        /// IncludeInfo for properties inside ComplexTypes.
        /// These are extracted and translated before EF Core processes them.
        /// </summary>
        public IReadOnlyList<IncludeInfo> ComplexTypeIncludes => _complexTypeIncludes;

        /// <summary>
        /// IncludeInfo for ArrayOf Reference properties.
        /// These are extracted and translated before EF Core processes them.
        /// </summary>
        public IReadOnlyList<IncludeInfo> ArrayOfIncludes => _arrayOfIncludes;

        /// <summary>
        /// Adds a ComplexType include for later processing.
        /// Called by ComplexTypeIncludeTranslator.
        /// </summary>
        internal void AddComplexTypeInclude(IncludeInfo includeInfo)
        {
            _complexTypeIncludes.Add(includeInfo);
        }

        /// <summary>
        /// Adds an ArrayOf Reference include for later processing.
        /// Called by ArrayOfIncludeTranslator.
        /// </summary>
        internal void AddArrayOfInclude(IncludeInfo includeInfo)
        {
            _arrayOfIncludes.Add(includeInfo);
        }
    }
}
