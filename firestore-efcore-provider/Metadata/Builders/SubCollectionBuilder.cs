// Archivo: Metadata/Builders/SubCollectionBuilder.cs
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Firestore.EntityFrameworkCore.Metadata.Builders;

public class SubCollectionBuilder<TEntity> where TEntity : class
{
    private readonly IMutableEntityType _entityType;
    private readonly IMutableNavigation _navigation;

    internal SubCollectionBuilder(
        IMutableEntityType entityType,
        IMutableNavigation navigation)
    {
        _entityType = entityType;
        _navigation = navigation;
        
        // Marcar la navigation como subcollection
        _navigation.SetAnnotation("Firestore:SubCollection", true);
    }

    /// <summary>
    /// Configura una subcollection anidada
    /// </summary>
    public SubCollectionBuilder<TRelatedEntity> SubCollection<TRelatedEntity>(
        Expression<Func<TEntity, IEnumerable<TRelatedEntity>>> navigationExpression)
        where TRelatedEntity : class
    {
        var memberInfo = navigationExpression.GetMemberAccess();
        var propertyName = memberInfo.Name;
        
        // Obtener el entity type de TEntity
        var model = _entityType.Model;
        var relatedEntityType = model.FindEntityType(typeof(TEntity));
        
        if (relatedEntityType == null)
        {
            throw new InvalidOperationException(
                $"Entity type '{typeof(TEntity).Name}' must be configured in the model before configuring subcollections.");
        }
        
        var relatedNavigation = relatedEntityType.FindNavigation(propertyName);
        
        if (relatedNavigation == null)
        {
            throw new InvalidOperationException(
                $"Navigation property '{propertyName}' not found on entity type '{typeof(TEntity).Name}'.");
        }
        
        return new SubCollectionBuilder<TRelatedEntity>(
            relatedEntityType,
            relatedNavigation);
    }
}