# Análisis: 20 Tests de Proyecciones Fallidos

**Fecha:** 2026-01-21
**Contexto:** Migración del pipeline a SnapshotShaper + Materializer

---

## 1. Arquitectura Actual

### Responsabilidades

| Servicio | Responsabilidad |
|----------|-----------------|
| **SnapshotShaper** | Preparar datos en diccionario plano con keys `"Direccion.Ciudad"` para lookup directo |
| **Materializer** | 1) Cachear constructor óptimo. 2) Llamar Converter. 3) Mapear a CLR. Usa `projectedFields` solo para lookup |

### Flujo de Datos

```
DocumentSnapshots
      ↓
SnapshotShaper.Shape() → Diccionario plano { "Direccion.Ciudad": "Madrid", "Cantidades": [1,2,3] }
      ↓
Materializer.Materialize() → Instancias CLR
```

---

## 2. Tests Fallidos por Categoría

### Grupo A: SelectArrayTests (7 tests)

Proyección directa de un array: `.Select(p => p.Cantidades)`

| Test | Query | TargetType | Error |
|------|-------|------------|-------|
| `Select_ListOfStrings_ReturnsStringArray` | `.Select(p => p.Etiquetas)` | `List<string>` | `Expected 3, found 0` |
| `Select_ListOfInts_ReturnsIntArray` | `.Select(p => p.Cantidades)` | `List<int>` | `Expected 4, found 0` |
| `Select_ListOfDoubles_ReturnsDoubleArray` | `.Select(p => p.Pesos)` | `List<double>` | `Expected 4, found 0` |
| `Select_ListOfDecimals_ReturnsDecimalArray` | `.Select(p => p.Precios)` | `List<decimal>` | `Expected 4, found 0` |
| `Select_ListOfEnums_ReturnsEnumArray` | `.Select(p => p.Tags)` | `List<CategoriaProducto>` | `Expected 3, found 0` |
| `Select_ListOfGeoLocations_ReturnsGeoLocationArray` | `.Select(p => p.Ubicaciones)` | `List<GeoLocation>` | `Expected 3, found 0` |
| `Select_ListOfComplexTypes_ReturnsComplexTypeArray` | `.Select(p => p.DireccionesEntrega)` | `List<Direccion>` | `Expected 2, found 0` |

### Grupo B: SelectComplexTypeTests (1 test)

Proyección directa de ComplexType: `.Select(p => p.Direccion)`

| Test | Query | TargetType | Error |
|------|-------|------------|-------|
| `Select_ComplexTypeCompleto_ReturnsEntireComplexType` | `.Select(p => p.Direccion)` | `Direccion` | `Calle is null` |

### Grupo C: SelectReferenceTests (12 tests)

Proyecciones con campos de FK

| Test | Query Simplificado | Error |
|------|-------------------|-------|
| `Select_PartialFkToDto_ReturnsProjectedFields` | `new { AutorNombre = l.Autor.Nombre }` | `AutorNombre is null` |
| `Select_PartialFkToRecord_ReturnsProjectedFields` | `new Record(l.Titulo, l.Autor.Nombre)` | `AutorNombre is null` |
| `Select_FullFkToRecord_ReturnsEntityWithFullReference` | `new Record(l.Titulo, l.Autor)` | `Autor.Nombre is null` |
| `Select_FullFkToDto_ReturnsEntityWithFullReference` | `new { Autor = l.Autor }` | `Autor.Nombre is null` |
| `Select_PartialComplexTypeAndPartialFk_ReturnsProjectedFields` | `new { ISBN, AutorNombre = l.Autor.Nombre }` | `AutorNombre is null` |
| `Select_FullComplexTypeAndFullFk_ReturnsCompleteEntities` | `new { DatosPublicacion, Autor }` | `Autor.Nombre is null` |
| `Select_NestedFk_ReturnsAllNestedFields` | `new { l.Autor.PaisOrigen.Nombre }` | `PaisNombre is null` |
| `Select_NestedFkFullEntities_ReturnsCompleteHierarchy` | `new { l.Autor }` | `Autor.Nombre is null` |
| `Select_SubcollectionWithFk_ReturnsAllProjectedFields` | `Ejemplares.Select(e => new { e.Libro.Titulo })` | `LibroTitulo is null` |
| `Select_SubcollectionWithFullFk_ReturnsCompleteEntities` | `Ejemplares.Select(e => new { e.Libro })` | `Libro.Titulo is null` |
| `Select_MultipleFkFields_ReturnsAllRequestedFields` | `new { l.Autor.Nombre, l.Editorial.Nombre }` | `AutorNombre is null` |
| `Select_MixedProjection_RootScalarsFkScalarsAndFkEntity` | `new { Titulo, AutorNombre, Editorial }` | `AutorNombre is null` |

