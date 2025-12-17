using System;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Visitors
{
    /// <summary>
    /// Marker expression for ArrayContainsAny operations.
    /// Created when detecting the pattern: e.ArrayField.Any(x => list.Contains(x))
    /// which translates to Firestore WhereArrayContainsAny.
    /// </summary>
    internal class FirestoreArrayContainsAnyExpression : Expression
    {
        public string PropertyName { get; }
        public Expression ValuesExpression { get; }

        public FirestoreArrayContainsAnyExpression(string propertyName, Expression valuesExpression)
        {
            PropertyName = propertyName;
            ValuesExpression = valuesExpression;
        }

        public override ExpressionType NodeType => ExpressionType.Extension;
        public override Type Type => typeof(bool);
        public override bool CanReduce => false;

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var newValues = visitor.Visit(ValuesExpression);
            if (newValues != ValuesExpression)
            {
                return new FirestoreArrayContainsAnyExpression(PropertyName, newValues);
            }
            return this;
        }
    }
}
