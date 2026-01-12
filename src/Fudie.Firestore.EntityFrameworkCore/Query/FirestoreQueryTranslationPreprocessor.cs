using Fudie.Firestore.EntityFrameworkCore.Infrastructure;
using Fudie.Firestore.EntityFrameworkCore.Query.Translators;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Fudie.Firestore.EntityFrameworkCore.Query
{
    /// <summary>
    /// Custom QueryTranslationPreprocessor that intercepts Include expressions
    /// targeting properties inside ComplexTypes before EF Core's NavigationExpandingExpressionVisitor
    /// rejects them.
    /// Also handles TakeLast which is not natively supported by EF Core.
    ///
    /// NOTE: Filtered Include extraction was removed. The FirestoreIncludeTranslator
    /// now handles translation directly in TranslateInclude, using FirestoreWhereTranslator,
    /// FirestoreOrderByTranslator, and FirestoreLimitTranslator for consistency.
    /// </summary>
    public class FirestoreQueryTranslationPreprocessor : QueryTranslationPreprocessor
    {
        private readonly IFirestoreCollectionManager _collectionManager;

        public FirestoreQueryTranslationPreprocessor(
            QueryTranslationPreprocessorDependencies dependencies,
            QueryCompilationContext queryCompilationContext,
            IFirestoreCollectionManager collectionManager)
            : base(dependencies, queryCompilationContext)
        {
            _collectionManager = collectionManager;
        }

        public override Expression Process(Expression query)
        {
            // Extract, translate to IncludeInfo, and remove ComplexType Includes before EF Core processes them
            var complexTypeIncludeTranslator = new ComplexTypeIncludeTranslator(QueryCompilationContext, _collectionManager);
            query = complexTypeIncludeTranslator.Visit(query);

            // Extract, translate to IncludeInfo, and remove ArrayOf Reference Includes before EF Core processes them
            var arrayOfIncludeTranslator = new ArrayOfIncludeTranslator(QueryCompilationContext, _collectionManager);
            query = arrayOfIncludeTranslator.Visit(query);

            // Transform TakeLast before EF Core's NavigationExpandingExpressionVisitor rejects it
            var takeLastTransformer = new TakeLastTransformingVisitor();
            query = takeLastTransformer.Visit(query);

            // Then let EF Core process the remaining (valid) Includes
            return base.Process(query);
        }
    }

    /// <summary>
    /// Transforms TakeLast(n) into a marker expression that can be processed later.
    /// TakeLast(n) is equivalent to reversing, taking first n, and reversing back,
    /// but Firestore has native LimitToLast support.
    /// We transform TakeLast into a custom method call that our translator can handle.
    /// </summary>
    internal class TakeLastTransformingVisitor : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // Check if this is a TakeLast call
            if (node.Method.Name == "TakeLast" &&
                node.Method.DeclaringType == typeof(Queryable) &&
                node.Arguments.Count == 2)
            {
                // Visit the source first
                var source = Visit(node.Arguments[0]);
                var count = node.Arguments[1];

                // Create a call to our custom TakeLast marker
                // We use Take but mark it specially by wrapping in our custom expression
                return new FirestoreTakeLastExpression(source, count, node.Type);
            }

            return base.VisitMethodCall(node);
        }
    }

    /// <summary>
    /// Marker expression for TakeLast that survives EF Core's processing pipeline.
    /// </summary>
    public class FirestoreTakeLastExpression : Expression
    {
        public Expression Source { get; }
        public Expression Count { get; }
        private readonly Type _type;

        public FirestoreTakeLastExpression(Expression source, Expression count, Type type)
        {
            Source = source;
            Count = count;
            _type = type;
        }

        public override Type Type => _type;
        public override ExpressionType NodeType => ExpressionType.Extension;

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var newSource = visitor.Visit(Source);
            var newCount = visitor.Visit(Count);

            if (newSource != Source || newCount != Count)
            {
                return new FirestoreTakeLastExpression(newSource, newCount, _type);
            }

            return this;
        }

        public override string ToString()
        {
            return $"FirestoreTakeLast({Source}, {Count})";
        }
    }
}