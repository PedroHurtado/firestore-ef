# Informe de Tests de Integración Fallidos

**Fecha:** 2026-01-21
**Proyecto:** Fudie.Firestore.EntityFrameworkCore
**Total Tests:** 489 | **Pasados:** 461 | **Fallidos:** 5 | **Omitidos:** 23

---

## Resumen Ejecutivo

| # | Test | Tipo de Error |
|---|------|---------------|
| 1 | `FullIntegration_Menu_WithCategories_Items_AndAllergens_ShouldPersistAndRetrieve` | HashSet vs List |
| 2 | `FullIntegration_MenuItem_WithAllFeatures_ShouldPersistAndRetrieve` | HashSet vs List |
| 3 | `Select_ComplexTypeCompleto_ReturnsEntireComplexType` | ComplexType en proyección |
| 4 | `Select_WithNestedAnonymousType_ReturnsNestedStructure` | Tipo anónimo anidado |
| 5 | `HashSet_RecordGeoPoint_AutoDetected_ShouldDeserializeAsGeoPoints` | HashSet vs List |

---

## Análisis Detallado

### ERROR 1: `FullIntegration_Menu_WithCategories_Items_AndAllergens`

**Clase:** `ProviderFixesPersistenceTests`

**Excepción:**
```
System.ArgumentException: Object of type 'System.Collections.Generic.List`1[PriceOption]'
cannot be converted to type 'System.Collections.Generic.HashSet`1[PriceOption]'.
```

**Ubicación del error:** `Materializer.cs:80` - `Constructor.Invoke(args)`

**Datos del ShapedResult (correctos):**
```
PriceOptions: [2] (IEnumerable<Object>, ObjectList)
  [0] ShapedItem
    PortionType: "Small" (String, Scalar)
    Price: 6,5 (Double, Scalar)
    IsActive: True (Boolean, Scalar)
  [1] ShapedItem
    PortionType: "Full" (String, Scalar)
    Price: 9,9 (Double, Scalar)
    IsActive: True (Boolean, Scalar)
```

**Causa Raíz:** El método `MaterializeObjectListWithTarget` crea una `List<T>` mediante `CreateCollection`, pero el constructor del tipo `MenuItem` espera un parámetro de tipo `HashSet<PriceOption>`. Aunque `CreateCollection` tiene lógica para crear `HashSet`, esta no se está ejecutando correctamente porque:

1. `CreateCollection` verifica `collectionType.GetGenericTypeDefinition() == typeof(HashSet<>)`
2. Pero el `targetType` que llega puede ser la interfaz `IEnumerable<>` o `ICollection<>`, no `HashSet<>`
3. Por defecto crea `List<T>` que luego es incompatible con el parámetro del constructor

---

### ERROR 2: `FullIntegration_MenuItem_WithAllFeatures`

**Clase:** `ProviderFixesPersistenceTests`

**Excepción:**
```
System.ArgumentException: Object of type 'System.Collections.Generic.List`1[System.DayOfWeek]'
cannot be converted to type 'System.Collections.Generic.HashSet`1[System.DayOfWeek]'.
```

**Ubicación del error:** `Materializer.cs:105` - `field.SetValue(instance, materializedValue)`

**Datos del ShapedResult (correctos):**
```
AvailableDays: [3] (IEnumerable<Object>, ScalarList)
  [0] "Friday"
  [1] "Saturday"
  [2] "Sunday"
```

**Causa Raíz:** El campo `AvailableDays` está marcado como `ScalarList` en el ShapedValue. El Materializer llama a `_converter.FromFirestore(shaped.Value, targetType)` para ScalarList (línea 125), pero:

1. El `targetType` del strategy es `HashSet<DayOfWeek>` (del backing field)
2. El converter devuelve `List<DayOfWeek>` porque no maneja `HashSet` específicamente
3. Al intentar asignar `List<DayOfWeek>` a un campo `HashSet<DayOfWeek>`, falla

---

### ERROR 3: `Select_ComplexTypeCompleto_ReturnsEntireComplexType`

**Clase:** `SelectComplexTypeTests`

**Excepción:**
```
Expected direccion.Calle to be "Gran Vía 123", but found <null>.
```

**Datos del ShapedResult (correctos):**
```
HasProjection: True

