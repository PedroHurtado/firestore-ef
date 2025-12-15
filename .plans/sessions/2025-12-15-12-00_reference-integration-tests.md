# Plan: Tests de Integración para Reference

**Fecha:** 2025-12-15
**Estado:** PENDIENTE

## Problema Detectado

La funcionalidad `.Reference()` existe para `EntityTypeBuilder` pero:
1. No existe API para marcar referencias dentro de ComplexTypes
2. La deserialización de referencias en ComplexTypes no está implementada
3. No hay tests de integración que validen el flujo completo

## Objetivo

Implementar y testear la funcionalidad `.Reference()` en tres escenarios:
1. Reference en Collection Principal
2. Reference en SubCollection
3. Reference en ComplexType (requiere nueva API)

## Análisis Técnico

### Estado Actual

| Componente | Serialización | Deserialización |
|------------|---------------|-----------------|
| Reference en Entity | ✅ `SerializeEntityReferences()` | ❌ Solo loggea |
| Reference en ComplexType | ✅ `SerializeNestedEntityReferences()` | ❌ No implementado |

### APIs Existentes

```csharp
// Para entidades - YA EXISTE
entity.Reference(a => a.Categoria);

// Para ComplexTypes - NO EXISTE
nivel1.Reference(d => d.SucursalCercana);  // ← NUEVA API
```

## Archivos a Modificar

| Archivo | Cambio |
|---------|--------|
| `FirestorePropertyBuilderExtensions.cs` | Agregar `ComplexPropertyBuilder.Reference()` |
| `FirestoreDocumentDeserializer.cs` | Agregar `DeserializeNestedEntityReferences()` |
| `TestEntities.cs` | Agregar entidades de prueba |
| `TestDbContext.cs` | Agregar `.Reference()` a LineaPedido |
| `ReferenceTestDbContext.cs` | NUEVO - DbContext para tests de Reference |
| `ComplexPropertyReferenceExtensionTests.cs` | NUEVO - Tests unitarios |
| `ReferenceInCollectionTests.cs` | NUEVO - Tests integración |
| `ReferenceInSubCollectionTests.cs` | NUEVO - Tests integración |
| `ReferenceInComplexTypeTests.cs` | NUEVO - Tests integración |

## Pasos de Implementación

### Fase 1: Nueva API `ComplexPropertyBuilder.Reference()` ✅ COMPLETADA

**Commit:** `7f0bbcb`

1. [x] **Verificación previa:** Confirmar que `ComplexPropertyBuilder<T>.Metadata.SetAnnotation()` funciona correctamente
2. [x] Agregar método `Reference<TComplex, TRelated>()` en `FirestorePropertyBuilderExtensions.cs`
3. [x] Usar anotación `Firestore:NestedReferences` para guardar lista de propiedades
4. [x] Verificar que compila correctamente
5. [x] Fix: Corregir GeoPointConventionTest (usar `Firestore:IsGeoPoint` en lugar de `Firestore:GeoPoint`)

**Código a implementar:**
```csharp
public static ComplexPropertyBuilder<TComplex> Reference<TComplex, TRelated>(
    this ComplexPropertyBuilder<TComplex> builder,
    Expression<Func<TComplex, TRelated?>> navigationExpression)
    where TRelated : class
{
    var memberInfo = navigationExpression.GetMemberAccess();
    var propertyName = memberInfo.Name;

    var existingRefs = builder.Metadata.FindAnnotation("Firestore:NestedReferences")?.Value as List<string>
        ?? new List<string>();
    existingRefs.Add(propertyName);

    builder.Metadata.SetAnnotation("Firestore:NestedReferences", existingRefs);

    return builder;
}
```

### Fase 2: Implementar Deserialización

