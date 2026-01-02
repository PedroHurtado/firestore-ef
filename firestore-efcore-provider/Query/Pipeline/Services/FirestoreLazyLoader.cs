using Firestore.EntityFrameworkCore.Query.Ast;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Firestore.EntityFrameworkCore.Query.Pipeline;

/// <summary>
/// Lazy loader that executes sub-pipelines to load navigation properties.
/// When a proxy intercepts access to a navigation property, this loader:
/// 1. Builds the appropriate query for the navigation
/// 2. Executes the full pipeline (including tracking and proxying)
/// 3. Assigns the result to the navigation property
/// </summary>
public class FirestoreLazyLoader : ILazyLoader
{
    private readonly IQueryPipelineMediator _mediator;
    private readonly IFirestoreQueryContext _queryContext;
    private readonly HashSet<(object Entity, string Navigation)> _loadedNavigations = new();

    /// <summary>
    /// Creates a new lazy loader.
    /// </summary>
    /// <param name="mediator">The pipeline mediator for executing sub-pipelines.</param>
    /// <param name="queryContext">The query context for building navigation queries.</param>
    public FirestoreLazyLoader(IQueryPipelineMediator mediator, IFirestoreQueryContext queryContext)
    {
        _mediator = mediator;
        _queryContext = queryContext;
    }

    /// <inheritdoc />
    public void Load(object entity, [CallerMemberName] string navigationName = "")
    {
        LoadAsync(entity, CancellationToken.None, navigationName).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public void SetLoaded(object entity, [CallerMemberName] string navigationName = "", bool loaded = true)
    {
        if (loaded)
        {
            _loadedNavigations.Add((entity, navigationName));
        }
        else
        {
            _loadedNavigations.Remove((entity, navigationName));
        }
    }

    /// <inheritdoc />
    public bool IsLoaded(object entity, [CallerMemberName] string navigationName = "")
    {
        return _loadedNavigations.Contains((entity, navigationName));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _loadedNavigations.Clear();
    }

    /// <inheritdoc />
    public async Task LoadAsync(
        object entity,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string navigationName = "")
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        if (string.IsNullOrEmpty(navigationName))
            throw new ArgumentException("Navigation name cannot be empty", nameof(navigationName));

        // Skip if already loaded
        if (IsLoaded(entity, navigationName))
            return;

        // Get entity type - for proxies, we need to check BaseType
        var entityClrType = entity.GetType();
        var entityType = _queryContext.Model.FindEntityType(entityClrType)
            ?? _queryContext.Model.FindEntityType(entityClrType.BaseType!);

        if (entityType == null)
            return;

        var navigation = entityType.FindNavigation(navigationName);
        if (navigation == null)
            return;

        // Build and execute sub-pipeline for the navigation
        var relatedEntities = await ExecuteNavigationQueryAsync(entity, entityType, navigation, cancellationToken);

        // Assign to navigation property
        AssignNavigationValue(entity, navigation, relatedEntities);

        // Mark navigation as loaded
        SetLoaded(entity, navigationName, true);
    }

    private async Task<IReadOnlyList<object>> ExecuteNavigationQueryAsync(
        object entity,
        IEntityType entityType,
        INavigation navigation,
        CancellationToken cancellationToken)
    {
        // Build context for sub-pipeline
        var context = BuildNavigationContext(entity, entityType, navigation);

        // Execute sub-pipeline
        var results = new List<object>();
        await foreach (var item in _mediator.ExecuteAsync<object>(context, cancellationToken))
        {
            results.Add(item);
        }

        return results;
    }

    private PipelineContext BuildNavigationContext(object entity, IEntityType entityType, INavigation navigation)
    {
        var targetEntityType = navigation.TargetEntityType;
        var targetClrType = targetEntityType.ClrType;

        // Build AST for navigation query
        var ast = BuildNavigationAst(entity, entityType, navigation);

        return new PipelineContext
        {
            Ast = ast,
            QueryContext = _queryContext,
            IsTracking = true, // Always track lazy-loaded entities
            ResultType = targetClrType,
            Kind = QueryKind.Entity,
            EntityType = targetClrType
        };
    }

    private FirestoreQueryExpression BuildNavigationAst(object entity, IEntityType entityType, INavigation navigation)
    {
        var targetEntityType = navigation.TargetEntityType;
        var collectionName = GetCollectionName(targetEntityType);

        // Get foreign key info
        var foreignKey = navigation.ForeignKey;
        var principalKey = foreignKey.PrincipalKey;

        // Create base query expression
        var ast = new FirestoreQueryExpression(targetEntityType, collectionName);

        if (navigation.IsCollection)
        {
            // Collection: child entities have FK pointing to parent
            // WHERE child.FK == parent.PK
            var parentKeyValue = GetKeyValue(entity, principalKey);
            var childFkProperty = foreignKey.Properties.First();

            var filter = new FirestoreWhereClause(
                childFkProperty.Name,
                FirestoreOperator.EqualTo,
                Expression.Constant(parentKeyValue));

            ast.AddFilter(filter);
        }
        else
        {
            // Reference: parent has FK pointing to child
            // Use WithIdValueExpression for single document lookup by ID
            var fkValue = GetForeignKeyValue(entity, foreignKey);
            if (fkValue != null)
            {
                ast.WithIdValueExpression(Expression.Constant(fkValue.ToString()));
            }
        }

        return ast;
    }

    private string GetCollectionName(IEntityType entityType)
    {
        // Try to get collection name from annotation or use ClrType name
        var collectionName = entityType.FindAnnotation("Firestore:CollectionName")?.Value as string;
        return collectionName ?? entityType.ClrType.Name;
    }

    private object? GetKeyValue(object entity, IKey key)
    {
        var property = key.Properties.First();
        return property.GetGetter().GetClrValue(entity);
    }

    private object? GetForeignKeyValue(object entity, IForeignKey foreignKey)
    {
        var property = foreignKey.Properties.First();
        return property.GetGetter().GetClrValue(entity);
    }

    private void AssignNavigationValue(object entity, INavigation navigation, IReadOnlyList<object> relatedEntities)
    {
        var propertyInfo = navigation.PropertyInfo;
        if (propertyInfo == null)
            return;

        if (navigation.IsCollection)
        {
            // Create appropriate collection type
            var collection = CreateCollection(navigation.ClrType, relatedEntities);
            propertyInfo.SetValue(entity, collection);
        }
        else
        {
            // Reference navigation - single entity or null
            propertyInfo.SetValue(entity, relatedEntities.FirstOrDefault());
        }
    }

    private object CreateCollection(Type collectionType, IReadOnlyList<object> items)
    {
        // Handle common collection types
        var elementType = collectionType.IsGenericType
            ? collectionType.GetGenericArguments()[0]
            : typeof(object);

        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (IList)Activator.CreateInstance(listType)!;

        foreach (var item in items)
        {
            list.Add(item);
        }

        return list;
    }
}
