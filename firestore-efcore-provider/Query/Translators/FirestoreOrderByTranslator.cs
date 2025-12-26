using Firestore.EntityFrameworkCore.Query.Ast;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Firestore.EntityFrameworkCore.Query.Translators
{
    /// <summary>
    /// Traduce expresiones OrderBy/ThenBy a FirestoreOrderByClause.
    /// </summary>
    internal class FirestoreOrderByTranslator
    {
        /// <summary>
        /// Traduce un keySelector de OrderBy/ThenBy a un FirestoreOrderByClause.
        /// </summary>
        /// <param name="keySelector">Lambda expression del selector (ej: e => e.Name)</param>
        /// <param name="ascending">True para ascendente, false para descendente</param>
        /// <returns>FirestoreOrderByClause o null si no se puede traducir</returns>
        public FirestoreOrderByClause? Translate(LambdaExpression keySelector, bool ascending)
        {
            var propertyName = ExtractPropertyName(keySelector);
            if (propertyName == null)
            {
                return null;
            }

            return new FirestoreOrderByClause(propertyName, descending: !ascending);
        }

        /// <summary>
        /// Extrae el nombre de la propiedad del keySelector.
        /// Maneja propiedades simples (e.Name) y anidadas (e.Address.City).
        /// </summary>
        private string? ExtractPropertyName(LambdaExpression keySelector)
        {
            var body = keySelector.Body;

            // Unwrap Convert expressions (common for value types)
            if (body is UnaryExpression unary &&
                (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
            {
                body = unary.Operand;
            }

            // Handle MemberExpression (e.g., e.Name, e.Address.City)
            if (body is MemberExpression memberExpr && memberExpr.Member is PropertyInfo)
            {
                return BuildPropertyPath(memberExpr);
            }

            return null;
        }

        /// <summary>
        /// Construye el path completo para propiedades anidadas.
        /// Para e.Address.City retorna "Address.City".
        /// Para e.Name retorna "Name".
        /// </summary>
        private static string BuildPropertyPath(MemberExpression memberExpr)
        {
            var parts = new List<string>();
            Expression? current = memberExpr;

            while (current is MemberExpression member)
            {
                parts.Add(member.Member.Name);
                current = member.Expression;
            }

            // Reverse to get correct order (parent to child)
            parts.Reverse();
            return string.Join(".", parts);
        }
    }
}
