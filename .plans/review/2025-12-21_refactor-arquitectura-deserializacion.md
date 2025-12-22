# Plan TDD: Refactor Arquitectura - Deserialización y Navegaciones

**Fecha:** 2025-12-21
**Estado:** Pendiente revisión

---

## Resumen Ejecutivo

Refactorización para:
1. Soportar entidades con constructores con parámetros
2. Soportar `HashSet<T>`, `ICollection<T>` en navegaciones
3. Eliminar 11 bypasses del wrapper (deuda técnica)
4. Separar responsabilidades según SRP

---

## Progreso

| Ciclo | Comportamiento | Estado | Commit | Tests |
|-------|----------------|--------|--------|-------|
| 1 | `IFirestoreDocumentDeserializer` - Crear interfaz | ✅ | 905a50e | 3 |
| 2 | Constructor sin parámetros (ya funciona) | ✅ | 19bb387 | 2 |
| 3 | Constructor con parámetros | ✅ | fc3e68d | 4+11 |
| 3.1 | Mover creación de entidades del Visitor al Deserializer | ✅ | 80fb01f | 547+189 |
| 4 | `List<T>` en navegaciones (ya funciona) | ✅ | 2dc29f7 | 1 |
| 5 | `ICollection<T>` en navegaciones | ✅ | 2dc29f7 | 2 |
| 6 | `HashSet<T>` en navegaciones | ✅ | 2dc29f7 | 3 |
| 7 | `IFirestoreClientWrapper` - Agregar métodos faltantes | ✅ | 0762f46 | 11 |
| 8 | `INavigationLoader` - Crear interfaz e implementación | ✅ | 9d39e29 | 7 |
| 9 | Eliminar bypass subcollection del Visitor | ✅ | c02bf88 | 1 |
| 10 | Extraer `LoadReferenceAsync` del Visitor | ⏳ | | |
| 11 | Eliminar bypasses en agregaciones | ⏳ | | |
| 12 | `IFirestoreQueryExecutor` - Crear interfaz | ⏳ | | |

---

## Fase 1: Interfaz del Deserializador

### Ciclo 1: Crear `IFirestoreDocumentDeserializer`

**Objetivo:** Extraer interfaz del deserializador existente.

**Test RED:**
```csharp
[Fact]
public void FirestoreDocumentDeserializer_ShouldImplementInterface()
{
    Assert.True(typeof(IFirestoreDocumentDeserializer)
        .IsAssignableFrom(typeof(FirestoreDocumentDeserializer)));
}
```

**Interfaz propuesta:**
```csharp
public interface IFirestoreDocumentDeserializer
{
    T DeserializeEntity<T>(DocumentSnapshot document) where T : class;
    T DeserializeIntoEntity<T>(DocumentSnapshot document, T entity) where T : class;
    List<T> DeserializeEntities<T>(IEnumerable<DocumentSnapshot> documents) where T : class;
}
```

**Nota:** Se elimina la constraint `new()` para permitir constructores con parámetros.

---

### Ciclo 2: Constructor sin parámetros (baseline)

**Objetivo:** Verificar que el comportamiento actual sigue funcionando.

**Test GREEN (ya existe):**
```csharp
[Fact]
public void DeserializeEntity_WithParameterlessConstructor_ShouldWork()
{
    // Arrange
    var document = CreateMockDocument(new Dictionary<string, object>
    {
        { "Name", "Test" }
    });

    // Act
    var entity = _deserializer.DeserializeEntity<SimpleEntity>(document);

    // Assert
    Assert.Equal("Test", entity.Name);
}

public class SimpleEntity
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
}
```

---

### Ciclo 3: Constructor con parámetros

**Objetivo:** Soportar entidades con constructores que requieren parámetros.

**Test RED:**
```csharp
[Fact]
public void DeserializeEntity_WithParameterizedConstructor_ShouldWork()
{
    // Arrange
    var document = CreateMockDocument(new Dictionary<string, object>
    {
        { "Name", "Test Order" },
        { "Total", 99.99 }
    });

    // Act
    var entity = _deserializer.DeserializeEntity<OrderWithConstructor>(document);

    // Assert
    Assert.Equal("order-123", entity.Id);
    Assert.Equal("Test Order", entity.Name);
    Assert.Equal(99.99m, entity.Total);
}

public class OrderWithConstructor
{
    public OrderWithConstructor(string id, string name, decimal total)
    {
        Id = id;
        Name = name;
        Total = total;
    }

    public string Id { get; }
    public string Name { get; }
    public decimal Total { get; }
}
```

**Implementación esperada:**
1. Detectar si el tipo tiene constructor sin parámetros
2. Si no, buscar constructor con parámetros que coincidan con propiedades
3. Usar `Activator.CreateInstance(type, args)` o reflexión del constructor

---

### Ciclo 3.1: Mover creación de entidades del Visitor al Deserializer

**Objetivo:** El Visitor NO debe crear entidades. Solo debe llamar a `IFirestoreDocumentDeserializer`.

