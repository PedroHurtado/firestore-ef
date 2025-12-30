using Firestore.EntityFrameworkCore.Query.Ast;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Translators
{
    /// <summary>
    /// Translates LeftJoin expressions to IncludeInfo.
    ///
    /// In Firestore, we don't support real JOINs. EF Core generates LeftJoin internally
    /// when processing Include() for navigations. This translator extracts the navigation
    /// information and converts it to an IncludeInfo that the executor can use to load
    /// related data.
    ///
    /// Strategy:
    /// 1. Try to extract navigation name from outerKeySelector (e.g., c => c.Pedidos)
    /// 2. If that fails, fallback to detecting navigation by matching target entity type
    /// </summary>
    internal class FirestoreLeftJoinTranslator
    {
        /// <summary>
        /// Translates a LeftJoin operation to an IncludeInfo.
        /// </summary>
        /// <param name="outerKeySelector">The outer key selector expression (e.g., c => c.Pedidos)</param>
        /// <param name="outerEntityType">The entity type of the outer query</param>
        /// <param name="innerEntityType">The entity type of the inner query (the navigation target)</param>
        /// <returns>IncludeInfo if navigation found, null otherwise</returns>
        public IncludeInfo? Translate(
            LambdaExpression outerKeySelector,
            IEntityType outerEntityType,
            IEntityType innerEntityType)
        {
            // Strategy 1: Extract navigation from outerKeySelector
            if (outerKeySelector.Body is MemberExpression memberExpression)
            {
                var memberName = memberExpression.Member.Name;
                var navigation = outerEntityType.FindNavigation(memberName);

                if (navigation != null)
                {
                    return new IncludeInfo(navigation.Name, navigation.IsCollection);
                }
            }

            // Strategy 2: Fallback - find navigation by target entity type
            foreach (var navigation in outerEntityType.GetNavigations())
            {
                if (navigation.TargetEntityType.ClrType == innerEntityType.ClrType)
                {
                    return new IncludeInfo(navigation.Name, navigation.IsCollection);
                }
            }

            // No navigation found
            return null;
        }
    }
}
