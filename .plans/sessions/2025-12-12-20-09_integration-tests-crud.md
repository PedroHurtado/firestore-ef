# Sesión: Tests de Integración y Fix de Queries

**Fecha:** 2025-12-12

## Objetivo

Crear tests de integración para el provider Firestore EF Core usando el emulador de Firestore (docker-compose).

## Trabajo Realizado

### 1. Proyecto de Tests de Integración

Creado `tests/Fudie.Firestore.IntegrationTest/` con la siguiente estructura:

```
tests/Fudie.Firestore.IntegrationTest/
├── Crud/
│   └── DbContextCrudTests.cs
├── Helpers/
│   ├── FirestoreTestFixture.cs
│   ├── TestDbContext.cs
│   └── TestEntities.cs
├── Infrastructure/
│   ├── DbContextConnectionTests.cs
│   └── FirestoreConnectionTests.cs
├── GlobalUsings.cs
└── Fudie.Firestore.IntegrationTest.csproj
```

### 2. Helpers Creados

#### FirestoreTestFixture
- Fixture reutilizable para xUnit con `ICollectionFixture<T>`
- Configura automáticamente `FIRESTORE_EMULATOR_HOST=localhost:8080`
- Método `CreateContext<TContext>()` para crear DbContext con el provider
- Método `GenerateId(prefix)` para IDs únicos en tests

#### TestEntities
- `Producto`: Entidad simple para tests CRUD
- `Cliente`: Entidad raíz con subcollection
- `Pedido`: Entidad subcollection de Cliente
- `EstadoPedido`: Enum para tests

#### TestDbContext
- `SimpleTestDbContext`: Para tests CRUD básicos
- `TestDbContext`: Con configuración de subcollections

### 3. Tests de Integración

#### Infrastructure (conexión)
- `FirestoreConnectionTests`: 5 tests de conexión directa al SDK de Firestore
- `DbContextConnectionTests`: 2 tests de conexión via DbContext con el provider

#### CRUD (operaciones)
- `Add_SingleEntity_ShouldPersistToFirestore` ✅
- `Add_MultipleEntities_ShouldPersistAllToFirestore` ✅
- `Query_WithWhere_ShouldFilterResults` ✅
- `Delete_ExistingEntity_ShouldRemoveFromFirestore` ✅
- `Update_ExistingEntity_ShouldPersistChanges` ⏭️ (skipped - tracking pendiente)

### 4. Fixes en el Provider

#### FirestoreClientWrapper.cs
Agregado `EmulatorDetection.EmulatorOrProduction` para detectar automáticamente el emulador:
```csharp
var builder = new FirestoreDbBuilder
{
    ProjectId = _options.ProjectId,
    DatabaseId = _options.DatabaseId ?? "(default)",
    EmulatorDetection = EmulatorDetection.EmulatorOrProduction
};
```

#### FirestoreWhereClause.cs y FirestoreQueryExecutor.cs
Fix para resolver variables capturadas en queries LINQ:

**Problema:**
```csharp
var uniqueTag = "test";
await context.Productos.Where(p => p.Nombre == uniqueTag).ToListAsync();
// Error: variable '__uniqueTag_0' not defined
```

**Solución:**
Actualizado `QueryContextParameterReplacer` para resolver parámetros desde `QueryContext.ParameterValues`:
```csharp
if (node.Name != null && _queryContext.ParameterValues.TryGetValue(node.Name, out var parameterValue))
{
    return Expression.Constant(parameterValue, node.Type);
}
```

## Resultados de Tests

| Suite | Passed | Skipped | Total |
|-------|--------|---------|-------|
| Integration Tests | 11 | 1 | 12 |
| Unit Tests | 521 | 0 | 521 |

## Pendientes Identificados

1. **Update tracking**: El test de Update está skipped porque el provider no detecta cambios en entidades tracked. Requiere investigación en el change tracker.

## Archivos Modificados

### Provider
- `firestore-efcore-provider/Infrastructure/Internal/FirestoreClientWrapper.cs`
- `firestore-efcore-provider/Query/FirestoreWhereClause.cs`
- `firestore-efcore-provider/Query/FirestoreQueryExecutor.cs`

### Tests (nuevos)
- `tests/Fudie.Firestore.IntegrationTest/*` (proyecto completo)

## Comandos Útiles

```bash
# Iniciar emulador
docker-compose up -d

# Ejecutar tests de integración
dotnet test tests/Fudie.Firestore.IntegrationTest

# Ejecutar solo tests CRUD
dotnet test tests/Fudie.Firestore.IntegrationTest --filter "FullyQualifiedName~DbContextCrudTests"
```
