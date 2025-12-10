using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Visitors
{
    /// <summary>
    /// Visitor especializado para extraer TODOS los includes del árbol de expresiones
    /// </summary>
    internal class IncludeExtractionVisitor : ExpressionVisitor
    {
        public List<IReadOnlyNavigation> DetectedNavigations { get; } = new();
        private int _depth = 0;

        protected override Expression VisitExtension(Expression node)
        {
            if (node is Microsoft.EntityFrameworkCore.Query.IncludeExpression includeExpression)
            {
                // Capturar esta navegación
                if (includeExpression.Navigation is IReadOnlyNavigation navigation)
                {
                    Console.WriteLine($"{GetIndent()}✓ Captured Include: {navigation.Name}");
                    DetectedNavigations.Add(navigation);
                }

                // CRÍTICO: Visitar EntityExpression y NavigationExpression
                // para encontrar ThenInclude anidados
                _depth++;
                Visit(includeExpression.EntityExpression);
                Visit(includeExpression.NavigationExpression);
                _depth--;

                return node; // No llamar a base, ya visitamos manualmente
            }

            // Para otras expresiones, dejar que el visitor base maneje la recursión
            return base.VisitExtension(node);
        }

        private string GetIndent() => new string(' ', _depth * 2);
    }
}
