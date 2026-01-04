# Plan de Refactorización: Include/ThenInclude Pipeline

## Problema Actual

### Orden del Pipeline (incorrecto)
```
1. ErrorHandlingHandler
2. ResolverHandler       → Genera ResolvedFirestoreQuery con Includes ya resueltos
3. LogQueryHandler
4. IncludeHandler        → Recibe ENTIDADES deserializadas, reconstruye queries
5. ProxyHandler
6. TrackingHandler
7. ConvertHandler        → Deserializa snapshots a entidades (sin FK)
8. ExecutionHandler      → Solo ejecuta query del root, ignora Includes
```

### Problemas Identificados

1. **ExecutionHandler ignora `resolved.Includes`** - El Resolver ya construyó el árbol completo pero no se usa

2. **IncludeHandler reconstruye queries** - En `ToResolvedQuery()` recrea lo que el Resolver ya hizo

3. **Orden de deserialización incorrecto** - Se deserializa el root antes de tener las FK

4. **FK perdidas** - El `DocumentReference` está en el snapshot, pero se pierde al deserializar

## Solución Propuesta

### Nuevo Orden del Pipeline
```
1. ErrorHandlingHandler
2. ResolverHandler       → Genera ResolvedFirestoreQuery con árbol de Includes
3. LogQueryHandler
4. ExecutionHandler      → Ejecuta TODAS las queries (root + includes + FK)
5. ConvertHandler        → Deserializa bottom-up (FK → SubCollections → Root)
6. TrackingHandler       → Trackea entidades completas
7. ProxyHandler
(IncludeHandler se elimina - su trabajo lo hace ExecutionHandler + ConvertHandler)
```

### Fase 1: ExecutionHandler Extendido

El `ExecutionHandler` debe:

1. Ejecutar query del root → `List<DocumentSnapshot>`
2. Para cada documento root, ejecutar sus `Includes`:
   - SubCollections: query a `{parentPath}/{collectionName}`
   - References (FK): leer `DocumentReference` del snapshot, hacer `GetDocumentAsync`
3. Recursivamente para `NestedIncludes`
4. Retornar estructura con todos los snapshots organizados

**Estructura de retorno propuesta:**
```csharp
public class DocumentWithIncludes
{
    public DocumentSnapshot Document { get; }
    public Dictionary<string, List<DocumentWithIncludes>> SubCollections { get; }
    public Dictionary<string, DocumentSnapshot?> References { get; }
}
```

### Fase 2: ConvertHandler Bottom-Up

El `ConvertHandler` debe:

1. Recibir `DocumentWithIncludes` en lugar de `DocumentSnapshot` simple
2. Deserializar en orden bottom-up:
   ```
   Para cada DocumentWithIncludes:
     1. Deserializar References (FK) primero
     2. Deserializar SubCollections recursivamente (bottom-up)
     3. Deserializar el documento actual con FK ya resueltas
     4. Asignar navegaciones
   ```

### Fase 3: Eliminar IncludeHandler

Una vez que ExecutionHandler + ConvertHandler manejan todo:
- Eliminar `IncludeHandler`
- Eliminar `FirestoreIncludeLoader`
- Eliminar `IIncludeLoader`

## Flujo de Datos Detallado

### Ejemplo: `db.Clientes.Include(c => c.Pedidos).ThenInclude(p => p.Articulo)`

**1. Resolver genera:**
```
ResolvedFirestoreQuery
├── CollectionPath: "Clientes"
└── Includes: [
      ResolvedInclude (Pedidos - SubCollection)
      ├── CollectionPath: "Pedidos"
      └── NestedIncludes: [
            ResolvedInclude (Articulo - Reference/FK)
            └── CollectionPath: "Articulos"
          ]
    ]
```

**2. ExecutionHandler ejecuta (top-down):**
```
1. Query: Clientes → [cli-001, cli-002]
2. Para cli-001:
   - Query: Clientes/cli-001/Pedidos → [ped-001, ped-002]
   - Para ped-001:
     - Leer DocumentReference de campo "Articulo" → "Articulos/art-001"
     - GetDocumentAsync: Articulos/art-001 → snapshot
   - Para ped-002:
     - Leer DocumentReference → "Articulos/art-002"
     - GetDocumentAsync: Articulos/art-002 → snapshot
3. Para cli-002:
   - (mismo proceso)
```

**3. ConvertHandler deserializa (bottom-up):**
```
1. Deserializar Articulos: art-001, art-002, etc.
2. Deserializar Pedidos con Articulo ya asignado
3. Deserializar Clientes con Pedidos ya asignados
4. Retornar entidades completas
```

**4. TrackingHandler:**
```
- Trackea entidades YA COMPLETAS con todas sus relaciones
```

## Consideraciones

### Referencias (FK)
- El `DocumentReference` está en el snapshot como valor del campo (navigation.Name)
- Formato: `DocumentReference` object con `.Id` y `.Path`
- Hay que leerlo ANTES de deserializar

### Optimizaciones Posibles (futuro)
- Batching de FK del mismo tipo (múltiples Articulos → una query con IN)
- Cache de entidades ya cargadas para evitar duplicados

### Tests que deben pasar
- [ ] HashSet SubCollection (3 tests) - YA PASAN
- [ ] Delete SubCollection (1 test)
- [ ] Reference con Include (4 tests)
- [ ] ThenInclude anidados
- [ ] Referencias en ComplexTypes

## Riesgos

1. **Cambio grande** - Afecta ExecutionHandler, ConvertHandler, elimina IncludeHandler
2. **Proyecciones** - Los tests de proyección están en Skip, hay que validar que no se rompen
3. **Performance** - El nuevo flujo debe ser igual o mejor

## Plan de Implementación

### Paso 1: Crear estructura DocumentWithIncludes
- Nueva clase para transportar snapshots con sus includes

### Paso 2: Extender ExecutionHandler
- Método para ejecutar Includes recursivamente
- Leer DocumentReferences de los snapshots
- Retornar DocumentWithIncludes

### Paso 3: Modificar ConvertHandler
- Recibir DocumentWithIncludes
- Deserializar bottom-up
- Asignar navegaciones durante deserialización

### Paso 4: Actualizar orden del pipeline
- Mover handlers en FirestoreServiceCollectionExtensions

### Paso 5: Eliminar código obsoleto
- IncludeHandler
- FirestoreIncludeLoader
- IIncludeLoader

### Paso 6: Tests
- Ejecutar todos los tests de SubCollection
- Ejecutar todos los tests de Reference
- Ejecutar todos los tests de integración

## Validación

Antes de implementar, confirmar:
- [ ] ¿El Resolver ya tiene toda la información necesaria en ResolvedInclude?
- [ ] ¿El DocumentReference se puede leer del snapshot antes de deserializar?
- [ ] ¿El nuevo flujo maneja correctamente los casos edge (null references, empty collections)?