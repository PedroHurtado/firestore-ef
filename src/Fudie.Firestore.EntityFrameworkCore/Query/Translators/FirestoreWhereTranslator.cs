using Fudie.Firestore.EntityFrameworkCore.Query.Ast;
using Fudie.Firestore.EntityFrameworkCore.Query.Visitors;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Translators
{
    internal class FirestoreWhereTranslator
    {
        public FirestoreFilterResult? Translate(Expression expression)
        {
            if (expression is BinaryExpression binaryExpression)
            {
                // Handle AND (&&) - flatten into multiple clauses
                if (binaryExpression.NodeType == ExpressionType.AndAlso)
                {
                    return TranslateAndExpression(binaryExpression);
                }

                // Handle OR (||) - create OR group
                if (binaryExpression.NodeType == ExpressionType.OrElse)
                {
                    return TranslateOrExpression(binaryExpression);
                }

                // Handle simple comparison
                var clause = TranslateBinaryExpression(binaryExpression);
                return clause != null ? FirestoreFilterResult.FromClause(clause) : null;
            }

            if (expression is MethodCallExpression methodCallExpression)
            {
                var clauses = TranslateMethodCallExpression(methodCallExpression);
                if (clauses != null && clauses.Count > 0)
                {
                    var result = new FirestoreFilterResult();
                    result.AndClauses.AddRange(clauses);
                    return result;
                }
                return null;
            }

            // Handle FirestoreArrayContainsExpression (created by FirestoreQueryableMethodTranslatingExpressionVisitor)
            if (expression is FirestoreArrayContainsExpression arrayContainsExpr)
            {
                var clause = new FirestoreWhereClause(
                    arrayContainsExpr.PropertyName,
                    FirestoreOperator.ArrayContains,
                    arrayContainsExpr.ValueExpression);
                return FirestoreFilterResult.FromClause(clause);
            }

            // Handle FirestoreArrayContainsAnyExpression (created by FirestoreQueryableMethodTranslatingExpressionVisitor)
            if (expression is FirestoreArrayContainsAnyExpression arrayContainsAnyExpr)
            {
                var clause = new FirestoreWhereClause(
                    arrayContainsAnyExpr.PropertyName,
                    FirestoreOperator.ArrayContainsAny,
                    arrayContainsAnyExpr.ValuesExpression);
                return FirestoreFilterResult.FromClause(clause);
            }

            // Handle NOT (!expression) - !list.Contains(field) → NotIn, !e.BoolField → field == false
            if (expression is UnaryExpression unaryExpression &&
                unaryExpression.NodeType == ExpressionType.Not)
            {
                var clause = TranslateNotExpression(unaryExpression);
                return clause != null ? FirestoreFilterResult.FromClause(clause) : null;
            }

            // Handle simple boolean property access: e.IsActive → field == true
            if (expression is MemberExpression memberExpr &&
                memberExpr.Member is PropertyInfo propInfo &&
                propInfo.PropertyType == typeof(bool))
            {
                var propertyPath = BuildPropertyPath(memberExpr);
                var clause = new FirestoreWhereClause(
                    propertyPath,
                    FirestoreOperator.EqualTo,
                    Expression.Constant(true));
                return FirestoreFilterResult.FromClause(clause);
            }

            return null;
        }

        private FirestoreFilterResult? TranslateAndExpression(BinaryExpression andExpression)
        {
            var clauses = new List<FirestoreWhereClause>();
            var nestedOrGroups = new List<FirestoreOrFilterGroup>();
            FlattenAndExpression(andExpression, clauses, nestedOrGroups);

            if (clauses.Count == 0 && nestedOrGroups.Count == 0)
                return null;

            var result = new FirestoreFilterResult();
            result.AndClauses.AddRange(clauses);
            result.NestedOrGroups.AddRange(nestedOrGroups);
            return result;
        }

        private void FlattenAndExpression(
            Expression expression,
            List<FirestoreWhereClause> clauses,
            List<FirestoreOrFilterGroup> nestedOrGroups)
        {
            if (expression is BinaryExpression binary)
            {
                if (binary.NodeType == ExpressionType.AndAlso)
                {
                    // Recursively flatten left and right
                    FlattenAndExpression(binary.Left, clauses, nestedOrGroups);
                    FlattenAndExpression(binary.Right, clauses, nestedOrGroups);
                    return;
                }

                if (binary.NodeType == ExpressionType.OrElse)
                {
                    // Found nested OR within AND - translate it as an OR group
                    var orClauses = new List<FirestoreWhereClause>();
                    FlattenOrExpression(binary, orClauses);
                    if (orClauses.Count > 0)
                    {
                        nestedOrGroups.Add(new FirestoreOrFilterGroup(orClauses));
                    }
                    return;
                }

                // Simple comparison
                var clause = TranslateBinaryExpression(binary);
                if (clause != null)
                {
                    clauses.Add(clause);
                }
                return;
            }

            if (expression is MethodCallExpression methodCall)
            {
                var translatedClauses = TranslateMethodCallExpression(methodCall);
                if (translatedClauses != null)
                {
                    clauses.AddRange(translatedClauses);
                }
                return;
            }

            // Handle NOT expressions within AND - e.g., !list.Contains(field)
            if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Not)
            {
                var clause = TranslateNotExpression(unary);
                if (clause != null)
                {
                    clauses.Add(clause);
                }
                return;
            }

            // Handle FirestoreArrayContainsExpression within AND
            if (expression is FirestoreArrayContainsExpression arrayContainsExpr)
            {
                var clause = new FirestoreWhereClause(
                    arrayContainsExpr.PropertyName,
                    FirestoreOperator.ArrayContains,
                    arrayContainsExpr.ValueExpression);
                clauses.Add(clause);
                return;
            }

            // Handle FirestoreArrayContainsAnyExpression within AND
            if (expression is FirestoreArrayContainsAnyExpression arrayContainsAnyExpr)
            {
                var clause = new FirestoreWhereClause(
                    arrayContainsAnyExpr.PropertyName,
                    FirestoreOperator.ArrayContainsAny,
                    arrayContainsAnyExpr.ValuesExpression);
                clauses.Add(clause);
                return;
            }

            // Handle simple boolean property access within AND: e.IsActive → field == true
            if (expression is MemberExpression memberExpr &&
                memberExpr.Member is PropertyInfo propInfo &&
                propInfo.PropertyType == typeof(bool))
            {
                var propertyPath = BuildPropertyPath(memberExpr);
                var clause = new FirestoreWhereClause(
                    propertyPath,
                    FirestoreOperator.EqualTo,
                    Expression.Constant(true));
                clauses.Add(clause);
            }
        }

        private FirestoreFilterResult? TranslateOrExpression(BinaryExpression orExpression)
        {
            var clauses = new List<FirestoreWhereClause>();
            FlattenOrExpression(orExpression, clauses);

            if (clauses.Count > 0)
            {
                var orGroup = new FirestoreOrFilterGroup(clauses);
                return FirestoreFilterResult.FromOrGroup(orGroup);
            }

            return null;
        }

        private void FlattenOrExpression(Expression expression, List<FirestoreWhereClause> clauses)
        {
            if (expression is BinaryExpression binary)
            {
                if (binary.NodeType == ExpressionType.OrElse)
                {
                    // Recursively flatten left and right
                    FlattenOrExpression(binary.Left, clauses);
                    FlattenOrExpression(binary.Right, clauses);
                    return;
                }

                // Simple comparison
                var clause = TranslateBinaryExpression(binary);
                if (clause != null)
                {
                    clauses.Add(clause);
                }
                return;
            }

            if (expression is MethodCallExpression methodCall)
            {
                var translatedClauses = TranslateMethodCallExpression(methodCall);
                if (translatedClauses != null)
                {
                    clauses.AddRange(translatedClauses);
                }
                return;
            }

            // Handle FirestoreArrayContainsExpression within OR
            if (expression is FirestoreArrayContainsExpression arrayContainsExpr)
            {
                var clause = new FirestoreWhereClause(
                    arrayContainsExpr.PropertyName,
                    FirestoreOperator.ArrayContains,
                    arrayContainsExpr.ValueExpression);
                clauses.Add(clause);
                return;
            }

            // Handle FirestoreArrayContainsAnyExpression within OR
            if (expression is FirestoreArrayContainsAnyExpression arrayContainsAnyExpr)
            {
                var clause = new FirestoreWhereClause(
                    arrayContainsAnyExpr.PropertyName,
                    FirestoreOperator.ArrayContainsAny,
                    arrayContainsAnyExpr.ValuesExpression);
                clauses.Add(clause);
                return;
            }

            // Handle simple boolean property access within OR: e.IsActive → field == true
            if (expression is MemberExpression memberExpr &&
                memberExpr.Member is PropertyInfo propInfo &&
                propInfo.PropertyType == typeof(bool))
            {
                var propertyPath = BuildPropertyPath(memberExpr);
                var clause = new FirestoreWhereClause(
                    propertyPath,
                    FirestoreOperator.EqualTo,
                    Expression.Constant(true));
                clauses.Add(clause);
            }
        }

        private FirestoreWhereClause? TranslateBinaryExpression(BinaryExpression binary)
        {
            string? propertyName = null;
            Expression? valueExpression = null;
            Type? enumType = null;

            // Intentar extraer propiedad del lado izquierdo
            var leftResult = ExtractPropertyInfo(binary.Left);
            if (leftResult.HasValue)
            {
                propertyName = leftResult.Value.PropertyName;
                enumType = leftResult.Value.EnumType;
                valueExpression = binary.Right;
            }
            else
            {
                // Intentar extraer propiedad del lado derecho
                var rightResult = ExtractPropertyInfo(binary.Right);
                if (rightResult.HasValue)
                {
                    propertyName = rightResult.Value.PropertyName;
                    enumType = rightResult.Value.EnumType;
                    valueExpression = binary.Left;
                }
            }

            // Fallback: EF.Property<T>()
            if (propertyName == null)
            {
                if (binary.Left is MethodCallExpression leftMethod &&
                    leftMethod.Method.Name == "Property" &&
                    leftMethod.Method.DeclaringType?.Name == "EF")
                {
                    propertyName = GetPropertyNameFromEFProperty(leftMethod);
                    valueExpression = binary.Right;
                }
                else if (binary.Right is MethodCallExpression rightMethod &&
                         rightMethod.Method.Name == "Property" &&
                         rightMethod.Method.DeclaringType?.Name == "EF")
                {
                    propertyName = GetPropertyNameFromEFProperty(rightMethod);
                    valueExpression = binary.Left;
                }
            }

            if (propertyName == null || valueExpression == null)
                return null;

            var firestoreOperator = binary.NodeType switch
            {
                ExpressionType.Equal => FirestoreOperator.EqualTo,
                ExpressionType.NotEqual => FirestoreOperator.NotEqualTo,
                ExpressionType.LessThan => FirestoreOperator.LessThan,
                ExpressionType.LessThanOrEqual => FirestoreOperator.LessThanOrEqualTo,
                ExpressionType.GreaterThan => FirestoreOperator.GreaterThan,
                ExpressionType.GreaterThanOrEqual => FirestoreOperator.GreaterThanOrEqualTo,
                _ => (FirestoreOperator?)null
            };

            if (!firestoreOperator.HasValue)
                return null;

            return new FirestoreWhereClause(propertyName, firestoreOperator.Value, valueExpression, enumType);
        }

        /// <summary>
        /// Extrae información de propiedad de una expresión, manejando casts de enum y propiedades anidadas (ComplexType).
        /// Retorna el path completo de la propiedad (ej: "Direccion.Ciudad") y opcionalmente el tipo de enum si hay cast.
        /// </summary>
        private (string PropertyName, Type? EnumType)? ExtractPropertyInfo(Expression expression)
        {
            // Caso 1: MemberExpression directo o anidado (p.Nombre, p.Direccion.Ciudad)
            if (expression is MemberExpression memberExpr && memberExpr.Member is PropertyInfo propInfo)
            {
                // Construir el path completo para propiedades anidadas
                var propertyPath = BuildPropertyPath(memberExpr);

                // Verificar si la propiedad es de tipo enum
                Type? enumType = null;
                if (propInfo.PropertyType.IsEnum)
                {
                    enumType = propInfo.PropertyType;
                }
                return (propertyPath, enumType);
            }

            // Caso 2: UnaryExpression con cast - típico de enums: (int)p.Categoria
            if (expression is UnaryExpression unaryExpr &&
                (unaryExpr.NodeType == ExpressionType.Convert || unaryExpr.NodeType == ExpressionType.ConvertChecked))
            {
                // Verificar si el operando es un MemberExpression
                if (unaryExpr.Operand is MemberExpression innerMember && innerMember.Member is PropertyInfo innerProp)
                {
                    // Construir el path completo para propiedades anidadas con cast
                    var propertyPath = BuildPropertyPath(innerMember);

                    // Verificar si la propiedad original es enum
                    Type? enumType = null;
                    if (innerProp.PropertyType.IsEnum)
                    {
                        enumType = innerProp.PropertyType;
                    }
                    return (propertyPath, enumType);
                }
            }

            return null;
        }

        /// <summary>
        /// Construye el path completo de una propiedad anidada.
        /// Para p.Direccion.Ciudad retorna "Direccion.Ciudad".
        /// Para p.Direccion.Coordenadas.Altitud retorna "Direccion.Coordenadas.Altitud".
        /// </summary>
        private string BuildPropertyPath(MemberExpression memberExpr)
        {
            var parts = new List<string>();
            Expression? current = memberExpr;

            while (current is MemberExpression member)
            {
                parts.Add(member.Member.Name);
                current = member.Expression;
            }

            // Revertir para obtener el orden correcto (de padre a hijo)
            parts.Reverse();
            return string.Join(".", parts);
        }

        private FirestoreWhereClause? TranslateNotExpression(UnaryExpression notExpression)
        {
            // Handle !e.BoolField → field == false
            if (notExpression.Operand is MemberExpression memberExpr &&
                memberExpr.Member is PropertyInfo propInfo &&
                propInfo.PropertyType == typeof(bool))
            {
                var propertyPath = BuildPropertyPath(memberExpr);
                return new FirestoreWhereClause(
                    propertyPath,
                    FirestoreOperator.EqualTo,
                    Expression.Constant(false));
            }

            // Handle !FirestoreArrayContainsExpression → NOT SUPPORTED by Firestore
            // This case occurs when the visitor has already transformed e.Array.Contains(value) to FirestoreArrayContainsExpression
            if (notExpression.Operand is FirestoreArrayContainsExpression arrayContainsExpr)
            {
                throw new NotSupportedException(
                    $"Firestore does not support 'NOT array-contains' queries. " +
                    $"The expression '!{arrayContainsExpr.PropertyName}.Contains(value)' cannot be translated. " +
                    $"Consider using client-side filtering with AsEnumerable() or restructuring your query.");
            }

            // Handle !FirestoreArrayContainsAnyExpression → NOT SUPPORTED by Firestore
            if (notExpression.Operand is FirestoreArrayContainsAnyExpression arrayContainsAnyExpr)
            {
                throw new NotSupportedException(
                    $"Firestore does not support 'NOT array-contains-any' queries. " +
                    $"The expression '!{arrayContainsAnyExpr.PropertyName}.ContainsAny(values)' cannot be translated. " +
                    $"Consider using client-side filtering with AsEnumerable() or restructuring your query.");
            }

            // Handle !list.Contains(field) → NotIn
            if (notExpression.Operand is MethodCallExpression methodCall &&
                methodCall.Method.Name == "Contains")
            {
                // Case 1: Instance method - !list.Contains(e.Field) → NotIn
                // Note: !e.ArrayField.Contains(value) is handled by ArrayContainsPatternTransformer
                // which throws NotSupportedException during preprocessing.
                if (methodCall.Object != null && methodCall.Arguments.Count == 1)
                {
                    var propertyResult = ExtractPropertyInfo(methodCall.Arguments[0]);
                    if (propertyResult.HasValue)
                    {
                        return new FirestoreWhereClause(propertyResult.Value.PropertyName, FirestoreOperator.NotIn, methodCall.Object);
                    }
                }

                // Case 2: Static !Enumerable.Contains(list, e.Field) → NotIn
                if (methodCall.Object == null && methodCall.Arguments.Count == 2)
                {
                    var propertyResult = ExtractPropertyInfo(methodCall.Arguments[1]);
                    if (propertyResult.HasValue)
                    {
                        return new FirestoreWhereClause(propertyResult.Value.PropertyName, FirestoreOperator.NotIn, methodCall.Arguments[0]);
                    }
                }
            }

            return null;
        }

        private List<FirestoreWhereClause>? TranslateMethodCallExpression(MethodCallExpression methodCall)
        {
            // Handle object.Equals(EF.Property<object>(e, "PropertyName"), value)
            // This pattern is generated by EF Core for FindAsync with non-string PKs (Guid, int, etc.)
            if (methodCall.Method.Name == "Equals" &&
                methodCall.Method.DeclaringType == typeof(object) &&
                methodCall.Arguments.Count == 2)
            {
                return TranslateObjectEquals(methodCall);
            }

            // Handle instance method e.Property.Equals(value) when resolved to object.Equals(object)
            // This pattern occurs with generics where TId.Equals(TId) resolves to object.Equals(object) due to boxing
            // Example: e.Id.Equals((object)id) in generic methods like GetRequiredAsync<T, TId>()
            if (methodCall.Method.Name == "Equals" &&
                methodCall.Method.DeclaringType == typeof(object) &&
                methodCall.Object != null &&
                methodCall.Arguments.Count == 1)
            {
                return TranslateInstanceObjectEquals(methodCall);
            }

            // Handle StartsWith - translated to range query: field >= "prefix" AND field < "prefix\uffff"
            if (methodCall.Method.Name == "StartsWith" && methodCall.Method.DeclaringType == typeof(string))
            {
                return TranslateStartsWith(methodCall);
            }

            if (methodCall.Method.Name == "Contains")
            {
                // Case 1: Instance method - list.Contains(e.Field) → WhereIn
                if (methodCall.Object != null && methodCall.Arguments.Count == 1)
                {
                    var propertyResult = ExtractPropertyInfo(methodCall.Arguments[0]);
                    if (propertyResult.HasValue)
                    {
                        return new List<FirestoreWhereClause>
                        {
                            new FirestoreWhereClause(propertyResult.Value.PropertyName, FirestoreOperator.In, methodCall.Object)
                        };
                    }
                }

                // Case 2: Static Enumerable.Contains(list, e.Field) → WhereIn
                if (methodCall.Object == null && methodCall.Arguments.Count == 2)
                {
                    var propertyResult = ExtractPropertyInfo(methodCall.Arguments[1]);
                    if (propertyResult.HasValue)
                    {
                        return new List<FirestoreWhereClause>
                        {
                            new FirestoreWhereClause(propertyResult.Value.PropertyName, FirestoreOperator.In, methodCall.Arguments[0])
                        };
                    }
                }

                // Note: ArrayContains patterns (e.ArrayField.Contains(value) and
                // EF.Property<List<T>>().AsQueryable().Contains()) are handled by
                // ArrayContainsPatternTransformer in the preprocessing phase, which converts
                // them to FirestoreArrayContainsExpression before reaching this translator.
            }

            return null;
        }

        /// <summary>
        /// Translates StartsWith to a range query: field &gt;= prefix AND field &lt; prefix\uffff.
        /// This is a workaround since Firestore does not support native string operators.
        /// </summary>
        private List<FirestoreWhereClause>? TranslateStartsWith(MethodCallExpression methodCall)
        {
            // StartsWith is an instance method: e.Name.StartsWith("prefix")
            if (methodCall.Object is MemberExpression memberExpr)
            {
                var propertyName = BuildPropertyPath(memberExpr);
                var prefixExpression = methodCall.Arguments[0];

                // Create GreaterThanOrEqual clause with the prefix
                var gteClause = new FirestoreWhereClause(
                    propertyName,
                    FirestoreOperator.GreaterThanOrEqualTo,
                    prefixExpression);

                // Create LessThan clause with prefix + \uffff (highest Unicode char)
                // We create a special expression that will be evaluated to add the suffix
                var ltExpression = new StartsWithUpperBoundExpression(prefixExpression);
                var ltClause = new FirestoreWhereClause(
                    propertyName,
                    FirestoreOperator.LessThan,
                    ltExpression);

                return new List<FirestoreWhereClause> { gteClause, ltClause };
            }

            return null;
        }

        /// <summary>
        /// Translates object.Equals(EF.Property&lt;object&gt;(e, "PropertyName"), value) to an equality filter.
        /// This pattern is generated by EF Core for FindAsync with non-string PKs (Guid, int, etc.)
        /// where it uses object.Equals for type-safe comparison.
        /// </summary>
        private List<FirestoreWhereClause>? TranslateObjectEquals(MethodCallExpression methodCall)
        {
            // Arguments[0] = EF.Property<object>(e, "PropertyName") or property access
            // Arguments[1] = value (constant or parameter)
            var leftArg = methodCall.Arguments[0];
            var rightArg = methodCall.Arguments[1];

            string? propertyName = null;
            Expression valueExpression;

            // Try to extract property name from left argument (EF.Property call)
            if (leftArg is MethodCallExpression leftMethod &&
                leftMethod.Method.Name == "Property" &&
                leftMethod.Method.DeclaringType?.Name == "EF")
            {
                propertyName = GetPropertyNameFromEFProperty(leftMethod);
                valueExpression = rightArg;
            }
            // Try to extract from right argument (in case arguments are swapped)
            else if (rightArg is MethodCallExpression rightMethod &&
                     rightMethod.Method.Name == "Property" &&
                     rightMethod.Method.DeclaringType?.Name == "EF")
            {
                propertyName = GetPropertyNameFromEFProperty(rightMethod);
                valueExpression = leftArg;
            }
            // Try regular property access on left
            else if (leftArg is MemberExpression leftMember)
            {
                propertyName = BuildPropertyPath(leftMember);
                valueExpression = rightArg;
            }
            // Try regular property access on right
            else if (rightArg is MemberExpression rightMember)
            {
                propertyName = BuildPropertyPath(rightMember);
                valueExpression = leftArg;
            }
            else
            {
                return null;
            }

            if (propertyName == null)
                return null;

            return new List<FirestoreWhereClause>
            {
                new FirestoreWhereClause(propertyName, FirestoreOperator.EqualTo, valueExpression)
            };
        }

        /// <summary>
        /// Translates instance method e.Property.Equals(value) to an equality filter.
        /// This pattern occurs when using generics where the compiler resolves .Equals(id)
        /// to object.Equals(object) due to boxing (e.g., in generic methods like GetRequiredAsync&lt;T, TId&gt;()).
        /// </summary>
        private List<FirestoreWhereClause>? TranslateInstanceObjectEquals(MethodCallExpression methodCall)
        {
            // methodCall.Object = e.Property (the property being compared)
            // methodCall.Arguments[0] = value (the value to compare against)
            var propertyExpression = methodCall.Object;
            var valueExpression = methodCall.Arguments[0];

            string? propertyName = null;

            // Extract property name from the object (e.Property)
            if (propertyExpression is MemberExpression memberExpr)
            {
                propertyName = BuildPropertyPath(memberExpr);
            }
            // Handle case where property has a cast: ((object)e.Property).Equals(value)
            else if (propertyExpression is UnaryExpression unaryExpr &&
                     (unaryExpr.NodeType == ExpressionType.Convert || unaryExpr.NodeType == ExpressionType.ConvertChecked) &&
                     unaryExpr.Operand is MemberExpression innerMember)
            {
                propertyName = BuildPropertyPath(innerMember);
            }

            if (propertyName == null)
                return null;

            return new List<FirestoreWhereClause>
            {
                new FirestoreWhereClause(propertyName, FirestoreOperator.EqualTo, valueExpression)
            };
        }

        private string? GetPropertyNameFromEFProperty(MethodCallExpression methodCall)
        {
            if (methodCall.Arguments.Count >= 2 && methodCall.Arguments[1] is ConstantExpression constant)
            {
                return constant.Value as string;
            }
            return null;
        }
    }
}
