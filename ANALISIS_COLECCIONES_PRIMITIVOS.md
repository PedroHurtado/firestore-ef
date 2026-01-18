# Análisis: Colecciones de Tipos Primitivos No Soportadas

**Fecha:** 2026-01-17
**Contexto:** Descubierto durante test `FullIntegration_MenuItem_WithAllFeatures_ShouldPersistAndRetrieve`

---

## 1. El Problema Detectado

### Síntoma
El test falla porque `AvailableDays` (un `HashSet<DayOfWeek>`) no se persiste en Firestore.

### Datos observados en debugging

**Entidad en memoria:**
```
_availableDays [HashSet] = Count = 3
  [0] = Friday
  [1] = Saturday
  [2] = Sunday
```

**Dict enviado a Firestore:**
```
[0]  {[AllergenNotes, Puede contener trazas de apio]}
[1]  {[Description, Cremoso risotto...]}
[2]  {[DisplayOrder, 10]}
...
[14] {[PriceOptions, List<Dictionary>]}
[15] {[Allergens, List<DocumentReference>]}
// ❌ AvailableDays NO ESTÁ
```

---

## 2. Análisis del Flujo de Serialización

El método `SerializeEntityFromEntry` ejecuta estos pasos:

1. `SerializeProperties` → propiedades escalares de EF Core
2. `SerializeComplexProperties` → ComplexTypes (Value Objects)
3. `SerializeArrayOfProperties` → propiedades con annotation ArrayOf
4. `SerializeEntityReferences` → referencias individuales
5. `SerializeEntityReferenceCollections` → colecciones de referencias
6. `SerializeShadowForeignKeyReferences` → FKs shadow
7. `SerializeJoinEntityReferences` → N:M

### ¿Por qué `HashSet<DayOfWeek>` no se serializa?

| Método | ¿Procesa `HashSet<DayOfWeek>`? | Razón |
|--------|-------------------------------|-------|
| `SerializeProperties` | ❌ NO | No es propiedad escalar de EF Core |
| `SerializeComplexProperties` | ❌ NO | No es ComplexType |
| `SerializeArrayOfProperties` | ❌ NO | No tiene annotation ArrayOf |
| Otros métodos | ❌ NO | No aplican |

**Resultado: La propiedad se pierde silenciosamente.**

---

## 3. Tipos Permitidos por Firestore

Firestore soporta estos tipos nativos:

| Tipo Firestore | Descripción |
|----------------|-------------|
| `string` | Texto |
| `number` | Enteros y flotantes (int, long, double) |
| `boolean` | true/false |
| `map` | Objeto anidado (diccionario) |
| `array` | Lista de cualquier tipo |
| `null` | Valor nulo |
| `timestamp` | Fecha/hora |
| `geopoint` | Coordenadas (lat, lng) |
| `reference` | Referencia a otro documento |

### Arrays en Firestore

Firestore permite:
- Array de strings: `["a", "b", "c"]`
- Array de numbers: `[1, 2, 3]`
- Array de booleans: `[true, false]`
- Array de maps: `[{...}, {...}]`
- Array de arrays: `[[1,2], [3,4]]`
- Array mixto: `["text", 123, true]`

---

## 4. Mapeo Actual del Provider

### Lo que SÍ está implementado

| C# Type | Firestore Type | Mecanismo |
|---------|---------------|-----------|
| `string` | string | Directo |
| `int`, `long` | number | Directo |
| `double`, `float` | number | Directo |
| `decimal` | number (double) | Converter |
| `bool` | boolean | Directo |
| `DateTime` | timestamp | Converter (UTC) |
| `enum` | string | Converter |
| `Guid` | string | Converter |
| `ComplexType` | map | Serialización |
| `Entity` (navegación) | reference | DocumentReference |
| `List<ComplexType>` | array of maps | ArrayOf Embedded |
| `List<Entity>` | array of references | ArrayOf Reference |
| `List<GeoPoint>` | array of geopoints | ArrayOf GeoPoint |

### Lo que NO está implementado ❌

| C# Type | Firestore Type Esperado | Estado |
|---------|------------------------|--------|
| `List<string>` | array of strings | ❌ No implementado |
| `List<int>` | array of numbers | ❌ No implementado |
| `List<decimal>` | array of numbers | ❌ No implementado |
| `List<bool>` | array of booleans | ❌ No implementado |
| `List<DateTime>` | array of timestamps | ❌ No implementado |
| `List<enum>` | array of strings | ❌ No implementado |
| `HashSet<T>` (primitivos) | array | ❌ No implementado |

