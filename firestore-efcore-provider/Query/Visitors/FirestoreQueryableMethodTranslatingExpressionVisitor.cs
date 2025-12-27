using Firestore.EntityFrameworkCore.Metadata.Conventions;
using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Preprocessing;
using Firestore.EntityFrameworkCore.Query.Translators;
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
            var preprocessed = ArrayContainsPatternTransformer.Transform(expression);
            return base.Visit(preprocessed);
        }

        /// <summary>
        /// Handles custom extension expressions like FirestoreTakeLastExpression.
        /// </summary>
        protected override Expression VisitExtension(Expression extensionExpression)
        {
            // Handle our custom TakeLast expression
            if (extensionExpression is FirestoreTakeLastExpression takeLastExpression)
            {
                var source = Visit(takeLastExpression.Source);
                if (source is ShapedQueryExpression shapedSource)
                {
                    return TranslateTakeLast(shapedSource, takeLastExpression.Count);
                }
            }

            return base.VisitExtension(extensionExpression);
        }

        /// <summary>
        /// Extracts the property name from an OrderBy/ThenBy key selector lambda.
        /// Handles expressions like: e => e.Name, e => e.Quantity, e => e.Price
        /// Also handles nested properties: e => e.Address.City
        /// </summary>
        private string? ExtractPropertyNameFromKeySelector(LambdaExpression keySelector)
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
        /// Builds the property path for nested properties.
        /// For e.Address.City returns "Address.City".
        /// For e.Name returns "Name".
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

            // Reverse to get correct order (parent to child)
            parts.Reverse();
            return string.Join(".", parts);
        }

        #region Translate Methods

        protected override ShapedQueryExpression? TranslateFirstOrDefault(
            ShapedQueryExpression source,
            LambdaExpression? predicate,
            Type returnType,
            bool returnDefault)
            => FirestoreQueryExpression.TranslateFirstOrDefault(new(source, predicate, returnType, returnDefault));

        protected override ShapedQueryExpression TranslateSelect(
            ShapedQueryExpression source,
            LambdaExpression selector)
        {
            // Procesar includes - estos NO son proyecciones reales
            if (selector.Body is Microsoft.EntityFrameworkCore.Query.IncludeExpression includeExpression)
            {
                var includeVisitor = new IncludeExtractionVisitor();
                includeVisitor.Visit(includeExpression);

                var firestoreQueryExpression = (FirestoreQueryExpression)source.QueryExpression;

                foreach (var navigation in includeVisitor.DetectedNavigations)
                {
                    // AddInclude ya maneja duplicados internamente
                    firestoreQueryExpression.AddInclude(navigation);
                }

                // Agregar IncludeInfo con filtros extraídos
                foreach (var includeInfo in includeVisitor.DetectedIncludes)
                {
                    // Evitar duplicados usando EffectiveNavigationName
                    if (!firestoreQueryExpression.PendingIncludesWithFilters.Any(i =>
                        i.EffectiveNavigationName == includeInfo.EffectiveNavigationName))
                    {
                        firestoreQueryExpression.AddIncludeWithFilters(includeInfo);
                    }
                }

                // Include no es una proyección real, retornar source sin modificar proyección
                return source;
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

            // TODO: Proyecciones serán implementadas en Fase 3 con FirestoreProjectionTranslator
            // Por ahora retornamos source sin modificar - los tests de proyección están en Skip
            return source;
        }

        /// <summary>
        /// Visitor that detects access to subcollection navigations in an expression.
        /// EF Core transforms c.Pedidos in different ways:
        /// 1. Simple access: MaterializeCollectionNavigationExpression
        /// 2. With LINQ operations: EntityQueryRootExpression with correlated Where
        /// </summary>
        private class SubcollectionAccessDetector : ExpressionVisitor
        {
            private readonly IEntityType _rootEntityType;
            private readonly List<IReadOnlyNavigation> _detectedNavigations = new();
            private readonly IModel _model;

            public SubcollectionAccessDetector(IEntityType rootEntityType)
            {
                _rootEntityType = rootEntityType;
                _model = rootEntityType.Model;
            }

            public bool HasSubcollectionAccess => _detectedNavigations.Count > 0;
            public IReadOnlyList<IReadOnlyNavigation> DetectedNavigations => _detectedNavigations;

            protected override Expression VisitExtension(Expression node)
            {
                var typeName = node.GetType().Name;

                // Pattern 1: EF Core transforms c.Pedidos into MaterializeCollectionNavigationExpression
                if (typeName == "MaterializeCollectionNavigationExpression")
                {
                    var navigationProperty = node.GetType().GetProperty("Navigation");
                    if (navigationProperty != null)
                    {
                        var navigation = navigationProperty.GetValue(node) as IReadOnlyNavigation;
                        if (navigation != null && navigation.IsCollection)
                        {
                            AddNavigationIfNotExists(navigation);
                        }
                    }
                }

                // Pattern 2: EF Core transforms c.Pedidos.Where/Select/etc into EntityQueryRootExpression
                // with a correlated subquery. Detect by checking if it's a query on a related entity type.
                if (typeName == "EntityQueryRootExpression")
                {
                    var entityTypeProperty = node.GetType().GetProperty("EntityType");
                    if (entityTypeProperty != null)
                    {
                        var queryEntityType = entityTypeProperty.GetValue(node) as IEntityType;
                        if (queryEntityType != null)
                        {
                            // Check if this entity type is a navigation target from the root entity
                            foreach (var navigation in _rootEntityType.GetNavigations())
                            {
                                if (navigation.IsCollection &&
                                    navigation.TargetEntityType == queryEntityType)
                                {
                                    AddNavigationIfNotExists(navigation);
                                }
                            }

                            // Also check if this is a nested navigation (level 2+)
                            // by checking all already detected navigations' target types
                            foreach (var detectedNav in _detectedNavigations.ToList())
                            {
                                foreach (var nestedNav in detectedNav.TargetEntityType.GetNavigations())
                                {
                                    if (nestedNav.IsCollection &&
                                        nestedNav.TargetEntityType == queryEntityType)
                                    {
                                        AddNavigationIfNotExists(nestedNav);
                                    }
                                }
                            }
                        }
                    }
                }

                return base.VisitExtension(node);
            }

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                // LINQ methods on subcollections - visit arguments recursively
                return base.VisitMethodCall(node);
            }

            protected override Expression VisitMember(MemberExpression node)
            {
                // Direct member access (before EF Core transformation)
                if (node.Expression is ParameterExpression)
                {
                    var navigation = _rootEntityType.FindNavigation(node.Member.Name);
                    if (navigation != null && navigation.IsCollection)
                    {
                        AddNavigationIfNotExists(navigation);
                    }
                }

                return base.VisitMember(node);
            }

            private void AddNavigationIfNotExists(IReadOnlyNavigation navigation)
            {
                if (!_detectedNavigations.Any(n =>
                    n.Name == navigation.Name &&
                    n.DeclaringEntityType == navigation.DeclaringEntityType))
                {
                    _detectedNavigations.Add(navigation);
                }
            }
        }

        /// <summary>
        /// Checks if an expression contains EF Core internal expressions that cannot be compiled directly.
        /// </summary>
        private bool ContainsNonCompilableExpressions(Expression expression)
        {
            // Check for EF Core internal types that indicate this is not a user projection
            if (expression is Microsoft.EntityFrameworkCore.Query.StructuralTypeShaperExpression)
                return true;

            if (expression is Microsoft.EntityFrameworkCore.Query.ProjectionBindingExpression)
                return true;

            // Recursively check children
            switch (expression)
            {
                case UnaryExpression unary:
                    return ContainsNonCompilableExpressions(unary.Operand);

                case BinaryExpression binary:
                    return ContainsNonCompilableExpressions(binary.Left) ||
                           ContainsNonCompilableExpressions(binary.Right);

                case NewExpression newExpr:
                    return newExpr.Arguments.Any(ContainsNonCompilableExpressions);

                case MemberInitExpression memberInit:
                    return ContainsNonCompilableExpressions(memberInit.NewExpression) ||
                           memberInit.Bindings.OfType<MemberAssignment>()
                               .Any(b => ContainsNonCompilableExpressions(b.Expression));

                case MethodCallExpression methodCall:
                    return (methodCall.Object != null && ContainsNonCompilableExpressions(methodCall.Object)) ||
                           methodCall.Arguments.Any(ContainsNonCompilableExpressions);

                case ConditionalExpression conditional:
                    return ContainsNonCompilableExpressions(conditional.Test) ||
                           ContainsNonCompilableExpressions(conditional.IfTrue) ||
                           ContainsNonCompilableExpressions(conditional.IfFalse);

                default:
                    return false;
            }
        }

        protected override ShapedQueryExpression? TranslateWhere(
            ShapedQueryExpression source,
            LambdaExpression predicate)
        {
            // Replace runtime parameters before delegating to the Slice
            var parameterReplacer = new RuntimeParameterReplacer(QueryCompilationContext);
            var evaluatedBody = parameterReplacer.Visit(predicate.Body);

            return FirestoreQueryExpression.TranslateWhere(new(source, evaluatedBody));
        }

        #endregion

        #region Not Implemented Methods

        protected override ShapedQueryExpression? TranslateAll(ShapedQueryExpression source, LambdaExpression predicate)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateAny(ShapedQueryExpression source, LambdaExpression? predicate)
            => FirestoreQueryExpression.TranslateAny(new(source, predicate));

        protected override ShapedQueryExpression? TranslateAverage(ShapedQueryExpression source, LambdaExpression? selector, Type resultType)
        {
            if (selector == null)
            {
                return null; // Client-side evaluation
            }

            var propertyName = ExtractPropertyNameFromKeySelector(selector);
            if (propertyName == null)
            {
                return null; // Client-side evaluation
            }

            var firestoreQueryExpression = (FirestoreQueryExpression)source.QueryExpression;
            var newQueryExpression = firestoreQueryExpression.WithAverage(propertyName, resultType);

            return source.UpdateQueryExpression(newQueryExpression);
        }

        protected override ShapedQueryExpression? TranslateCast(ShapedQueryExpression source, Type castType)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateConcat(ShapedQueryExpression source1, ShapedQueryExpression source2)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateContains(ShapedQueryExpression source, Expression item)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateCount(ShapedQueryExpression source, LambdaExpression? predicate)
            => FirestoreQueryExpression.TranslateCount(new(source, predicate));

        protected override ShapedQueryExpression? TranslateDefaultIfEmpty(ShapedQueryExpression source, Expression? defaultValue)
            => FirestoreQueryExpression.TranslateDefaultIfEmpty(new(source, defaultValue));

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
        {
            if (selector == null)
            {
                return null; // Client-side evaluation
            }

            var propertyName = ExtractPropertyNameFromKeySelector(selector);
            if (propertyName == null)
            {
                return null; // Client-side evaluation
            }

            var firestoreQueryExpression = (FirestoreQueryExpression)source.QueryExpression;
            var newQueryExpression = firestoreQueryExpression.WithMax(propertyName, resultType);

            return source.UpdateQueryExpression(newQueryExpression);
        }

        protected override ShapedQueryExpression? TranslateMin(ShapedQueryExpression source, LambdaExpression? selector, Type resultType)
        {
            if (selector == null)
            {
                return null; // Client-side evaluation
            }

            var propertyName = ExtractPropertyNameFromKeySelector(selector);
            if (propertyName == null)
            {
                return null; // Client-side evaluation
            }

            var firestoreQueryExpression = (FirestoreQueryExpression)source.QueryExpression;
            var newQueryExpression = firestoreQueryExpression.WithMin(propertyName, resultType);

            return source.UpdateQueryExpression(newQueryExpression);
        }

        protected override ShapedQueryExpression? TranslateOfType(ShapedQueryExpression source, Type resultType)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateOrderBy(ShapedQueryExpression source, LambdaExpression keySelector, bool ascending)
            => FirestoreQueryExpression.TranslateOrderBy(new(source, keySelector, ascending, IsFirst: true));

        protected override ShapedQueryExpression? TranslateReverse(ShapedQueryExpression source)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateSelectMany(ShapedQueryExpression source, LambdaExpression collectionSelector, LambdaExpression resultSelector)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateSelectMany(ShapedQueryExpression source, LambdaExpression selector)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateSingleOrDefault(ShapedQueryExpression source, LambdaExpression? predicate, Type returnType, bool returnDefault)
            => FirestoreQueryExpression.TranslateSingleOrDefault(new(source, predicate, returnType, returnDefault));

        protected override ShapedQueryExpression? TranslateSkip(ShapedQueryExpression source, Expression count)
            => FirestoreQueryExpression.TranslateSkip(new(source, count));

        protected override ShapedQueryExpression? TranslateSkipWhile(ShapedQueryExpression source, LambdaExpression predicate)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateSum(ShapedQueryExpression source, LambdaExpression? selector, Type resultType)
        {
            if (selector == null)
            {
                return null; // Client-side evaluation
            }

            var propertyName = ExtractPropertyNameFromKeySelector(selector);
            if (propertyName == null)
            {
                return null; // Client-side evaluation
            }

            var firestoreQueryExpression = (FirestoreQueryExpression)source.QueryExpression;
            var newQueryExpression = firestoreQueryExpression.WithSum(propertyName, resultType);

            return source.UpdateQueryExpression(newQueryExpression);
        }

        protected override ShapedQueryExpression? TranslateTake(ShapedQueryExpression source, Expression count)
            => FirestoreQueryExpression.TranslateLimit(new(source, count, IsLimitToLast: false));

        /// <summary>
        /// Translates TakeLast to Firestore's LimitToLast.
        /// Note: LimitToLast requires an OrderBy clause to work correctly.
        /// </summary>
        private ShapedQueryExpression TranslateTakeLast(ShapedQueryExpression source, Expression count)
            => FirestoreQueryExpression.TranslateLimit(new(source, count, IsLimitToLast: true));

        protected override ShapedQueryExpression? TranslateTakeWhile(ShapedQueryExpression source, LambdaExpression predicate)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateThenBy(ShapedQueryExpression source, LambdaExpression keySelector, bool ascending)
            => FirestoreQueryExpression.TranslateOrderBy(new(source, keySelector, ascending, IsFirst: false));

        protected override ShapedQueryExpression? TranslateUnion(ShapedQueryExpression source1, ShapedQueryExpression source2)
            => throw new NotImplementedException();

        #endregion
    }
}
