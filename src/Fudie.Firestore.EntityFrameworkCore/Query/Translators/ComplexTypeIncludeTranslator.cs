using Fudie.Firestore.EntityFrameworkCore.Infrastructure;
using Fudie.Firestore.EntityFrameworkCore.Query.Ast;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Translators
{
    /// <summary>
    /// Translator that extracts Include expressions targeting ComplexType properties,
    /// converts them to IncludeInfo, and removes them from the expression tree
    /// to prevent EF Core from rejecting them.
    /// </summary>
    /// <remarks>
    /// Pattern: e => e.ComplexTypeProperty.NavigationProperty
    /// Example: e => e.DireccionPrincipal.SucursalCercana
    ///
    /// The translator:
    /// 1. Detects if an Include targets a property inside a ComplexType
    /// 2. Converts it to IncludeInfo with the full path (e.g., "DireccionPrincipal.SucursalCercana")
    /// 3. Stores the IncludeInfo in FirestoreQueryCompilationContext
    /// 4. Removes the Include from the expression tree so EF Core doesn't reject it
    /// </remarks>
    internal class ComplexTypeIncludeTranslator : ExpressionVisitor
    {
        private readonly FirestoreQueryCompilationContext _firestoreContext;
        private readonly IFirestoreCollectionManager _collectionManager;

        public ComplexTypeIncludeTranslator(
            QueryCompilationContext queryCompilationContext,
            IFirestoreCollectionManager collectionManager)
        {
            _firestoreContext = (FirestoreQueryCompilationContext)queryCompilationContext;
            _collectionManager = collectionManager;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // Check if this is an Include call
            if (node.Method.Name == "Include" &&
                node.Method.DeclaringType == typeof(Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions))
            {
                // Get the lambda expression (second argument)
                LambdaExpression? lambda = null;
                if (node.Arguments.Count >= 2)
                {
                    if (node.Arguments[1] is UnaryExpression unary && unary.Operand is LambdaExpression lambdaFromUnary)
                    {
                        lambda = lambdaFromUnary;
                    }
                    else if (node.Arguments[1] is LambdaExpression lambdaDirect)
                    {
                        lambda = lambdaDirect;
                    }
                }

                if (lambda != null && IsComplexTypeInclude(lambda.Body))
                {
                    // Convert to IncludeInfo and store
                    var includeInfo = TranslateToIncludeInfo(lambda);
                    if (includeInfo != null)
                    {
                        _firestoreContext.AddComplexTypeInclude(includeInfo);
                    }

                    // Remove the Include from the expression tree
                    return Visit(node.Arguments[0]);
                }
            }

            return base.VisitMethodCall(node);
        }

        /// <summary>
        /// Determines if the Include expression targets a property inside a ComplexType.
        /// </summary>
        private bool IsComplexTypeInclude(Expression expression)
        {
            if (expression is MemberExpression memberExpr)
            {
                if (memberExpr.Expression is MemberExpression parentMemberExpr)
                {
                    var parentProperty = parentMemberExpr.Member as PropertyInfo;
                    if (parentProperty != null)
                    {
                        var rootType = GetRootEntityType(parentMemberExpr);
                        var entityType = _firestoreContext.Model.FindEntityType(rootType);

                        if (entityType != null)
                        {
                            var complexProperty = entityType.FindComplexProperty(parentProperty.Name);
                            if (complexProperty != null)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Converts a ComplexType Include lambda to IncludeInfo.
        /// </summary>
        private IncludeInfo? TranslateToIncludeInfo(LambdaExpression includeExpression)
        {
            // Pattern: e => e.DireccionPrincipal.SucursalCercana
            if (includeExpression.Body is not MemberExpression navigationMember)
                return null;

            // Get the navigation property (e.g., SucursalCercana)
            var targetClrType = navigationMember.Type;

            // Get the ComplexType property (e.g., DireccionPrincipal)
            if (navigationMember.Expression is not MemberExpression complexTypeMember)
                return null;

            var complexTypePropertyName = complexTypeMember.Member.Name;
            var navigationPropertyName = navigationMember.Member.Name;

            // Build the full path for document data lookup
            var fullNavigationPath = $"{complexTypePropertyName}.{navigationPropertyName}";

            // Get collection name for the target type
            var collectionName = _collectionManager.GetCollectionName(targetClrType);

            // References in ComplexTypes are always single (not collection)
            return new IncludeInfo(
                navigationName: fullNavigationPath,
                isCollection: false,
                collectionName: collectionName,
                targetClrType: targetClrType);
        }

        /// <summary>
        /// Gets the root entity type from a member expression chain.
        /// </summary>
        private Type GetRootEntityType(MemberExpression memberExpr)
        {
            var current = memberExpr.Expression;
            while (current is MemberExpression parent)
            {
                current = parent.Expression;
            }

            if (current is ParameterExpression param)
            {
                return param.Type;
            }

            return current?.Type ?? typeof(object);
        }
    }
}