---

## 3. Análisis por Grupo

### Grupo A: Arrays - ¿Por qué falla?

**Query:** `.Select(p => p.Cantidades)` donde `Cantidades` es `List<int>`

**Lo que pasa en SnapshotShaper:**
1. `ShapeNode` copia `rawDict["Cantidades"]` = `[1, 2, 3, 4]` al diccionario
2. `FlattenForProjection` recibe `{ "Cantidades": [1,2,3,4] }`
3. `FlattenRecursive` ve que `[1,2,3,4]` NO es `Dictionary<string, object?>`, así que lo deja como leaf
4. Resultado: `{ "Cantidades": [1,2,3,4] }` ✓

**Lo que pasa en Materializer:**
1. `targetType` = `List<int>` (tipo simple de colección, no entidad)
2. `IsSimpleType(List<int>)` = `false` (no es primitivo)
3. Entra a `GetOrCreateStrategy(List<int>)`
4. `FindBestConstructor(List<int>)` busca constructores de `List<int>`
5. **PROBLEMA**: Intenta materializar `List<int>` como si fuera una entidad con propiedades

**Causa raíz:** El Materializer no detecta que `List<int>` es una colección de primitivos que debe extraerse directamente del diccionario.

---

### Grupo B: ComplexType Completo - ¿Por qué falla?

**Query:** `.Select(p => p.Direccion)` donde `Direccion` es ComplexType

**Lo que pasa en SnapshotShaper:**
1. `ShapeNode` copia todo de `rawDict` incluyendo `Direccion: { Calle, Ciudad, ... }`
2. `FlattenForProjection` aplana a `{ "Direccion.Calle": "...", "Direccion.Ciudad": "...", ... }`
3. Resultado: `{ "Direccion.Calle": "Gran Vía", "Direccion.Ciudad": "Madrid", ... }`

**Lo que pasa en Materializer:**
1. `targetType` = `Direccion`
2. `IsSimpleType(Direccion)` = `false`
3. `GetOrCreateStrategy(Direccion)` encuentra constructor con params `(calle, ciudad, codigoPostal, coordenadas)`
4. `ExecuteStrategy` busca en diccionario por `"Calle"`, `"Ciudad"`, etc.
5. **PROBLEMA**: El diccionario tiene `"Direccion.Calle"`, no `"Calle"`

**Causa raíz:** Cuando el targetType ES el ComplexType, las keys del diccionario tienen prefijo `"Direccion."` pero el Materializer busca sin prefijo.

**Solución existente:** `GetValueOrNestedDict` debería reconstruir el diccionario anidado, PERO no se está llamando correctamente porque `projectedFields` es null o no tiene el mapping correcto.

---

### Grupo C: Referencias FK - ¿Por qué falla?

**Query:** `.Select(l => new { AutorNombre = l.Autor.Nombre })`

**Lo que debería pasar:**
1. El `ResolvedProjectionDefinition.Fields` debería tener:
   - `FieldPath = "Autor.Nombre"`, `ResultName = "AutorNombre"`
