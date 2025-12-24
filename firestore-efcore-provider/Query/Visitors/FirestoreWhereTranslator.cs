using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Firestore.EntityFrameworkCore.Query.Visitors
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

            // Handle NOT (!expression) - typically !list.Contains(field) → NotIn
            if (expression is UnaryExpression unaryExpression &&
                unaryExpression.NodeType == ExpressionType.Not)
            {
                var clause = TranslateNotExpression(unaryExpression);
                return clause != null ? FirestoreFilterResult.FromClause(clause) : null;
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
            // Handle !list.Contains(field) → NotIn
            if (notExpression.Operand is MethodCallExpression methodCall &&
                methodCall.Method.Name == "Contains")
            {
                // Case 1: Instance method - !list.Contains(e.Field) → NotIn
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

                // Case 3: e.ArrayField.Contains(value) → ArrayContains
                if (methodCall.Object is MemberExpression objMember &&
                    objMember.Member is PropertyInfo objProp &&
                    methodCall.Arguments.Count == 1)
                {
                    var propertyName = BuildPropertyPath(objMember);
                    return new List<FirestoreWhereClause>
                    {
                        new FirestoreWhereClause(propertyName, FirestoreOperator.ArrayContains, methodCall.Arguments[0])
                    };
                }

                // Case 4: EF.Property<List<T>>(e, "Field").AsQueryable().Contains(value) → ArrayContains
                // EF Core transforms e.ArrayField.Contains(value) into this form
                if (methodCall.Arguments.Count == 1)
                {
                    var propertyName = ExtractPropertyNameFromEFPropertyChain(methodCall.Object);
                    if (propertyName != null)
                    {
                        return new List<FirestoreWhereClause>
                        {
                            new FirestoreWhereClause(propertyName, FirestoreOperator.ArrayContains, methodCall.Arguments[0])
                        };
                    }
                }
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
        /// Extracts property name from EF.Property chain like:
        /// EF.Property(e, "Field").AsQueryable() or just EF.Property(e, "Field")
        /// </summary>
        private string? ExtractPropertyNameFromEFPropertyChain(Expression? expression)
        {
            if (expression is MethodCallExpression methodCall)
            {
                // Check if it's AsQueryable() wrapping EF.Property
                if (methodCall.Method.Name == "AsQueryable" && methodCall.Arguments.Count == 1)
                {
                    return ExtractPropertyNameFromEFPropertyChain(methodCall.Arguments[0]);
                }

                // Check if it's EF.Property<T>(entity, "PropertyName")
                if (methodCall.Method.Name == "Property" &&
                    methodCall.Method.DeclaringType?.Name == "EF")
                {
                    return GetPropertyNameFromEFProperty(methodCall);
                }
            }

            return null;
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
