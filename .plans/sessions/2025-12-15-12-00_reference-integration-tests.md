# Plan: Reference - TDD Real

**Fecha:** 2025-12-15
**Estado:** EN PROGRESO (Ciclos 1-6 + 7.1 completados, 7.2 limitación documentada)
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

### Ciclo 1: Serialización Reference en Collection Principal ✅ `0b38dbb`

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

### Ciclo 2: Serialización Reference en SubCollection ✅ `0b38dbb`

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

### Ciclo 3: Serialización Reference en ComplexType Nivel 1 ✅ `0b38dbb`

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

### Ciclo 4: Deserialización sin Include retorna null ✅ `0b38dbb`

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

### Ciclo 5: Include carga Reference en Entity ✅ `0b38dbb`

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

### Ciclo 6: Include carga Reference en ComplexType ✅ `dd73bbf`

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

### Ciclo 7: Lazy Loading para References y SubCollections

**Objetivo:** Implementar carga perezosa que se active al acceder a la propiedad sin necesidad de `.Include()`.

#### 7.1: Lazy Loading para References ✅ `af86f67`

**Implementación:**
- `FirestoreQueryingEnumerable<T>` ahora implementa `IEnumerable<T>` (sync lazy loading)
- `TryCreateLazyLoadingProxy<T>` crea proxies via reflection (sin dependencia dura)
- `SetShadowForeignKeys` pobla shadow FK desde DocumentReference
- `DeserializeIntoEntity` permite poblar proxies existentes

**Documentación arquitectónica:** Ver [.plans/review/LazyLoading.md](../review/LazyLoading.md)

**Commit 7.1.1 (RED):**
```
test(lazy): verificar lazy loading carga reference al acceder
```

```csharp
[Fact]
public async Task LazyLoading_Reference_ShouldLoadWhenAccessed()
{
    // Arrange - Articulo con Categoria ya guardado

    // Act - Query SIN Include, pero acceder a la propiedad
    var articulo = await context.Articulos
        .FirstOrDefaultAsync(a => a.Id == articuloId);

    // Acceder a la propiedad dispara lazy loading
    var categoria = articulo!.Categoria;

    // Assert - Categoria se cargó automáticamente
    categoria.Should().NotBeNull();
    categoria!.Id.Should().Be(categoriaId);
}
```

**Commit 7.1.2 (GREEN):**
```
feat(lazy): implementar lazy loading para references
```

#### 7.2: Lazy Loading para SubCollections ⚠️ LIMITACIÓN DOCUMENTADA

**Estado:** No soportado - Limitación arquitectónica

**Razón técnica:**
- SubCollections en Firestore son **path-based**: `/Clientes/{id}/Pedidos/`
- EF Core Lazy/Explicit Loading crea queries **FK-based**: `WHERE ClienteId = @id`
- No existe collection global "Pedidos" en Firestore para consultar

**Solución para usuarios:** Usar `Include()` para cargar SubCollections.

**Tests:** Marcados como `Skip` con documentación de la limitación.

**Posible implementación futura:**
1. Interceptar queries para entity types que son SubCollections
2. Extraer parent ID del filtro FK
3. Traducir a path de subcollection: `/Parents/{parentId}/Children`
4. Requiere cambios significativos en el query translator

#### 7.3: Lazy Loading para References en ComplexTypes ⚠️ LIMITACIÓN DOCUMENTADA

**Estado:** No soportado - Limitación arquitectónica

**Razón técnica:**
- Castle crea proxy solo para la **entidad raíz** (EmpresaLazy) ✅
- Los ComplexTypes (DireccionLazy) son records/clases simples **sin proxy**
- Acceder a `DireccionPrincipal.SucursalCercana` no pasa por el lazy loader
- La interceptación de Castle ocurre solo a nivel de propiedades de entidad

**Solución para usuarios:** Usar `Include()` para cargar References en ComplexTypes.

```csharp
// ✅ Funciona - Include explícito
var empresa = await context.Empresas
    .Include(e => e.DireccionPrincipal.SucursalCercana)
    .FirstOrDefaultAsync(e => e.Id == empresaId);

// ❌ No funciona - Lazy loading en ComplexType
var empresa = await context.Empresas
    .FirstOrDefaultAsync(e => e.Id == empresaId);
var sucursal = empresa.DireccionPrincipal.SucursalCercana; // null
```

**Tests:** Marcados como `Skip` con documentación de la limitación.

**Posible implementación futura:**
1. Crear proxies para ComplexTypes que contienen References
2. Requiere modificar cómo se crean los ComplexTypes en el shaper
3. Complejidad alta por la naturaleza embebida de ComplexTypes

---

## Reglas

1. **No escribir código sin test rojo primero**
2. **No planificar archivos antes de tener tests**
3. **Cada ciclo termina con commit(s)**
4. **Si el test pasa en verde de inmediato** → el comportamiento ya existe, siguiente ciclo

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

---

## ✅ Resuelto: AsyncLocal eliminado en Ciclo 6

**Problema inicial:** Se usó `AsyncLocal<List<LambdaExpression>>` como workaround para pasar datos entre etapas del pipeline.

**Solución aplicada:** Cast directo a `FirestoreQueryCompilationContext` - mismo patrón que usan los providers oficiales (Cosmos DB, SQL Server).

**Documentación completa:** Ver [.plans/review/AsynLocal.md](../review/AsynLocal.md)
