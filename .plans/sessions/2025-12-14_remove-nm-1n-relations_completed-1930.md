# Plan: Bloquear Relaciones N:M y 1:N en el Provider

**Fecha:** 2025-12-14
**Estado:** COMPLETADO
**Commit:** `c5146d4`

## Objetivo

Hacer que el provider **lance errores** cuando el desarrollador intente configurar relaciones N:M o 1:N tradicionales, guiándolo hacia las alternativas correctas en Firestore.

## Justificación

- Las SubCollections cubren el caso de uso de 1:N de forma nativa en Firestore
- Las relaciones N:M no tienen sentido en un modelo de documentos
- **El desarrollador debe recibir un error claro**, no un comportamiento silencioso
- Las FK (1:1) sí tienen sentido como DocumentReferences en Firestore

## Comportamiento Esperado

| Configuración | Resultado |
|---------------|-----------|
| `HasMany().WithMany()` | **ERROR** en tiempo de configuración del modelo |
| `HasMany().WithOne()` | **ERROR** en tiempo de configuración del modelo |
| `HasOne().WithOne()` | **ERROR** en tiempo de configuración del modelo |
| `.SubCollection()` | OK - Forma correcta de 1:N |
| `.Reference()` | OK - Forma correcta de FK (DocumentReference) |

## Archivos Modificados

| Archivo | Cambio |
|---------|--------|
| `FirestoreNavigationExtensions.cs` | Agregado `IsDocumentReference()`, `SetIsDocumentReference()`, `IsFirestoreConfigured()` |
| `FirestoreEntityTypeBuilderExtensions.cs` | Agregado método `.Reference()` |
| `FirestoreModelValidator.cs` | Agregado `ValidateNoUnsupportedRelationships()` (internal) |
| `FirestoreModelValidatorRelationshipTests.cs` | **Nuevo** - 6 tests unitarios |

## Pasos de Implementación

### Fase 1: Análisis ✅
1. [x] Identificar dónde EF Core procesa HasMany().WithMany()
2. [x] Identificar dónde EF Core procesa HasMany().WithOne()
3. [x] Identificar dónde EF Core procesa HasOne().WithOne()
4. [x] Determinar el mejor punto para interceptar y lanzar error

### Fase 2: Crear API .Reference() ✅
5. [x] Crear extensión `.Reference()` similar a `.SubCollection()`
6. [x] Marcar la propiedad como DocumentReference en metadata
7. [x] Integrar con el sistema de lectura/escritura existente

### Fase 3: Crear Convention/Validator para Bloquear Relaciones ✅
8. [x] Usar `FirestoreModelValidator` existente
9. [x] Detectar relaciones N:M (`HasMany().WithMany()`) → lanzar error
10. [x] Detectar relaciones 1:N (`HasMany().WithOne()`) → lanzar error
11. [x] Detectar relaciones 1:1 (`HasOne().WithOne()`) → lanzar error
12. [x] Mensajes de error claros con la alternativa correcta

### Fase 4: Tests ✅
13. [x] Test: HasMany().WithMany() debe lanzar NotSupportedException
14. [x] Test: HasMany().WithOne() debe lanzar NotSupportedException
15. [x] Test: HasOne().WithOne() debe lanzar NotSupportedException
16. [x] Test: .SubCollection() sigue funcionando
17. [x] Test: .Reference() funciona correctamente

### Fase 5: Verificación ✅
18. [x] Build del provider
19. [x] Ejecutar todos los tests (6/6 pasaron)
20. [x] Verificar mensajes de error son claros

### Fase 6: Commit ✅
21. [x] Commit `c5146d4`

## API Implementada

### .Reference()
```csharp
modelBuilder.Entity<Articulo>(entity =>
{
    entity.HasOne(a => a.Categoria)
        .WithMany()
        .HasForeignKey(a => a.CategoriaId);

    entity.Reference(a => a.Categoria);  // Marca como DocumentReference
});
```

### Errores que se lanzan
```csharp
// Many-to-Many
"Many-to-Many relationship detected: 'Author.Books' -> 'Book'.
Many-to-Many relationships (HasMany().WithMany()) are not supported in Firestore.
Consider using SubCollections or denormalization instead."

// One-to-Many sin SubCollection
"One-to-Many relationship detected: 'Parent.Children' -> 'Child'.
One-to-Many relationships (HasMany().WithOne()) are not supported in Firestore.
Use SubCollections instead: entity.SubCollection(e => e.Children)"

// FK sin Reference
"Foreign Key relationship detected: 'Articulo.Categoria' -> 'Categoria'.
Traditional FK relationships are not supported in Firestore.
Use DocumentReferences instead: entity.Reference(e => e.Categoria)"
```