**Problema actual (líneas 1210-1226 del Visitor):**
```csharp
// El Visitor decide cómo crear entidades - VIOLA SRP
T? entity = null;
if (typeof(T).GetConstructor(Type.EmptyTypes) != null)
{
    entity = TryCreateLazyLoadingProxy<T>(dbContext, serviceProvider);
    if (entity != null)
    {
        deserializer.DeserializeIntoEntity(documentSnapshot, entity);
    }
}

if (entity == null)
{
    entity = deserializer.DeserializeEntity<T>(documentSnapshot);
}
```

**Solución esperada:**
```csharp
// El Visitor SOLO llama al deserializer
entity = deserializer.DeserializeEntity<T>(documentSnapshot, dbContext);
```

**Cambios requeridos:**

1. **Extender `IFirestoreDocumentDeserializer`** - Añadir sobrecarga que reciba `DbContext`:
   ```csharp
   T DeserializeEntity<T>(DocumentSnapshot document, DbContext? dbContext = null) where T : class;
   ```

2. **Mover `TryCreateLazyLoadingProxy` al Deserializer** - El deserializer internamente:
   - Si recibe `DbContext` y hay lazy loading habilitado, crea proxy
   - Si no, crea entidad normal con constructor apropiado

3. **Simplificar el Visitor** - Solo una línea:
   ```csharp
   entity = deserializer.DeserializeEntity<T>(documentSnapshot, dbContext);
   ```

4. **Eliminar código duplicado del Visitor**:
   - Eliminar `TryCreateLazyLoadingProxy<T>` del Visitor (mover al Deserializer)
   - Eliminar lógica de decisión de creación

**Beneficios:**
- SRP: El Visitor solo orquesta, el Deserializer crea entidades
- Un solo punto de creación de entidades
- Testabilidad: El deserializer es más fácil de testear aislado

---

## Fase 2: Tipos de Colección en Navegaciones

### Ciclo 4: `List<T>` (baseline)

**Objetivo:** Verificar que `List<T>` sigue funcionando.

**Test GREEN (ya funciona con `.Include()`):**
```csharp
[Fact]
public async Task Include_SubCollection_WithListProperty_ShouldWork()
{
    // Arrange - entidad con List<T>
    public class Cliente
    {
        public string Id { get; set; } = default!;
        public List<Pedido> Pedidos { get; set; } = new();
    }

    // Act
    var cliente = await _context.Clientes
        .Include(c => c.Pedidos)
        .FirstOrDefaultAsync(c => c.Id == "cliente-1");

    // Assert
    Assert.NotNull(cliente);
    Assert.IsType<List<Pedido>>(cliente.Pedidos);
}
```

---

### Ciclo 5: `ICollection<T>`

**Objetivo:** Soportar propiedades declaradas como `ICollection<T>`.

**Test RED:**
```csharp
[Fact]
public async Task Include_SubCollection_WithICollectionProperty_ShouldWork()
{
    // Arrange - entidad con ICollection<T>
    public class ClienteConICollection
    {
        public string Id { get; set; } = default!;
        public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
    }

    // Act
    var cliente = await _context.ClientesConICollection
        .Include(c => c.Pedidos)
        .FirstOrDefaultAsync(c => c.Id == "cliente-1");

    // Assert
    Assert.NotNull(cliente);
    Assert.NotNull(cliente.Pedidos);
    Assert.True(cliente.Pedidos.Count > 0);
}
```

**Implementación esperada:**
- Detectar tipo declarado de la propiedad
- Si es interfaz (`ICollection<T>`), usar `List<T>` como implementación por defecto

---

### Ciclo 6: `HashSet<T>`

**Objetivo:** Soportar propiedades declaradas como `HashSet<T>`.

**Test RED:**
```csharp
[Fact]
public async Task Include_SubCollection_WithHashSetProperty_ShouldWork()
{
    // Arrange - entidad con HashSet<T>
    public class ClienteConHashSet
    {
        public string Id { get; set; } = default!;
        public HashSet<Pedido> Pedidos { get; set; } = new();
    }

    // Act
    var cliente = await _context.ClientesConHashSet
        .Include(c => c.Pedidos)
        .FirstOrDefaultAsync(c => c.Id == "cliente-1");

    // Assert
    Assert.NotNull(cliente);
    Assert.IsType<HashSet<Pedido>>(cliente.Pedidos);
    Assert.True(cliente.Pedidos.Count > 0);
}
```

**Implementación esperada:**
- Detectar si el tipo es `HashSet<T>`
- Crear `HashSet<T>` en lugar de `List<T>`
- Añadir elementos con `Add()` en lugar de `list.Add()`

---

## Fase 3: Extender `IFirestoreClientWrapper`

### Ciclo 7: Agregar métodos faltantes al wrapper

**Objetivo:** Centralizar todas las operaciones de Firestore.

