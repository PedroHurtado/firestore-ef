using Fudie.Firestore.EntityFrameworkCore.Infrastructure;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;
using Fudie.Firestore.EntityFrameworkCore.Query.Ast;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Translators
{
    /// <summary>
    /// Translator that extracts Include expressions targeting ArrayOf properties,
    /// converts them to IncludeInfo (for References), and removes them from the expression tree
    /// to prevent EF Core from rejecting them.
    /// </summary>
    /// <remarks>
    /// Handles three cases:
    /// 1. ArrayOf Reference: e => e.Proveedores - generates IncludeInfo, removes Include
    /// 2. ArrayOf Embedded: e => e.Secciones - removes Include without IncludeInfo (data comes with parent)
    /// 3. ThenInclude chains on Embedded elements:
    ///    .Include(r => r.Menus).ThenInclude(m => m.Secciones).ThenInclude(s => s.Items).ThenInclude(i => i.Plato)
    ///    - Detects the entire chain of ThenIncludes on Embedded arrays
    ///    - Generates IncludeInfo with full path (e.g., "Menus.Secciones.Items.Plato")
    ///    - Removes the entire chain from the expression tree
    /// </remarks>
    internal class ArrayOfIncludeTranslator : ExpressionVisitor
    {
        private readonly FirestoreQueryCompilationContext _firestoreContext;
        private readonly IFirestoreCollectionManager _collectionManager;

        // Track removed ArrayOf Embedded includes so ThenIncludes can reference them
        // Key: element type of the removed Include, Value: property name
        private readonly Dictionary<Type, string> _removedEmbeddedIncludes = [];

        public ArrayOfIncludeTranslator(
            QueryCompilationContext queryCompilationContext,
            IFirestoreCollectionManager collectionManager)
        {
            _firestoreContext = (FirestoreQueryCompilationContext)queryCompilationContext;
            _collectionManager = collectionManager;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // Check if this is an Include or ThenInclude call
            if ((node.Method.Name == "Include" || node.Method.Name == "ThenInclude") &&
                node.Method.DeclaringType == typeof(Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions))
            {
                var isThenInclude = node.Method.Name == "ThenInclude";

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

                if (lambda != null)
                {
                    // Case 1: ArrayOf Reference Include/ThenInclude (on entity)
                    if (IsArrayOfReferenceInclude(lambda))
                    {
                        // For ThenInclude, the parameter type is the parent entity type
                        Type? parentClrType = isThenInclude ? lambda.Parameters[0].Type : null;

                        // Convert to IncludeInfo and store
                        var includeInfo = TranslateToIncludeInfo(lambda, parentClrType);
                        if (includeInfo != null)
                        {
                            _firestoreContext.AddArrayOfInclude(includeInfo);
                        }

                        // Remove the Include/ThenInclude from the expression tree
                        return Visit(node.Arguments[0]);
                    }

                    // Case 2: ArrayOf Embedded Include (direct, no ThenInclude chain)
                    if (!isThenInclude && IsArrayOfEmbeddedInclude(lambda))
                    {
                        // Track this for potential ThenIncludes
                        TrackRemovedEmbeddedInclude(lambda);

                        // Remove the Include from the expression tree (data comes with parent)
                        return Visit(node.Arguments[0]);
                    }

                    // Case 3: Detect chain of Include/ThenInclude on ArrayOf Embedded (root entity)
                    // This handles: .Include(r => r.Menus).ThenInclude(m => m.Secciones).ThenInclude(s => s.Items).ThenInclude(i => i.Plato)
                    if (TryExtractEmbeddedIncludeChain(node, out var chainPath, out var sourceExpression, out var targetInfo))
                    {
                        if (targetInfo.HasValue)
                        {
                            // Generate IncludeInfo with the full path
                            var includeInfo = new IncludeInfo(
                                navigationName: chainPath,
                                isCollection: false,
                                collectionName: _collectionManager.GetCollectionName(targetInfo.Value.TargetType),
                                targetClrType: targetInfo.Value.TargetType,
                                parentClrType: null);

                            _firestoreContext.AddArrayOfInclude(includeInfo);
                        }

                        // Skip the entire chain and continue from the source
                        return Visit(sourceExpression);
                    }

                    // Case 4: Detect ThenInclude chain on SubCollection that targets ArrayOf Embedded with Reference
                    // This handles: .Include(m => m.Categories).ThenInclude(c => c.Items).ThenInclude(i => i.MenuItem)
                    // Where Categories is SubCollection, Items is ArrayOf Embedded, MenuItem is Reference
                    if (TryExtractSubCollectionArrayOfChain(node, out var subCollectionChainPath, out var subCollectionSource, out var subCollectionTargetInfo, out var parentEntityType))
                    {
                        if (subCollectionTargetInfo.HasValue && parentEntityType != null)
                        {
                            // Generate IncludeInfo with the path relative to SubCollection entity
                            // ParentClrType is the SubCollection entity type (e.g., MenuCategory)
                            var includeInfo = new IncludeInfo(
                                navigationName: subCollectionChainPath,
                                isCollection: false,
                                collectionName: _collectionManager.GetCollectionName(subCollectionTargetInfo.Value.TargetType),
                                targetClrType: subCollectionTargetInfo.Value.TargetType,
                                parentClrType: parentEntityType);

                            _firestoreContext.AddArrayOfInclude(includeInfo);
                        }

                        // Skip the ThenInclude chain but KEEP the SubCollection Include for EF Core
                        return Visit(subCollectionSource);
                    }
                }
            }

            return base.VisitMethodCall(node);
        }

        /// <summary>
        /// Tries to extract a chain of Include/ThenInclude calls that target ArrayOf Embedded properties,
        /// ending with a reference to an entity.
        /// </summary>
        /// <returns>True if a chain was found and should be removed</returns>
        private bool TryExtractEmbeddedIncludeChain(
            MethodCallExpression node,
            out string chainPath,
            out Expression sourceExpression,
            out (Type TargetType, bool IsCollection)? targetInfo)
        {
            chainPath = "";
            sourceExpression = node;
            targetInfo = null;

            // Build the chain by walking down the expression tree
            var pathParts = new List<string>();
            var current = node;
            Expression? rootSource = null;
            (Type TargetType, bool IsCollection)? finalTarget = null;

            while (current != null)
            {
                if (current.Method.DeclaringType != typeof(Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions))
                    break;

                if (current.Method.Name != "Include" && current.Method.Name != "ThenInclude")
                    break;

                // Get lambda from this node
                var lambda = ExtractLambda(current);
                if (lambda == null)
                    break;

                // Get property info from lambda
                if (lambda.Body is not MemberExpression memberExpr)
                    break;

                var propertyInfo = memberExpr.Member as PropertyInfo;
                if (propertyInfo == null)
                    break;

                var propertyName = propertyInfo.Name;
                var propertyType = propertyInfo.PropertyType;
                var parameterType = lambda.Parameters[0].Type;

                // Determine the target type for the final reference
                if (propertyType.IsGenericType)
                {
                    var elementType = propertyType.GetGenericArguments()[0];

                    // Is the element type an entity?
                    var entityType = _firestoreContext.Model.FindEntityType(elementType);
                    if (entityType != null)
                    {
                        // Check if the parent type is an entity with ArrayOf annotation
                        var parentEntityType = _firestoreContext.Model.FindEntityType(parameterType);
                        if (parentEntityType != null && parentEntityType.IsArrayOfReference(propertyName))
                        {
                            // ArrayOf Reference on entity - this is the end of an Embedded chain
                            finalTarget = (elementType, true);
                        }
                        else if (parentEntityType == null)
                        {
                            // Parent is not an entity (it's a ValueObject), assume this is a Reference
                            finalTarget = (elementType, true);
                        }
                        // else: ArrayOf Embedded on entity - continue the chain
                    }
                    // else: Element is not an entity - this is an Embedded property, continue the chain
                }
                else
                {
                    // Single property (not collection)
                    var entityType = _firestoreContext.Model.FindEntityType(propertyType);
                    if (entityType != null)
                    {
                        // This is a reference to an entity
                        finalTarget = (propertyType, false);
                    }
                }

                // Add to path
                pathParts.Insert(0, propertyName);

                // Check if this is the root Include
                if (current.Method.Name == "Include")
                {
                    // Check if this Include targets an ArrayOf Embedded on entity
                    var entityType = _firestoreContext.Model.FindEntityType(parameterType);
                    if (entityType != null && entityType.IsArrayOfEmbedded(propertyName))
                    {
                        // Found the root of an Embedded chain
                        rootSource = current.Arguments[0];
                        break;
                    }
                    else
                    {
                        // Not an Embedded Include, stop
                        break;
                    }
                }

                // Move to parent (ThenInclude's source is Arg[0])
                if (current.Arguments[0] is MethodCallExpression parentCall)
                {
                    current = parentCall;
                }
                else
                {
                    break;
                }
            }

            // We have a valid chain if:
            // 1. We found a root Include on ArrayOf Embedded
            // 2. We have at least 2 path parts (Embedded + something)
            if (rootSource != null && pathParts.Count >= 2)
            {
                chainPath = string.Join(".", pathParts);
                sourceExpression = rootSource;
                targetInfo = finalTarget;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tries to extract a chain of ThenInclude calls after a SubCollection Include,
        /// where the ThenIncludes target ArrayOf Embedded properties with References.
        /// </summary>
        /// <remarks>
        /// Pattern: .Include(m => m.Categories).ThenInclude(c => c.Items).ThenInclude(i => i.MenuItem)
        /// Where:
        /// - Categories is a SubCollection (EF Core navigation)
        /// - Items is ArrayOf Embedded on MenuCategory
        /// - MenuItem is a Reference inside CategoryItem
        ///
        /// Returns the path "Items.MenuItem" with parentEntityType = typeof(MenuCategory)
        /// and sourceExpression = the Include(Categories) call (so EF Core still processes it)
        /// </remarks>
        private bool TryExtractSubCollectionArrayOfChain(
            MethodCallExpression node,
            out string chainPath,
            out Expression sourceExpression,
            out (Type TargetType, bool IsCollection)? targetInfo,
            out Type? parentEntityType)
        {
            chainPath = "";
            sourceExpression = node;
            targetInfo = null;
            parentEntityType = null;

            // Build the chain by walking down the expression tree
            var pathParts = new List<string>();
            var current = node;
            Expression? subCollectionInclude = null;
            Type? subCollectionTargetType = null;
            (Type TargetType, bool IsCollection)? finalTarget = null;

            while (current != null)
            {
                if (current.Method.DeclaringType != typeof(Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions))
                    break;

                if (current.Method.Name != "Include" && current.Method.Name != "ThenInclude")
                    break;

                // Get lambda from this node
                var lambda = ExtractLambda(current);
                if (lambda == null)
                    break;

                // Get property info from lambda
                if (lambda.Body is not MemberExpression memberExpr)
                    break;

                var propertyInfo = memberExpr.Member as PropertyInfo;
                if (propertyInfo == null)
                    break;

                var propertyName = propertyInfo.Name;
                var propertyType = propertyInfo.PropertyType;
                var parameterType = lambda.Parameters[0].Type;

                // Check if this is the root Include targeting a SubCollection
                if (current.Method.Name == "Include")
                {
                    var entityType = _firestoreContext.Model.FindEntityType(parameterType);
                    if (entityType != null)
                    {
                        // Check if this is a navigation (SubCollection)
                        var navigation = entityType.FindNavigation(propertyName);
                        if (navigation != null && navigation.IsCollection)
                        {
                            // Found SubCollection Include - this is the root
                            // We need at least one ThenInclude after this for ArrayOf
                            if (pathParts.Count > 0)
                            {
                                subCollectionInclude = current;
                                subCollectionTargetType = navigation.TargetEntityType.ClrType;
                            }
                            break;
                        }
                    }
                    // Not a SubCollection Include, stop
                    break;
                }

                // This is a ThenInclude - check what it targets
                var parentEntityTypeForProperty = _firestoreContext.Model.FindEntityType(parameterType);

                if (propertyType.IsGenericType)
                {
                    var elementType = propertyType.GetGenericArguments()[0];

                    // Check if the element type is an entity (Reference target)
                    var elementEntityType = _firestoreContext.Model.FindEntityType(elementType);
                    if (elementEntityType != null)
                    {
                        // Check if this is ArrayOf Reference on an entity
                        if (parentEntityTypeForProperty != null && parentEntityTypeForProperty.IsArrayOfReference(propertyName))
                        {
                            // ArrayOf Reference - this is the end of the chain
                            finalTarget = (elementType, true);
                            pathParts.Insert(0, propertyName);
                        }
                        // else: Could be navigation or ArrayOf Embedded with entity elements
                    }
                    else
                    {
                        // Element is not an entity - this is ArrayOf Embedded, continue
                        pathParts.Insert(0, propertyName);
                    }
                }
                else
                {
                    // Single property (not collection) - could be Reference inside Embedded
                    var singleEntityType = _firestoreContext.Model.FindEntityType(propertyType);
                    if (singleEntityType != null)
                    {
                        // This is a reference to an entity
                        finalTarget = (propertyType, false);
                        pathParts.Insert(0, propertyName);
                    }
                }

                // Move to parent (ThenInclude's source is Arg[0])
                if (current.Arguments[0] is MethodCallExpression parentCall)
                {
                    current = parentCall;
                }
                else
                {
                    break;
                }
            }

            // We have a valid chain if:
            // 1. We found a root Include on SubCollection
            // 2. We have path parts (ArrayOf + Reference)
            // 3. We have a final target (Reference entity)
            if (subCollectionInclude != null && pathParts.Count > 0 && finalTarget.HasValue)
            {
                chainPath = string.Join(".", pathParts);
                sourceExpression = subCollectionInclude;
                targetInfo = finalTarget;
                parentEntityType = subCollectionTargetType;
                return true;
            }

            return false;
        }

        private static LambdaExpression? ExtractLambda(MethodCallExpression node)
        {
            if (node.Arguments.Count < 2)
                return null;

            if (node.Arguments[1] is UnaryExpression unary && unary.Operand is LambdaExpression lambdaFromUnary)
                return lambdaFromUnary;

            if (node.Arguments[1] is LambdaExpression lambdaDirect)
                return lambdaDirect;

            return null;
        }

        /// <summary>
        /// Determines if the Include expression targets a property marked as ArrayOf Reference.
        /// </summary>
        private bool IsArrayOfReferenceInclude(LambdaExpression lambda)
        {
            if (lambda.Body is not MemberExpression memberExpr)
                return false;

            // Get the property name
            var propertyName = memberExpr.Member.Name;

            // Get the entity type from the parameter
            var entityClrType = lambda.Parameters[0].Type;
            var entityType = _firestoreContext.Model.FindEntityType(entityClrType);

            if (entityType == null)
                return false;

            // Check if this property is marked as ArrayOf Reference
            return entityType.IsArrayOfReference(propertyName);
        }

        /// <summary>
        /// Determines if the Include expression targets a property marked as ArrayOf Embedded.
        /// </summary>
        private bool IsArrayOfEmbeddedInclude(LambdaExpression lambda)
        {
            if (lambda.Body is not MemberExpression memberExpr)
                return false;

            // Get the property name
            var propertyName = memberExpr.Member.Name;

            // Get the entity type from the parameter
            var entityClrType = lambda.Parameters[0].Type;
            var entityType = _firestoreContext.Model.FindEntityType(entityClrType);

            if (entityType == null)
                return false;

            // Check if this property is marked as ArrayOf Embedded
            return entityType.IsArrayOfEmbedded(propertyName);
        }

        /// <summary>
        /// Tracks a removed ArrayOf Embedded Include for ThenInclude resolution.
        /// </summary>
        private void TrackRemovedEmbeddedInclude(LambdaExpression lambda)
        {
            if (lambda.Body is not MemberExpression memberExpr)
                return;

            var propertyName = memberExpr.Member.Name;
            var propertyInfo = memberExpr.Member as PropertyInfo;
            if (propertyInfo == null)
                return;

            // Get the element type from the collection
            var propertyType = propertyInfo.PropertyType;
            if (propertyType.IsGenericType)
            {
                var elementType = propertyType.GetGenericArguments()[0];
                _removedEmbeddedIncludes[elementType] = propertyName;
            }
        }

        /// <summary>
        /// Converts an ArrayOf Reference Include lambda to IncludeInfo.
        /// </summary>
        /// <param name="includeExpression">The lambda expression from Include/ThenInclude</param>
        /// <param name="parentClrType">For ThenInclude, the parent entity type; null for direct Include</param>
        private IncludeInfo? TranslateToIncludeInfo(LambdaExpression includeExpression, Type? parentClrType)
        {
            // Pattern: e => e.Proveedores
            if (includeExpression.Body is not MemberExpression memberExpr)
                return null;

            var propertyName = memberExpr.Member.Name;
            var propertyInfo = memberExpr.Member as PropertyInfo;
            if (propertyInfo == null)
                return null;

            // Get the element type from the collection
            var propertyType = propertyInfo.PropertyType;
            Type? elementType = null;

            if (propertyType.IsGenericType)
            {
                elementType = propertyType.GetGenericArguments()[0];
            }

            if (elementType == null)
                return null;

            // Get collection name for the target type
            var collectionName = _collectionManager.GetCollectionName(elementType);

            // ArrayOf References are treated as references (not subcollections)
            // The executor will read the array of DocumentReferences from the parent document
            // and load each one individually
            return new IncludeInfo(
                navigationName: propertyName,
                isCollection: false,
                collectionName: collectionName,
                targetClrType: elementType,
                parentClrType: parentClrType);
        }
    }
}
