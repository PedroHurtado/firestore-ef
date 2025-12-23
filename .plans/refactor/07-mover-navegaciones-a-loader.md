# 07 - Mover Navegaciones del Visitor al NavigationLoader

**Fecha:** 2025-12-23
**Estado:** PENDIENTE

---

## Objetivo

Mover los métodos de carga de navegaciones del Visitor al `NavigationLoader`, dejando el Visitor solo como compilador de expresiones.

---

## Contexto

Actualmente el Visitor tiene métodos de instancia que cargan navegaciones:
- `LoadIncludes`
- `LoadNavigationAsync`
- `LoadSubCollectionAsync`
- `LoadReferenceAsync`
- `LoadComplexTypeIncludes`
- `LoadComplexTypeInclude`

Existe `INavigationLoader` y `NavigationLoader` con `NotImplementedException`.

---

## Fase 1: Mover métodos de navegación

### Paso 1: Actualizar INavigationLoader

Rediseñar la interfaz para los métodos reales:
```csharp
public interface INavigationLoader
{
    Task LoadIncludesAsync<T>(
        T entity,
        DocumentSnapshot documentSnapshot,
        List<IReadOnlyNavigation> allIncludes,
        List<IncludeInfo> allIncludesWithFilters,
        IFirestoreDocumentDeserializer deserializer,
        IModel model,
        bool isTracking,
        DbContext? dbContext,
        QueryContext queryContext);
}
```

### Paso 2: Implementar NavigationLoader

Mover la lógica del Visitor a `NavigationLoader`:
- `LoadIncludes` → `LoadIncludesAsync`
- `LoadNavigationAsync` → método privado
- `LoadSubCollectionAsync` → método privado
- `LoadReferenceAsync` → método privado
- `LoadComplexTypeIncludes` → método privado o público según necesidad
- `LoadComplexTypeInclude` → método privado

### Paso 3: Inyectar INavigationLoader en Visitor

El Visitor recibirá `INavigationLoader` y delegará la carga.

### Paso 4: Eliminar métodos del Visitor

Eliminar los 6 métodos de navegación del Visitor.

---

## Fase 2 (Posterior): Mover deserialización

Después de la Fase 1, mover:
- `DeserializeEntity`
- `DeserializeWithIncludesAndProject`
- `DeserializeAndProject`

Al `FirestoreQueryExecutor` para que devuelva tipos CLR.

---

## Archivos a Modificar

| Archivo | Cambio |
|---------|--------|
| `Storage/Contracts/INavigationLoader.cs` | Rediseñar interfaz |
| `Storage/NavigationLoader.cs` | Implementar lógica real |
| `Query/Visitors/FirestoreShapedQueryCompilingExpressionVisitor.cs` | Usar INavigationLoader, eliminar métodos |
| `Query/Visitors/FirestoreShapedQueryCompilingExpressionVisitorFactory.cs` | Inyectar INavigationLoader |

---

## Dependencias

El `NavigationLoader` necesita:
- `IFirestoreQueryExecutor` (para `GetSubCollectionAsync`, `GetDocumentByReferenceAsync`)
- O directamente `IFirestoreClientWrapper`

**Decisión pendiente:** ¿El loader usa el executor o el wrapper directamente?

---

## Beneficios

1. **Separación de responsabilidades**: Visitor compila, Loader carga navegaciones
2. **Testabilidad**: Se puede mockear `INavigationLoader`
3. **Cohesión**: Cada clase hace una sola cosa
4. **Preparación**: Facilita la Fase 2 de mover deserialización
