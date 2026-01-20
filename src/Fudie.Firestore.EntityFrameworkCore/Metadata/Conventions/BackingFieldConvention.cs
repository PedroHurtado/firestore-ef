using System;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

/// <summary>
/// Convention que detecta backing fields para TODAS las propiedades de colección en el modelo.
/// Esto incluye:
/// - ArrayOf (Embedded, Reference, GeoPoint, Primitive)
/// - SubCollections (navegaciones de EF Core marcadas como SubCollection)
///
/// Esta convention centraliza la detección de backing fields en un único lugar,
/// evitando lógica dispersa en el Deserializer o en múltiples conventions.
///
/// Se ejecuta en ModelFinalizing DESPUÉS de que todas las configuraciones
/// (OnModelCreating, otras conventions) se hayan aplicado.
/// </summary>
public class BackingFieldConvention : IModelFinalizingConvention
{
    private const string NavigationBackingFieldAnnotation = "Firestore:Navigation:BackingField";

    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        var model = modelBuilder.Metadata;

        foreach (var entityType in model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            var mutableEntityType = (IMutableEntityType)entityType;

            // 1. Detectar backing fields para propiedades ArrayOf
            DetectArrayOfBackingFields(entityType, clrType, mutableEntityType);

            // 2. Detectar backing fields para navegaciones (SubCollections)
            DetectNavigationBackingFields(entityType, clrType, mutableEntityType);
        }

        // Debug: ver todas las anotaciones de backing fields
        var debug = model.GetEntityTypes()
            .Select(et => new
            {
                Entity = et.ClrType.Name,
                ArrayOfBackingFields = et.GetAnnotations()
                    .Where(a => a.Name.StartsWith("Firestore:ArrayOf:BackingField:"))
                    .Select(a => $"{a.Name}={a.Value}")
                    .ToList(),
                NavigationBackingFields = et.GetAnnotations()
                    .Where(a => a.Name.StartsWith(NavigationBackingFieldAnnotation))
                    .Select(a => $"{a.Name}={a.Value}")
                    .ToList()
            })
            .Where(x => x.ArrayOfBackingFields.Count > 0 || x.NavigationBackingFields.Count > 0)
            .ToList();
    }

    /// <summary>
    /// Detecta backing fields para propiedades ArrayOf que no lo tengan configurado.
    /// </summary>
    private static void DetectArrayOfBackingFields(
        IConventionEntityType entityType,
        Type clrType,
        IMutableEntityType mutableEntityType)
    {
        foreach (var propertyInfo in clrType.GetProperties())
        {
            var propertyName = propertyInfo.Name;

            // Solo procesar propiedades ArrayOf
            if (!entityType.IsArrayOf(propertyName))
                continue;

            // Ya tiene backing field configurado
            if (entityType.GetArrayOfBackingField(propertyName) != null)
                continue;

            // Detectar backing field
            var backingField = ConventionHelpers.FindBackingField(clrType, propertyName);
            if (backingField != null)
            {
                mutableEntityType.SetArrayOfBackingField(propertyName, backingField);
            }
        }
    }

    /// <summary>
    /// Detecta backing fields para navegaciones de colección (SubCollections).
    /// </summary>
    private static void DetectNavigationBackingFields(
        IConventionEntityType entityType,
        Type clrType,
        IMutableEntityType mutableEntityType)
    {
        foreach (var navigation in entityType.GetNavigations())
        {
            // Solo procesar colecciones
            if (!navigation.IsCollection)
                continue;

            var propertyName = navigation.Name;

            // Ya tiene backing field configurado
            if (GetNavigationBackingField(entityType, propertyName) != null)
                continue;

            // Detectar backing field
            var backingField = ConventionHelpers.FindBackingField(clrType, propertyName);
            if (backingField != null)
            {
                SetNavigationBackingField(mutableEntityType, propertyName, backingField);
            }
        }
    }

    #region Extension Methods para Navigation BackingField

    /// <summary>
    /// Obtiene el backing field para una navegación de colección.
    /// </summary>
    public static FieldInfo? GetNavigationBackingField(IReadOnlyEntityType entityType, string navigationName)
    {
        return entityType.FindAnnotation($"{NavigationBackingFieldAnnotation}:{navigationName}")?.Value as FieldInfo;
    }

    /// <summary>
    /// Establece el backing field para una navegación de colección.
    /// </summary>
    public static void SetNavigationBackingField(IMutableEntityType entityType, string navigationName, FieldInfo fieldInfo)
    {
        entityType.SetAnnotation($"{NavigationBackingFieldAnnotation}:{navigationName}", fieldInfo);
    }

    #endregion
}