[0] ShapedItem
  Direccion: (Direccion, ComplexType)
    Calle: "Gran Vía 123" (String, Scalar)
    Ciudad: "Madrid" (String, Scalar)
    CodigoPostal: "28013" (String, Scalar)
    Coordenadas: (Object, ComplexType)
      Altitud: 650 (Double, Scalar)
      Posicion: GeoPoint: Latitude=40,42; Longitude=-3,705 (GeoPoint, Scalar)
```

**Nota:** El log muestra `>>> MATERIALIZED: 1 items` - la materialización se completó sin excepción, pero el objeto resultante tiene propiedades `null`.

**Causa Raíz:** Cuando la proyección es `Select(p => p.Direccion)`:

1. El `targetType` es `Direccion`
2. El ShapedItem raíz tiene una única clave `"Direccion"` cuyo valor es un `ShapedItem` anidado
3. El Materializer intenta crear una instancia de `Direccion` buscando propiedades `Calle`, `Ciudad` directamente en el ShapedItem raíz
4. Estas propiedades no existen en el raíz - están dentro del ShapedItem anidado bajo la clave `"Direccion"`
5. Como no las encuentra, todas las propiedades quedan en `null`

**Solución esperada:** Cuando `HasProjection = true` y el ShapedItem tiene una única clave cuyo `ResultName` coincide con el nombre del tipo (o es un ComplexType), el Materializer debería extraer el ShapedItem anidado y usarlo para la materialización.

---

### ERROR 4: `Select_WithNestedAnonymousType_ReturnsNestedStructure`

**Clase:** `SelectSubcollectionTests`

**Excepción:**
```
System.NullReferenceException: Object reference not set to an instance of an object.
```
En línea 961: `result.Resumen.TotalPedidos.Should().Be(500m);`

**Query:**
```csharp
.Select(c => new
{
    c.Nombre,
    Resumen = new
    {
        TotalPedidos = c.Pedidos.Sum(p => p.Total),
        Cantidad = c.Pedidos.Count()
    }
})
```

**Datos del ShapedResult (correctos):**
```
HasProjection: True

[0] ShapedItem
  Nombre: "Cliente Nested" (String, Scalar)
  Resumen.TotalPedidos: 500 (Decimal, Scalar)
  Resumen.Cantidad: 2 (Decimal, Scalar)
```

**Nota:** El log muestra `>>> MATERIALIZED: 1 items` - pero `result.Resumen` es `null`.

**Causa Raíz:** Las claves del ShapedItem usan dot notation:
- Clave: `"Resumen.TotalPedidos"`, ResultName: `"TotalPedidos"`
- Clave: `"Resumen.Cantidad"`, ResultName: `"Cantidad"`

El tipo anónimo tiene:
- Propiedad `Nombre` (string) ✓ Se encuentra correctamente
- Propiedad `Resumen` (tipo anónimo anidado) ✗ No se encuentra

El Materializer busca un `ShapedValue` con `ResultName = "Resumen"`, pero:
1. No existe ninguna clave con ese ResultName
2. Solo existen `"TotalPedidos"` y `"Cantidad"` como ResultNames
3. Por lo tanto, `Resumen` queda en `null`

**Solución esperada:** El SnapshotShaper debería agrupar los campos con prefijo común (`Resumen.`) en un ShapedItem anidado, o el Materializer debería reconstruir la estructura jerárquica a partir de las claves con dot notation.

---

### ERROR 5: `HashSet_RecordGeoPoint_AutoDetected`

**Clase:** `HashSetArrayOfTests_Query`

**Excepción:**
```
System.ArgumentException: Object of type 'System.Collections.Generic.List`1[Ubicacion]'
cannot be converted to type 'System.Collections.Generic.HashSet`1[Ubicacion]'.
```

**Ubicación del error:** `Materializer.cs:101` - `prop.SetValue(instance, materializedValue)`

**Datos del ShapedResult (correctos):**
```
[0] ShapedItem
  Id: "prod-13f50b419aa5469eabb2aec9f7f58e08" (String, Scalar)
  Nombre: "Laptop Gaming" (String, Scalar)
  PuntosVenta: [2] (IEnumerable<Object>, ScalarList)
    [0] GeoPoint: Latitude=40,4168; Longitude=-3,7038
    [1] GeoPoint: Latitude=41,3851; Longitude=2,1734