**Test RED:**
```csharp
[Fact]
public void IFirestoreClientWrapper_ShouldHaveAggregateMethod()
{
    var method = typeof(IFirestoreClientWrapper).GetMethod("ExecuteAggregateQueryAsync");
    Assert.NotNull(method);
}

[Fact]
public void IFirestoreClientWrapper_ShouldHaveSubCollectionMethod()
{
    var method = typeof(IFirestoreClientWrapper).GetMethod("GetSubCollectionAsync");
    Assert.NotNull(method);
}

[Fact]
public void IFirestoreClientWrapper_ShouldHaveDocumentByReferenceMethod()
{
    var method = typeof(IFirestoreClientWrapper).GetMethod("GetDocumentByReferenceAsync");
    Assert.NotNull(method);
}
```

**Interfaz extendida:**
```csharp
public interface IFirestoreClientWrapper
{
    // Métodos existentes...

    // Nuevos métodos
    Task<AggregateQuerySnapshot> ExecuteAggregateQueryAsync(
        AggregateQuery query,
        CancellationToken cancellationToken = default);

    Task<QuerySnapshot> GetSubCollectionAsync(
        DocumentReference parentDoc,
        string subCollectionName,
        CancellationToken cancellationToken = default);

    Task<DocumentSnapshot> GetDocumentByReferenceAsync(
        DocumentReference docRef,
        CancellationToken cancellationToken = default);
}
```

---

## Fase 4: Crear `INavigationLoader`

### Ciclo 8: Crear interfaz e implementación

**Objetivo:** Extraer lógica de carga de navegaciones a servicio dedicado.

**Test RED:**
```csharp
[Fact]
public void NavigationLoader_ShouldImplementInterface()
{
    Assert.True(typeof(INavigationLoader)
        .IsAssignableFrom(typeof(NavigationLoader)));
}

[Fact]
public void NavigationLoader_ShouldUseClientWrapper()
{
    // Verificar que usa IFirestoreClientWrapper, no llamadas directas
    var constructor = typeof(NavigationLoader).GetConstructors().First();
    var parameters = constructor.GetParameters();

    Assert.Contains(parameters, p => p.ParameterType == typeof(IFirestoreClientWrapper));
}
```

**Interfaz propuesta:**
```csharp
public interface INavigationLoader
{
    Task LoadSubCollectionAsync<TParent, TChild>(
        TParent parentEntity,
        DocumentSnapshot parentDoc,
        IReadOnlyNavigation navigation,
        IEnumerable<IncludeInfo>? filters = null,
        CancellationToken cancellationToken = default)
        where TParent : class
        where TChild : class;

    Task LoadReferenceAsync<TParent, TChild>(
        TParent parentEntity,
        DocumentSnapshot parentDoc,
        IReadOnlyNavigation navigation,
        CancellationToken cancellationToken = default)
        where TParent : class
        where TChild : class;
}
```

---

### Ciclo 9: Extraer `LoadSubCollectionAsync` del Visitor

**Objetivo:** Mover lógica del Visitor al NavigationLoader.

**Test RED:**
```csharp
[Fact]
public async Task NavigationLoader_LoadSubCollection_ShouldUseWrapper()
{
    // Arrange
    var mockWrapper = new Mock<IFirestoreClientWrapper>();
    var loader = new NavigationLoader(mockWrapper.Object, ...);

    // Act
    await loader.LoadSubCollectionAsync<Cliente, Pedido>(...);

    // Assert - Verifica que usa el wrapper, no llamadas directas
    mockWrapper.Verify(w => w.GetSubCollectionAsync(
        It.IsAny<DocumentReference>(),
        It.IsAny<string>(),
        It.IsAny<CancellationToken>()),
        Times.Once);
}
```

**Cambios:**
1. Crear `NavigationLoader.LoadSubCollectionAsync()`
2. Usar `IFirestoreClientWrapper.GetSubCollectionAsync()`
3. Soportar diferentes tipos de colección (`List<T>`, `HashSet<T>`, `ICollection<T>`)
4. Eliminar bypass en línea 1439 del Visitor

---

### Ciclo 10: Extraer `LoadReferenceAsync` del Visitor

**Objetivo:** Mover carga de referencias al NavigationLoader.

**Test RED:**
```csharp
[Fact]
public async Task NavigationLoader_LoadReference_ShouldUseWrapper()
{
    // Arrange
    var mockWrapper = new Mock<IFirestoreClientWrapper>();
    var loader = new NavigationLoader(mockWrapper.Object, ...);

    // Act
    await loader.LoadReferenceAsync<Pedido, Cliente>(...);

    // Assert
    mockWrapper.Verify(w => w.GetDocumentByReferenceAsync(
        It.IsAny<DocumentReference>(),
        It.IsAny<CancellationToken>()),
        Times.Once);
}
```

**Cambios:**
1. Crear `NavigationLoader.LoadReferenceAsync()`
2. Usar `IFirestoreClientWrapper.GetDocumentByReferenceAsync()`
3. Eliminar bypasses en líneas 1568, 1591, 1743, 1754 del Visitor

