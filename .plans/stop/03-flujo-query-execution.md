# Flujo de Ejecución de Queries - Implementación Actual

**Fecha:** 2025-12-20
**Estado:** Documentación del flujo real implementado

---

## Diagrama de Flujo Completo

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                          USUARIO                                                 │
│   context.Clientes.Where(c => c.Nombre == "Juan").Include(c => c.Pedidos)       │
│                              .ToListAsync()                                      │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│  PASO 1: EF Core recibe la Expression Tree del IQueryable                       │
│  Archivo: Microsoft.EntityFrameworkCore (interno)                               │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│  PASO 2: FirestoreQueryTranslationPreprocessor.Process()                        │
│  Archivo: FirestoreQueryTranslationPreprocessor.cs:29-48                        │
│                                                                                  │
│  Acciones:                                                                       │
│  1. FilteredIncludeExtractorVisitor → Extrae filtros de Includes (línea 33-34)  │
│  2. ComplexTypeIncludeExtractorVisitor → Extrae includes de ComplexTypes (39-40)│
│  3. TakeLastTransformingVisitor → Transforma TakeLast (línea 43-44)             │
│  4. base.Process() → Delega a EF Core (línea 47)                                │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│  PASO 3: FirestoreQueryableMethodTranslatingExpressionVisitor                   │
│  Archivo: FirestoreQueryableMethodTranslatingExpressionVisitor.cs               │
│                                                                                  │
│  Acciones:                                                                       │
│  1. CreateShapedQueryExpression() (línea 29-43)                                 │
│     - Crea FirestoreQueryExpression con CollectionName                          │
│     - Crea StructuralTypeShaperExpression                                       │
│     - Retorna ShapedQueryExpression                                             │
│                                                                                  │
│  2. Traduce operadores LINQ a FirestoreQueryExpression:                         │
│     - Where → FirestoreWhereClause con filtros                                  │
│     - OrderBy → FirestoreOrderByClause                                          │
│     - Take/Skip → Limit/Skip en FirestoreQueryExpression                        │
│     - Include → PendingIncludes en FirestoreQueryExpression                     │
│     - Select → ProjectionSelector en FirestoreQueryExpression                   │
│     - Count/Sum/etc → AggregationType en FirestoreQueryExpression               │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│  PASO 4: FirestoreShapedQueryCompilingExpressionVisitor.VisitShapedQuery()      │
│  Archivo: FirestoreShapedQueryCompilingExpressionVisitor.cs:34-148              │
│                                                                                  │
│  Acciones:                                                                       │
│  1. Copia ComplexTypeIncludes al FirestoreQueryExpression (línea 39-44)         │
│  2. Copia FilteredIncludes al FirestoreQueryExpression (línea 46-88)            │
│  3. Decide tipo de query:                                                        │
│     - IsAggregation → CreateAggregationQueryExpression (línea 91-94)            │
│     - HasSubcollectionProjection → CreateSubcollectionProjectionQueryExpression │
│     - HasProjection → CreateProjectionQueryExpression (línea 103-106)           │
│     - Normal → Continúa                                                          │
│                                                                                  │
│  4. Crea Shaper Expression (línea 117-127):                                     │
│     - CreateShaperExpression() genera código de deserialización                 │
│     - Compila el shaper como Func<QueryContext, DocumentSnapshot, bool, T>      │
│                                                                                  │
│  5. Crea FirestoreQueryingEnumerable<T> (línea 129-147):                        │
│     - Pasa QueryContext, FirestoreQueryExpression, shaper compilado             │
│     - Este es el objeto que implementa IAsyncEnumerable<T>                      │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│  PASO 5: ToListAsync() / foreach itera sobre FirestoreQueryingEnumerable       │
│  Archivo: FirestoreQueryingEnumerable.cs                                        │
│                                                                                  │
│  Acciones (en MoveNextAsync, línea 173-253):                                    │
│  1. Primera iteración → InitializeEnumeratorAsync() (línea 177)                 │
│  2. Obtiene servicios del DI:                                                   │
│     - IFirestoreClientWrapper (línea 196)                                       │
│     - ILoggerFactory (línea 197)                                                │
│  3. Crea FirestoreQueryExecutor (línea 200-201)                                 │
│  4. Decide tipo de ejecución:                                                   │
│     - IsIdOnlyQuery → ExecuteIdQueryAsync (línea 205-221)                       │
│     - Normal → ExecuteQueryAsync (línea 226-252)                                │
│  5. Aplica Skip en memoria si es necesario (línea 234-249)                      │
│  6. Guarda enumerador de DocumentSnapshots                                      │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│  PASO 6: FirestoreQueryExecutor.ExecuteQueryAsync()                             │
│  Archivo: FirestoreQueryExecutor.cs:34-64                                       │
│                                                                                  │
│  Acciones:                                                                       │
│  1. Valida que no sea IdOnlyQuery (línea 45-50)                                 │
│  2. BuildQuery() → Construye Google.Cloud.Firestore.Query (línea 56)            │
│  3. _client.ExecuteQueryAsync() → VÍA WRAPPER (línea 59)                        │
│  4. Retorna QuerySnapshot                                                        │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│  PASO 6b: FirestoreQueryExecutor.BuildQuery()                                   │
│  Archivo: FirestoreQueryExecutor.cs:219-294                                     │
│                                                                                  │
│  Acciones:                                                                       │
│  1. _client.GetCollection() → Obtiene CollectionReference (línea 224)           │
│  2. ApplyWhereClause() por cada filtro (línea 227-230)                          │
│  3. ApplyOrFilterGroup() por cada grupo OR (línea 232-236)                      │
│  4. ApplyOrderByClause() por cada ordenamiento (línea 238-242)                  │
│  5. Calcula Skip para ajustar Limit (línea 244-254)                             │
│  6. query.Limit() si hay Take (línea 257-270)                                   │
│  7. query.LimitToLast() si hay TakeLast (línea 272-283)                         │
│  8. query.StartAfter() si hay cursor (línea 285-291)                            │
│  9. Retorna Google.Cloud.Firestore.Query construida                             │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│  PASO 7: IFirestoreClientWrapper.ExecuteQueryAsync()                            │
│  Archivo: FirestoreClientWrapper.cs (implementación)                            │
│                                                                                  │
│  Acción: query.GetSnapshotAsync() → Llamada real a Firestore                    │
│  Retorna: QuerySnapshot con DocumentSnapshots                                   │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│  PASO 8: Por cada documento, Shaper orquesta la materialización                 │
│  Archivo: FirestoreQueryingEnumerable.cs:182-183                                │
│                                                                                  │
│  Acción: _shaper(queryContext, document, isTracking)                            │
│  El shaper fue compilado en PASO 4 y ORQUESTA (no deserializa directamente):    │
│    1. Llama a FirestoreDocumentDeserializer.DeserializeEntity<T>()              │
│    2. Carga Includes si hay (LoadIncludes/LoadSubCollectionAsync/etc)           │
│    3. Maneja tracking si está habilitado                                        │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│  PASO 9: Si hay Includes, LoadIncludes()                                        │
│  Archivo: FirestoreShapedQueryCompilingExpressionVisitor.cs:1380-1418           │
│                                                                                  │
│  Por cada navegación en PendingIncludes:                                        │
│  1. Si IsCollection → LoadSubCollectionAsync() (línea 1412)                     │
│  2. Si Reference → LoadReferenceAsync() (línea 1416)                            │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                    ┌───────────────────┴───────────────────┐
                    ▼                                       ▼