```

**Causa Raíz:** Idéntica al Error 2. El ShapedValue tiene `Kind = ScalarList` pero el tipo destino de la propiedad es `HashSet<Ubicacion>`. El converter devuelve `List<Ubicacion>` que es incompatible.

---

## Clasificación de Errores

### Grupo A: HashSet vs List (3 tests)
**Tests afectados:** 1, 2, 5

**Problema común:** El Materializer no crea colecciones del tipo correcto cuando el destino es `HashSet<T>`.

**Archivos involucrados:**
- `Materializer.cs` - métodos `MaterializeObjectListWithTarget`, `MaterializeValueWithTargetType`
- Posiblemente `IFirestoreValueConverter` - método `FromFirestore`

**Puntos de corrección:**
1. `MaterializeObjectListWithTarget` (línea 156-218): Debe verificar si `targetType` es `HashSet<>` y crear el tipo correcto
2. `MaterializeValueWithTargetType` para `ScalarList` (línea 125): Debe manejar `HashSet<T>` además de `List<T>`
3. `CreateCollection` (línea 456-477): Tiene la lógica pero no se está usando correctamente

### Grupo B: ComplexType en proyección (1 test)
**Test afectado:** 3

**Problema:** Cuando se proyecta un ComplexType completo (`Select(p => p.Direccion)`), el Materializer no extrae el ShapedItem anidado.

**Archivos involucrados:**
- `Materializer.cs` - método `Materialize` o `MaterializeItem`

**Punto de corrección:**
- En `Materialize`, cuando `HasProjection = true` y el ShapedItem tiene una única clave cuyo valor es un ShapedItem de tipo ComplexType, usar ese ShapedItem anidado para materializar.

### Grupo C: Tipos anónimos anidados (1 test)
**Test afectado:** 4

**Problema:** Las claves con dot notation no se reconstruyen en estructura jerárquica.

**Archivos involucrados:**
- `SnapshotShaper.cs` - debería crear ShapedItems anidados para prefijos comunes
- O `Materializer.cs` - debería reconstruir la jerarquía desde dot notation

**Punto de corrección:**
- Opción A (SnapshotShaper): Agrupar campos con prefijo común en ShapedItems anidados
- Opción B (Materializer): Detectar claves con dot notation y construir objetos anidados dinámicamente

---

## Prioridad de Corrección Sugerida

1. **Alta - Grupo A (HashSet):** Afecta 3 tests y es un problema de conversión de tipos relativamente directo.

2. **Media - Grupo B (ComplexType):** Afecta 1 test pero es un escenario común en proyecciones.

3. **Media - Grupo C (Anónimos anidados):** Afecta 1 test y requiere decisión arquitectónica sobre dónde hacer la corrección.

---

## Archivos de Referencia

- Log de ejecución: `query-execution.log`
- Materializer: `src/Fudie.Firestore.EntityFrameworkCore/Query/Pipeline/Services/Materializer.cs`
- SnapshotShaper: `src/Fudie.Firestore.EntityFrameworkCore/Query/Pipeline/Services/SnapshotShaper.cs`
- Tests fallidos:
  - `tests/Fudie.Firestore.IntegrationTest/ProviderFixes/ProviderFixesPersistenceTests.cs`
  - `tests/Fudie.Firestore.IntegrationTest/Projections/SelectComplexTypeTests.cs`
  - `tests/Fudie.Firestore.IntegrationTest/Projections/SelectSubcollectionTests.cs`
  - `tests/Fudie.Firestore.IntegrationTest/ArrayOf/Query/HashSetArrayOfTests_Query.cs`
