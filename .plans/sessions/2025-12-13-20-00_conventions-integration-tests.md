# Plan: Tests de Integración para Conventions

**Fecha:** 2025-12-13 20:00

## Objetivo

Crear tests de integración que cubran todas las conventions del provider Firestore EF Core, basándose en el archivo `firestore-test/_Program_cs`.

## Conventions a Cubrir

| # | Convention | Descripción | Prioridad |
|---|------------|-------------|-----------|
| 1 | DecimalToDoubleConvention | `decimal` → `double` | Alta |
| 2 | EnumToStringConvention | `enum` → `string` | Alta |
| 3 | ListDecimalToDoubleArrayConvention | `List<decimal>` → `List<double>` | Alta |
| 4 | ListEnumToStringArrayConvention | `List<enum>` → `List<string>` | Alta |
| 5 | ArrayConvention | `List<int>`, `List<string>` → arrays nativos | Alta |
| 6 | GeoPointConvention | Clase/record con `Latitude`/`Longitude` → `GeoPoint` | Alta |
| 7 | TimestampConvention | `DateTime` con nombres específicos → `Timestamp` | Media |
| 8 | PrimaryKeyConvention | Auto-detecta `Id` como PK | Media |
| 9 | CollectionNamingConvention | Pluralización automática | Media |
| 10 | ComplexTypeConvention | Tipos complejos anidados | Alta |

> **Nota:** Los tests de `.Reference()` (DocumentReference/FK) se implementarán en el plan `2025-12-14_remove-nm-1n-relations.md`

## Estructura Propuesta

```
tests/Fudie.Firestore.IntegrationTest/
├── Conventions/                    <- NUEVA CARPETA
│   ├── DecimalConventionTests.cs
│   ├── EnumConventionTests.cs
│   ├── ArrayConventionTests.cs
│   ├── GeoPointConventionTests.cs
│   └── ComplexTypeConventionTests.cs
├── Helpers/
│   ├── TestEntities.cs            <- MODIFICAR
│   └── TestDbContext.cs           <- MODIFICAR
```

## Modificaciones a TestEntities.cs

### Nuevas Entidades

```csharp
// === VALUE OBJECTS para GeoPoint ===
// Detección por estructura: cualquier clase/record con Latitude + Longitude
public record GeoLocation(double Latitude, double Longitude);

public record Coordenadas
{
    public double Altitud { get; init; }
    public required GeoLocation Posicion { get; init; }
}

// === ComplexType ===
public record Direccion
{
    public required string Calle { get; init; }
    public required string Ciudad { get; init; }
    public required string CodigoPostal { get; init; }
    public required Coordenadas Coordenadas { get; init; }
}

// === Enum para tests ===
public enum CategoriaProducto
{
    Electronica,
    Ropa,
    Alimentos,
    Hogar
}

// === Entidad con TODAS las conventions ===
public class ProductoCompleto
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public decimal Precio { get; set; }                      // DecimalToDouble
    public CategoriaProducto Categoria { get; set; }         // EnumToString
    public DateTime FechaCreacion { get; set; }              // Timestamp
    public required GeoLocation Ubicacion { get; set; }      // GeoPoint directo
    public required Direccion Direccion { get; set; }        // ComplexType con GeoPoint anidado
    public List<decimal> Precios { get; set; } = [];         // ListDecimalToDouble
    public List<CategoriaProducto> Tags { get; set; } = [];  // ListEnumToString
    public List<int> Cantidades { get; set; } = [];          // Array nativo int
    public List<string> Etiquetas { get; set; } = [];        // Array nativo string
}

```

## Tests a Implementar

### 1. DecimalConventionTests.cs

| Test | Descripción |
|------|-------------|
| `Add_EntityWithDecimal_ShouldPersistAsDouble` | decimal único |
| `Add_EntityWithListDecimal_ShouldPersistAsDoubleArray` | List<decimal> |
| `Query_EntityWithDecimal_ShouldReturnCorrectValue` | Lectura decimal |
| `Update_DecimalProperty_ShouldPersistChanges` | Actualización decimal |

### 2. EnumConventionTests.cs

| Test | Descripción |
|------|-------------|
| `Add_EntityWithEnum_ShouldPersistAsString` | enum único |
| `Add_EntityWithListEnum_ShouldPersistAsStringArray` | List<enum> |
| `Query_EntityWithEnum_ShouldReturnCorrectValue` | Lectura enum |
| `Query_FilterByEnum_ShouldWork` | Filtro por enum |

### 3. ArrayConventionTests.cs

| Test | Descripción |
|------|-------------|
| `Add_EntityWithListInt_ShouldPersistAsArray` | List<int> |
| `Add_EntityWithListString_ShouldPersistAsArray` | List<string> |
| `Query_EntityWithArrays_ShouldReturnCorrectValues` | Lectura arrays |
| `Update_ArrayProperty_ShouldPersistChanges` | Actualización arrays |

### 4. GeoPointConventionTests.cs

