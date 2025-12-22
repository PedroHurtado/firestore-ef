# 04 - Plan de Ejecución

## Orden de Implementación (TDD)

Los cambios deben hacerse en orden de dependencias: primero las hojas, luego hacia arriba.

---

## Fase 1: Preparar FirestoreQueryExecutor

### Ciclo 1.1: Inyectar IFirestoreDocumentDeserializer

**Test RED:**
```csharp
[Fact]
public void Constructor_ShouldRequireDeserializer()
{
    var constructor = typeof(FirestoreQueryExecutor).GetConstructors().First();
    var parameters = constructor.GetParameters();
    parameters.Should().Contain(p => p.ParameterType == typeof(IFirestoreDocumentDeserializer));
}
```

**Cambios:**
1. Agregar `IFirestoreDocumentDeserializer` al constructor
2. Actualizar tests existentes

---

### Ciclo 1.2: Cambiar firma de ExecuteQueryAsync

**Test RED:**
```csharp
[Fact]
public void ExecuteQueryAsync_ShouldReturnEntities()
{
    var method = typeof(IFirestoreQueryExecutor).GetMethod("ExecuteQueryAsync");
    method.Should().NotBeNull();
    method!.IsGenericMethod.Should().BeTrue();
    // Retorna IReadOnlyList<TEntity>, no QuerySnapshot
}
```

**Cambios:**
1. Cambiar `IFirestoreQueryExecutor.ExecuteQueryAsync` a genérico
2. Implementar deserialización dentro del executor
3. Retornar `IReadOnlyList<TEntity>`

---

### Ciclo 1.3: Cambiar firma de ExecuteIdQueryAsync

**Test RED:**
```csharp
[Fact]
public void ExecuteIdQueryAsync_ShouldReturnEntity()
{
    var method = typeof(IFirestoreQueryExecutor).GetMethod("ExecuteIdQueryAsync");
    method.Should().NotBeNull();
    method!.IsGenericMethod.Should().BeTrue();
    // Retorna TEntity?, no DocumentSnapshot?
}
```

---

## Fase 2: Registrar en DI

### Ciclo 2.1: Registrar IFirestoreQueryExecutor

**Test RED:**
```csharp
[Fact]
public void ServiceCollection_ShouldRegisterIFirestoreQueryExecutor()
{
    var services = new ServiceCollection();
    services.AddEntityFrameworkFirestore();

    var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IFirestoreQueryExecutor));
    descriptor.Should().NotBeNull();
}
```

**Cambios:**
1. Agregar `.TryAddScoped<IFirestoreQueryExecutor, FirestoreQueryExecutor>()` en extensions

---

## Fase 3: Inyectar en la Cadena

### Ciclo 3.1: Factory recibe IFirestoreQueryExecutor

**Test RED:**
```csharp
[Fact]
public void Factory_Constructor_ShouldRequireQueryExecutor()
{
    var constructor = typeof(FirestoreShapedQueryCompilingExpressionVisitorFactory)
        .GetConstructors().First();
    var parameters = constructor.GetParameters();
    parameters.Should().Contain(p => p.ParameterType == typeof(IFirestoreQueryExecutor));
}
```

---

### Ciclo 3.2: Visitor recibe IFirestoreQueryExecutor

**Test RED:**
```csharp
[Fact]
public void Visitor_Constructor_ShouldRequireQueryExecutor()
{
    var constructor = typeof(FirestoreShapedQueryCompilingExpressionVisitor)
        .GetConstructors().First();
    var parameters = constructor.GetParameters();
    parameters.Should().Contain(p => p.ParameterType == typeof(IFirestoreQueryExecutor));
}
```

---

### Ciclo 3.3: QueryingEnumerable recibe IFirestoreQueryExecutor

**Test RED:**
```csharp
[Fact]
public void QueryingEnumerable_Constructor_ShouldRequireQueryExecutor()
{
    var constructor = typeof(FirestoreQueryingEnumerable<>)
        .GetConstructors().First();
    var parameters = constructor.GetParameters();
    parameters.Should().Contain(p => p.ParameterType == typeof(IFirestoreQueryExecutor));
}
```

**Cambios:**
1. Agregar parámetro al constructor
2. Eliminar Service Locator (líneas 100-101, 196-197)
3. Eliminar `new FirestoreQueryExecutor()` (líneas 105, 201)

---

### Ciclo 3.4: AggregationQueryingEnumerable recibe IFirestoreQueryExecutor

**Test RED:**
```csharp
[Fact]
public void AggregationEnumerable_Constructor_ShouldRequireQueryExecutor()
{
    var constructor = typeof(FirestoreAggregationQueryingEnumerable<>)
        .GetConstructors().First();
    var parameters = constructor.GetParameters();
    parameters.Should().Contain(p => p.ParameterType == typeof(IFirestoreQueryExecutor));
}
```

**Cambios:**
1. Agregar parámetro al constructor
2. Eliminar Service Locator (líneas 75-76, 123-124)
3. Eliminar `new FirestoreQueryExecutor()` (líneas 79, 127)

---

## Fase 4: Limpieza

### Ciclo 4.1: Eliminar Service Locator del Visitor

**Cambios:**
1. Eliminar líneas 1198-1201
2. Las dependencias ahora vienen por constructor o no son necesarias

---

### Ciclo 4.2: Eliminar using Google.Cloud.Firestore

**Test RED:**
```csharp
[Fact]
public void Visitor_ShouldNotReferenceFirestoreSdk()
{
    var assembly = typeof(FirestoreShapedQueryCompilingExpressionVisitor).Assembly;
    var visitorType = typeof(FirestoreShapedQueryCompilingExpressionVisitor);

    // Verificar que no usa tipos de Google.Cloud.Firestore directamente
    var methods = visitorType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance);
    // ... verificar retornos y parámetros
}
```

---

## Verificación Final

```bash
dotnet test
# 766 tests passing (571 unit + 195 integration)
```

---

## Riesgos y Mitigación

| Riesgo | Probabilidad | Mitigación |
|--------|--------------|------------|
| Romper queries existentes | Media | Tests de integración cubren todos los casos |
| Cambios en firma rompen compilación | Alta | Hacer cambios incrementales, test por test |
| Performance por deserialización duplicada | Baja | El Deserializer ya está optimizado |

---

## Métricas de Éxito

| Métrica | Antes | Después |
|---------|-------|---------|
| `new FirestoreQueryExecutor()` | 4 | 0 |
| Service Locator en Query | 12 | 0 |
| `using Google.Cloud.Firestore` en Visitor | 1 | 0 |
| Tests pasando | 766 | 766+ |