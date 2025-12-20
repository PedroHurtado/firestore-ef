using Microsoft.EntityFrameworkCore.Query;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query
{
    public class FirestoreQueryCompilationContext : QueryCompilationContext
    {
        private readonly List<LambdaExpression> _complexTypeIncludes = new();
        private readonly Dictionary<string, IncludeInfo> _filteredIncludes = new();

        public FirestoreQueryCompilationContext(
            QueryCompilationContextDependencies dependencies,
            bool async) : base(dependencies, async)
        {
        }

        /// <summary>
        /// Include expressions that target properties inside ComplexTypes.
        /// These are extracted before EF Core processes them and used during deserialization.
        /// </summary>
        public IReadOnlyList<LambdaExpression> ComplexTypeIncludes => _complexTypeIncludes;

        /// <summary>
        /// Filtered Include information keyed by navigation name.
        /// These are extracted before EF Core processes them to capture filter expressions.
        /// </summary>
        public IReadOnlyDictionary<string, IncludeInfo> FilteredIncludes => _filteredIncludes;

        /// <summary>
        /// Adds a ComplexType include expression for later processing.
        /// Called by ComplexTypeIncludeExtractorVisitor.
        /// </summary>
        internal void AddComplexTypeInclude(LambdaExpression includeExpression)
        {
            _complexTypeIncludes.Add(includeExpression);
        }

        /// <summary>
        /// Adds a Filtered Include for later processing.
        /// Called by FilteredIncludeExtractorVisitor.
        /// </summary>
        internal void AddFilteredInclude(string navigationName, IncludeInfo includeInfo)
        {
            // Use the navigation name as key, overwrite if already exists
            _filteredIncludes[navigationName] = includeInfo;
        }

        /// <summary>
        /// Gets the IncludeInfo for a navigation if a filter was applied.
        /// </summary>
        public IncludeInfo? GetFilteredIncludeInfo(string navigationName)
        {
            return _filteredIncludes.TryGetValue(navigationName, out var info) ? info : null;
        }
    }
}