┌───────────────────────────────────┐   ┌───────────────────────────────────┐
│  PASO 9a: LoadSubCollectionAsync  │   │  PASO 9b: LoadReferenceAsync      │
│  Líneas 1420-1517                 │   │  Líneas 1519-1632                 │
│                                   │   │                                   │
│  ⚠️ BYPASS DEL WRAPPER:           │   │  ⚠️ BYPASS DEL WRAPPER:           │
│  subCollectionRef.GetSnapshotAsync│   │  docRef.GetSnapshotAsync()        │
│  (línea 1439)                     │   │  (líneas 1568, 1591)              │
│                                   │   │                                   │
│  1. Obtiene subcollection name    │   │  1. Lee referencia del documento  │
│  2. parentDoc.Reference.Collection│   │  2. Si DocumentReference → Get    │
│  3. LLAMADA DIRECTA A FIRESTORE   │   │  3. Si string ID → Get por ID     │
│  4. Deserializa cada documento    │   │  4. LLAMADA DIRECTA A FIRESTORE   │
│  5. Aplica filtro si Filtered Inc │   │  5. Deserializa entidad           │
│  6. Carga child includes recursivo│   │  6. Carga child includes recursivo│
│  7. Attach al ChangeTracker       │   │  7. Attach al ChangeTracker       │
│  8. ApplyFixup()                  │   │  8. ApplyFixup()                  │
└───────────────────────────────────┘   └───────────────────────────────────┘
                    │                                       │
                    └───────────────────┬───────────────────┘
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│  PASO 10: Retorna entidad materializada con sus Includes                        │
│  Current = entidad deserializada y poblada                                      │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│  PASO 11: Siguiente iteración (MoveNext) o fin                                  │
│  Si hay más documentos → volver a PASO 8                                        │
│  Si no hay más → retorna false, fin de la iteración                             │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## Flujo Alternativo: Agregaciones (Count, Sum, Average, etc.)

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│  PASO 4-ALT: VisitShapedQuery() detecta IsAggregation                           │
│  Archivo: FirestoreShapedQueryCompilingExpressionVisitor.cs:91-94               │
│                                                                                  │
│  Acción: CreateAggregationQueryExpression()                                     │
│  Retorna: FirestoreAggregationQueryingEnumerable<TResult>                       │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│  FirestoreAggregationQueryingEnumerable                                         │
│  Archivo: FirestoreAggregationQueryingEnumerable.cs                             │
│                                                                                  │
│  Acción: Llama a FirestoreQueryExecutor.ExecuteAggregationAsync<T>()            │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│  FirestoreQueryExecutor.ExecuteAggregationAsync<T>()                            │
│  Archivo: FirestoreQueryExecutor.cs:736-760                                     │
│                                                                                  │
│  1. BuildQuery() → Construye query base (línea 748)                             │
│  2. Switch por AggregationType:                                                 │
│     - Count → ExecuteCountAsync (línea 752)                                     │
│     - Any → ExecuteAnyAsync (línea 753)                                         │
│     - Sum → ExecuteSumAsync (línea 754)                                         │
│     - Average → ExecuteAverageAsync (línea 755)                                 │
│     - Min → ExecuteMinAsync (línea 756)                                         │
│     - Max → ExecuteMaxAsync (línea 757)                                         │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│  ⚠️ BYPASS DEL WRAPPER EN TODAS LAS AGREGACIONES                                │
│                                                                                  │
│  ExecuteCountAsync (línea 765):                                                 │
│    aggregateQuery.GetSnapshotAsync() ← DIRECTO                                  │
│                                                                                  │
│  ExecuteAnyAsync (línea 778):                                                   │
│    limitedQuery.GetSnapshotAsync() ← DIRECTO                                    │
│                                                                                  │
│  ExecuteSumAsync (línea 797):                                                   │
│    aggregateQuery.GetSnapshotAsync() ← DIRECTO                                  │
│                                                                                  │
│  ExecuteAverageAsync (línea 820):                                               │
│    aggregateQuery.GetSnapshotAsync() ← DIRECTO                                  │
│                                                                                  │
│  ExecuteMinAsync (línea 848):                                                   │
│    minQuery.GetSnapshotAsync() ← DIRECTO                                        │
│                                                                                  │
│  ExecuteMaxAsync (línea 876):                                                   │
│    maxQuery.GetSnapshotAsync() ← DIRECTO                                        │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## Flujo Alternativo: Query por ID

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│  context.Clientes.Where(c => c.Id == "abc123").FirstOrDefaultAsync()            │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│  FirestoreQueryExpression.IsIdOnlyQuery = true                                  │
│  (Detectado en TranslateWhere cuando el único filtro es Id == valor)            │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│  FirestoreQueryingEnumerable.InitializeEnumeratorAsync()                        │
│  Archivo: FirestoreQueryingEnumerable.cs:205-221                                │
│                                                                                  │
│  if (_queryExpression.IsIdOnlyQuery)                                            │
│  {                                                                               │
│      executor.ExecuteIdQueryAsync() ← USA ESTE MÉTODO                           │
│  }                                                                               │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│  FirestoreQueryExecutor.ExecuteIdQueryAsync()                                   │
│  Archivo: FirestoreQueryExecutor.cs:69-114                                      │
│                                                                                  │
│  1. EvaluateIdExpression() → Obtiene el valor del ID (línea 88)                 │
│  2. _client.GetDocumentAsync() → VÍA WRAPPER (línea 99-102)                     │
│  3. Retorna DocumentSnapshot o null                                             │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## Resumen de Clases Involucradas

