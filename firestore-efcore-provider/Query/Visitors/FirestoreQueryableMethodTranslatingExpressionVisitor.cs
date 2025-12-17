using Firestore.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Firestore.EntityFrameworkCore.Query.Visitors
{
    public class FirestoreQueryableMethodTranslatingExpressionVisitor
        : QueryableMethodTranslatingExpressionVisitor
    {
        public FirestoreQueryableMethodTranslatingExpressionVisitor(
            QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
            QueryCompilationContext queryCompilationContext)
            : base(dependencies, queryCompilationContext, subquery: false)
        {
        }

        protected FirestoreQueryableMethodTranslatingExpressionVisitor(
            FirestoreQueryableMethodTranslatingExpressionVisitor parentVisitor)
            : base(parentVisitor.Dependencies, parentVisitor.QueryCompilationContext, subquery: true)
        {
        }

        protected override ShapedQueryExpression CreateShapedQueryExpression(IEntityType entityType)
        {
            var collectionName = GetCollectionName(entityType);
            var queryExpression = new FirestoreQueryExpression(entityType, collectionName);

            var entityShaperExpression = new StructuralTypeShaperExpression(
                entityType,
                new ProjectionBindingExpression(
                    queryExpression,
                    new ProjectionMember(),
                    typeof(ValueBuffer)),
                nullable: false);

            return new ShapedQueryExpression(queryExpression, entityShaperExpression);
        }

        private string GetCollectionName(IEntityType entityType)
        {
            var tableAttribute = entityType.ClrType
                .GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.TableAttribute>();

            if (tableAttribute != null && !string.IsNullOrEmpty(tableAttribute.Name))
                return tableAttribute.Name;

            var entityName = entityType.ClrType.Name;
            return Pluralize(entityName);
        }

        private static string Pluralize(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            if (name.EndsWith("y", StringComparison.OrdinalIgnoreCase) &&
                name.Length > 1 &&
                !IsVowel(name[name.Length - 2]))
            {
                return name.Substring(0, name.Length - 1) + "ies";
            }

            if (name.EndsWith("s", StringComparison.OrdinalIgnoreCase))
                return name + "es";

            return name + "s";
        }

        private static bool IsVowel(char c)
        {
            c = char.ToLowerInvariant(c);
            return c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u';
        }

        protected override QueryableMethodTranslatingExpressionVisitor CreateSubqueryVisitor()
        {
            return new FirestoreQueryableMethodTranslatingExpressionVisitor(this);
        }

        /// <summary>
        /// Override Visit to preprocess the expression tree and transform array Contains patterns
        /// BEFORE the base class tries to process them as subqueries.
        /// </summary>
        public override Expression? Visit(Expression? expression)
        {
            if (expression == null) return null;

            // Preprocess the expression tree to transform array Contains patterns
            var preprocessed = PreprocessArrayContainsPatterns(expression);
            return base.Visit(preprocessed);
        }

        /// <summary>
        /// Recursively preprocesses an expression tree to replace array Contains patterns
        /// with FirestoreArrayContainsExpression markers.
        /// </summary>
        private Expression PreprocessArrayContainsPatterns(Expression expression)
        {
            // Handle the specific pattern: EF.Property<List<T>>().AsQueryable().Contains(value)
            if (expression is MethodCallExpression methodCall)
            {
                // First, recursively preprocess children
                var newObject = methodCall.Object != null
                    ? PreprocessArrayContainsPatterns(methodCall.Object)
                    : null;
                var newArgs = methodCall.Arguments.Select(PreprocessArrayContainsPatterns).ToList();

                // Check if this is the pattern we're looking for
                if (methodCall.Method.Name == "Contains")
                {
                    // Pattern 1: Instance .Contains(value) where object is AsQueryable()
                    if (newObject is MethodCallExpression asQueryableCall &&
                        asQueryableCall.Method.Name == "AsQueryable")
                    {
                        var propertyName = ExtractPropertyNameFromEFPropertyChain(
                            asQueryableCall.Arguments.Count == 1
                                ? asQueryableCall.Arguments[0]
                                : asQueryableCall.Object);

                        if (propertyName != null && newArgs.Count == 1)
                        {
                            return new FirestoreArrayContainsExpression(propertyName, newArgs[0]);
                        }
                    }

                    // Pattern 2: Static Enumerable.Contains(asQueryable, value)
                    if (newObject == null && newArgs.Count == 2 &&
                        newArgs[0] is MethodCallExpression asQueryable &&
                        asQueryable.Method.Name == "AsQueryable")
                    {
                        var propertyName = ExtractPropertyNameFromEFPropertyChain(
                            asQueryable.Arguments.Count == 1
                                ? asQueryable.Arguments[0]
                                : asQueryable.Object);

                        if (propertyName != null)
                        {
                            return new FirestoreArrayContainsExpression(propertyName, newArgs[1]);
                        }
                    }
                }

                // Return updated method call if children changed
                if (newObject != methodCall.Object || !newArgs.SequenceEqual(methodCall.Arguments))
                {
                    return methodCall.Update(newObject, newArgs);
                }
            }

            // Handle lambda expressions
            if (expression is LambdaExpression lambda)
            {
                var newBody = PreprocessArrayContainsPatterns(lambda.Body);
                if (newBody != lambda.Body)
                {
                    return Expression.Lambda(lambda.Type, newBody, lambda.Parameters);
                }
            }

            // Handle binary expressions (AND, OR)
            if (expression is BinaryExpression binary)
            {
                var newLeft = PreprocessArrayContainsPatterns(binary.Left);
                var newRight = PreprocessArrayContainsPatterns(binary.Right);
                if (newLeft != binary.Left || newRight != binary.Right)
                {
                    return binary.Update(newLeft, binary.Conversion, newRight);
                }
            }

            // Handle unary expressions (NOT)
            if (expression is UnaryExpression unary)
            {
                var newOperand = PreprocessArrayContainsPatterns(unary.Operand);
                if (newOperand != unary.Operand)
                {
                    return unary.Update(newOperand);
                }
            }

            return expression;
        }

        /// <summary>
        /// Intercepts method calls to detect array Contains patterns that EF Core would
        /// otherwise try to process as subqueries.
        /// Pattern: EF.Property&lt;List&lt;T&gt;&gt;(e, "Field").AsQueryable().Contains(value)
        /// </summary>
        protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
        {
            // Detect the pattern: Something.AsQueryable().Contains(value)
            // Where Something is EF.Property<List<T>>(entity, "PropertyName")
            if (methodCallExpression.Method.Name == "Contains" &&
                methodCallExpression.Arguments.Count == 1 &&
                methodCallExpression.Object is MethodCallExpression asQueryableCall &&
                asQueryableCall.Method.Name == "AsQueryable")
            {
                // Check if AsQueryable wraps EF.Property<List<T>>()
                var propertyName = ExtractPropertyNameFromEFPropertyChain(asQueryableCall.Arguments.Count == 1
                    ? asQueryableCall.Arguments[0]
                    : asQueryableCall.Object);

                if (propertyName != null)
                {
                    // Return a marker expression that will be handled by FirestoreWhereTranslator
                    // We create a synthetic MethodCallExpression that our translator understands
                    return new FirestoreArrayContainsExpression(propertyName, methodCallExpression.Arguments[0]);
                }
            }

            // Also detect static Enumerable.Contains pattern
            if (methodCallExpression.Method.Name == "Contains" &&
                methodCallExpression.Object == null &&
                methodCallExpression.Arguments.Count == 2)
            {
                // Check if first argument is AsQueryable wrapping EF.Property
                if (methodCallExpression.Arguments[0] is MethodCallExpression asQueryable &&
                    asQueryable.Method.Name == "AsQueryable")
                {
                    var propertyName = ExtractPropertyNameFromEFPropertyChain(asQueryable.Arguments.Count == 1
                        ? asQueryable.Arguments[0]
                        : asQueryable.Object);

                    if (propertyName != null)
                    {
                        return new FirestoreArrayContainsExpression(propertyName, methodCallExpression.Arguments[1]);
                    }
                }
            }

            return base.VisitMethodCall(methodCallExpression);
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
                    if (methodCall.Arguments.Count >= 2 && methodCall.Arguments[1] is ConstantExpression constant)
                    {
                        return constant.Value as string;
                    }
                }
            }

            return null;
        }

        #region Translate Methods

        protected override ShapedQueryExpression? TranslateFirstOrDefault(
            ShapedQueryExpression source,
            LambdaExpression? predicate,
            Type returnType,
            bool returnDefault)
        {
            if (predicate != null)
            {
                source = TranslateWhere(source, predicate) ?? source;
            }

            var firestoreQueryExpression = (FirestoreQueryExpression)source.QueryExpression;
            var newQueryExpression = firestoreQueryExpression.WithLimit(1);

            return source.UpdateQueryExpression(newQueryExpression);
        }

        protected override ShapedQueryExpression TranslateSelect(
            ShapedQueryExpression source,
            LambdaExpression selector)
        {
            // Procesar includes
            if (selector.Body is Microsoft.EntityFrameworkCore.Query.IncludeExpression includeExpression)
            {
                var includeVisitor = new IncludeExtractionVisitor();
                includeVisitor.Visit(includeExpression);

                var firestoreQueryExpression = (FirestoreQueryExpression)source.QueryExpression;

                foreach (var navigation in includeVisitor.DetectedNavigations)
                {
                    // Evitar duplicados
                    if (!firestoreQueryExpression.PendingIncludes.Any(n =>
                        n.Name == navigation.Name &&
                        n.DeclaringEntityType == navigation.DeclaringEntityType))
                    {
                        firestoreQueryExpression.PendingIncludes.Add(navigation);
                    }
                }
            }

            // Proyección de identidad (x => x)
            if (selector.Body == selector.Parameters[0])
            {
                return source;
            }

            // Proyección con conversión de tipo
            if (selector.Body is UnaryExpression unary &&
                unary.NodeType == ExpressionType.Convert &&
                unary.Operand == selector.Parameters[0])
            {
                return source;
            }

            return source;
        }

        protected override ShapedQueryExpression? TranslateWhere(
            ShapedQueryExpression source,
            LambdaExpression predicate)
        {
            var firestoreQueryExpression = (FirestoreQueryExpression)source.QueryExpression;

            var parameterReplacer = new RuntimeParameterReplacer(QueryCompilationContext);
            var evaluatedBody = parameterReplacer.Visit(predicate.Body);

            var translator = new FirestoreWhereTranslator();
            var filterResult = translator.Translate(evaluatedBody);

            if (filterResult == null)
            {
                return null;
            }

            // Handle OR groups
            if (filterResult.IsOrGroup)
            {
                if (firestoreQueryExpression.IsIdOnlyQuery)
                {
                    throw new InvalidOperationException(
                        "Cannot add OR filters to an ID-only query.");
                }

                var newQueryExpression = firestoreQueryExpression.AddOrFilterGroup(filterResult.OrGroup!);
                return source.UpdateQueryExpression(newQueryExpression);
            }

            // Handle AND clauses (single or multiple) with possible nested OR groups
            var clauses = filterResult.AndClauses;
            var nestedOrGroups = filterResult.NestedOrGroups;

            if (clauses.Count == 0 && nestedOrGroups.Count == 0)
            {
                return null;
            }

            // Check for ID-only queries (optimization: use GetDocumentAsync instead of query)
            // Only valid when there's a SINGLE Id == clause with NO other filters
            if (clauses.Count == 1 && clauses[0].PropertyName == "Id")
            {
                var whereClause = clauses[0];
                if (whereClause.Operator != FirestoreOperator.EqualTo)
                {
                    throw new InvalidOperationException(
                        $"Firestore ID queries only support the '==' operator.");
                }

                // If there are already other filters, treat Id as a normal filter
                // (executor will use FieldPath.DocumentId)
                if (firestoreQueryExpression.Filters.Count > 0 || firestoreQueryExpression.OrFilterGroups.Count > 0)
                {
                    var normalQueryExpression = firestoreQueryExpression.AddFilter(whereClause);
                    return source.UpdateQueryExpression(normalQueryExpression);
                }

                if (firestoreQueryExpression.IsIdOnlyQuery)
                {
                    throw new InvalidOperationException(
                        "Cannot apply multiple ID filters.");
                }

                // Create IdOnlyQuery (optimization for single document fetch)
                var newQueryExpression = new FirestoreQueryExpression(
                    firestoreQueryExpression.EntityType,
                    firestoreQueryExpression.CollectionName)
                {
                    IdValueExpression = whereClause.ValueExpression,
                    Filters = new List<FirestoreWhereClause>(firestoreQueryExpression.Filters),
                    OrFilterGroups = new List<FirestoreOrFilterGroup>(firestoreQueryExpression.OrFilterGroups),
                    OrderByClauses = new List<FirestoreOrderByClause>(firestoreQueryExpression.OrderByClauses),
                    Limit = firestoreQueryExpression.Limit,
                    StartAfterDocument = firestoreQueryExpression.StartAfterDocument,
                    PendingIncludes = firestoreQueryExpression.PendingIncludes
                };

                return source.UpdateQueryExpression(newQueryExpression);
            }

            // If we already have an IdOnlyQuery and need to add more filters,
            // convert it to a normal query with FieldPath.DocumentId
            if (firestoreQueryExpression.IsIdOnlyQuery)
            {
                // Create Id clause from the existing IdValueExpression
                var idClause = new FirestoreWhereClause(
                    "Id", FirestoreOperator.EqualTo, firestoreQueryExpression.IdValueExpression!, null);

                // Create new query without IdValueExpression (will use FieldPath.DocumentId)
                var convertedQuery = new FirestoreQueryExpression(
                    firestoreQueryExpression.EntityType,
                    firestoreQueryExpression.CollectionName)
                {
                    Filters = new List<FirestoreWhereClause> { idClause },
                    OrFilterGroups = new List<FirestoreOrFilterGroup>(firestoreQueryExpression.OrFilterGroups),
                    OrderByClauses = new List<FirestoreOrderByClause>(firestoreQueryExpression.OrderByClauses),
                    Limit = firestoreQueryExpression.Limit,
                    StartAfterDocument = firestoreQueryExpression.StartAfterDocument,
                    PendingIncludes = firestoreQueryExpression.PendingIncludes
                };

                // Add the new clauses
                var convertedWithFilters = convertedQuery.AddFilters(clauses);
                return source.UpdateQueryExpression(convertedWithFilters);
            }

            // Add all AND clauses
            var resultQuery = firestoreQueryExpression.AddFilters(clauses);

            // Add nested OR groups (for patterns like A && (B || C))
            foreach (var orGroup in nestedOrGroups)
            {
                resultQuery = resultQuery.AddOrFilterGroup(orGroup);
            }

            return source.UpdateQueryExpression(resultQuery);
        }

        #endregion

        #region Not Implemented Methods

        protected override ShapedQueryExpression? TranslateAll(ShapedQueryExpression source, LambdaExpression predicate)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateAny(ShapedQueryExpression source, LambdaExpression? predicate)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateAverage(ShapedQueryExpression source, LambdaExpression? selector, Type resultType)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateCast(ShapedQueryExpression source, Type castType)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateConcat(ShapedQueryExpression source1, ShapedQueryExpression source2)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateContains(ShapedQueryExpression source, Expression item)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateCount(ShapedQueryExpression source, LambdaExpression? predicate)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateDefaultIfEmpty(ShapedQueryExpression source, Expression? defaultValue)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateDistinct(ShapedQueryExpression source)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateElementAtOrDefault(ShapedQueryExpression source, Expression index, bool returnDefault)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateExcept(ShapedQueryExpression source1, ShapedQueryExpression source2)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateGroupBy(ShapedQueryExpression source, LambdaExpression keySelector, LambdaExpression? elementSelector, LambdaExpression? resultSelector)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateGroupJoin(ShapedQueryExpression outer, ShapedQueryExpression inner, LambdaExpression outerKeySelector, LambdaExpression innerKeySelector, LambdaExpression resultSelector)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateIntersect(ShapedQueryExpression source1, ShapedQueryExpression source2)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateJoin(ShapedQueryExpression outer, ShapedQueryExpression inner, LambdaExpression outerKeySelector, LambdaExpression innerKeySelector, LambdaExpression resultSelector)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateLastOrDefault(ShapedQueryExpression source, LambdaExpression? predicate, Type returnType, bool returnDefault)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateLeftJoin(
            ShapedQueryExpression outer,
            ShapedQueryExpression inner,
            LambdaExpression outerKeySelector,
            LambdaExpression innerKeySelector,
            LambdaExpression resultSelector)
        {
            // En Firestore NO hacemos joins reales.
            // LeftJoin se usa internamente por EF Core para Include de navegaciones.
            // Estrategia: extraer la navegación y agregarla a PendingIncludes
            // para que el executor la cargue después.

            var outerQueryExpression = (FirestoreQueryExpression)outer.QueryExpression;
            var innerQueryExpression = (FirestoreQueryExpression)inner.QueryExpression;

            // Intentar extraer la navegación del outerKeySelector
            IReadOnlyNavigation? navigation = null;

            if (outerKeySelector.Body is MemberExpression memberExpression)
            {
                var memberName = memberExpression.Member.Name;
                navigation = outerQueryExpression.EntityType.FindNavigation(memberName);
            }

            // Si encontramos una navegación, agregarla a PendingIncludes
            if (navigation != null)
            {
                var newQueryExpression = outerQueryExpression.AddInclude(navigation);
                return outer.UpdateQueryExpression(newQueryExpression);
            }

            // Si no pudimos extraer la navegación, intentar detectarla desde el inner
            var innerEntityType = innerQueryExpression.EntityType;
            var outerEntityType = outerQueryExpression.EntityType;

            // Buscar navegación en outer que apunte a inner
            foreach (var nav in outerEntityType.GetNavigations())
            {
                if (nav.TargetEntityType == innerEntityType)
                {
                    var newQueryExpression = outerQueryExpression.AddInclude(nav);
                    return outer.UpdateQueryExpression(newQueryExpression);
                }
            }

            // Si llegamos aquí, no pudimos identificar la navegación
            throw new NotSupportedException(
                $"Firestore does not support real joins. " +
                $"Could not identify navigation for LeftJoin between " +
                $"'{outerEntityType.ClrType.Name}' and '{innerEntityType.ClrType.Name}'. " +
                $"Use .Reference() to configure DocumentReference navigations.");
        }

        protected override ShapedQueryExpression? TranslateLongCount(ShapedQueryExpression source, LambdaExpression? predicate)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateMax(ShapedQueryExpression source, LambdaExpression? selector, Type resultType)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateMin(ShapedQueryExpression source, LambdaExpression? selector, Type resultType)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateOfType(ShapedQueryExpression source, Type resultType)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateOrderBy(ShapedQueryExpression source, LambdaExpression keySelector, bool ascending)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateReverse(ShapedQueryExpression source)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateSelectMany(ShapedQueryExpression source, LambdaExpression collectionSelector, LambdaExpression resultSelector)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateSelectMany(ShapedQueryExpression source, LambdaExpression selector)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateSingleOrDefault(ShapedQueryExpression source, LambdaExpression? predicate, Type returnType, bool returnDefault)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateSkip(ShapedQueryExpression source, Expression count)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateSkipWhile(ShapedQueryExpression source, LambdaExpression predicate)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateSum(ShapedQueryExpression source, LambdaExpression? selector, Type resultType)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateTake(ShapedQueryExpression source, Expression count)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateTakeWhile(ShapedQueryExpression source, LambdaExpression predicate)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateThenBy(ShapedQueryExpression source, LambdaExpression keySelector, bool ascending)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateUnion(ShapedQueryExpression source1, ShapedQueryExpression source2)
            => throw new NotImplementedException();

        #endregion
    }
}