2. El SnapshotShaper debería resolver el Include de `Autor` y poner `"Autor.Nombre": "Gabriel..."`
3. El Materializer busca por `FieldPath` y asigna a `ResultName`

**Lo que probablemente pasa:**
1. El SnapshotShaper NO está incluyendo los campos de la FK en el diccionario
2. O los Include de FK no se están resolviendo en el contexto de proyecciones

**Causa raíz:** El SnapshotShaper procesa `Includes` para navegación pero las proyecciones con FK usan un mecanismo diferente que no está conectado.

---

## 4. Diagnóstico de Cada Problema

### Problema 1: Proyección de Array Directo (Grupo A)

**Ubicación del fix:** MATERIALIZER

**Por qué:** El SnapshotShaper produce `{ "Cantidades": [1,2,3,4] }` correctamente. El Materializer debe detectar que cuando `targetType` es `List<T>` o `T[]`, debe extraer el valor directamente del diccionario sin intentar materializarlo como entidad.

**Fix necesario:** En `Materializer.Materialize()`, antes de `GetOrCreateStrategy()`:
```csharp
if (IsCollectionType(targetType))
{
    return MaterializeDirectCollection(shaped, targetType);
}
```

---

### Problema 2: Proyección de ComplexType Completo (Grupo B)

**Ubicación del fix:** MATERIALIZER

**Por qué:** El SnapshotShaper produce `{ "Direccion.Calle": "...", "Direccion.Ciudad": "..." }`. El problema es que el Materializer busca `"Calle"` pero la key es `"Direccion.Calle"`.

Cuando `targetType` = `Direccion` y el diccionario tiene keys con prefijo `"Direccion."`, el Materializer debe:
1. Detectar que todas las keys tienen un prefijo común
2. Usar `GetValueOrNestedDict` para reconstruir el diccionario sin prefijo

**Fix necesario:** En `Materializer.ExecuteStrategy()`, usar `GetValueOrNestedDict` cuando el mapping directo falla.

---

### Problema 3: Proyección con FK (Grupo C)

**Ubicación del fix:** SNAPSHOTSHAPER

**Por qué:** Los campos de la FK (`Autor.Nombre`) NO están en el diccionario de salida. El SnapshotShaper procesa `ResolvedInclude` pero no incorpora los campos al diccionario aplanado cuando es una proyección.

**Investigar:**
1. ¿`ResolvedFirestoreQuery.Includes` tiene los Include de FK para proyecciones?
2. ¿`ResolveReferenceInPlace` se está llamando?
3. ¿Los campos de la FK se pierden en `FlattenForProjection`?

**Fix necesario:** Verificar que cuando hay proyección con FK, los campos de la entidad referenciada se incluyan en el diccionario con el path correcto (`"Autor.Nombre"`).

---

## 5. Tabla de Resolución

