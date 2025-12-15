using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Visitors
{
    /// <summary>
    /// Visitor especializado para extraer TODOS los includes del árbol de expresiones.
    /// </summary>
    internal class IncludeExtractionVisitor : ExpressionVisitor
    {
        public List<IReadOnlyNavigation> DetectedNavigations { get; } = new();

        protected override Expression VisitExtension(Expression node)
        {
            if (node is Microsoft.EntityFrameworkCore.Query.IncludeExpression includeExpression)
            {
                if (includeExpression.Navigation is IReadOnlyNavigation navigation)
                {
                    DetectedNavigations.Add(navigation);
                }

                // Visitar EntityExpression y NavigationExpression para ThenInclude anidados
                Visit(includeExpression.EntityExpression);
                Visit(includeExpression.NavigationExpression);

                return node;
            }

            return base.VisitExtension(node);
        }
    }
}