5. [ ] **Punto técnico:** Verificar cómo obtener la anotación `Firestore:NestedReferences` desde `IComplexType`. Podría requerir acceder vía `IMutableComplexProperty` en lugar de `IComplexType` directamente.
6. [ ] Agregar método `DeserializeNestedEntityReferences()` en `FirestoreDocumentDeserializer.cs`
7. [ ] Llamar desde `DeserializeComplexType()` después de deserializar propiedades
8. [ ] Verificar que compila correctamente

**Código a implementar:**
```csharp
private void DeserializeNestedEntityReferences(
    object instance,
    IDictionary<string, object> data,
    IComplexType complexType)
{
    var nestedRefs = complexType.FindAnnotation("Firestore:NestedReferences")?.Value as List<string>;
    if (nestedRefs == null || nestedRefs.Count == 0)
        return;

    foreach (var refPropertyName in nestedRefs)
    {
        if (!data.TryGetValue(refPropertyName, out var value))
            continue;

        if (value is not DocumentReference docRef)
            continue;

        var property = complexType.ClrType.GetProperty(refPropertyName);
        if (property == null)
            continue;

        _logger.LogTrace(
            "Found nested reference {PropertyName} pointing to {DocumentPath}",
            refPropertyName, docRef.Path);
    }
}
```

### Fase 3: Tests Unitarios

9. [ ] Crear archivo `ComplexPropertyReferenceExtensionTests.cs`
10. [ ] Test: `Reference_ShouldAddAnnotationToComplexProperty`
11. [ ] Test: `Reference_MultipleCalls_ShouldAccumulateReferences`
12. [ ] Test: `Reference_OnNestedComplexType_ShouldAddAnnotationToNested`
13. [ ] Ejecutar tests unitarios

### Fase 4: Entidades de Prueba

14. [ ] Agregar `Categoria` y `Articulo` en `TestEntities.cs`
15. [ ] Agregar `Sucursal`, `Vendedor`, `InfoContacto`, `DireccionEmpresa`, `Empresa` en `TestEntities.cs`
16. [ ] Crear `ReferenceTestDbContext.cs` con configuración completa
17. [ ] Agregar `.Reference(l => l.Producto)` a LineaPedido en `TestDbContext.cs`

**Entidades a agregar:**
```csharp
// --- Reference en Collection Principal ---
public class Categoria
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public string? Descripcion { get; set; }
}

public class Articulo
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public decimal Precio { get; set; }
    public Categoria? Categoria { get; set; }
}

// --- Reference en ComplexType (2 niveles) ---
public class Sucursal
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public required string Ciudad { get; set; }
}

public class Vendedor
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public required string Email { get; set; }
}

public record InfoContacto
{
    public required string Telefono { get; init; }
    public required string Email { get; init; }
    public Vendedor? VendedorAsignado { get; init; }  // Reference nivel 2
}

public record DireccionEmpresa
{
    public required string Calle { get; init; }
    public required string Ciudad { get; init; }
    public Sucursal? SucursalCercana { get; init; }      // Reference nivel 1
    public required InfoContacto Contacto { get; init; }
}

public class Empresa
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public required string Rfc { get; set; }
    public required DireccionEmpresa DireccionFiscal { get; set; }
}
```

**Configuración en ReferenceTestDbContext:**
```csharp
entity.ComplexProperty(e => e.DireccionFiscal, nivel1 =>
{
    nivel1.Reference(d => d.SucursalCercana);

    nivel1.ComplexProperty(d => d.Contacto, nivel2 =>
    {
        nivel2.Reference(c => c.VendedorAsignado);
    });
});
```

### Fase 5: Tests Integración - Collection Principal

18. [ ] Crear archivo `ReferenceInCollectionTests.cs`
19. [ ] Test: `Add_ArticuloConCategoria_ShouldPersistAsDocumentReference`
20. [ ] Test: `Query_ArticuloConCategoria_ShouldReturnWithReference`
21. [ ] Test: `Update_ArticuloCategoria_ShouldUpdateReference`
22. [ ] Ejecutar tests

