using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Metadata.Conventions;
using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Projections;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Firestore.EntityFrameworkCore.Query.Translators
{
    /// <summary>
    /// Visitor especializado para extraer información de proyección de una LambdaExpression.
    /// Detecta el tipo de proyección y extrae los campos proyectados.
    ///
    /// Casos soportados:
    /// 1. SingleField: e => e.Name
    /// 2. AnonymousType: e => new { e.Id, e.Name }
    /// 3. DtoClass: e => new Dto { Id = e.Id }
    /// 4. Record: e => new Record(e.Id, e.Name)
    /// 5. ComplexType: e => e.Direccion
    /// 6. Nested field: e => e.Direccion.Ciudad
    /// 7. With Subcollections: e => new { e.Nombre, e.Pedidos }
    /// 8. Subcollection with operations: e => new { Items = e.Pedidos.Where().OrderBy().Take() }
    /// </summary>
    internal class ProjectionExtractionVisitor : ExpressionVisitor
    {
        private readonly IFirestoreCollectionManager _collectionManager;
        private readonly IEntityType? _entityType;
        private readonly FirestoreWhereTranslator _whereTranslator = new();
        private readonly FirestoreOrderByTranslator _orderByTranslator = new();
        private readonly FirestoreLimitTranslator _limitTranslator = new();

        /// <summary>
        /// Creates a new ProjectionExtractionVisitor with the required dependencies.
        /// </summary>
        /// <param name="collectionManager">Manager for resolving Firestore collection names.</param>
        /// <param name="entityType">The source entity type for navigation resolution.</param>
        public ProjectionExtractionVisitor(IFirestoreCollectionManager collectionManager, IEntityType? entityType = null)
        {
            _collectionManager = collectionManager ?? throw new ArgumentNullException(nameof(collectionManager));
            _entityType = entityType;
        }

        /// <summary>
        /// Campos extraídos de la proyección.
        /// </summary>
        public List<FirestoreProjectedField> Fields { get; } = new();

        /// <summary>
        /// Subcollections extraídas de la proyección.
        /// </summary>
        public List<FirestoreSubcollectionProjection> Subcollections { get; } = new();

        /// <summary>
        /// Tipo de resultado detectado.
        /// </summary>
        public ProjectionResultType ResultType { get; private set; } = ProjectionResultType.Entity;

        /// <summary>
        /// Tipo CLR del resultado de la proyección.
        /// </summary>
        public Type? ClrType { get; private set; }

        /// <summary>
        /// Extrae la definición de proyección de una LambdaExpression.
        /// </summary>
        public FirestoreProjectionDefinition? Extract(LambdaExpression selector)
        {
            if (selector == null)
                return null;

            var body = selector.Body;
            ClrType = body.Type;

            // Case 1: Identity projection (e => e)
            if (body == selector.Parameters[0])
                return null; // No projection needed

            // Case 2: Type conversion (e => (BaseType)e)
            if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            {
                if (unary.Operand == selector.Parameters[0])
                    return null; // No projection needed
            }

            // Analyze the body to determine projection type
            AnalyzeExpression(body, selector.Parameters[0]);

            return BuildProjectionDefinition();
        }

        private void AnalyzeExpression(Expression expression, ParameterExpression parameter)
        {
            switch (expression)
            {
                // Case: e => e.Name or e => e.Direccion or e => e.Direccion.Ciudad
                case MemberExpression memberExpr:
                    AnalyzeMemberExpression(memberExpr, parameter);
                    break;

                // Case: e => new { e.Id, e.Name } (anonymous type)
                case NewExpression newExpr when IsAnonymousType(newExpr.Type):
                    AnalyzeAnonymousTypeExpression(newExpr, parameter);
                    break;

                // Case: e => new Record(e.Id, e.Name) (record or class with constructor)
                case NewExpression newExpr:
                    AnalyzeConstructorExpression(newExpr, parameter);
                    break;

                // Case: e => new Dto { Id = e.Id, Name = e.Name } (member init)
                case MemberInitExpression memberInitExpr:
                    AnalyzeMemberInitExpression(memberInitExpr, parameter);
                    break;

                // Case: Method call (could be subcollection operation)
                case MethodCallExpression methodCallExpr:
                    AnalyzeMethodCallExpression(methodCallExpr, parameter);
                    break;

                // Unsupported: e => e.Price * 1.21m
                case BinaryExpression binaryExpr:
                    throw new NotSupportedException(
                        $"Binary expressions are not supported in projections: {binaryExpr.NodeType}");

                // Unsupported: e => e.Stock > 0 ? "A" : "B"
                case ConditionalExpression conditionalExpr:
                    throw new NotSupportedException(
                        "Conditional expressions (ternary operator) are not supported in projections.");

                // Unsupported: Constants or closures not derived from entity
                case ConstantExpression constantExpr:
                    throw new NotSupportedException(
                        "Constant values in projections are not supported. All projected values must come from entity properties.");

                default:
                    throw new NotSupportedException(
                        $"Expression type '{expression.NodeType}' is not supported in projections.");
            }
        }

        /// <summary>
        /// Analyzes a member access expression (e.g., e.Name, e.Direccion.Ciudad)
        /// </summary>
        private void AnalyzeMemberExpression(MemberExpression memberExpr, ParameterExpression parameter)
        {
            ResultType = ProjectionResultType.SingleField;
            ClrType = memberExpr.Type;

            var fieldPath = BuildFieldPath(memberExpr);
            var resultName = memberExpr.Member.Name;

            // Check if this is a navigation property (subcollection)
            if (IsCollectionNavigation(memberExpr))
            {
                var subcollection = CreateSubcollectionProjection(memberExpr.Member.Name, memberExpr.Member.Name);
                Subcollections.Add(subcollection);
            }
            else
            {
                Fields.Add(new FirestoreProjectedField(fieldPath, resultName, memberExpr.Type));
            }
        }

        /// <summary>
        /// Analyzes an anonymous type expression (e.g., new { e.Id, e.Name })
        /// </summary>
        private void AnalyzeAnonymousTypeExpression(NewExpression newExpr, ParameterExpression parameter)
        {
            ResultType = ProjectionResultType.AnonymousType;
            ClrType = newExpr.Type;

            for (int i = 0; i < newExpr.Arguments.Count; i++)
            {
                var argument = newExpr.Arguments[i];
                var member = newExpr.Members?[i];
                var resultName = member?.Name ?? $"Item{i}";

                ProcessProjectionArgument(argument, resultName, parameter);
            }
        }

        /// <summary>
        /// Analyzes a constructor expression (e.g., new Record(e.Id, e.Name))
        /// </summary>
        private void AnalyzeConstructorExpression(NewExpression newExpr, ParameterExpression parameter)
        {
            ResultType = ProjectionResultType.Record;
            ClrType = newExpr.Type;

            var constructorParams = newExpr.Constructor?.GetParameters() ?? Array.Empty<ParameterInfo>();

            for (int i = 0; i < newExpr.Arguments.Count; i++)
            {
                var argument = newExpr.Arguments[i];
                var paramName = i < constructorParams.Length ? constructorParams[i].Name ?? $"arg{i}" : $"arg{i}";

                ProcessProjectionArgument(argument, paramName, parameter, constructorParameterIndex: i);
            }
        }

        /// <summary>
        /// Analyzes a member init expression (e.g., new Dto { Id = e.Id })
        /// </summary>
        private void AnalyzeMemberInitExpression(MemberInitExpression memberInitExpr, ParameterExpression parameter)
        {
            ResultType = ProjectionResultType.DtoClass;
            ClrType = memberInitExpr.Type;

            foreach (var binding in memberInitExpr.Bindings)
            {
                if (binding is MemberAssignment assignment)
                {
                    var resultName = assignment.Member.Name;
                    ProcessProjectionArgument(assignment.Expression, resultName, parameter);
                }
            }
        }

        /// <summary>
        /// Analyzes a method call expression (could be a subcollection with operations or EF.Property access)
        /// </summary>
        private void AnalyzeMethodCallExpression(MethodCallExpression methodCallExpr, ParameterExpression parameter)
        {
            // Check if this is EF.Property<T>(entity, "PropertyName") - used by EF Core for array properties
            if (IsEfPropertyCall(methodCallExpr, out var propertyName))
            {
                ResultType = ProjectionResultType.SingleField;
                ClrType = methodCallExpr.Type;
                Fields.Add(new FirestoreProjectedField(propertyName!, propertyName!, methodCallExpr.Type));
                return;
            }

            // Check if this is a LINQ operation on a navigation property
            var navigationInfo = ExtractNavigationFromMethodChain(methodCallExpr);
            if (navigationInfo != null)
            {
                var subcollection = CreateSubcollectionProjection(
                    navigationInfo.NavigationName,
                    navigationInfo.NavigationName);

                ExtractSubcollectionOperations(methodCallExpr, subcollection);
                Subcollections.Add(subcollection);
            }
            else
            {
                // Method call on property: e.Name.ToUpper()
                throw new NotSupportedException(
                    $"Method calls in projections are not supported: {methodCallExpr.Method.Name}. " +
                    "Projections must reference entity properties directly.");
            }
        }

        /// <summary>
        /// Checks if a method call is EF.Property&lt;T&gt;(entity, "PropertyName").
        /// EF Core uses this for accessing array/collection properties in projections.
        /// </summary>
        private static bool IsEfPropertyCall(MethodCallExpression methodCall, out string? propertyName)
        {
            propertyName = null;

            // EF.Property is a static method named "Property" on EF class
            if (methodCall.Method.Name != "Property" || !methodCall.Method.IsStatic)
                return false;

            // Should have 2 arguments: entity and property name
            if (methodCall.Arguments.Count != 2)
                return false;

            // Second argument should be a constant string (property name)
            if (methodCall.Arguments[1] is ConstantExpression constantExpr && constantExpr.Value is string name)
            {
                propertyName = name;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Processes a projection argument, determining if it's a field or subcollection.
        /// </summary>
        private void ProcessProjectionArgument(
            Expression argument,
            string resultName,
            ParameterExpression parameter,
            int constructorParameterIndex = -1)
        {
            switch (argument)
            {
                case MemberExpression memberExpr when IsCollectionNavigation(memberExpr):
                    // Subcollection: e.Pedidos
                    var subcollection = CreateSubcollectionProjection(memberExpr.Member.Name, resultName);
                    Subcollections.Add(subcollection);
                    break;

                case MemberExpression memberExpr when IsClosure(memberExpr):
                    // Closure/captured variable: var fecha = DateTime.Now; new { Fecha = fecha }
                    throw new NotSupportedException(
                        $"Closures and captured variables are not supported in projections. Field '{resultName}' references a closure.");

                case MemberExpression memberExpr:
                    // Regular field: e.Name or e.Direccion.Ciudad
                    var fieldPath = BuildFieldPath(memberExpr);
                    Fields.Add(new FirestoreProjectedField(fieldPath, resultName, memberExpr.Type, constructorParameterIndex));
                    break;

                case MethodCallExpression methodCallExpr:
                    // Check if this is EF.Property<T>(entity, "PropertyName") - used for array properties
                    if (IsEfPropertyCall(methodCallExpr, out var efPropertyName))
                    {
                        Fields.Add(new FirestoreProjectedField(efPropertyName!, resultName, methodCallExpr.Type, constructorParameterIndex));
                    }
                    // Check if this is a subcollection operation
                    else if (ExtractNavigationFromMethodChain(methodCallExpr) is { } navInfo)
                    {
                        var subProj = CreateSubcollectionProjection(navInfo.NavigationName, resultName, navInfo.TargetClrType);
                        ExtractSubcollectionOperations(methodCallExpr, subProj);
                        Subcollections.Add(subProj);
                    }
                    // Handle Enum.ToString() - required by compiler for projecting enums to string
                    else if (IsEnumToStringCall(methodCallExpr, out var enumMemberExpr))
                    {
                        var enumFieldPath = BuildFieldPath(enumMemberExpr!);
                        // Store as string type since Firestore returns enums as strings
                        Fields.Add(new FirestoreProjectedField(enumFieldPath, resultName, typeof(string), constructorParameterIndex));
                    }
                    else
                    {
                        // Debug: show what expression we got
                        var rootExpr = GetRootExpression(methodCallExpr);
                        throw new NotSupportedException(
                            $"Method calls in projections are not supported: {methodCallExpr.Method.Name}. " +
                            $"Root expression type: {rootExpr?.GetType().Name ?? "null"}, " +
                            $"Root: {rootExpr}. " +
                            "Projections must reference entity properties directly.");
                    }
                    break;

                case UnaryExpression unaryExpr when unaryExpr.NodeType == ExpressionType.Convert:
                    // Handle type conversions
                    ProcessProjectionArgument(unaryExpr.Operand, resultName, parameter, constructorParameterIndex);
                    break;

                // Handle nested anonymous types: new { Resumen = new { Sum = ..., Count = ... } }
                case NewExpression nestedNewExpr when IsAnonymousType(nestedNewExpr.Type):
                    ProcessNestedAnonymousType(nestedNewExpr, resultName, parameter, constructorParameterIndex);
                    break;

                // Handle EF Core's MaterializeCollectionNavigationExpression (Extension node type)
                case Expression expr when expr.NodeType == ExpressionType.Extension:
                    ProcessExtensionExpression(expr, resultName, parameter, constructorParameterIndex);
                    break;

                case ConstantExpression:
                    throw new NotSupportedException(
                        $"Constant values are not supported in projections. Field '{resultName}' uses a constant.");

                case BinaryExpression binaryExpr:
                    throw new NotSupportedException(
                        $"Binary expressions are not supported in projections: {binaryExpr.NodeType}");

                case ConditionalExpression:
                    throw new NotSupportedException(
                        "Conditional expressions (ternary operator) are not supported in projections.");

                default:
                    throw new NotSupportedException(
                        $"Expression type '{argument.NodeType}' is not supported in projection field '{resultName}'.");
            }
        }

        /// <summary>
        /// Processes EF Core extension expressions like MaterializeCollectionNavigationExpression.
        /// </summary>
        private void ProcessExtensionExpression(
            Expression expr,
            string resultName,
            ParameterExpression parameter,
            int constructorParameterIndex)
        {
            // Handle MaterializeCollectionNavigationExpression directly using EF Core's public API
            if (expr is MaterializeCollectionNavigationExpression materializeExpr)
            {
                var navigation = materializeExpr.Navigation;
                if (navigation != null)
                {
                    var subcollection = CreateSubcollectionProjection(navigation.Name, resultName);

                    // Extract operations from Subquery (Where, OrderBy, Take, Select, aggregations)
                    if (materializeExpr.Subquery is MethodCallExpression subqueryMethodCall)
                    {
                        ExtractSubcollectionOperations(subqueryMethodCall, subcollection);
                    }

                    Subcollections.Add(subcollection);
                    return;
                }

                // Fallback: try to extract from Subquery
                if (materializeExpr.Subquery != null)
                {
                    var navInfo = ExtractNavigationFromExpression(materializeExpr.Subquery);
                    if (navInfo != null)
                    {
                        var subcollection = CreateSubcollectionProjection(navInfo.NavigationName, resultName, navInfo.TargetClrType);

                        // Extract operations from Subquery
                        if (materializeExpr.Subquery is MethodCallExpression subqueryMethodCall)
                        {
                            ExtractSubcollectionOperations(subqueryMethodCall, subcollection);
                        }

                        Subcollections.Add(subcollection);
                        return;
                    }
                }

                // Last resort: use the result name and expression type to create subcollection
                var elementType = GetCollectionElementType(expr.Type);
                if (elementType != null)
                {
                    var subcollection = CreateSubcollectionProjection(resultName, resultName, elementType);

                    // Extract operations from Subquery
                    if (materializeExpr.Subquery is MethodCallExpression subqueryMethodCall)
                    {
                        ExtractSubcollectionOperations(subqueryMethodCall, subcollection);
                    }

                    Subcollections.Add(subcollection);
                    return;
                }
            }

            throw new NotSupportedException(
                $"Extension expression type '{expr.GetType().Name}' is not supported in projection field '{resultName}'.");
        }

        /// <summary>
        /// Extracts navigation info from an expression tree.
        /// </summary>
        private NavigationInfo? ExtractNavigationFromExpression(Expression expression)
        {
            // Try to find navigation from method chain
            if (expression is MethodCallExpression methodCall)
            {
                return ExtractNavigationFromMethodChain(methodCall);
            }

            // Try to find navigation from member expression
            if (expression is MemberExpression memberExpr && IsCollectionNavigation(memberExpr))
            {
                return new NavigationInfo(memberExpr.Member.Name);
            }

            return null;
        }

        /// <summary>
        /// Gets the element type from a collection type.
        /// </summary>
        private static Type? GetCollectionElementType(Type collectionType)
        {
            if (collectionType.IsGenericType)
            {
                var genericDef = collectionType.GetGenericTypeDefinition();
                if (genericDef == typeof(IEnumerable<>) ||
                    genericDef == typeof(ICollection<>) ||
                    genericDef == typeof(IList<>) ||
                    genericDef == typeof(List<>) ||
                    genericDef == typeof(HashSet<>))
                {
                    return collectionType.GetGenericArguments()[0];
                }
            }

            // Check interfaces for other collection types
            var enumerableInterface = collectionType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            return enumerableInterface?.GetGenericArguments()[0];
        }

        /// <summary>
        /// Checks if a member expression is accessing a closure (captured variable).
        /// </summary>
        private static bool IsClosure(MemberExpression memberExpr)
        {
            // A closure is a MemberExpression where the root is a ConstantExpression
            // (the compiler-generated closure class instance)
            Expression? current = memberExpr;
            while (current is MemberExpression member)
            {
                current = member.Expression;
            }
            return current is ConstantExpression;
        }

        /// <summary>
        /// Builds the field path for nested member access (e.g., "Direccion.Ciudad").
        /// For LeftJoin projections, handles Outer/Inner prefixes appropriately:
        /// - For root entity fields: strips prefix entirely (Libro.Outer.Titulo → Titulo)
        /// - For FK/Reference fields: uses collection name as prefix (Autors.Nombre)
        /// The FirestoreQueryBuilder expects collection names to match with includes.
        /// </summary>
        private string BuildFieldPath(MemberExpression memberExpr)
        {
            var parts = new List<string>();
            Expression? current = memberExpr;
            string? collectionPrefix = null;

            while (current is MemberExpression member)
            {
                var memberName = member.Member.Name;

                // Skip "Outer" - this is the root entity in EF Core LeftJoin
                if (memberName == "Outer")
                {
                    current = member.Expression;
                    continue;
                }

                // "Inner" represents a navigation - use target collection name as prefix
                if (memberName == "Inner")
                {
                    var innerType = member.Type;
                    collectionPrefix = _collectionManager.GetCollectionName(innerType);
                    current = member.Expression;
                    continue;
                }

                // Check if this member is a navigation property on our entity type
                if (_entityType != null)
                {
                    var navigation = _entityType.FindNavigation(memberName);
                    if (navigation != null)
                    {
                        // Use target collection name as prefix
                        collectionPrefix = _collectionManager.GetCollectionName(navigation.TargetEntityType.ClrType);
                        current = member.Expression;
                        continue;
                    }
                }

                parts.Insert(0, memberName);
                current = member.Expression;
            }

            // Prepend collection prefix if we found one (it's a FK/Reference field)
            if (collectionPrefix != null)
            {
                parts.Insert(0, collectionPrefix);
            }

            return string.Join(".", parts);
        }

        /// <summary>
        /// Checks if a type is an anonymous type.
        /// </summary>
        private static bool IsAnonymousType(Type type)
        {
            return type.IsClass
                && type.IsSealed
                && type.Attributes.HasFlag(TypeAttributes.NotPublic)
                && type.Name.Contains("AnonymousType");
        }

        /// <summary>
        /// Checks if a method call is Enum.ToString() - required by C# compiler for projecting enums to string.
        /// </summary>
        private static bool IsEnumToStringCall(MethodCallExpression methodCall, out MemberExpression? enumMember)
        {
            enumMember = null;

            // Check if it's a ToString() call
            if (methodCall.Method.Name != "ToString")
                return false;

            // The object should be a member expression (the enum property)
            if (methodCall.Object is not MemberExpression memberExpr)
                return false;

            // Check if the member type is an enum
            var memberType = memberExpr.Type;
            if (!memberType.IsEnum)
                return false;

            enumMember = memberExpr;
            return true;
        }

        /// <summary>
        /// Processes a nested anonymous type expression (e.g., Resumen = new { TotalPedidos = ..., Cantidad = ... }).
        /// Creates a nested field structure using dot notation for field paths.
        /// </summary>
        private void ProcessNestedAnonymousType(
            NewExpression nestedNewExpr,
            string parentResultName,
            ParameterExpression parameter,
            int constructorParameterIndex)
        {
            for (int i = 0; i < nestedNewExpr.Arguments.Count; i++)
            {
                var argument = nestedNewExpr.Arguments[i];
                var member = nestedNewExpr.Members?[i];
                var nestedResultName = member?.Name ?? $"Item{i}";

                // Build the full result name with parent prefix (e.g., "Resumen.TotalPedidos")
                var fullResultName = $"{parentResultName}.{nestedResultName}";

                // Process the nested argument recursively
                ProcessProjectionArgument(argument, fullResultName, parameter, constructorParameterIndex);
            }
        }

        /// <summary>
        /// Checks if a member expression refers to a collection navigation property (subcollection).
        /// Uses EF Core metadata to distinguish between navigation properties and ArrayOf fields.
        /// </summary>
        private bool IsCollectionNavigation(MemberExpression memberExpr)
        {
            // Use EF Core metadata - the authoritative source
            if (_entityType != null)
            {
                var propertyName = memberExpr.Member.Name;

                // Check if it's configured as ArrayOf (embedded array, not subcollection)
                // ArrayOf properties are ignored by EF Core but have Firestore annotations
                if (_entityType.IsArrayOf(propertyName))
                    return false;

                // If it's a navigation property, it's a subcollection
                var navigation = _entityType.FindNavigation(propertyName);
                if (navigation != null)
                    return true;

                // If it's a regular property, it's an ArrayOf field (not a subcollection)
                var property = _entityType.FindProperty(propertyName);
                if (property != null)
                    return false;
            }

            // Fallback when no metadata: assume collections are subcollections
            // This maintains backward compatibility but should rarely be hit
            var memberType = memberExpr.Type;
            if (!memberType.IsGenericType)
                return false;

            var genericDef = memberType.GetGenericTypeDefinition();
            return genericDef == typeof(List<>)
                || genericDef == typeof(IList<>)
                || genericDef == typeof(ICollection<>)
                || genericDef == typeof(IEnumerable<>)
                || genericDef == typeof(HashSet<>)
                || genericDef == typeof(ISet<>);
        }

        /// <summary>
        /// Creates a subcollection projection with navigation info resolved from entity type.
        /// </summary>
        private FirestoreSubcollectionProjection CreateSubcollectionProjection(
            string navigationName,
            string resultName,
            Type? targetClrTypeHint = null)
        {
            // Try to get navigation info from entity type by name
            var navigation = _entityType?.FindNavigation(navigationName);

            // If not found by name and we have a type hint, try to find navigation by target type
            if (navigation == null && targetClrTypeHint != null && _entityType != null)
            {
                navigation = _entityType.GetNavigations()
                    .FirstOrDefault(n => n.TargetEntityType.ClrType == targetClrTypeHint);
            }

            Type targetClrType;
            bool isCollection;
            string collectionName;
            string actualNavigationName = navigationName;

            if (navigation != null)
            {
                targetClrType = navigation.TargetEntityType.ClrType;
                isCollection = navigation.IsCollection;
                collectionName = _collectionManager.GetCollectionName(targetClrType);
                actualNavigationName = navigation.Name; // Use actual navigation name
            }
            else
            {
                // Fallback: use type hint or navigation name
                targetClrType = targetClrTypeHint ?? typeof(object);
                isCollection = true; // Assume collection since we're in subcollection context
                collectionName = targetClrTypeHint != null
                    ? _collectionManager.GetCollectionName(targetClrTypeHint)
                    : navigationName;
            }

            return new FirestoreSubcollectionProjection(actualNavigationName, resultName, collectionName, isCollection, targetClrType);
        }

        /// <summary>
        /// Extracts navigation information from a method call chain.
        /// </summary>
        private NavigationInfo? ExtractNavigationFromMethodChain(MethodCallExpression methodCallExpr)
        {
            // Walk up the method chain to find the root member access
            Expression? current = methodCallExpr;

            while (current is MethodCallExpression call)
            {
                // LINQ methods have the source as the first argument
                if (call.Arguments.Count > 0)
                {
                    current = call.Arguments[0];
                }
                else if (call.Object != null)
                {
                    current = call.Object;
                }
                else
                {
                    return null;
                }
            }

            // Should end up at a member expression (navigation property)
            if (current is MemberExpression memberExpr && IsCollectionNavigation(memberExpr))
            {
                return new NavigationInfo(memberExpr.Member.Name);
            }

            // EF Core may wrap the navigation in different ways, check if it's a member access
            // even if IsCollectionNavigation doesn't recognize the exact type
            if (current is MemberExpression memberExpr2)
            {
                // Check if the type implements IEnumerable<T> (broader check)
                var memberType = memberExpr2.Type;
                if (IsEnumerableType(memberType))
                {
                    return new NavigationInfo(memberExpr2.Member.Name);
                }
            }

            // EF Core transforms navigations like c.Pedidos.Select(...) into:
            // [EntityQueryRootExpression].Where(p => Property(c, "Id") == Property(p, "ClienteId")).Select(...)
            // We need to extract the entity type from the EntityQueryRootExpression
            if (current != null && IsEntityQueryRootExpression(current))
            {
                var entityClrType = GetEntityTypeFromQueryRoot(current);
                if (entityClrType != null)
                {
                    // Use the entity type name as the navigation name, but also pass the CLR type
                    // so CreateSubcollectionProjection can find the navigation by target type
                    return new NavigationInfo(entityClrType.Name, entityClrType);
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if an expression is an EF Core EntityQueryRootExpression.
        /// </summary>
        private static bool IsEntityQueryRootExpression(Expression expression)
        {
            // EntityQueryRootExpression is an internal EF Core type
            // We check by type name to avoid coupling to internals
            return expression.GetType().Name == "EntityQueryRootExpression";
        }

        /// <summary>
        /// Extracts the entity CLR type from an EntityQueryRootExpression.
        /// </summary>
        private static Type? GetEntityTypeFromQueryRoot(Expression expression)
        {
            // EntityQueryRootExpression has an EntityType property
            var entityTypeProp = expression.GetType().GetProperty("EntityType");
            if (entityTypeProp != null)
            {
                var entityType = entityTypeProp.GetValue(expression) as IEntityType;
                return entityType?.ClrType;
            }
            return null;
        }

        /// <summary>
        /// Checks if a type is an enumerable type (implements IEnumerable&lt;T&gt;).
        /// More permissive than IsCollectionNavigation to handle EF Core type transformations.
        /// </summary>
        private static bool IsEnumerableType(Type type)
        {
            if (type == typeof(string)) return false; // string is IEnumerable<char> but not a collection

            // Check if type itself is IEnumerable<T>
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return true;

            // Check if type implements IEnumerable<T>
            return type.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        }

        /// <summary>
        /// Gets the root expression from a method call chain (for debugging).
        /// </summary>
        private static Expression? GetRootExpression(MethodCallExpression methodCallExpr)
        {
            Expression? current = methodCallExpr;
            while (current is MethodCallExpression call)
            {
                if (call.Arguments.Count > 0)
                    current = call.Arguments[0];
                else if (call.Object != null)
                    current = call.Object;
                else
                    return null;
            }
            return current;
        }

        /// <summary>
        /// Extracts LINQ operations from a method call chain and applies them to a subcollection projection.
        /// </summary>
        private void ExtractSubcollectionOperations(
            MethodCallExpression methodCallExpr,
            FirestoreSubcollectionProjection subcollection)
        {
            // Collect all method calls in the chain
            var methodCalls = new List<MethodCallExpression>();
            Expression? current = methodCallExpr;

            while (current is MethodCallExpression call)
            {
                methodCalls.Insert(0, call); // Insert at beginning to process in order
                current = call.Arguments.Count > 0 ? call.Arguments[0] : call.Object;
            }

            // Process each method call
            // Note: We no longer skip all Where clauses based on EntityQueryRootExpression.
            // Instead, each Where is checked individually to see if it's a correlation filter.
            foreach (var call in methodCalls)
            {
                ProcessSubcollectionMethodCall(call, subcollection);
            }
        }

        /// <summary>
        /// Processes a single method call for a subcollection.
        /// </summary>
        /// <param name="methodCall">The method call expression to process.</param>
        /// <param name="subcollection">The subcollection projection to update.</param>
        private void ProcessSubcollectionMethodCall(
            MethodCallExpression methodCall,
            FirestoreSubcollectionProjection subcollection)
        {
            var methodName = methodCall.Method.Name;

            switch (methodName)
            {
                case "Where":
                    // Check if this is a correlation filter (EF Core internal join condition)
                    // Correlation filters use Property(...) expressions like:
                    // Property(c, "Id") == Property(p, "ClienteId")
                    // User filters use direct member access like: p.Estado == EstadoPedido.Confirmado
                    if (!IsCorrelationFilter(methodCall))
                    {
                        ProcessSubcollectionWhere(methodCall, subcollection);
                    }
                    break;

                case "OrderBy":
                    ProcessSubcollectionOrderBy(methodCall, subcollection, ascending: true, isFirst: true);
                    break;

                case "OrderByDescending":
                    ProcessSubcollectionOrderBy(methodCall, subcollection, ascending: false, isFirst: true);
                    break;

                case "ThenBy":
                    ProcessSubcollectionOrderBy(methodCall, subcollection, ascending: true, isFirst: false);
                    break;

                case "ThenByDescending":
                    ProcessSubcollectionOrderBy(methodCall, subcollection, ascending: false, isFirst: false);
                    break;

                case "Take":
                    ProcessSubcollectionTake(methodCall, subcollection);
                    break;

                case "Select":
                    ProcessSubcollectionSelect(methodCall, subcollection);
                    break;

                case "Count":
                    subcollection.Aggregation = FirestoreAggregationType.Count;
                    break;

                case "Sum":
                    ProcessSubcollectionAggregation(methodCall, subcollection, FirestoreAggregationType.Sum);
                    break;

                case "Average":
                    ProcessSubcollectionAggregation(methodCall, subcollection, FirestoreAggregationType.Average);
                    break;

                case "Min":
                    ProcessSubcollectionAggregation(methodCall, subcollection, FirestoreAggregationType.Min);
                    break;

                case "Max":
                    ProcessSubcollectionAggregation(methodCall, subcollection, FirestoreAggregationType.Max);
                    break;

                case "ToList":
                case "AsEnumerable":
                    // These are just materializers, no additional processing needed
                    break;

                case "LeftJoin":
                    ProcessSubcollectionLeftJoin(methodCall, subcollection);
                    break;

                // Unsupported subcollection operations
                case "Skip":
                    throw new NotSupportedException(
                        "Skip is not supported in subcollection projections. Firestore does not support offset-based pagination in subcollections.");

                case "First":
                case "FirstOrDefault":
                    throw new NotSupportedException(
                        $"{methodName} is not supported in subcollection projections. Use Take(1) instead.");

                case "Single":
                case "SingleOrDefault":
                    throw new NotSupportedException(
                        $"{methodName} is not supported in subcollection projections. Use Take(1) instead.");

                case "Last":
                case "LastOrDefault":
                    throw new NotSupportedException(
                        $"{methodName} is not supported in subcollection projections. Use OrderByDescending with Take(1) instead.");

                case "Any":
                    throw new NotSupportedException(
                        "Any is not supported in subcollection projections. Use Count() > 0 instead.");

                case "All":
                    throw new NotSupportedException(
                        "All is not supported in subcollection projections.");

                case "Contains":
                    throw new NotSupportedException(
                        "Contains is not supported in subcollection projections. Use Where with equality filter instead.");

                case "Distinct":
                    throw new NotSupportedException(
                        "Distinct is not supported in subcollection projections.");

                case "GroupBy":
                    throw new NotSupportedException(
                        "GroupBy is not supported in subcollection projections.");

                default:
                    throw new NotSupportedException(
                        $"Method '{methodName}' is not supported in subcollection projections.");
            }
        }

        private void ProcessSubcollectionWhere(
            MethodCallExpression methodCall,
            FirestoreSubcollectionProjection subcollection)
        {
            if (methodCall.Arguments.Count < 2)
                return;

            var predicateLambda = ExtractLambda(methodCall.Arguments[1]);
            if (predicateLambda == null)
                return;

            var filterResult = _whereTranslator.Translate(predicateLambda.Body);
            if (filterResult != null)
            {
                // Store the filter result for later processing
                subcollection.FilterResults.Add(filterResult);

                foreach (var clause in filterResult.AndClauses)
                {
                    subcollection.Filters.Add(clause);
                }
            }
        }

        private void ProcessSubcollectionOrderBy(
            MethodCallExpression methodCall,
            FirestoreSubcollectionProjection subcollection,
            bool ascending,
            bool isFirst)
        {
            if (methodCall.Arguments.Count < 2)
                return;

            var keySelector = ExtractLambda(methodCall.Arguments[1]);
            if (keySelector == null)
                return;

            var clause = _orderByTranslator.Translate(keySelector, ascending);
            if (clause != null)
            {
                if (isFirst)
                {
                    subcollection.OrderByClauses.Clear();
                }
                subcollection.OrderByClauses.Add(clause);
            }
        }

        private void ProcessSubcollectionTake(
            MethodCallExpression methodCall,
            FirestoreSubcollectionProjection subcollection)
        {
            if (methodCall.Arguments.Count < 2)
                return;

            var countExpression = methodCall.Arguments[1];
            var constantValue = _limitTranslator.Translate(countExpression);
            if (constantValue.HasValue)
            {
                subcollection.Pagination.WithLimit(constantValue.Value);
            }
            else
            {
                subcollection.Pagination.WithLimitExpression(countExpression);
            }
        }

        private void ProcessSubcollectionSelect(
            MethodCallExpression methodCall,
            FirestoreSubcollectionProjection subcollection)
        {
            if (methodCall.Arguments.Count < 2)
                return;

            var selectorLambda = ExtractLambda(methodCall.Arguments[1]);
            if (selectorLambda == null)
                return;

            // For nested selects, try to get the target entity type
            var navigation = _entityType?.FindNavigation(subcollection.NavigationName);
            var nestedEntityType = navigation?.TargetEntityType;

            // Extract projected fields from the nested select
            var nestedVisitor = new ProjectionExtractionVisitor(_collectionManager, nestedEntityType);
            var nestedDefinition = nestedVisitor.Extract(selectorLambda);

            if (nestedDefinition?.Fields != null)
            {
                subcollection.Fields = nestedDefinition.Fields.ToList();
            }

            // Handle nested subcollections
            foreach (var nestedSubcollection in nestedVisitor.Subcollections)
            {
                subcollection.NestedSubcollections.Add(nestedSubcollection);
            }
        }

        private void ProcessSubcollectionAggregation(
            MethodCallExpression methodCall,
            FirestoreSubcollectionProjection subcollection,
            FirestoreAggregationType aggregationType)
        {
            subcollection.Aggregation = aggregationType;

            if (methodCall.Arguments.Count >= 2)
            {
                var selectorLambda = ExtractLambda(methodCall.Arguments[1]);
                if (selectorLambda?.Body is MemberExpression memberExpr)
                {
                    subcollection.AggregationPropertyName = memberExpr.Member.Name;
                }
            }
        }

        private static LambdaExpression? ExtractLambda(Expression expression)
        {
            if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Quote)
            {
                return unary.Operand as LambdaExpression;
            }
            return expression as LambdaExpression;
        }

        /// <summary>
        /// Processes a LeftJoin in a subcollection, creating an IncludeInfo for the FK navigation.
        /// Uses the same logic as FirestoreLeftJoinTranslator.
        /// </summary>
        private void ProcessSubcollectionLeftJoin(
            MethodCallExpression methodCall,
            FirestoreSubcollectionProjection subcollection)
        {
            // LeftJoin signature: LeftJoin(outer, inner, outerKeySelector, innerKeySelector, resultSelector)
            // Arguments[2] is the outerKeySelector containing the FK property
            if (methodCall.Arguments.Count < 3)
                return;

            var outerKeySelector = ExtractLambda(methodCall.Arguments[2]);
            if (outerKeySelector == null)
                return;

            // Get the entity type from the subcollection
            var subcollectionEntityType = _entityType?.Model.FindEntityType(subcollection.TargetClrType);
            if (subcollectionEntityType == null)
                return;

            // Use FirestoreLeftJoinTranslator to create the IncludeInfo
            var translator = new FirestoreLeftJoinTranslator(_collectionManager);
            var innerEntityType = subcollectionEntityType; // The inner is the related entity
            var includeInfo = translator.Translate(outerKeySelector, subcollectionEntityType, innerEntityType);

            if (includeInfo != null)
            {
                subcollection.Includes.Add(includeInfo);
            }
        }

        /// <summary>
        /// Checks if a Where method call is a correlation filter (EF Core internal join condition).
        /// Correlation filters use Property(...) expressions like: Property(c, "Id") == Property(p, "ClienteId")
        /// User filters use direct member access like: p.Estado == EstadoPedido.Confirmado
        /// </summary>
        private static bool IsCorrelationFilter(MethodCallExpression whereCall)
        {
            if (whereCall.Arguments.Count < 2)
                return false;

            var predicateLambda = ExtractLambda(whereCall.Arguments[1]);
            if (predicateLambda == null)
                return false;

            // Check if the predicate body contains Property(...) calls
            // which indicate EF Core's internal correlation conditions
            var exprString = predicateLambda.Body.ToString();
            return exprString.Contains("Property(");
        }

        /// <summary>
        /// Builds the final FirestoreProjectionDefinition from extracted data.
        /// </summary>
        private FirestoreProjectionDefinition? BuildProjectionDefinition()
        {
            if (ClrType == null)
                return null;

            FirestoreProjectionDefinition projection;

            switch (ResultType)
            {
                case ProjectionResultType.SingleField:
                    if (Fields.Count == 1)
                    {
                        projection = FirestoreProjectionDefinition.CreateSingleFieldProjection(ClrType, Fields[0]);
                    }
                    else
                    {
                        return null;
                    }
                    break;

                case ProjectionResultType.AnonymousType:
                    projection = FirestoreProjectionDefinition.CreateAnonymousTypeProjection(ClrType, Fields.ToList());
                    break;

                case ProjectionResultType.DtoClass:
                    projection = FirestoreProjectionDefinition.CreateDtoClassProjection(ClrType, Fields.ToList());
                    break;

                case ProjectionResultType.Record:
                    projection = FirestoreProjectionDefinition.CreateRecordProjection(ClrType, Fields.ToList());
                    break;

                default:
                    return null;
            }

            // Add subcollections
            foreach (var subcollection in Subcollections)
            {
                projection.Subcollections.Add(subcollection);
            }

            return projection;
        }

        /// <summary>
        /// Helper class to store navigation information.
        /// </summary>
        private sealed class NavigationInfo
        {
            public string NavigationName { get; }
            public Type? TargetClrType { get; }

            public NavigationInfo(string navigationName, Type? targetClrType = null)
            {
                NavigationName = navigationName;
                TargetClrType = targetClrType;
            }
        }
    }
}