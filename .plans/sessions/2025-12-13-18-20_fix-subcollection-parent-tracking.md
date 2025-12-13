# Plan: Fix SubCollection Parent Tracking

**Fecha:** 2025-12-13 18:20

## Problema

Cuando se actualiza/elimina una entidad de subcollection, el provider requiere que el padre esté marcado como `Modified` para poder construir el path. Esto obliga al usuario a escribir:

```csharp
// Workaround actual (innecesario)
updateContext.Entry(clienteParaActualizar).State = EntityState.Modified;
```

## Causa Raíz

El método `FindParentEntry` en `FirestoreDatabase.cs:179-211` solo busca en `allEntries`, que contiene únicamente las entidades con cambios pendientes (Added, Modified, Deleted).

Cuando el padre está trackeado como `Unchanged` (que es el caso normal después de un Include), no aparece en `allEntries` y el método retorna `null`.

## Solución

Modificar `FindParentEntry` para buscar en dos lugares:
1. Primero en `allEntries` (mantener comportamiento actual)
2. Si no encuentra, buscar en el **ChangeTracker completo** del DbContext

### Código Actual (líneas 179-211)

```csharp
private IUpdateEntry? FindParentEntry(
    IUpdateEntry childEntry,
    INavigation parentNavigation,
    IList<IUpdateEntry> allEntries)
{
    var childEntity = childEntry.ToEntityEntry().Entity;
    var parentEntityType = parentNavigation.DeclaringEntityType;

    // Solo busca en allEntries (entidades con cambios)
    foreach (var entry in allEntries)
    {
        // ...
    }

    return null;
}
```

### Código Propuesto

```csharp
private IUpdateEntry? FindParentEntry(
    IUpdateEntry childEntry,
    INavigation parentNavigation,
    IList<IUpdateEntry> allEntries)
{
    var childEntity = childEntry.ToEntityEntry().Entity;
    var parentClrType = parentNavigation.DeclaringEntityType.ClrType;

    // 1. Buscar primero en allEntries (entidades con cambios pendientes)
    foreach (var entry in allEntries)
    {
        // Usar IsAssignableTo para soportar herencia
        if (!entry.EntityType.ClrType.IsAssignableTo(parentClrType))
            continue;

        if (IsChildInParentCollection(childEntity, entry.ToEntityEntry().Entity, parentNavigation))
            return entry;
    }

    // 2. Si no encontró, buscar en el ChangeTracker completo (incluye Unchanged)
    var dbContext = childEntry.ToEntityEntry().Context;
    foreach (var trackedEntry in dbContext.ChangeTracker.Entries())
    {
        // Usar IsAssignableTo para soportar herencia
        if (!trackedEntry.Metadata.ClrType.IsAssignableTo(parentClrType))
            continue;

        if (IsChildInParentCollection(childEntity, trackedEntry.Entity, parentNavigation))
            return trackedEntry.GetInfrastructure();
    }

    return null;
}

/// <summary>
/// Verifica si una entidad hijo está contenida en la colección de navegación del padre
/// </summary>
private static bool IsChildInParentCollection(
    object childEntity,
    object parentEntity,
    INavigation parentNavigation)
{
    var childrenCollection = parentNavigation.PropertyInfo?.GetValue(parentEntity) as IEnumerable;
    if (childrenCollection == null)
        return false;

    return childrenCollection
        .Cast<object>()
        .Any(item => ReferenceEquals(item, childEntity));
}
```

## Pasos de Implementación

### Fase 1: Modificar FirestoreDatabase.cs ✅ COMPLETADA
1. [x] Modificar método `FindParentEntry` para buscar en ChangeTracker
2. [x] Agregar método helper `IsChildInParentCollection`
3. [x] Agregar using `Microsoft.EntityFrameworkCore.Infrastructure`
4. [x] Build para verificar compilación

### Fase 2: Actualizar Tests ✅ COMPLETADA
5. [x] Eliminar workaround de `Update_PedidoEnSubCollection_ShouldPersistChanges`
6. [x] Eliminar workaround de `Delete_PedidoFromSubCollection_ShouldRemoveFromFirestore`
7. [x] Ejecutar tests y verificar (20 tests pasando)

### Fase 3: Commit ✅ COMPLETADA
8. [x] Commit con mensaje descriptivo

## Archivos a Modificar

| Archivo | Cambio |
|---------|--------|
| `firestore-efcore-provider/Storage/FirestoreDatabase.cs` | Modificar `FindParentEntry` |
| `tests/.../SubCollectionTests.cs` | Eliminar workarounds |

## Comandos de Verificación

```bash
# Build
dotnet build firestore-efcore-provider

# Tests de SubCollections
dotnet test tests/Fudie.Firestore.IntegrationTest --filter "SubCollectionTests"

# Todos los tests de integración
dotnet test tests/Fudie.Firestore.IntegrationTest
```

## Riesgos

| Riesgo | Mitigación |
|--------|------------|
| Rendimiento al buscar en ChangeTracker | Solo se busca si no encuentra en allEntries |
| Compatibilidad con código existente | El workaround seguirá funcionando |
