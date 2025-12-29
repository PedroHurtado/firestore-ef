using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Preprocessing;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using System;
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

        #region Translate Methods

        protected override ShapedQueryExpression? TranslateAny(ShapedQueryExpression source, LambdaExpression? predicate)
            => FirestoreQueryExpression.TranslateAny(new(source, predicate));

        protected override ShapedQueryExpression? TranslateAverage(ShapedQueryExpression source, LambdaExpression? selector, Type resultType)
            => FirestoreQueryExpression.TranslateAverage(new(source, selector, resultType));

        protected override ShapedQueryExpression? TranslateCount(ShapedQueryExpression source, LambdaExpression? predicate)
            => FirestoreQueryExpression.TranslateCount(new(source, predicate));

        protected override ShapedQueryExpression? TranslateDefaultIfEmpty(ShapedQueryExpression source, Expression? defaultValue)
            => FirestoreQueryExpression.TranslateDefaultIfEmpty(new(source, defaultValue));

        protected override ShapedQueryExpression? TranslateFirstOrDefault(
            ShapedQueryExpression source,
            LambdaExpression? predicate,
            Type returnType,
            bool returnDefault)
            => FirestoreQueryExpression.TranslateFirstOrDefault(new(source, predicate, returnType, returnDefault));

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
                var newQueryExpression = outerQueryExpression.AddInclude(navigation.Name, navigation.IsCollection);
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
                    var newQueryExpression = outerQueryExpression.AddInclude(nav.Name, nav.IsCollection);
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

        protected override ShapedQueryExpression? TranslateMax(ShapedQueryExpression source, LambdaExpression? selector, Type resultType)
            => FirestoreQueryExpression.TranslateMax(new(source, selector, resultType));

        protected override ShapedQueryExpression? TranslateMin(ShapedQueryExpression source, LambdaExpression? selector, Type resultType)
            => FirestoreQueryExpression.TranslateMin(new(source, selector, resultType));

        protected override ShapedQueryExpression? TranslateOrderBy(ShapedQueryExpression source, LambdaExpression keySelector, bool ascending)
            => FirestoreQueryExpression.TranslateOrderBy(new(source, keySelector, ascending, IsFirst: true));

        protected override ShapedQueryExpression TranslateSelect(
            ShapedQueryExpression source,
            LambdaExpression selector)
        {
            // Include expressions - delegate to MicroDomain (one-liner)
            if (selector.Body is IncludeExpression includeExpression)
                return FirestoreQueryExpression.TranslateInclude(new(source, includeExpression));

            // Identity projection (x => x)
            if (selector.Body == selector.Parameters[0])
                return source;

            // Type conversion projection
            if (selector.Body is UnaryExpression unary &&
                unary.NodeType == ExpressionType.Convert &&
                unary.Operand == selector.Parameters[0])
                return source;

            // TODO: Real projections will be implemented in Phase 3 with FirestoreProjectionTranslator
            return source;
        }

        protected override ShapedQueryExpression? TranslateSingleOrDefault(ShapedQueryExpression source, LambdaExpression? predicate, Type returnType, bool returnDefault)
            => FirestoreQueryExpression.TranslateSingleOrDefault(new(source, predicate, returnType, returnDefault));

        protected override ShapedQueryExpression? TranslateSkip(ShapedQueryExpression source, Expression count)
            => FirestoreQueryExpression.TranslateSkip(new(source, count));

        protected override ShapedQueryExpression? TranslateSum(ShapedQueryExpression source, LambdaExpression? selector, Type resultType)
            => FirestoreQueryExpression.TranslateSum(new(source, selector, resultType));

        protected override ShapedQueryExpression? TranslateTake(ShapedQueryExpression source, Expression count)
            => FirestoreQueryExpression.TranslateLimit(new(source, count, IsLimitToLast: false));

        /// <summary>
        /// Translates TakeLast to Firestore's LimitToLast.
        /// Note: LimitToLast requires an OrderBy clause to work correctly.
        /// </summary>
        private ShapedQueryExpression TranslateTakeLast(ShapedQueryExpression source, Expression count)
            => FirestoreQueryExpression.TranslateLimit(new(source, count, IsLimitToLast: true));

        protected override ShapedQueryExpression? TranslateThenBy(ShapedQueryExpression source, LambdaExpression keySelector, bool ascending)
            => FirestoreQueryExpression.TranslateOrderBy(new(source, keySelector, ascending, IsFirst: false));

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

        protected override ShapedQueryExpression? TranslateCast(ShapedQueryExpression source, Type castType)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateConcat(ShapedQueryExpression source1, ShapedQueryExpression source2)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateContains(ShapedQueryExpression source, Expression item)
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

        protected override ShapedQueryExpression? TranslateLongCount(ShapedQueryExpression source, LambdaExpression? predicate)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateOfType(ShapedQueryExpression source, Type resultType)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateReverse(ShapedQueryExpression source)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateSelectMany(ShapedQueryExpression source, LambdaExpression collectionSelector, LambdaExpression resultSelector)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateSelectMany(ShapedQueryExpression source, LambdaExpression selector)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateSkipWhile(ShapedQueryExpression source, LambdaExpression predicate)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateTakeWhile(ShapedQueryExpression source, LambdaExpression predicate)
            => throw new NotImplementedException();

        protected override ShapedQueryExpression? TranslateUnion(ShapedQueryExpression source1, ShapedQueryExpression source2)
            => throw new NotImplementedException();

        #endregion
    }
}
