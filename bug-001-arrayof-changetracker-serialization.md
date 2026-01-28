# Bug 001: Serialización incorrecta en ArrayOfChangeTracker

## Resumen

El `ArrayOfChangeTracker` serializa los elementos de arrays como objetos vacíos `{}` cuando los elementos son records con propiedades read-only.

## Archivo afectado

`src/Fudie.Firestore.EntityFrameworkCore/ChangeTracking/ArrayOfChangeTracker.cs`

## Causa raíz

La configuración de `JsonSerializerOptions` usa `IgnoreReadOnlyProperties = true`:

```csharp
private static readonly JsonSerializerOptions JsonOptions = new()
{
    WriteIndented = false,
    PropertyNamingPolicy = null,
    IgnoreReadOnlyProperties = true,  // ← PROBLEMA
    Converters = { new JsonStringEnumConverter() }
};
```

Los records de C# tienen propiedades read-only por diseño (`public string Code { get; }`). Con `IgnoreReadOnlyProperties = true`, el serializador ignora TODAS las propiedades del record, resultando en `"{}"`.

## Síntomas

1. La shadow property `__Features_Json` contiene `"[{}]"` en lugar de los datos reales
2. La detección de cambios no funciona correctamente porque compara objetos vacíos
3. El `ArrayUnion` de Firestore no puede hacer diff correctamente y duplica elementos

## Ejemplo del problema

```csharp
// Entidad con un Feature
plan._features.Add(new Feature("USERS", "Usuarios", null, FeatureType.Limit, 5, "usuarios"));

// Después de cargar la entidad:
var shadowProp = entry.Properties.First(p => p.Metadata.Name == "__Features_Json");
// shadowProp.OriginalValue = "[{}]"  ← Debería ser "[{\"Code\":\"USERS\",\"Name\":\"Usuarios\",...}]"
```

## Solución requerida

1. **Quitar `IgnoreReadOnlyProperties = true`** de `JsonOptions`

2. **Crear un sistema que ignore solo las propiedades configuradas con `Ignore()` en el modelo**

   Las propiedades ignoradas se almacenan en anotaciones del modelo con el formato:
   ```
   Firestore:ArrayOf:IgnoredProperties:{arrayPropertyName}
   ```
   
   El valor es un `HashSet<string>` con los nombres de las propiedades a ignorar.

3. **Modificar `SerializeArrayProperty`** para que:
   - Reciba el `IEntityType` para acceder a las anotaciones
   - Obtenga las propiedades ignoradas de la anotación
   - Use un `JsonConverter` personalizado o `TypeInfoResolver` que excluya solo esas propiedades

## Métodos a modificar

- `SerializeArrayProperty(object entity, string propertyName)` → Añadir parámetro para propiedades ignoradas
- `SyncEntityArrays(EntityEntry entry)` → Pasar las propiedades ignoradas a `SerializeArrayProperty`
- `InitializeShadowProperties(EntityEntry entry, IEntityType entityType)` → Pasar las propiedades ignoradas
- `InitializeShadowProperties(object entity, IEntityType entityType, InternalEntityEntry internalEntry)` → Pasar las propiedades ignoradas

## Cómo obtener las propiedades ignoradas

```csharp
private static HashSet<string> GetIgnoredProperties(IEntityType entityType, string arrayPropertyName)
{
    var annotation = entityType.FindAnnotation($"Firestore:ArrayOf:IgnoredProperties:{arrayPropertyName}");
    return annotation?.Value as HashSet<string> ?? new HashSet<string>();
}
```

## Consideraciones para el JsonConverter

El converter debe funcionar para:
- Records (propiedades read-only)
- Clases normales
- Tipos primitivos (int, long, double) - serializar directamente
- Enums - serializar directamente
- Arrays anidados

## Tests de regresión

Ejecutar todos los tests de integración en:
- `Fudie.Firestore.IntegrationTest/Plan/PlanCrudTests.cs`

Específicamente verificar que pase:
- `Update_PlanFeatures_ShouldPersistArrayOfChanges`

Y crear un test nuevo que verifique que la shadow property contiene los datos correctos:
```csharp
[Fact]
public async Task ShadowProperty_ShouldContainSerializedFeatures()
{
    // Crear plan con features
    // Cargar plan
    // Verificar que __Features_Json contiene los datos, no "[{}]"
}
```
