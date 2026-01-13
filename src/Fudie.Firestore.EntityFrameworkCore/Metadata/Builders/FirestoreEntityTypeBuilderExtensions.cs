// Archivo: Metadata/Builders/FirestoreEntityTypeBuilderExtensions.cs
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata.Builders;

public static class FirestoreEntityTypeBuilderExtensions
{
    /// <summary>
    /// Configura una propiedad de navegación como subcollection en Firestore.
    /// Auto-registra el entity type hijo si no está en el modelo.
    /// </summary>
    /// <example>
    /// <code>
    /// modelBuilder.Entity&lt;Cliente&gt;(entity =&gt;
    /// {
    ///     entity.SubCollection(c =&gt; c.Pedidos);
    /// });
    /// </code>
    /// </example>
    public static SubCollectionBuilder<TRelatedEntity> SubCollection<TEntity, TRelatedEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, IEnumerable<TRelatedEntity>?>> navigationExpression)
        where TEntity : class
        where TRelatedEntity : class
    {
        return builder.SubCollection(navigationExpression, _ => { });
    }

    /// <summary>
    /// Configura una propiedad de navegación como subcollection en Firestore con configuración adicional.
    /// Permite configurar referencias y arrays embebidos dentro de los documentos de la subcollection.
    /// </summary>
    /// <example>
    /// <code>
    /// modelBuilder.Entity&lt;Cliente&gt;(entity =&gt;
    /// {
    ///     // SubCollection con Reference dentro
    ///     entity.SubCollection(c =&gt; c.Pedidos, pedido =&gt;
    ///     {
    ///         pedido.Reference(p =&gt; p.Vendedor);
    ///     });
    ///
    ///     // SubCollection con ArrayOf dentro
    ///     entity.SubCollection(c =&gt; c.Pedidos, pedido =&gt;
    ///     {
    ///         pedido.ArrayOf(p =&gt; p.Lineas, linea =&gt;
    ///         {
    ///             linea.Reference(l =&gt; l.Producto);
    ///         });
    ///     });
    /// });
    /// </code>
    /// </example>
    public static SubCollectionBuilder<TRelatedEntity> SubCollection<TEntity, TRelatedEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, IEnumerable<TRelatedEntity>?>> navigationExpression,
        Action<SubCollectionElementBuilder<TRelatedEntity>> configure)
        where TEntity : class
        where TRelatedEntity : class
    {
        var memberInfo = navigationExpression.GetMemberAccess();
        var propertyName = memberInfo.Name;

        var entityType = builder.Metadata;
        var mutableModel = (IMutableModel)entityType.Model;

        // Auto-registrar el entity type si no existe (SubCollections no necesitan DbSet)
        var targetEntityType = mutableModel.FindEntityType(typeof(TRelatedEntity))
            ?? mutableModel.AddEntityType(typeof(TRelatedEntity));

        // Configurar la relación HasMany para crear la navegación
        builder.HasMany(navigationExpression)
            .WithOne()
            .HasForeignKey(ConventionHelpers.GetForeignKeyPropertyName<TEntity>())
            .OnDelete(DeleteBehavior.Cascade);

        // Buscar la navegación recién creada
        var navigation = entityType.FindNavigation(propertyName)
            ?? throw new InvalidOperationException(
                $"Navigation property '{propertyName}' not found on entity type '{typeof(TEntity).Name}'.");

        // Crear el builder para configurar elementos de la subcollection
        var elementBuilder = new SubCollectionElementBuilder<TRelatedEntity>(targetEntityType);
        configure(elementBuilder);

        return new SubCollectionBuilder<TRelatedEntity>(targetEntityType, navigation);
    }

    /// <summary>
    /// Configura una propiedad de navegación como DocumentReference en Firestore.
    /// Esto permite FK 1:1 almacenadas como referencias a documentos en otras colecciones.
    /// </summary>
    /// <example>
    /// modelBuilder.Entity&lt;Articulo&gt;(entity =&gt;
    /// {
    ///     entity.Reference(a =&gt; a.Categoria);
    /// });
    /// </example>
    public static EntityTypeBuilder<TEntity> Reference<TEntity, TRelatedEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, TRelatedEntity?>> navigationExpression)
        where TEntity : class
        where TRelatedEntity : class
    {
        var memberInfo = navigationExpression.GetMemberAccess();
        var propertyName = memberInfo.Name;

        var entityType = builder.Metadata;

        // Verificar que el entity type relacionado existe en el modelo
        var targetEntityType = entityType.Model.FindEntityType(typeof(TRelatedEntity));
        if (targetEntityType == null)
        {
            throw new InvalidOperationException(
                $"Entity type '{typeof(TRelatedEntity).Name}' must be added to the model " +
                $"(via DbSet<{typeof(TRelatedEntity).Name}>) before configuring as DocumentReference.");
        }

        // Buscar la navegación
        var navigation = entityType.FindNavigation(propertyName);
        if (navigation == null)
        {
            throw new InvalidOperationException(
                $"Navigation property '{propertyName}' not found on entity type '{typeof(TEntity).Name}'. " +
                $"Make sure '{typeof(TRelatedEntity).Name}' is configured in the model with a DbSet.");
        }

        // Marcar la navigation como DocumentReference
        navigation.SetAnnotation("Firestore:DocumentReference", true);

        return builder;
    }
}