---

## 5. Evidencia en Tests

### Tests Unitarios (`ArrayOfConventionTest.cs`)

Los modelos de prueba cubren:

```csharp
// ✅ Cubierto
private class TiendaConUbicaciones  // List<GeoLocation>
private class TiendaConDirecciones  // List<Direccion> (ComplexType)
private class TiendaConProductos    // List<Producto> (Entity)

// ❌ Existe pero documenta que NO se procesa
private class TiendaConTags         // List<string>
```

El test `ProcessEntityTypeAdded_ListOfPrimitive_DoesNotApply` afirma explícitamente:

```csharp
[Fact]
public void ProcessEntityTypeAdded_ListOfPrimitive_DoesNotApply()
{
    // Assert - No debería aplicar para tipos primitivos
    annotations.Should().NotContainKey($"{ArrayOfAnnotations.Type}:Tags");
    wasIgnored("Tags").Should().BeFalse();
}
```

**Este test valida que `ArrayOfConvention` ignora primitivos, pero nadie verificó que OTRO mecanismo los maneje.**

### Tests de Integración (`ArrayOfTestEntities.cs`)

Revisando las 63 pruebas de ArrayOf (29 + 34):

```bash
grep -E "List<|HashSet<" ArrayOfTestEntities.cs
```

Resultados:
- `List<HorarioAtencion>` → ComplexType ✅
- `List<UbicacionGeo>` → GeoPoint ✅
- `List<Etiqueta>` → Entity (Reference) ✅
- `HashSet<Tag>` → ComplexType ✅

**NO hay ni un solo test con:**
- `List<string>`
- `List<int>`
- `List<decimal>`
- `List<DayOfWeek>`
- `HashSet<enum>`

---

## 6. Impacto

### Severidad: ALTA

Cualquier entidad con colecciones de primitivos:
1. Se guarda sin esos datos (pérdida silenciosa)
2. Se recupera con la colección vacía
3. No hay error ni warning

### Casos de uso afectados

- `List<string>` → Tags, categorías, palabras clave
- `List<int>` → IDs externos, cantidades
- `List<decimal>` → Precios históricos, descuentos
- `List<DayOfWeek>` → Días disponibles
- `List<DateTime>` → Fechas de eventos
- `HashSet<enum>` → Estados permitidos, roles

---

## 7. Causa Raíz

### Diseño incompleto

La especificación de ArrayOf contemplaba autodetección para:
- ComplexTypes (sin Id) → Embedded
- Entities (con Id) → Reference
- GeoPoints (Lat/Lng) → GeoPoint

Pero **nunca se definió** qué hacer con colecciones de primitivos.

### Asunción implícita incorrecta

Se asumió que EF Core manejaría `List<string>` como propiedad escalar, pero:
- EF Core para SQL sí lo hace (JSON column o tabla separada)
- Nuestro provider NO implementó ese mapeo

### Tests que validan comportamiento incompleto

El test unitario `ProcessEntityTypeAdded_ListOfPrimitive_DoesNotApply` verifica que la convention ignora primitivos, creando una falsa sensación de que "está controlado".

---

## 8. Solución Propuesta

### Opción A: Autodetección en ArrayOfConvention

Modificar `ArrayOfConvention` para detectar colecciones de primitivos y marcarlas como un nuevo tipo `ArrayType.Primitive`.

### Opción B: Serialización directa

Modificar `SerializeEntityFromEntry` para detectar colecciones de primitivos por reflexión y serializarlas directamente.

### Opción C: TypeMapping de EF Core

Crear `FirestoreListPrimitiveTypeMapping` que maneje `List<T>` donde T es primitivo.

---

## 9. Próximos Pasos

1. Decidir estrategia de implementación
2. Crear tests de integración para colecciones de primitivos
3. Implementar serialización
4. Implementar deserialización
5. Verificar que no hay regresiones

---

## 10. Lecciones Aprendidas

1. **Los tests unitarios validan piezas, no el sistema completo**
2. **Un test que verifica "no hacer nada" debe acompañarse de otro que verifique "quién lo hace"**
3. **La pérdida silenciosa de datos es el peor tipo de bug**
4. **Revisar qué tipos soporta la base de datos destino ANTES de diseñar el mapeo**