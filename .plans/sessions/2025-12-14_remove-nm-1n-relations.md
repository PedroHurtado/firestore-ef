# Plan: Bloquear Relaciones N:M y 1:N en el Provider

**Fecha:** 2025-12-14

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

## Archivos a Revisar

| Archivo | Acción |
|---------|--------|
| `firestore-efcore-provider/Conventions/` | Agregar convention que detecte y lance error |
| `firestore-efcore-provider/Infrastructure/` | Posible validación en ModelValidator |

## Pasos de Implementación

### Fase 1: Análisis
1. [ ] Identificar dónde EF Core procesa HasMany().WithMany()
2. [ ] Identificar dónde EF Core procesa HasMany().WithOne()
3. [ ] Identificar dónde EF Core procesa HasOne().WithOne()
4. [ ] Determinar el mejor punto para interceptar y lanzar error

### Fase 2: Crear API .Reference()
5. [ ] Crear extensión `.Reference()` similar a `.SubCollection()`
6. [ ] Marcar la propiedad como DocumentReference en metadata
7. [ ] Integrar con el sistema de lectura/escritura existente

### Fase 3: Crear Convention/Validator para Bloquear Relaciones
8. [ ] Crear `RelationshipValidationConvention` o similar
9. [ ] Detectar relaciones N:M (`HasMany().WithMany()`) → lanzar error
10. [ ] Detectar relaciones 1:N (`HasMany().WithOne()`) → lanzar error
11. [ ] Detectar relaciones 1:1 (`HasOne().WithOne()`) → lanzar error
12. [ ] Mensajes de error claros con la alternativa correcta

### Fase 4: Tests
13. [ ] Test: HasMany().WithMany() debe lanzar NotSupportedException
14. [ ] Test: HasMany().WithOne() debe lanzar NotSupportedException
15. [ ] Test: HasOne().WithOne() debe lanzar NotSupportedException
16. [ ] Test: .SubCollection() sigue funcionando
17. [ ] Test: .Reference() funciona correctamente

### Fase 5: Verificación
18. [ ] Build del provider
19. [ ] Ejecutar todos los tests
20. [ ] Verificar mensajes de error son claros

### Fase 6: Commit
21. [ ] Commit con mensaje descriptivo

## Errores a Lanzar

```csharp
// Para N:M - Al detectar HasMany().WithMany()
throw new NotSupportedException(
    "Many-to-Many relationships (HasMany().WithMany()) are not supported in Firestore. " +
    "Consider using SubCollections or denormalization instead.");

// Para 1:N - Al detectar HasMany().WithOne()
throw new NotSupportedException(
    "One-to-Many relationships (HasMany().WithOne()) are not supported in Firestore. " +
    "Use SubCollections instead: entity.SubCollection(e => e.Children)");

// Para 1:1 - Al detectar HasOne().WithOne()
throw new NotSupportedException(
    "One-to-One relationships (HasOne().WithOne()) are not supported in Firestore. " +
    "Use DocumentReferences instead: entity.Reference(e => e.Related)");
```

## API .Reference()

Similar a `.SubCollection()`, la nueva API será:

```csharp
// Configuración
modelBuilder.Entity<Articulo>(entity =>
{
    entity.Reference(a => a.Categoria);  // FK como DocumentReference
});

// Uso - El desarrollador escribe
public class Articulo
{
    public string? Id { get; set; }
    public string? CategoriaId { get; set; }      // ID de la referencia
    public Categoria? Categoria { get; set; }     // Navegación
}
```

## Punto de Intercepción

Opciones para detectar y bloquear:

1. **IModelFinalizingConvention** - Se ejecuta al finalizar el modelo
   ```csharp
   public class BlockUnsupportedRelationshipsConvention : IModelFinalizingConvention
   {
       public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, ...)
       {
           foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
           {
               foreach (var navigation in entityType.GetNavigations())
               {
                   if (IsManyToMany(navigation) || IsOneToManyWithoutSubCollection(navigation))
                       throw new NotSupportedException(...);
               }
           }
       }
   }
   ```

2. **IModelValidator** - Validación post-construcción del modelo

## Notas

- El error debe ocurrir **al construir el DbContext**, no en runtime
- Los mensajes deben ser claros y guiar al desarrollador hacia la API correcta
- `.SubCollection()` ya existe y sigue funcionando
- `.Reference()` es nueva y se implementa en este plan
- Todas las relaciones de EF Core tradicionales quedan bloqueadas
