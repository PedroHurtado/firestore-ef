using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Firestore.EntityFrameworkCore.Query.Visitors
{
    internal class FirestoreWhereTranslator
    {
        public FirestoreWhereClause? Translate(Expression expression)
        {
            if (expression is BinaryExpression binaryExpression)
            {
                return TranslateBinaryExpression(binaryExpression);
            }

            if (expression is MethodCallExpression methodCallExpression)
            {
                return TranslateMethodCallExpression(methodCallExpression);
            }

            return null;
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
        /// Extrae información de propiedad de una expresión, manejando casts de enum.
        /// Retorna el nombre de la propiedad y opcionalmente el tipo de enum si hay cast.
        /// </summary>
        private (string PropertyName, Type? EnumType)? ExtractPropertyInfo(Expression expression)
        {
            // Caso 1: MemberExpression directo (p.Nombre, p.Precio)
            if (expression is MemberExpression memberExpr && memberExpr.Member is PropertyInfo propInfo)
            {
                // Verificar si la propiedad es de tipo enum
                Type? enumType = null;
                if (propInfo.PropertyType.IsEnum)
                {
                    enumType = propInfo.PropertyType;
                }
                return (propInfo.Name, enumType);
            }

            // Caso 2: UnaryExpression con cast - típico de enums: (int)p.Categoria
            if (expression is UnaryExpression unaryExpr &&
                (unaryExpr.NodeType == ExpressionType.Convert || unaryExpr.NodeType == ExpressionType.ConvertChecked))
            {
                // Verificar si el operando es un MemberExpression
                if (unaryExpr.Operand is MemberExpression innerMember && innerMember.Member is PropertyInfo innerProp)
                {
                    // Verificar si la propiedad original es enum
                    Type? enumType = null;
                    if (innerProp.PropertyType.IsEnum)
                    {
                        enumType = innerProp.PropertyType;
                    }
                    return (innerProp.Name, enumType);
                }
            }

            return null;
        }

        private FirestoreWhereClause? TranslateMethodCallExpression(MethodCallExpression methodCall)
        {
            if (methodCall.Method.Name == "Contains")
            {
                if (methodCall.Object != null && methodCall.Arguments.Count == 1)
                {
                    if (methodCall.Arguments[0] is MemberExpression member && member.Member is PropertyInfo prop)
                    {
                        var propertyName = prop.Name;
                        return new FirestoreWhereClause(propertyName, FirestoreOperator.In, methodCall.Object);
                    }
                }

                if (methodCall.Object is MemberExpression objMember &&
                    objMember.Member is PropertyInfo objProp &&
                    methodCall.Arguments.Count == 1)
                {
                    var propertyName = objProp.Name;
                    return new FirestoreWhereClause(propertyName, FirestoreOperator.ArrayContains, methodCall.Arguments[0]);
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