---

### Ciclo 11: Eliminar bypasses en agregaciones

**Objetivo:** Que `FirestoreQueryExecutor` use el wrapper para agregaciones.

**Ubicación de bypasses (`FirestoreQueryExecutor.cs`):**

| Línea | Método | Bypass | Wrapper Method |
|-------|--------|--------|----------------|
| 765 | `ExecuteCountAsync` | `aggregateQuery.GetSnapshotAsync()` | `ExecuteAggregateQueryAsync` |
| 778 | `ExecuteAnyAsync` | `limitedQuery.GetSnapshotAsync()` | `ExecuteQueryAsync` |
| 797 | `ExecuteSumAsync` | `aggregateQuery.GetSnapshotAsync()` | `ExecuteAggregateQueryAsync` |
| 820 | `ExecuteAverageAsync` | `aggregateQuery.GetSnapshotAsync()` | `ExecuteAggregateQueryAsync` |
| 848 | `ExecuteMinAsync` | `minQuery.GetSnapshotAsync()` | `ExecuteQueryAsync` (OrderBy+Limit) |
| 876 | `ExecuteMaxAsync` | `maxQuery.GetSnapshotAsync()` | `ExecuteQueryAsync` (OrderByDesc+Limit) |

**Nota:** Min y Max no usan agregaciones nativas de Firestore. Usan `OrderBy + Limit(1)`, por lo que deben usar `ExecuteQueryAsync` en lugar de `ExecuteAggregateQueryAsync`.

**Test RED:**
```csharp
[Fact]
public async Task ExecuteCountAsync_ShouldUseWrapper()
{
    // Arrange
    var mockWrapper = new Mock<IFirestoreClientWrapper>();
    var executor = new FirestoreQueryExecutor(mockWrapper.Object, ...);

    // Act
    await executor.ExecuteCountAsync(...);

    // Assert
    mockWrapper.Verify(w => w.ExecuteAggregateQueryAsync(
        It.IsAny<AggregateQuery>(),
        It.IsAny<CancellationToken>()),
        Times.Once);
}

[Fact]
public async Task ExecuteMinAsync_ShouldUseWrapper()
{
    // Arrange
    var mockWrapper = new Mock<IFirestoreClientWrapper>();
    var executor = new FirestoreQueryExecutor(mockWrapper.Object, ...);

    // Act
    await executor.ExecuteMinAsync<decimal>(...);

    // Assert - Min usa ExecuteQueryAsync porque Firestore no tiene Min nativo
    mockWrapper.Verify(w => w.ExecuteQueryAsync(
        It.IsAny<Google.Cloud.Firestore.Query>(),
        It.IsAny<CancellationToken>()),
        Times.Once);
}
```

**Cambios:**
1. Reemplazar 4 bypasses de agregaciones (Count, Sum, Average, Any) con `ExecuteAggregateQueryAsync` o `ExecuteQueryAsync`
2. Reemplazar 2 bypasses de Min/Max con `ExecuteQueryAsync` (usan OrderBy+Limit, no agregaciones)
3. El `FirestoreQueryExecutor` debe recibir `IFirestoreClientWrapper` por constructor

---

### Ciclo 12: Crear `IFirestoreQueryExecutor`

**Objetivo:** Extraer interfaz del executor para poder mockear y testear.

**Test RED:**
```csharp
[Fact]
public void FirestoreQueryExecutor_ShouldImplementInterface()
{
    Assert.True(typeof(IFirestoreQueryExecutor)
        .IsAssignableFrom(typeof(FirestoreQueryExecutor)));
}

[Fact]
public void IFirestoreQueryExecutor_ShouldHaveRequiredMethods()
{
    var type = typeof(IFirestoreQueryExecutor);

    Assert.NotNull(type.GetMethod("ExecuteQueryAsync"));
    Assert.NotNull(type.GetMethod("ExecuteIdQueryAsync"));
    Assert.NotNull(type.GetMethod("ExecuteCountAsync"));
    Assert.NotNull(type.GetMethod("ExecuteAnyAsync"));
    Assert.NotNull(type.GetMethod("ExecuteSumAsync"));
    Assert.NotNull(type.GetMethod("ExecuteAverageAsync"));
    Assert.NotNull(type.GetMethod("ExecuteMinAsync"));
    Assert.NotNull(type.GetMethod("ExecuteMaxAsync"));
}
```

