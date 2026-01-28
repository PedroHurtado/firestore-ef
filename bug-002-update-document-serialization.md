# Bug 002: Serialización del Update no respeta Ignore() del modelo

## Resumen

Cuando se actualiza una entidad con arrays modificados, la serialización de los elementos para `ArrayUnion`/`ArrayRemove` de Firestore no respeta las propiedades configuradas con `Ignore()` en el modelo.

## Archivo afectado

`src/Fudie.Firestore.EntityFrameworkCore/Storage/FirestoreDatabase.cs`

Método: `UpdateDocumentAsync`

## Causa raíz

Al serializar los elementos del array para enviar a Firestore con `ArrayUnion`, no se consultan las anotaciones del modelo para excluir las propiedades marcadas con `Ignore()`.

## Síntomas

1. Las propiedades computed (`IsValid`, `DisplayValue`) se guardan en Firestore cuando no deberían
2. Los elementos del array se guardan inconsistentemente:
   - En `Add` (entidad nueva): se ignoran correctamente
   - En `Modified` (entidad existente): NO se ignoran

Ejemplo de datos en Firestore después de un Update:
```
Features:
  [0] { Code: "USERS", Name: "Usuarios", Limit: 5, ... }  ← Creado con Add (correcto)
  [1] { Code: "USERS", Name: "Usuarios", Limit: 5, IsValid: true, DisplayValue: "5 usuarios", ... }  ← Creado con Update (incorrecto)
```

## Configuración del modelo

```csharp
entity.ArrayOf(p => p.Features, feature =>
{
    feature.Ignore(f => f.IsValid);
    feature.Ignore(f => f.DisplayValue);
});
```

Las propiedades ignoradas se almacenan en anotaciones:
```
Firestore:ArrayOf:IgnoredProperties:{arrayPropertyName}
```

## Solución requerida

1. En `UpdateDocumentAsync`, al procesar arrays modificados, obtener las propiedades ignoradas de las anotaciones del modelo

2. Al serializar elementos para `ArrayUnion`/`ArrayRemove`, excluir las propiedades que están en la lista de ignoradas

3. Usar la misma lógica de serialización que se usa para entidades nuevas (`Add`)

## Cómo obtener las propiedades ignoradas

Desde el `IEntityType` del modelo:

```csharp
private static HashSet<string> GetIgnoredProperties(IEntityType entityType, string arrayPropertyName)
{
    var annotation = entityType.FindAnnotation($"Firestore:ArrayOf:IgnoredProperties:{arrayPropertyName}");
    return annotation?.Value as HashSet<string> ?? new HashSet<string>();
}
```

## Consideraciones

- El nombre de la propiedad del array se puede obtener de la anotación `Firestore:ArrayOf:JsonTrackerFor` en la shadow property
- Los elementos deben serializarse de forma consistente entre `Add` y `Update`
- La serialización debe funcionar para records, clases, tipos primitivos y enums

## Tests de regresión

Ejecutar todos los tests de integración en:
- `Fudie.Firestore.IntegrationTest/Plan/PlanCrudTests.cs`

Específicamente verificar:
- `Update_PlanFeatures_ShouldPersistArrayOfChanges` - debe guardar exactamente 3 features, no 4
- Los features guardados no deben contener `IsValid` ni `DisplayValue`

Crear un test nuevo que verifique la consistencia:
```csharp
[Fact]
public async Task Update_Features_ShouldNotIncludeIgnoredProperties()
{
    // Crear plan con 1 feature
    // Actualizar añadiendo 2 features más
    // Leer directamente de Firestore (sin el provider)
    // Verificar que ningún feature tiene IsValid ni DisplayValue
}
```

## Relación con Bug 001

Este bug está relacionado con el Bug 001 (ArrayOfChangeTracker). Ambos afectan a la serialización de arrays pero en diferentes momentos:

- **Bug 001**: Afecta a la detección de cambios (shadow property `__Features_Json`)
- **Bug 002**: Afecta a la persistencia real en Firestore

Es recomendable arreglar primero el Bug 001 ya que afecta a la detección de cambios, y luego el Bug 002.

Idealmente, ambos deberían usar la **misma lógica de serialización** para garantizar consistencia.
