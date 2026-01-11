using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Query.Ast;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Linq;
using System.Linq.Expressions;

namespace Firestore.EntityFrameworkCore.Query.Translators;

/// <summary>
/// Translates LeftJoin expressions to IncludeInfo.
///
/// In Firestore, we don't support real JOINs. EF Core generates LeftJoin internally
/// when processing Include() or projections with navigations. This translator extracts
/// the navigation information and converts it to an IncludeInfo that the executor can
/// use to load related data.
///
/// EF Core generates OuterKeySelector as: Property(entity, "ForeignKeyName")
/// For nested navigations: Property(l.Inner, "ForeignKeyName")
/// We use the FK name to find the corresponding navigation property.
/// </summary>
internal class FirestoreLeftJoinTranslator
{
    private readonly IFirestoreCollectionManager _collectionManager;

    public FirestoreLeftJoinTranslator(IFirestoreCollectionManager collectionManager)
    {
        _collectionManager = collectionManager;
    }

    /// <summary>
    /// Translates a LeftJoin operation to an IncludeInfo.
    /// </summary>
    /// <param name="outerKeySelector">The outer key selector expression containing the FK property</param>
    /// <param name="outerEntityType">The entity type of the outer query</param>
    /// <param name="innerEntityType">The entity type of the inner query (unused, kept for API compatibility)</param>
    /// <returns>IncludeInfo if navigation found, null otherwise</returns>
    public IncludeInfo? Translate(
        LambdaExpression outerKeySelector,
        IEntityType outerEntityType,
        IEntityType innerEntityType)
    {
        var (navigation, parentClrType) = FindNavigationByForeignKey(outerKeySelector.Body, outerEntityType);

        return navigation != null ? CreateIncludeInfo(navigation, parentClrType) : null;
    }

    /// <summary>
    /// Finds a navigation property by its foreign key name and returns the parent CLR type if nested.
    /// EF Core generates: Property(entity, "ForeignKeyName")
    /// For nested navigations: Property(l.Inner, "ForeignKeyName") where l.Inner has the actual type
    /// </summary>
    private static (INavigation? Navigation, System.Type? ParentClrType) FindNavigationByForeignKey(Expression expression, IEntityType entityType)
    {
        if (expression is not MethodCallExpression { Method.Name: "Property", Arguments.Count: >= 2 } methodCall)
            return (null, null);

        if (methodCall.Arguments[1] is not ConstantExpression { Value: string fkPropertyName })
            return (null, null);

        var sourceExpression = methodCall.Arguments[0];
        var actualEntityType = GetActualEntityType(sourceExpression, entityType);

        // If the source type differs from the root entity type, this is a nested navigation
        System.Type? parentClrType = actualEntityType.ClrType != entityType.ClrType
            ? actualEntityType.ClrType
            : null;

        var navigation = actualEntityType
            .GetNavigations()
            .FirstOrDefault(n => n.ForeignKey.Properties.Any(p => p.Name == fkPropertyName));

        return (navigation, parentClrType);
    }

    /// <summary>
    /// Gets the actual entity type from the source expression.
    /// Handles cases like l.Inner, l.Outer where the CLR type differs from the passed entityType.
    /// </summary>
    private static IEntityType GetActualEntityType(Expression sourceExpression, IEntityType fallbackEntityType)
    {
        var clrType = sourceExpression.Type;

        if (clrType == fallbackEntityType.ClrType)
            return fallbackEntityType;

        var model = fallbackEntityType.Model;
        var actualEntityType = model.FindEntityType(clrType);

        return actualEntityType ?? fallbackEntityType;
    }

    /// <summary>
    /// Creates an IncludeInfo from a navigation.
    /// </summary>
    private IncludeInfo CreateIncludeInfo(INavigation navigation, System.Type? parentClrType)
    {
        var targetEntityType = navigation.TargetEntityType;
        var targetClrType = targetEntityType.ClrType;
        var collectionName = _collectionManager.GetCollectionName(targetClrType);
        var pkProperties = targetEntityType.FindPrimaryKey()?.Properties;
        var primaryKeyPropertyName = pkProperties is { Count: > 0 } ? pkProperties[0].Name : null;

        return new IncludeInfo(
            navigation.Name,
            navigation.IsCollection,
            collectionName,
            targetClrType,
            primaryKeyPropertyName,
            parentClrType);
    }
}