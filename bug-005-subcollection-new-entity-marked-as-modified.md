# Bug 005: SubCollection New Entity Incorrectly Marked as Modified

## Resumen

Cuando se añade una entidad nueva a una subcolección vacía, el ChangeTracker la marca incorrectamente como `Modified` en lugar de `Added`, causando que Firestore intente hacer un UPDATE en un documento que no existe.

## Error Observado

```
Grpc.Core.RpcException: Status(StatusCode="NotFound", Detail="no entity to update: app: "dev~demo-project"
path <
  Element {
    type: "Menus"
    name: "d7adfa84-7a60-4589-ad49-3b90230dce0d"
  }
  Element {
    type: "MenuCategories"
    name: "1b9c9d90-a516-406b-a1ba-ac5a694ef0c4"
  }
>
")
```

## Escenario de Reproducción

### Configuración del DbContext

```csharp
entity.SubCollection(m => m.Categories, category =>
{
    category.ArrayOf(c => c.Items, item =>
    {
        item.Reference(i => i.MenuItem);
    });
});
```

### Flujo

1. **Crear un Menu** (sin categorías)
2. **Leer el Menu** con Include de Categories
3. **Añadir una nueva categoría** al HashSet `_categories`
4. **Llamar a SaveChangesAsync()**

### Query Ejecutada

```
Query: Menus
  .Where(TenantId == "df03f6d3-2b27-4a71-90c3-b9273c621d5c")
  .Where(Id == "89f7cdce-e8fa-4418-9fde-ebc2b590a51c" [PK])
  .Limit(1)
  .Include(Categories) [SubCollection]
    → Query: MenuCategories
```

### Resultado de la Query

```
Categories: [0] (IEnumerable<Object>, ObjectList)  // VACÍO
```

### ChangeTracker ANTES de añadir la categoría

```
Count = 1
[0] = {Menu {Id: 85848230-...} Unchanged}
```

### ChangeTracker DESPUÉS de añadir la categoría

```
Count = 2
[0] = {MenuCategory {Id: f56113e4-...} Modified FK {MenuId: 85848230-...}}  // DEBERÍA SER Added
[1] = {Menu {Id: 85848230-...} Unchanged}
```

## Análisis del ChangeTracker Entry

```
[0] = { State = Modified, Property = "Id", Current = {guid}, Original = {guid}, IsModified = false }
[1] = { State = Modified, Property = "Description", Current = null, Original = null, IsModified = true }
[2] = { State = Modified, Property = "DisplayOrder", Current = 0, Original = 0, IsModified = true }
[3] = { State = Modified, Property = "IsActive", Current = true, Original = true, IsModified = true }
[4] = { State = Modified, Property = "MenuId", Current = {guid}, Original = {guid}, IsModified = true, IsShadow = true }
[5] = { State = Modified, Property = "Name", Current = "Test Category", Original = "Test Category", IsModified = true }
[6] = { State = Modified, Property = "__Items_Json", Current = null, Original = null, IsModified = false, IsShadow = true }
```

## Problema Identificado

1. **Estado incorrecto**: La entidad nueva tiene estado `Modified` en lugar de `Added`
2. **Valores originales copiados**: Los valores originales son idénticos a los actuales (imposible para una entidad nueva)
3. **IsModified = true con valores iguales**: Las propiedades tienen `IsModified = true` aunque `Current == Original`

## Comportamiento Esperado

Para una entidad **nueva** añadida a una subcolección:

1. Estado: `Added`
2. Sin valores originales (o valores por defecto)
3. `IsModified = false` para todas las propiedades
4. Firestore debería ejecutar un **CREATE** (no UPDATE)

## Comportamiento Actual

1. Estado: `Modified`
2. Valores originales = valores actuales (copiados)
3. `IsModified = true` para casi todas las propiedades
4. Firestore intenta ejecutar un **UPDATE** → Falla con "no entity to update"

## Causa Probable

El código en el proveedor de Firestore que detecta cambios en subcolecciones (`DetectChanges`) no está verificando si la entidad ya existe. Cuando encuentra una entidad en la colección navegable:

- **Debería**: Verificar si existe en el ChangeTracker → Si no existe, marcar como `Added`
- **Está haciendo**: Asumir que es `Modified`, copiar valores actuales como originales, marcar propiedades como modificadas

## Archivos Relevantes (Provider)

- Convenciones de SubCollection
- Lógica de DetectChanges para subcolecciones
- Materialización de entidades de subcolecciones

## Tests de Integración Necesarios (Provider)