| Paso | Clase | Responsabilidad |
|------|-------|-----------------|
| 2 | `FirestoreQueryTranslationPreprocessor` | Pre-procesa la Expression Tree |
| 2 | `FilteredIncludeExtractorVisitor` | Extrae filtros de Filtered Includes |
| 2 | `ComplexTypeIncludeExtractorVisitor` | Extrae Includes de ComplexTypes |
| 2 | `TakeLastTransformingVisitor` | Transforma TakeLast |
| 3 | `FirestoreQueryableMethodTranslatingExpressionVisitor` | Traduce operadores LINQ a Firestore |
| 4 | `FirestoreShapedQueryCompilingExpressionVisitor` | Compila la query y crea el Enumerable |
| 5-8 | `FirestoreQueryingEnumerable<T>` | Ejecuta la query y itera resultados |
| 6-6b | `FirestoreQueryExecutor` | Construye y ejecuta queries |
| 7 | `IFirestoreClientWrapper` | Punto de entrada a Firestore (cuando se usa) |
| 8-9 | `FirestoreDocumentDeserializer` | Deserializa DocumentSnapshot a entidad |
| 9 | `FirestoreShapedQueryCompilingExpressionVisitor` | Carga Includes (con bypasses) |

---

## Puntos de I/O a Firestore