| # | Test | Grupo | Dónde Resolver | Razón del Fallo |
|---|------|-------|----------------|-----------------|
| 1 | `Select_ListOfStrings_ReturnsStringArray` | A | **MATERIALIZER** | No detecta `List<string>` como colección directa |
| 2 | `Select_ListOfInts_ReturnsIntArray` | A | **MATERIALIZER** | No detecta `List<int>` como colección directa |
| 3 | `Select_ListOfDoubles_ReturnsDoubleArray` | A | **MATERIALIZER** | No detecta `List<double>` como colección directa |
| 4 | `Select_ListOfDecimals_ReturnsDecimalArray` | A | **MATERIALIZER** | No detecta `List<decimal>` como colección directa |
| 5 | `Select_ListOfEnums_ReturnsEnumArray` | A | **MATERIALIZER** | No detecta `List<enum>` como colección directa |
| 6 | `Select_ListOfGeoLocations_ReturnsGeoLocationArray` | A | **MATERIALIZER** | No detecta `List<GeoLocation>` como colección directa |
| 7 | `Select_ListOfComplexTypes_ReturnsComplexTypeArray` | A | **MATERIALIZER** | No detecta `List<ComplexType>` como colección directa |
| 8 | `Select_ComplexTypeCompleto_ReturnsEntireComplexType` | B | **MATERIALIZER** | Busca `"Calle"` pero key es `"Direccion.Calle"` |
| 9 | `Select_PartialFkToDto_ReturnsProjectedFields` | C | **SNAPSHOTSHAPER** | FK `Autor.Nombre` no está en diccionario |
| 10 | `Select_PartialFkToRecord_ReturnsProjectedFields` | C | **SNAPSHOTSHAPER** | FK `Autor.Nombre` no está en diccionario |
| 11 | `Select_FullFkToRecord_ReturnsEntityWithFullReference` | C | **SNAPSHOTSHAPER** | FK `Autor` completo no está en diccionario |
| 12 | `Select_FullFkToDto_ReturnsEntityWithFullReference` | C | **SNAPSHOTSHAPER** | FK `Autor` completo no está en diccionario |
| 13 | `Select_PartialComplexTypeAndPartialFk_ReturnsProjectedFields` | C | **SNAPSHOTSHAPER** | FK `Autor.Nombre` no está en diccionario |
| 14 | `Select_FullComplexTypeAndFullFk_ReturnsCompleteEntities` | C | **SNAPSHOTSHAPER** | FK `Autor` completo no está en diccionario |
| 15 | `Select_NestedFk_ReturnsAllNestedFields` | C | **SNAPSHOTSHAPER** | FK anidada `Autor.PaisOrigen.Nombre` no está |
| 16 | `Select_NestedFkFullEntities_ReturnsCompleteHierarchy` | C | **SNAPSHOTSHAPER** | FK `Autor` con nested no está |
| 17 | `Select_SubcollectionWithFk_ReturnsAllProjectedFields` | C | **SNAPSHOTSHAPER** | FK en subcollection no resuelto |
| 18 | `Select_SubcollectionWithFullFk_ReturnsCompleteEntities` | C | **SNAPSHOTSHAPER** | FK completa en subcollection no resuelto |
| 19 | `Select_MultipleFkFields_ReturnsAllRequestedFields` | C | **SNAPSHOTSHAPER** | Múltiples FK no en diccionario |
| 20 | `Select_MixedProjection_RootScalarsFkScalarsAndFkEntity` | C | **SNAPSHOTSHAPER** | FK mixta no en diccionario |

---

## 6. Resumen de Fixes

### MATERIALIZER (8 tests: Grupo A + Grupo B)

1. **Detectar colección directa como targetType**
   - Si `targetType` es `List<T>`, `T[]`, `HashSet<T>`, etc.
   - Extraer el valor directamente del diccionario (primera key que sea array)
   - Convertir elementos con el Converter

2. **Reconstruir ComplexType desde diccionario aplanado**
   - Si `targetType` es ComplexType y diccionario tiene keys con prefijo
   - Usar `GetValueOrNestedDict` para reconstruir

### SNAPSHOTSHAPER (12 tests: Grupo C)

1. **Incorporar campos de FK al diccionario**
   - Cuando hay proyección con FK (`l.Autor.Nombre`)
   - Los Include deben resolverse Y sus campos deben aplanarse al diccionario
   - Keys: `"Autor.Nombre"`, `"Autor.Pais"`, etc.

---

## 7. Orden de Implementación

1. **Primero:** Fix en Materializer para Grupo A (colecciones directas) - 7 tests
2. **Segundo:** Fix en Materializer para Grupo B (ComplexType directo) - 1 test
3. **Tercero:** Fix en SnapshotShaper para Grupo C (FK en proyecciones) - 12 tests

Este orden minimiza el riesgo de regresiones ya que cada fix es independiente.