**Interfaz propuesta:**
```csharp
public interface IFirestoreQueryExecutor
{
    Task<QuerySnapshot> ExecuteQueryAsync(
        FirestoreQueryExpression queryExpression,
        QueryContext queryContext,
        CancellationToken cancellationToken = default);

    Task<DocumentSnapshot?> ExecuteIdQueryAsync(
        FirestoreQueryExpression queryExpression,
        QueryContext queryContext,
        CancellationToken cancellationToken = default);

    Task<long> ExecuteCountAsync(
        FirestoreQueryExpression queryExpression,
        QueryContext queryContext,
        CancellationToken cancellationToken = default);

    Task<bool> ExecuteAnyAsync(
        FirestoreQueryExpression queryExpression,
        QueryContext queryContext,
        CancellationToken cancellationToken = default);

    Task<TResult?> ExecuteSumAsync<TResult>(
        FirestoreQueryExpression queryExpression,
        QueryContext queryContext,
        string propertyName,
        CancellationToken cancellationToken = default);

    Task<TResult?> ExecuteAverageAsync<TResult>(
        FirestoreQueryExpression queryExpression,
        QueryContext queryContext,
        string propertyName,
        CancellationToken cancellationToken = default);

    Task<TResult?> ExecuteMinAsync<TResult>(
        FirestoreQueryExpression queryExpression,
        QueryContext queryContext,
        string propertyName,
        CancellationToken cancellationToken = default);

    Task<TResult?> ExecuteMaxAsync<TResult>(
        FirestoreQueryExpression queryExpression,
        QueryContext queryContext,
        string propertyName,
        CancellationToken cancellationToken = default);

    int EvaluateIntExpression(Expression expression, QueryContext queryContext);
}
```

**Beneficios de la interfaz:**
1. **Testabilidad:** Permite mockear el executor en tests unitarios
2. **Flexibilidad:** Permite inyectar implementaciones alternativas
3. **Consistencia:** Sigue el patrón de las otras interfaces del proyecto
4. **DI:** Permite registrar en el contenedor de DI

**Cambios:**
1. Crear `IFirestoreQueryExecutor` en `Infrastructure/Contracts/`
2. `FirestoreQueryExecutor` implementa la interfaz
3. Opcionalmente registrar en DI (actualmente se crea en runtime)

---

## Resumen de Bypasses a Eliminar

| # | Archivo | Línea | Bypass | Wrapper Method | Ciclo |
|---|---------|-------|--------|----------------|-------|
| 1 | `FirestoreShapedQueryCompilingExpressionVisitor.cs` | 1439 | `subCollectionRef.GetSnapshotAsync()` | `GetSubCollectionAsync` | 9 |
| 2 | `FirestoreShapedQueryCompilingExpressionVisitor.cs` | 1568 | `docRef.GetSnapshotAsync()` | `GetDocumentByReferenceAsync` | 10 |
| 3 | `FirestoreShapedQueryCompilingExpressionVisitor.cs` | 1591 | `docRefFromId.GetSnapshotAsync()` | `GetDocumentByReferenceAsync` | 10 |
| 4 | `FirestoreShapedQueryCompilingExpressionVisitor.cs` | 1743 | `docRef.GetSnapshotAsync()` | `GetDocumentByReferenceAsync` | 10 |
| 5 | `FirestoreShapedQueryCompilingExpressionVisitor.cs` | 1754 | `docRefFromId.GetSnapshotAsync()` | `GetDocumentByReferenceAsync` | 10 |
| 6 | `FirestoreQueryExecutor.cs` | 765 | `aggregateQuery.GetSnapshotAsync()` (Count) | `ExecuteAggregateQueryAsync` | 11 |
| 7 | `FirestoreQueryExecutor.cs` | 778 | `limitedQuery.GetSnapshotAsync()` (Any) | `ExecuteQueryAsync` | 11 |
| 8 | `FirestoreQueryExecutor.cs` | 797 | `aggregateQuery.GetSnapshotAsync()` (Sum) | `ExecuteAggregateQueryAsync` | 11 |
| 9 | `FirestoreQueryExecutor.cs` | 820 | `aggregateQuery.GetSnapshotAsync()` (Avg) | `ExecuteAggregateQueryAsync` | 11 |
| 10 | `FirestoreQueryExecutor.cs` | 848 | `minQuery.GetSnapshotAsync()` (Min) | `ExecuteQueryAsync` | 11 |
| 11 | `FirestoreQueryExecutor.cs` | 876 | `maxQuery.GetSnapshotAsync()` (Max) | `ExecuteQueryAsync` | 11 |

---

## Arquitectura Final - Flujo Completo

### Flujo de Ejecución: 1ª vez (sin caché)

```
context.Clientes.Where(...).Include(c => c.Pedidos).ToListAsync()
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  1. EF Core Pipeline (SOLO 1ª VEZ - después se cachea)                       │
│                                                                              │
│     QueryTranslationPreprocessor                                             │
│         │                                                                    │
│         ▼                                                                    │
│     QueryableMethodTranslatingExpressionVisitor                             │
│         │  Traduce LINQ → FirestoreQueryExpression                          │
│         ▼                                                                    │
│     ShapedQueryCompilingExpressionVisitor                                   │
│         │  Compila Shaper (Func<QueryContext, DocumentSnapshot, bool, T>)   │
│         │                                                                    │
│         ▼                                                                    │
│     GENERA: FirestoreQueryingEnumerable<T>                                  │
│              ├── _queryExpression  (la query traducida)                     │
│              └── _shaper           (la función compilada)                   │
│                                                                              │
│     EF CORE CACHEA: (queryExpression + shaper) para reusar                  │
└─────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
                              (continúa en paso 2)
```