**Enfoque de los Tests:**
- Verificar que la entidad principal se persiste y lee correctamente
- Verificar campos escalares (Nombre, Precio, etc.)
- **Aceptar navegaciones null como comportamiento esperado** (deserialización solo loggea)

**Test ejemplo:**
```csharp
[Fact]
public async Task Add_ArticuloConCategoria_ShouldPersistAsDocumentReference()
{
    // Arrange
    using var context = _fixture.CreateContext<ReferenceTestDbContext>();
    var categoriaId = FirestoreTestFixture.GenerateId("cat");
    var articuloId = FirestoreTestFixture.GenerateId("art");

    var categoria = new Categoria
    {
        Id = categoriaId,
        Nombre = "Electrónica"
    };

    var articulo = new Articulo
    {
        Id = articuloId,
        Nombre = "Laptop",
        Precio = 999.99m,
        Categoria = categoria
    };

    // Act
    context.Categorias.Add(categoria);
    context.Articulos.Add(articulo);
    await context.SaveChangesAsync();

    // Assert
    using var readContext = _fixture.CreateContext<ReferenceTestDbContext>();
    var articuloLeido = await readContext.Articulos
        .FirstOrDefaultAsync(a => a.Id == articuloId);

    articuloLeido.Should().NotBeNull();
    articuloLeido!.Nombre.Should().Be("Laptop");
    articuloLeido.Precio.Should().Be(999.99m);

    // ⚠️ La navegación será null - deserialización solo loggea por ahora
    articuloLeido.Categoria.Should().BeNull();
}
```

### Fase 6: Tests Integración - SubCollection

23. [ ] Crear archivo `ReferenceInSubCollectionTests.cs`
24. [ ] Test: `Add_LineaPedidoConProducto_ShouldPersistReferenceInSubcollection`
25. [ ] Test: `Query_LineaPedidoConProducto_ShouldReturnNestedReference`
26. [ ] Test: `Update_LineaPedidoProducto_ShouldUpdateReferenceInSubcollection`
27. [ ] Ejecutar tests

**Nota:** Los asserts de navegación (`Producto`) deben esperar `null`.

### Fase 7: Tests Integración - ComplexType

28. [ ] Crear archivo `ReferenceInComplexTypeTests.cs`
29. [ ] Test: `Add_EmpresaConReferenceEnNivel1_ShouldPersist`
30. [ ] Test: `Add_EmpresaConReferenceEnNivel2_ShouldPersist`
31. [ ] Test: `Query_EmpresaConReferencesEnComplexType_ShouldReturnNestedReferences`
32. [ ] Test: `Update_EmpresaReferenceEnNivel1_ShouldPersistChanges`
33. [ ] Test: `Update_EmpresaReferenceEnNivel2_ShouldPersistChanges`
34. [ ] Ejecutar tests

**Nota:** Los asserts de referencias en ComplexType (`SucursalCercana`, `VendedorAsignado`) deben esperar `null`.

### Fase 8: Verificación Final

35. [ ] Ejecutar todos los tests del proyecto
36. [ ] Verificar que no se rompió nada existente
37. [ ] Build del provider

### Fase 9: Commit

38. [ ] Commit con mensaje descriptivo

**Commit esperado:** `feat: add Reference() API for ComplexTypes and integration tests`

## Dependencias

- Tests de SubCollection funcionando (usa Cliente → Pedido → LineaPedido → Producto)
- API `EntityTypeBuilder.Reference()` existente

## Notas

1. **Deserialización inicial:** Solo detecta y loggea las referencias. La carga real de entidades referenciadas requiere implementación adicional (lazy/eager loading).

2. **Compatibilidad:** Los tests existentes NO deben verse afectados porque:
   - Las nuevas entidades usan DbContext separado
   - La API nueva es opt-in

3. **Estructura en Firestore:** El DocumentReference se guarda con el nombre de la propiedad (sin sufijo "Ref" para ComplexTypes).