| Test | Descripción |
|------|-------------|
| `Add_EntityWithGeoPoint_ShouldPersist` | GeoPoint directo |
| `Add_EntityWithNestedGeoPoint_ShouldPersist` | GeoPoint en ComplexType |
| `Query_EntityWithGeoPoint_ShouldReturnCoordinates` | Lectura GeoPoint |
| `Update_GeoPointProperty_ShouldPersistChanges` | Actualización coordenadas |

### 5. ComplexTypeConventionTests.cs

| Test | Descripción |
|------|-------------|
| `Add_EntityWithComplexType_ShouldPersist` | ComplexType simple |
| `Add_EntityWithNestedComplexType_ShouldPersist` | ComplexType anidado |
| `Query_EntityWithComplexType_ShouldReturnData` | Lectura ComplexType |
| `Update_ComplexTypeProperty_ShouldPersistChanges` | Actualización ComplexType |

## Orden de Implementación

### Fase 1: Preparación de Entidades ✅ COMPLETADA
**Commit:** `9f0b0c6`
1. [x] Agregar `GeoLocation` record (detección por Latitude/Longitude)
2. [x] Agregar `Coordenadas` record
3. [x] Agregar `Direccion` record
4. [x] Agregar `CategoriaProducto` enum
5. [x] Agregar `ProductoCompleto` entidad
6. [x] Modificar `TestDbContext` con configuraciones
7. [x] **Build para verificar compilación**

### Fase 2: Tests de Decimal Convention ✅ COMPLETADA
**Commit:** `9a5d491`
8. [x] Crear `Conventions/DecimalConventionTests.cs`
9. [x] Implementar tests de decimal
10. [x] **Ejecutar tests y verificar** (4 tests pasando)

### Fase 3: Tests de Enum Convention ✅ COMPLETADA
**Commit:** `18552b9`
11. [x] Crear `Conventions/EnumConventionTests.cs`
12. [x] Implementar tests de enum
13. [x] **Ejecutar tests y verificar** (4 tests pasando)

### Fase 4: Tests de Array Convention ✅ COMPLETADA
**Commit:** `0c83998`
14. [x] Crear `Conventions/ArrayConventionTests.cs`
15. [x] Implementar tests de arrays (int, string)
16. [x] **Ejecutar tests y verificar** (4 tests pasando)
**Fix:** Agregado soporte de deserialización para `List<int>`, `List<string>`, `List<double>`, `List<long>` en `FirestoreDocumentDeserializer.cs`

### Fase 5: Tests de GeoPoint Convention ✅ COMPLETADA
**Commit:** `765b48d`
17. [x] Crear `Conventions/GeoPointConventionTests.cs`
18. [x] Implementar tests de GeoPoint
19. [x] **Ejecutar tests y verificar** (4 tests pasando)
**Fixes:**
- GeoPointConvention ahora detecta por ESTRUCTURA (Latitude+Longitude), no por nombre
- DeserializeGeoPoint soporta positional records (constructor con parámetros)
- Annotation corregido de `"Firestore:GeoPoint"` a `"Firestore:IsGeoPoint"`

### Fase 6: Tests de ComplexType
20. [ ] Crear `Conventions/ComplexTypeConventionTests.cs`
21. [ ] Implementar tests de ComplexType
22. [ ] **Ejecutar tests y verificar**

### Fase 7: Commit
23. [ ] Commit con mensaje descriptivo

## Comandos de Verificación

```bash
# Build del proyecto de tests
dotnet build tests/Fudie.Firestore.IntegrationTest

# Ejecutar solo tests de Conventions
dotnet test tests/Fudie.Firestore.IntegrationTest --filter "ConventionTests"

# Ejecutar todos los tests de integración
dotnet test tests/Fudie.Firestore.IntegrationTest
```

## Dependencias

- Emulador de Firestore corriendo: `docker-compose up -d`
- Provider con soporte de conventions (ya implementado)
- Tracking de entidades (ya implementado)
- SubCollections (ya implementado)

## Notas Importantes

### Sobre GeoPoint
La convention detecta **por estructura**, no por nombre:
- Cualquier clase o record con propiedades `Latitude` y `Longitude` (ambas `double`)
- No importa el nombre del tipo (puede ser `GeoLocation`, `Coordenadas`, `Position`, etc.)
- Ejemplo: `record GeoLocation(double Latitude, double Longitude)`

### Sobre ComplexTypes
Para simplificar los tests, usaremos `Direccion` sin navigation properties internas.

### Sobre Relaciones
**ELIMINADAS** del scope de este plan. Ver plan `2025-12-14_remove-nm-1n-relations.md`.
- N:M y 1:N tradicionales serán bloqueadas con error
- FK 1:1 usará nueva API `.Reference()` (implementada mañana)
- SubCollections ya funcionan con `.SubCollection()`

### Sobre Arrays
Firestore soporta arrays nativos. Los tests cubren:
- `List<int>` → array de números
- `List<string>` → array de strings
- `List<decimal>` → array de doubles (via convention)
- `List<enum>` → array de strings (via convention)

## Riesgos

| Riesgo | Mitigación |
|--------|------------|
| GeoPoint no detectado correctamente | Verificar que detecte por Latitude/Longitude |
| ComplexTypes con muchos niveles | Limitar a 2-3 niveles de anidación |