### Flujo de Ejecución: 2ª vez (con caché)

```
context.Clientes.Where(...).Include(c => c.Pedidos).ToListAsync()
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  EF Core detecta que la query ya está cacheada                              │
│  → Reutiliza FirestoreQueryingEnumerable con queryExpression y shaper       │
│  → SALTA el pipeline de compilación                                         │
└─────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
                              (continúa en paso 2)
```

### Paso 2: Ejecución (SIEMPRE se ejecuta - 1ª y 2ª vez)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  2. ToListAsync() itera sobre FirestoreQueryingEnumerable<T>                │
│                                                                              │
│     FirestoreQueryingEnumerable ya tiene:                                   │
│     ├── _queryExpression  (qué query ejecutar)                              │
│     └── _shaper           (cómo materializar cada documento)                │
└─────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  3. MoveNextAsync() - Primera llamada                                        │
│                                                                              │
│     a) Obtiene IFirestoreClientWrapper del DI                               │
│     b) Crea FirestoreQueryExecutor(wrapper)                                 │
│     c) Llama executor.ExecuteQueryAsync(_queryExpression)                   │
│            │                                                                 │
│            ▼                                                                 │
│     ┌─────────────────────────────────────────────────────────────────────┐ │
│     │  FirestoreQueryExecutor                                             │ │
│     │                                                                     │ │
│     │  Recibe: _queryExpression (la query ya traducida)                   │ │
│     │                                                                     │ │
│     │  1. BuildQuery() → Construye Google.Cloud.Firestore.Query           │ │
│     │  2. wrapper.ExecuteQueryAsync(query) → QuerySnapshot                │ │
│     │  3. Retorna List<DocumentSnapshot>                                  │ │
│     └─────────────────────────────────────────────────────────────────────┘ │
│     d) Guarda los DocumentSnapshots en _enumerator                          │
└─────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  4. MoveNextAsync() - Por cada documento                                     │
│                                                                              │
│     while (_enumerator.MoveNext())                                          │
│     {                                                                        │
│         var document = _enumerator.Current;                                 │
│         Current = _shaper(queryContext, document, isTracking);              │
│                      │                                                       │
│                      ▼                                                       │
│         ┌───────────────────────────────────────────────────────────────┐   │
│         │  Shaper (función compilada)                                   │   │
│         │                                                               │   │
│         │  Recibe: DocumentSnapshot (ya obtenido de Firestore)          │   │
│         │                                                               │   │
│         │  1. Deserializer.DeserializeEntity<T>(document)               │   │
│         │  2. Si hay Includes:                                          │   │
│         │     NavigationLoader.LoadSubCollectionAsync(...)              │   │
│         │     NavigationLoader.LoadReferenceAsync(...)                  │   │
│         │  3. Si tracking: context.Attach(entity)                       │   │
│         │  4. Retorna: entidad materializada                            │   │
│         └───────────────────────────────────────────────────────────────┘   │
│         return true;                                                         │
│     }                                                                        │
└─────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  5. ToListAsync() acumula las entidades y retorna List<Cliente>             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Diagrama de Componentes y Dependencias

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         IFirestoreClientWrapper                              │
│                    (ÚNICO punto de entrada a Firestore)                      │
├─────────────────────────────────────────────────────────────────────────────┤
│  Métodos existentes:                                                         │
│  ├── GetDocumentAsync(collection, id)         → DocumentSnapshot            │
│  ├── GetCollectionAsync(name)                 → CollectionReference         │
│  └── ExecuteQueryAsync(Query)                 → QuerySnapshot               │
│                                                                              │
│  Métodos nuevos (a agregar):                                                 │
│  ├── ExecuteAggregateQueryAsync(AggregateQuery) → AggregateQuerySnapshot    │
│  ├── GetSubCollectionAsync(docRef, name)        → QuerySnapshot             │
│  └── GetDocumentByReferenceAsync(docRef)        → DocumentSnapshot          │
└─────────────────────────────────────────────────────────────────────────────┘
                                        ▲
                                        │
                    ┌───────────────────┴───────────────────┐
                    │                                       │
                    │                                       │