1. **Test: Nueva entidad en subcolección vacía**
   - Crear padre
   - Leer padre con Include de subcolección (vacía)
   - Añadir nueva entidad a la subcolección
   - Verificar que ChangeTracker la marca como `Added`
   - Verificar que SaveChanges crea el documento

2. **Test: Nueva entidad en subcolección con elementos existentes**
   - Crear padre con hijos existentes
   - Leer padre con Include
   - Añadir nueva entidad
   - Verificar estados correctos (existentes = Unchanged, nuevo = Added)

3. **Test: Modificar entidad existente en subcolección**
   - Crear padre con hijo
   - Leer padre con Include
   - Modificar el hijo existente
   - Verificar que ChangeTracker la marca como `Modified`
   - Verificar que SaveChanges actualiza el documento

4. **Test: Eliminar entidad de subcolección**
   - Crear padre con hijos
   - Leer padre con Include
   - Eliminar hijo de la colección
   - Verificar que ChangeTracker la marca como `Deleted`
   - Verificar que SaveChanges elimina el documento

---

## Bug Relacionado: Entidad Eliminada Marcada como Modified

### Problema

Cuando se elimina una entidad de una subcolección (ej: `menu.Categories.Remove(category)`), EF Core la marca como `Modified` con el FK (`MenuId`) establecido a `null`, en lugar de marcarla como `Deleted`.

### Escenario

```csharp
// Leer menú con categorías
var menu = await context.Menus
    .Include(m => m.Categories)
    .FirstAsync(m => m.Id == menuId);

// Eliminar una categoría
var categoryToRemove = menu.Categories.First();
menu.Categories.Remove(categoryToRemove);

// ChangeTracker muestra:
// MenuCategory { State = Modified, MenuId = null }  // DEBERÍA SER Deleted
```

### Causa

EF Core detecta que el FK (`MenuId`) cambió de un valor a `null`, lo que interpreta como una modificación. Pero en el contexto de SubCollections de Firestore, esto indica que la entidad fue removida de la colección del padre.

### Solución Implementada

Se creó `SubCollectionChangeTracker.FixSubCollectionDeleteState()` que se ejecuta en el interceptor `ArrayOfSaveChangesInterceptor` antes de `SaveChanges()`:

```csharp
public static void FixSubCollectionDeleteState(DbContext context)
{
    foreach (var entry in context.ChangeTracker.Entries())
    {
        if (entry.State != EntityState.Modified)
            continue;

        var parentNavigation = FindParentNavigationForSubCollection(entry.Metadata, model);
        if (parentNavigation == null)
            continue;

        var fkPropertyName = ConventionHelpers.GetForeignKeyPropertyName(
            parentNavigation.DeclaringEntityType.ClrType);

        var fkProperty = entry.Property(fkPropertyName);
        var originalValue = fkProperty.OriginalValue;
        var currentValue = fkProperty.CurrentValue;

        // Si FK pasó de un valor a null → la entidad fue removida
        if (originalValue != null && !IsDefaultValue(originalValue) && currentValue == null)
        {
            entry.State = EntityState.Deleted;
        }
    }
}
```

### Archivos Modificados

- `SubCollectionChangeTracker.cs` - Nueva clase para corregir estados
- `ArrayOfSaveChangesInterceptor.cs` - Llama a `FixSubCollectionDeleteState()`
- `FirestoreDatabase.cs` - `FindParentByOriginalForeignKey()` para encontrar padre de entidades eliminadas

---

## Solución Completa del Bug 005

### Archivos Modificados

1. **FirestoreEntityTypeBuilderExtensions.cs**
   - Añadido `ValueGeneratedNever` automático para PKs de SubCollections
   - Evita que EF Core asuma que entidades con Id ya existen

2. **SubCollectionElementBuilder.cs**
   - Añadido método `Entity()` para exponer `EntityTypeBuilder<T>`
   - Permite usar `Ignore()`, `Property()`, etc. en SubCollections

3. **SubCollectionChangeTracker.cs** (nuevo)
   - Corrige estado de entidades eliminadas: `Modified` → `Deleted`

4. **ArrayOfSaveChangesInterceptor.cs**
   - Llama a `SubCollectionChangeTracker.FixSubCollectionDeleteState()`

5. **FirestoreDatabase.cs**
   - `FindParentByOriginalForeignKey()` para localizar padre vía FK original

---

**Fecha**: 2026-02-02
**Proyecto afectado**: Customer (webapi)
**Provider**: Fudie.Firestore.EntityFrameworkCore
**Estado**: ✅ RESUELTO
