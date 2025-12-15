# Plan: Reference - TDD Real

**Fecha:** 2025-12-15
**Estado:** EN PROGRESO (Ciclos 1-5 completados)
**Enfoque:** TDD (Test First, commits por ciclo)

---

## Paso 0: Diagnóstico

Antes de escribir código, verificar qué funciona HOY:

```bash
# Tests existentes de Reference
dotnet test --filter "Reference" --list-tests

# Buscar archivos de test relacionados
find . -name "*Reference*Tests*.cs"
```

**Preguntas a responder:**

| Pregunta | Respuesta |
|----------|-----------|
| ¿Existe test que verifique serialización de Reference en Entity? | ? |
| ¿Existe test que verifique serialización de Reference en SubCollection? | ? |
| ¿Existe test que verifique serialización de Reference en ComplexType? | ? |
| ¿La API `entity.Reference()` tiene tests? | ? |
| ¿La API `complexProperty.Reference()` tiene tests? | ? |
| ¿Qué hace el deserializador cuando encuentra un DocumentReference? | ? |

**Sin estas respuestas, no se puede planificar.**

---

## Ciclos TDD

Cada ciclo = 1 comportamiento observable = commit(s)

---

### Ciclo 1: Serialización Reference en Collection Principal ✅

**Commit 1.1 (RED):**
```
test(ref): verificar serialización reference en collection principal
```

```csharp
[Fact]
public async Task Serialization_Reference_InCollection_ShouldStoreAsDocumentReference()
{
    // Arrange
    var categoriaId = GenerateId("cat");
    var articuloId = GenerateId("art");

    var categoria = new Categoria { Id = categoriaId, Nombre = "Electrónica" };
    var articulo = new Articulo
    {
        Id = articuloId,
        Nombre = "Laptop",
        Categoria = categoria
    };

    context.Categorias.Add(categoria);
    context.Articulos.Add(articulo);
    await context.SaveChangesAsync();

    // Act - Leer documento raw de Firestore
    var doc = await GetRawDocument("articulos", articuloId);

    // Assert - Verificar que es DocumentReference, no objeto embebido
    doc["Categoria"].Should().BeOfType<DocumentReference>();
    ((DocumentReference)doc["Categoria"]).Path.Should().Contain(categoriaId);
}
```

**Commit 1.2 (GREEN)** - Solo si 1.1 falló:
```
feat(ref): implementar serialización reference en collection principal
```

---

### Ciclo 2: Serialización Reference en SubCollection ✅

**Commit 2.1 (RED):**
```
test(ref): verificar serialización reference en subcollection
```

```csharp
[Fact]
public async Task Serialization_Reference_InSubCollection_ShouldStoreAsDocumentReference()
{
    // Arrange - LineaPedido en subcollection de Pedido, con referencia a Producto

    // Act - Leer documento raw

    // Assert - El campo Producto es DocumentReference
}
```

**Commit 2.2 (GREEN)** - Solo si 2.1 falló

---

### Ciclo 3: Serialización Reference en ComplexType Nivel 1 ✅

**Commit 3.1 (RED):**
```
test(ref): verificar serialización reference en complextype nivel 1
```

```csharp
[Fact]
public async Task Serialization_Reference_InComplexType_Level1_ShouldStoreAsDocumentReference()
{
    // Arrange - Empresa con DireccionFiscal.SucursalCercana

    // Act - Leer documento raw

    // Assert - DireccionFiscal.SucursalCercana es DocumentReference
}
```

**Commit 3.2 (GREEN)** - Solo si 3.1 falló

---

### Ciclo 4: Deserialización sin Include retorna null ✅

**Commit 4.1 (RED):**
```
test(ref): verificar deserialización sin include retorna null
```

```csharp
[Fact]
public async Task Deserialization_WithoutInclude_ShouldReturnNull()
{
    // Arrange - Articulo con Categoria ya guardado

    // Act - Query SIN Include
    var articulo = await context.Articulos
        .FirstOrDefaultAsync(a => a.Id == articuloId);

    // Assert - Categoria es null (consistente: no pediste cargarla)
    articulo.Should().NotBeNull();
    articulo!.Categoria.Should().BeNull();
}
```

**Commit 4.2 (GREEN)** - Solo si 4.1 falló

---

### Ciclo 5: Include carga Reference en Entity ✅

**Commit 5.1 (RED):**
```
test(ref): verificar include carga reference en entity
```

```csharp
[Fact]
public async Task Include_Reference_InEntity_ShouldLoadRelatedEntity()
{
    // Arrange - Articulo con Categoria ya guardado

    // Act - Query CON Include
    var articulo = await context.Articulos
        .Include(a => a.Categoria)
        .FirstOrDefaultAsync(a => a.Id == articuloId);

    // Assert - Categoria está completa
    articulo.Should().NotBeNull();
    articulo!.Categoria.Should().NotBeNull();
    articulo.Categoria!.Id.Should().Be(categoriaId);
    articulo.Categoria.Nombre.Should().Be("Electrónica");
}
```

**Commit 5.2 (GREEN):**
```
feat(ref): implementar include para reference en entity
```

---

### Ciclo 6: Include carga Reference en ComplexType

**Commit 6.1 (RED):**
```
test(ref): verificar include carga reference en complextype
```

```csharp
[Fact]
public async Task Include_Reference_InComplexType_ShouldLoadRelatedEntity()
{
    // Arrange - Empresa con DireccionFiscal.SucursalCercana ya guardado

    // Act
    var empresa = await context.Empresas
        .Include(e => e.DireccionFiscal.SucursalCercana)
        .FirstOrDefaultAsync(e => e.Id == empresaId);

    // Assert
    empresa.Should().NotBeNull();
    empresa!.DireccionFiscal.SucursalCercana.Should().NotBeNull();
    empresa.DireccionFiscal.SucursalCercana!.Id.Should().Be(sucursalId);
}
```

**Commit 6.2 (GREEN):**
```
feat(ref): implementar include para reference en complextype
```

---

### Ciclo 7: Lazy Loading (OPCIONAL)

**Evaluar primero:** ¿Se necesita lazy loading o Include es suficiente?

Si se necesita:

**Commit 7.1 (RED):**
```
test(ref): verificar lazy loading carga reference al acceder
```

**Commit 7.2 (GREEN):**
```
feat(ref): implementar lazy loading para references
```

---

## Reglas

1. **No escribir código sin test rojo primero**
2. **No planificar archivos antes de tener tests**
3. **Cada ciclo termina con commit(s)**
4. **Si el test pasa en verde de inmediato** → el comportamiento ya existe, siguiente ciclo
5. **Lazy Loading es OPCIONAL** → evaluar si realmente se necesita

---

## Helper necesario

```csharp
// Para verificar datos raw en Firestore
private async Task<Dictionary<string, object>> GetRawDocument(string collection, string id)
{
    var docRef = _firestoreDb.Collection(collection).Document(id);
    var snapshot = await docRef.GetSnapshotAsync();
    return snapshot.ToDictionary();
}
```

---

## Notas

- Los tests verifican **comportamiento observable** (datos en Firestore, entidades cargadas)
- NO verifican detalles internos (anotaciones, métodos privados)
- Los archivos se crean cuando el test los exige, no antes