┌───────────────────┴───────────────────┐   ┌───────────────┴───────────────────┐
│  FirestoreQueryExecutor               │   │  INavigationLoader (NUEVO)        │
│                                       │   │                                   │
│  Recibe: FirestoreQueryExpression     │   │  - LoadSubCollectionAsync()       │
│                                       │   │  - LoadReferenceAsync()           │
│  - BuildQuery()                       │   │  - Soporta List<T>, HashSet<T>,   │
│  - ExecuteQueryAsync()                │   │    ICollection<T>                 │
│  - ExecuteIdQueryAsync()              │   │  - Filtered Includes              │
│  - ExecuteAggregationAsync()          │   │  - Carga recursiva                │
│                                       │   │                                   │
│  Usa: IFirestoreClientWrapper         │   │  Usa: IFirestoreClientWrapper     │
│                                       │   │  Usa: IFirestoreDocumentDeserializer
└───────────────────┬───────────────────┘   └───────────────┬───────────────────┘
                    │                                       │
                    │                                       │
                    └───────────────────┬───────────────────┘
                                        │
                                        ▼
                    ┌───────────────────────────────────────────────────────────┐
                    │  IFirestoreDocumentDeserializer (NUEVO - con interfaz)    │
                    │                                                           │
                    │  - DeserializeEntity<T>(DocumentSnapshot)                 │
                    │  - DeserializeEntities<T>(IEnumerable<DocumentSnapshot>)  │
                    │  - Soporta constructores con parámetros                   │
                    │  - Deserializa propiedades simples                        │
                    │  - Deserializa ComplexTypes                               │
                    │                                                           │
                    │  NO hace I/O (solo transforma datos)                      │
                    │                                                           │
                    │  Lo usan:                                                 │
                    │  - El Shaper (para la entidad principal)                  │
                    │  - INavigationLoader (para subcollections y references)   │
                    └───────────────────────────────────────────────────────────┘
