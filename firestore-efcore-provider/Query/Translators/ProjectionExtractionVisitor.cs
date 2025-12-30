using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Projections;
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
        private readonly FirestoreWhereTranslator _whereTranslator = new();
        private readonly FirestoreOrderByTranslator _orderByTranslator = new();
        private readonly FirestoreLimitTranslator _limitTranslator = new();

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
        /// Analyzes a method call expression (could be a subcollection with operations)
        /// </summary>
        private void AnalyzeMethodCallExpression(MethodCallExpression methodCallExpr, ParameterExpression parameter)
        {
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
                    // Check if this is a subcollection operation
                    var navInfo = ExtractNavigationFromMethodChain(methodCallExpr);
                    if (navInfo != null)
                    {
                        var subProj = CreateSubcollectionProjection(navInfo.NavigationName, resultName);
                        ExtractSubcollectionOperations(methodCallExpr, subProj);
                        Subcollections.Add(subProj);
                    }
                    else
                    {
                        // Method call on property: e.Name.ToUpper()
                        throw new NotSupportedException(
                            $"Method calls in projections are not supported: {methodCallExpr.Method.Name}. " +
                            "Projections must reference entity properties directly.");
                    }
                    break;

                case UnaryExpression unaryExpr when unaryExpr.NodeType == ExpressionType.Convert:
                    // Handle type conversions
                    ProcessProjectionArgument(unaryExpr.Operand, resultName, parameter, constructorParameterIndex);
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
        /// Builds the field path for nested member access (e.g., "Direccion.Ciudad")
        /// </summary>
        private static string BuildFieldPath(MemberExpression memberExpr)
        {
            var parts = new List<string>();
            Expression? current = memberExpr;

            while (current is MemberExpression member)
            {
                parts.Insert(0, member.Member.Name);
                current = member.Expression;
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
        /// Checks if a member expression refers to a collection navigation property.
        /// </summary>
        private static bool IsCollectionNavigation(MemberExpression memberExpr)
        {
            var memberType = memberExpr.Type;

            // Check if it's a generic collection (List<T>, ICollection<T>, etc.)
            if (memberType.IsGenericType)
            {
                var genericDef = memberType.GetGenericTypeDefinition();
                return genericDef == typeof(List<>)
                    || genericDef == typeof(IList<>)
                    || genericDef == typeof(ICollection<>)
                    || genericDef == typeof(IEnumerable<>);
            }

            return false;
        }

        /// <summary>
        /// Creates a subcollection projection.
        /// </summary>
        private static FirestoreSubcollectionProjection CreateSubcollectionProjection(
            string navigationName,
            string resultName)
        {
            // Collection name will be resolved later by the executor
            return new FirestoreSubcollectionProjection(navigationName, resultName, navigationName);
        }

        /// <summary>
        /// Extracts navigation information from a method call chain.
        /// </summary>
        private static NavigationInfo? ExtractNavigationFromMethodChain(MethodCallExpression methodCallExpr)
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

            return null;
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
            foreach (var call in methodCalls)
            {
                ProcessSubcollectionMethodCall(call, subcollection);
            }
        }

        /// <summary>
        /// Processes a single method call for a subcollection.
        /// </summary>
        private void ProcessSubcollectionMethodCall(
            MethodCallExpression methodCall,
            FirestoreSubcollectionProjection subcollection)
        {
            var methodName = methodCall.Method.Name;

            switch (methodName)
            {
                case "Where":
                    ProcessSubcollectionWhere(methodCall, subcollection);
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

            // Extract projected fields from the nested select
            var nestedVisitor = new ProjectionExtractionVisitor();
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

            public NavigationInfo(string navigationName)
            {
                NavigationName = navigationName;
            }
        }
    }
}