using Fudie.Firestore.EntityFrameworkCore.Query.Ast;
using System.Collections.Generic;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Translators
{
    /// <summary>
    /// Translates IncludeExpression to IncludeInfo.
    ///
    /// Delegates to IncludeExtractionVisitor which properly traverses the expression tree
    /// to find all includes (including ThenInclude chains inside MaterializeCollectionNavigationExpression).
    /// </summary>
    internal class FirestoreIncludeTranslator
    {
        private readonly IncludeExtractionVisitor _visitor;

        public FirestoreIncludeTranslator(IncludeExtractionVisitor visitor)
        {
            _visitor = visitor;
        }

        /// <summary>
        /// Translates an IncludeExpression to a list of IncludeInfo.
        /// Returns multiple items when there are ThenInclude chains.
        /// </summary>
        public List<IncludeInfo> Translate(Microsoft.EntityFrameworkCore.Query.IncludeExpression includeExpression)
        {
            _visitor.Visit(includeExpression);
            return _visitor.DetectedIncludes;
        }
    }
}
