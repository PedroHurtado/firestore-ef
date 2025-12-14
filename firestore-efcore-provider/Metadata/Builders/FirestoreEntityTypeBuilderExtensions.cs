// Archivo: Metadata/Builders/FirestoreEntityTypeBuilderExtensions.cs
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Firestore.EntityFrameworkCore.Metadata.Builders;

public static class FirestoreEntityTypeBuilderExtensions
{
    /// <summary>
    /// Configura una propiedad de navegación como subcollection en Firestore
    /// </summary>
    public static SubCollectionBuilder<TRelatedEntity> SubCollection<TEntity, TRelatedEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, IEnumerable<TRelatedEntity>>> navigationExpression)
        where TEntity : class
        where TRelatedEntity : class
    {
        var memberInfo = navigationExpression.GetMemberAccess();
        var propertyName = memberInfo.Name;

        var entityType = builder.Metadata;

        // Verificar que el entity type relacionado existe en el modelo
        var targetEntityType = entityType.Model.FindEntityType(typeof(TRelatedEntity)) ?? throw new InvalidOperationException(
                $"Entity type '{typeof(TRelatedEntity).Name}' must be added to the model (via DbSet<{typeof(TRelatedEntity).Name}>) before configuring as subcollection.");

        // Buscar la navegación - debería existir porque la propiedad existe en la clase
        var navigation = entityType.FindNavigation(propertyName) ?? throw new InvalidOperationException(
                $"Navigation property '{propertyName}' not found on entity type '{typeof(TEntity).Name}'. " +
                $"Make sure '{typeof(TRelatedEntity).Name}' is configured in the model with a DbSet.");
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