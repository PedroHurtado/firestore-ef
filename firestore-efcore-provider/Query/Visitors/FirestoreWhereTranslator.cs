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

            if (binary.Left is MemberExpression leftMember && leftMember.Member is PropertyInfo leftProp)
            {
                propertyName = leftProp.Name;
                valueExpression = binary.Right;
            }
            else if (binary.Right is MemberExpression rightMember && rightMember.Member is PropertyInfo rightProp)
            {
                propertyName = rightProp.Name;
                valueExpression = binary.Left;
            }
            else if (binary.Left is MethodCallExpression leftMethod &&
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

            return new FirestoreWhereClause(propertyName, firestoreOperator.Value, valueExpression);
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
