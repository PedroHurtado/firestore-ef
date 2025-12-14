# Plan: Soporte de Queries por Enum

**Fecha:** 2025-12-14

## Problema Detectado

En los tests de `EnumConventionTests` (Fase 3), se detectó que el provider **no soporta filtrar por enum** en queries LINQ:

```csharp
// ESTO FALLA ACTUALMENTE
var productos = await context.ProductosCompletos
    .Where(p => p.Categoria == CategoriaProducto.Hogar)
    .ToListAsync();
```

**Error:**
```
System.InvalidOperationException: The LINQ expression 'DbSet<ProductoCompleto>()
    .Where(p => (int)p.Categoria == 3 && p.Id == __idHogar_0)' could not be translated.
```

## Objetivo

Permitir filtrar entidades por propiedades de tipo `enum` en queries LINQ, traduciendo la comparación a string en Firestore.

## Análisis Técnico

### Cómo funciona actualmente
1. **Escritura:** `EnumToStringConvention` convierte enum → string al guardar ✅
2. **Lectura:** `FirestoreDocumentDeserializer` convierte string → enum al leer ✅
3. **Query:** El translator LINQ no sabe convertir `p.Categoria == CategoriaProducto.Hogar` a `Categoria == "Hogar"` ❌

### Solución propuesta
Modificar `FirestoreWhereTranslator` para detectar comparaciones de enum y traducirlas a comparaciones de string.

## Archivos a Modificar

| Archivo | Cambio |
|---------|--------|
| `firestore-efcore-provider/Query/Visitors/FirestoreWhereTranslator.cs` | Detectar comparaciones de enum |
| `firestore-efcore-provider/Query/FirestoreWhereClause.cs` | Posible ajuste en cómo se genera el where |

## Pasos de Implementación

### Fase 1: Análisis del Query Translator ✅ COMPLETADA
1. [x] Revisar `FirestoreWhereTranslator.cs` y entender el flujo actual
2. [x] Identificar dónde se procesan las comparaciones binarias (==, !=, etc.)
3. [x] Determinar cómo detectar que un operando es enum

### Fase 2: Implementar Traducción de Enum ✅ COMPLETADA
4. [x] Modificar el visitor para detectar `EnumProperty == EnumValue`
5. [x] Convertir `EnumValue` a su representación string (`.ToString()`)
6. [x] Generar cláusula where con el string en lugar del int

### Fase 3: Tests ✅ COMPLETADA
7. [x] Crear test `Query_FilterByEnum_ShouldWork` en `EnumConventionTests.cs`
8. [x] Test con operador `==`
9. [x] Test con operador `!=`
10. [x] Test con variable de enum

### Fase 4: Verificación ✅ COMPLETADA
11. [x] Ejecutar todos los tests de conventions
12. [x] Verificar que no se rompió nada existente (43 tests pasando)

### Fase 5: Commit ✅ COMPLETADA
**Commit:** `e02decc`
13. [x] Commit con mensaje descriptivo

## Ejemplo de Traducción Esperada

```csharp
// C# LINQ
.Where(p => p.Categoria == CategoriaProducto.Hogar)

// Firestore Query (interno)
// Campo: "Categoria", Operador: "==", Valor: "Hogar"
```

## Test a Implementar

```csharp
[Fact]
public async Task Query_FilterByEnum_ShouldWork()
{
    // Arrange
    using var context = _fixture.CreateContext<TestDbContext>();
    var id = FirestoreTestFixture.GenerateId("prod");

    var producto = new ProductoCompleto
    {
        Id = id,
        Nombre = "Test Filter Enum",
        Precio = 200m,
        Categoria = CategoriaProducto.Hogar,
        // ... resto de propiedades
    };

    context.ProductosCompletos.Add(producto);
    await context.SaveChangesAsync();

    // Act - Filtrar por categoría
    using var readContext = _fixture.CreateContext<TestDbContext>();
    var productos = await readContext.ProductosCompletos
        .Where(p => p.Categoria == CategoriaProducto.Hogar)
        .ToListAsync();

    // Assert
    productos.Should().Contain(p => p.Id == id);
}
```

## Dependencias

- Tests de EnumConvention completados (Fase 3 del plan de conventions)
- Conocimiento de cómo funciona el query translator actual

## Notas

- El enum se guarda como string en Firestore (ej: "Hogar", no 3)
- La traducción debe usar el **nombre** del enum, no su valor numérico
- Considerar case sensitivity (Firestore es case-sensitive)
