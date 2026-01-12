using System;
using System.Linq.Expressions;

namespace Fudie.Firestore.EntityFrameworkCore.Query.Visitors
{
    /// <summary>
    /// Marker expression for ArrayContains operations.
    /// Created when detecting the pattern: e.ArrayField.Contains(value)
    /// which EF Core transforms to: EF.Property&lt;List&lt;T&gt;&gt;(e, "Field").AsQueryable().Contains(value)
    /// </summary>
    internal class FirestoreArrayContainsExpression : Expression
    {
        public string PropertyName { get; }
        public Expression ValueExpression { get; }

        public FirestoreArrayContainsExpression(string propertyName, Expression valueExpression)
        {
            PropertyName = propertyName;
            ValueExpression = valueExpression;
        }

        public override ExpressionType NodeType => ExpressionType.Extension;
        public override Type Type => typeof(bool);
        public override bool CanReduce => false;

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var newValue = visitor.Visit(ValueExpression);
            if (newValue != ValueExpression)
            {
                return new FirestoreArrayContainsExpression(PropertyName, newValue);
            }
            return this;
        }
    }
}