### Correctos (pasan por wrapper)
| Ubicación | Método del Wrapper |
|-----------|-------------------|
| `FirestoreQueryExecutor:59` | `ExecuteQueryAsync()` |
| `FirestoreQueryExecutor:99-102` | `GetDocumentAsync()` |

### Bypasses (NO pasan por wrapper)
| Ubicación | Llamada Directa |
|-----------|-----------------|
| `FirestoreQueryExecutor:765` | `aggregateQuery.GetSnapshotAsync()` |
| `FirestoreQueryExecutor:778` | `limitedQuery.GetSnapshotAsync()` |
| `FirestoreQueryExecutor:797` | `aggregateQuery.GetSnapshotAsync()` |
| `FirestoreQueryExecutor:820` | `aggregateQuery.GetSnapshotAsync()` |
| `FirestoreQueryExecutor:848` | `minQuery.GetSnapshotAsync()` |
| `FirestoreQueryExecutor:876` | `maxQuery.GetSnapshotAsync()` |
| `FirestoreShapedQueryCompilingExpressionVisitor:1439` | `subCollectionRef.GetSnapshotAsync()` |
| `FirestoreShapedQueryCompilingExpressionVisitor:1568` | `docRef.GetSnapshotAsync()` |
| `FirestoreShapedQueryCompilingExpressionVisitor:1591` | `docRefFromId.GetSnapshotAsync()` |
| `FirestoreShapedQueryCompilingExpressionVisitor:1743` | `docRef.GetSnapshotAsync()` |
| `FirestoreShapedQueryCompilingExpressionVisitor:1754` | `docRefFromId.GetSnapshotAsync()` |