```

**Flujo de deserialización:**
- **Query principal:** Shaper → `IFirestoreDocumentDeserializer.DeserializeEntity<T>(doc)`
- **SubCollections:** NavigationLoader → obtiene docs del wrapper → `IFirestoreDocumentDeserializer.DeserializeEntities<T>(docs)`
- **References:** NavigationLoader → obtiene doc del wrapper → `IFirestoreDocumentDeserializer.DeserializeEntity<T>(doc)`

Todos los DocumentSnapshots (1 o N) pasan por el mismo deserializador.

### Qué se cachea vs. qué se ejecuta siempre

| Aspecto | 1ª ejecución | 2ª ejecución |
|---------|--------------|--------------|
| Pipeline de compilación (Visitors) | ✅ Se ejecuta | ❌ Se salta (cacheado) |
| `FirestoreQueryExpression` | ✅ Se genera | ✅ Se reutiliza del caché |
| `Shaper` (función compilada) | ✅ Se compila | ✅ Se reutiliza del caché |
| `FirestoreQueryExecutor.ExecuteQueryAsync()` | ✅ Se ejecuta | ✅ Se ejecuta (siempre va a Firestore) |
| `Shaper()` por cada documento | ✅ Se ejecuta N veces | ✅ Se ejecuta N veces |
| Datos de Firestore | ✅ Se obtienen | ✅ Se obtienen (no hay caché de datos) |

---

### Diagrama de Dependencias (DI Container)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            DI Container                                      │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  Singleton:                                                                  │
│  └── IFirestoreClientWrapper → FirestoreClientWrapper                       │
│                                                                              │
│  Scoped (por DbContext):                                                     │
│  ├── IFirestoreDocumentDeserializer → FirestoreDocumentDeserializer         │
│  └── INavigationLoader → NavigationLoader                                   │
│                                                                              │
│  Transient (creados en runtime):                                             │
│  └── FirestoreQueryExecutor (creado en FirestoreQueryingEnumerable)         │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

### Responsabilidades por Componente

| Componente | Responsabilidad Única | Usa Wrapper |
|------------|----------------------|-------------|
| `FirestoreQueryingEnumerable` | Orquestar ejecución de query | Indirecto (vía Executor) |
| `FirestoreQueryExecutor` | Construir y ejecutar queries | ✅ Sí |
| `Shaper` (compilado) | Orquestar materialización de 1 entidad | No (delega) |
| `IFirestoreDocumentDeserializer` | Convertir DocumentSnapshot → Entity | No (no hace I/O) |
| `INavigationLoader` | Cargar navegaciones (SubCollections, References) | ✅ Sí |
| `IFirestoreClientWrapper` | Punto único de I/O a Firestore | N/A (es el punto) |

---

### Cambio Clave: El Shaper

**Antes (actual - problemático):**
```csharp
// El Shaper hace llamadas directas a Firestore (BYPASSES)
var snapshot = await subCollectionRef.GetSnapshotAsync();  // ❌ BYPASS
var docSnapshot = await docRef.GetSnapshotAsync();          // ❌ BYPASS
```

**Después (propuesto):**
```csharp
// El Shaper obtiene INavigationLoader del DI y delega
var navigationLoader = serviceProvider.GetService<INavigationLoader>();
await navigationLoader.LoadSubCollectionAsync(...);  // ✅ Usa wrapper internamente
await navigationLoader.LoadReferenceAsync(...);      // ✅ Usa wrapper internamente
```

---

### Nota sobre Tracking: Comparación con Providers Oficiales

**Pregunta:** ¿El tracking debería gestionarse en el Shaper?

**Respuesta:** Sí, es el patrón oficial de EF Core.

#### Cómo lo hace EF Core (clase base `ShapedQueryCompilingExpressionVisitor`)

Fuente: [ShapedQueryCompilingExpressionVisitor.cs](https://github.com/dotnet/efcore/blob/main/src/EFCore/Query/ShapedQueryCompilingExpressionVisitor.cs)

```csharp
// En el método MaterializeEntity(), cuando _queryStateManager es true:
if (_queryStateManager && primaryKey is not null)
{
    expressions.Add(
        Assign(
            entryVariable!,
            Call(
                QueryCompilationContext.QueryContextParameter,
                StartTrackingMethodInfo,  // ← QueryContext.StartTracking()
                concreteEntityTypeVariable,
                instanceVariable,
                shadowValuesVariable)));
}
```

El tracking se **inyecta en la expresión compilada del Shaper** por EF Core. Cuando se materializa una entidad, el código generado llama a `QueryContext.StartTracking()`.

#### Comparación con nuestro provider

| Aspecto | EF Core (oficial) | Firestore (nuestro) |
|---------|-------------------|---------------------|
| ¿Dónde se hace tracking? | En el Shaper (código generado) | En el Shaper (código en el Visitor) |
| ¿Cómo se llama? | `QueryContext.StartTracking()` | `dbContext.Attach()` |
| ¿Es correcto? | ✅ Patrón oficial | ⚠️ Funciona pero usa `Attach` en lugar de `StartTracking` |

#### Conclusión

El tracking en el Shaper **es el patrón correcto de EF Core**. No es un error de diseño nuestro.

Sin embargo, hay una diferencia de implementación:
- **EF Core oficial:** Usa `QueryContext.StartTracking()` que es más eficiente
- **Nuestro provider:** Usa `dbContext.Attach()` que es más simple pero menos eficiente

**Acción futura (fuera del scope de este refactor):**
Considerar migrar de `dbContext.Attach()` a `QueryContext.StartTracking()` para alinearnos con el patrón oficial.

**Referencias:**
- [ShapedQueryCompilingExpressionVisitor.cs](https://github.com/dotnet/efcore/blob/main/src/EFCore/Query/ShapedQueryCompilingExpressionVisitor.cs)
- [CosmosShapedQueryCompilingExpressionVisitor.cs](https://github.com/dotnet/efcore/blob/main/src/EFCore.Cosmos/Query/Internal/CosmosShapedQueryCompilingExpressionVisitor.cs)
- [RelationalShapedQueryCompilingExpressionVisitor.cs](https://github.com/dotnet/efcore/blob/main/src/EFCore.Relational/Query/RelationalShapedQueryCompilingExpressionVisitor.cs)

---

## Reglas TDD

1. Un ciclo = un comportamiento
2. Test RED primero, luego GREEN, luego REFACTOR
3. Los 583 tests unitarios + 183 de integración deben pasar siempre
4. Cada ciclo = commit(s)
5. No avanzar si hay tests rotos

---

## Verificación Final

Al completar todos los ciclos:

- [ ] `IFirestoreDocumentDeserializer` existe y está implementada
- [ ] Constructores con parámetros funcionan
- [ ] `HashSet<T>` funciona en subcollections
- [ ] `ICollection<T>` funciona en subcollections
- [ ] `IFirestoreClientWrapper` tiene todos los métodos
- [ ] `INavigationLoader` existe y está implementada
- [ ] 0 bypasses del wrapper (los 11 eliminados)
- [ ] Todos los tests pasan (583 unit + 183 integration)
- [ ] Un breakpoint en el wrapper captura TODAS las operaciones de Firestore
- [ ] `IFirestoreQueryExecutor` existe y está implementada

---

## Notas Pendientes de Revisión

### `IFirestoreDocumentSerializer` - Interfaz vacía

**Archivo:** `Infrastructure/Contracts/IFirestoreDocumentSerializer.cs`

**Estado actual:** La interfaz existe pero **no tiene ningún método definido**.

```csharp
public interface IFirestoreDocumentSerializer
{
    // Vacía - sin métodos
}
```

**Problema:**
- La interfaz vacía no aporta valor
- No está claro si se dejó vacía intencionalmente o si faltó implementar
- `FirestoreDocumentSerializer` (la implementación) tiene métodos que no están en la interfaz

**Acción requerida:**
1. Revisar `FirestoreDocumentSerializer.cs` para identificar métodos públicos
2. Decidir qué métodos deberían formar parte del contrato
3. Actualizar la interfaz o eliminarla si no es necesaria

**Métodos candidatos (a verificar):**
```csharp
public interface IFirestoreDocumentSerializer
{
    Dictionary<string, object> SerializeEntity<T>(T entity) where T : class;
    Dictionary<string, object> SerializeForUpdate<T>(T entity, IEnumerable<string> changedProperties) where T : class;
    // ... otros métodos de serialización
}
```

**Prioridad:** Baja - No bloquea este refactor, pero debe revisarse para consistencia arquitectónica.