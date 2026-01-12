// Archivo: Metadata/Builders/SubCollectionBuilder.cs
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata.Builders;

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
    /// Configura una subcollection anidada.
    /// Auto-registra el entity type hijo si no está en el modelo.
    /// </summary>
    /// <example>
    /// <code>
    /// entity.SubCollection(c =&gt; c.Pedidos)
    ///       .SubCollection(p =&gt; p.Lineas);
    /// </code>
    /// </example>
    public SubCollectionBuilder<TRelatedEntity> SubCollection<TRelatedEntity>(
        Expression<Func<TEntity, IEnumerable<TRelatedEntity>?>> navigationExpression)
        where TRelatedEntity : class
    {
        var memberInfo = navigationExpression.GetMemberAccess();
        var propertyName = memberInfo.Name;

        var mutableModel = (IMutableModel)_entityType.Model;

        // Auto-registrar el entity type hijo si no existe
        var targetEntityType = mutableModel.FindEntityType(typeof(TRelatedEntity))
            ?? mutableModel.AddEntityType(typeof(TRelatedEntity));

        // Obtener el entity type de TEntity (el padre de esta subcollection)
        var parentEntityType = mutableModel.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException(
                $"Entity type '{typeof(TEntity).Name}' must be configured in the model.");

        // Configurar la relación HasMany usando el EntityTypeBuilder
#pragma warning disable EF1001 // Internal EF Core API usage
        var entityTypeBuilder = new EntityTypeBuilder<TEntity>(parentEntityType);
#pragma warning restore EF1001

        entityTypeBuilder.HasMany(navigationExpression)
            .WithOne()
            .HasForeignKey($"{typeof(TEntity).Name}Id");

        // Buscar la navegación recién creada
        var navigation = parentEntityType.FindNavigation(propertyName)
            ?? throw new InvalidOperationException(
                $"Navigation property '{propertyName}' not found on entity type '{typeof(TEntity).Name}'.");

        return new SubCollectionBuilder<TRelatedEntity>(targetEntityType, navigation);
    }

    /// <summary>
    /// Configura una subcollection anidada con configuración adicional.
    /// Permite configurar referencias y arrays embebidos dentro de los documentos de la subcollection.
    /// </summary>
    /// <example>
    /// <code>
    /// entity.SubCollection(c =&gt; c.Pedidos, pedido =&gt;
    /// {
    ///     pedido.Reference(p =&gt; p.Vendedor);
    ///     pedido.ArrayOf(p =&gt; p.Lineas);
    /// });
    /// </code>
    /// </example>
    public SubCollectionBuilder<TRelatedEntity> SubCollection<TRelatedEntity>(
        Expression<Func<TEntity, IEnumerable<TRelatedEntity>?>> navigationExpression,
        Action<SubCollectionElementBuilder<TRelatedEntity>> configure)
        where TRelatedEntity : class
    {
        var memberInfo = navigationExpression.GetMemberAccess();
        var propertyName = memberInfo.Name;

        var mutableModel = (IMutableModel)_entityType.Model;

        // Auto-registrar el entity type hijo si no existe
        var targetEntityType = mutableModel.FindEntityType(typeof(TRelatedEntity))
            ?? mutableModel.AddEntityType(typeof(TRelatedEntity));

        // Obtener el entity type de TEntity (el padre de esta subcollection)
        var parentEntityType = mutableModel.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException(
                $"Entity type '{typeof(TEntity).Name}' must be configured in the model.");

        // Configurar la relación HasMany usando el EntityTypeBuilder
#pragma warning disable EF1001 // Internal EF Core API usage
        var entityTypeBuilder = new EntityTypeBuilder<TEntity>(parentEntityType);
#pragma warning restore EF1001

        entityTypeBuilder.HasMany(navigationExpression)
            .WithOne()
            .HasForeignKey($"{typeof(TEntity).Name}Id");

        // Buscar la navegación recién creada
        var navigation = parentEntityType.FindNavigation(propertyName)
            ?? throw new InvalidOperationException(
                $"Navigation property '{propertyName}' not found on entity type '{typeof(TEntity).Name}'.");

        // Crear el builder para configurar elementos de la subcollection
        var elementBuilder = new SubCollectionElementBuilder<TRelatedEntity>(targetEntityType);
        configure(elementBuilder);

        return new SubCollectionBuilder<TRelatedEntity>(targetEntityType, navigation);
    }
}