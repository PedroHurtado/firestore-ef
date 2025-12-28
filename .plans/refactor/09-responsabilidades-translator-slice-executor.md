# 09 - Responsabilidades: Translator, Slice y Executor

## Problema Detectado (2025-12-28)

Se detectó lógica de decisión dispersa entre los slices y el executor, causando:
- Tests que fallan por casos no manejados (`OrGroup` en `IncludeTranslator`)
- Duplicación de lógica de "cómo aplicar `FirestoreFilterResult`"
- El slice `FirestoreQueryExpression_Where` decide si usar `GetDocumentAsync` vs query normal (debería ser el Executor)

---

## Principio Fundamental

**La cadena de responsabilidades debe ser unidireccional y sin lógica de decisión en capas intermedias:**

```
EF Core Expression
      ↓
  Translator  →  AST puro (datos)
      ↓
    Slice     →  almacena en AST (coordinador, sin lógica)
      ↓
  Executor    →  decide qué método llamar, ejecuta
```

---

## Responsabilidades Claras

### Translator
| Aspecto | Descripción |
|---------|-------------|
| **Qué hace** | Recibe `Expression` de EF Core → Devuelve tipos AST puros |
| **Qué NO hace** | No toma decisiones, no tiene lógica condicional, no modifica estado |
| **Input** | `Expression` (MethodCallExpression, BinaryExpression, etc.) |
| **Output** | Tipos AST: `FirestoreFilterResult`, `FirestoreOrderByClause`, `IncludeInfo`, etc. |

**Ejemplo correcto:**
```csharp
// FirestoreWhereTranslator
public FirestoreFilterResult? Translate(Expression expression)
{
    // Solo traduce, no decide nada
    return new FirestoreFilterResult { ... };
}
```

### Slice (MicroDomain)
| Aspecto | Descripción |
|---------|-------------|
| **Qué hace** | Coordinador. Recibe Request, llama al Translator, almacena resultado en AST |
| **Qué NO hace** | No tiene lógica de negocio, no decide qué método del Executor llamar |
| **Input** | `TranslateXxxRequest` (Source + expresiones necesarias) |
| **Output** | `ShapedQueryExpression` actualizado con datos en el AST |

**Ejemplo correcto:**
```csharp
// FirestoreQueryExpression_Where.TranslateWhere
public static ShapedQueryExpression? TranslateWhere(TranslateWhereRequest request)
{
    var ast = (FirestoreQueryExpression)request.Source.QueryExpression;
    var translator = new FirestoreWhereTranslator();
    var filterResult = translator.Translate(request.PredicateBody);

    if (filterResult == null) return null;

    // Solo almacena, NO decide si es IdOnlyQuery
    ast.SetFilterResult(filterResult);
    return request.Source.UpdateQueryExpression(ast);
}
```

### AST (FirestoreQueryExpression)
| Aspecto | Descripción |
|---------|-------------|
| **Qué hace** | Almacena datos. Es un Value Object (o casi inmutable) |
| **Qué NO hace** | No tiene lógica de decisión |
| **Contiene** | `FilterResult`, `Includes`, `OrderByClauses`, `Limit`, `Offset` - solo datos |

**Ejemplo correcto:**
```csharp
public partial class FirestoreQueryExpression
{
    // Solo datos, sin lógica de decisión
    public FirestoreFilterResult? FilterResult { get; private set; }
    public IReadOnlyList<IncludeInfo> PendingIncludes => _pendingIncludes;
    public IReadOnlyList<FirestoreOrderByClause> OrderByClauses => _orderByClauses;

    // Setters simples
    public void SetFilterResult(FirestoreFilterResult result) => FilterResult = result;
}
```

### Executor
| Aspecto | Descripción |
|---------|-------------|
| **Qué hace** | Lee el AST y decide qué hacer. Ejecuta contra Firestore |
| **Qué NO hace** | No traduce expresiones |
| **Decide** | Qué método de `IFirestoreClientWrapper` llamar basado en el estado del AST |

**Ejemplo correcto:**
```csharp
public async Task<IReadOnlyList<TEntity>> ExecuteQueryAsync<TEntity>(
    FirestoreQueryExpression ast, CancellationToken ct)
{
    // El EXECUTOR decide qué método llamar
    if (IsIdOnlyQuery(ast))
    {
        return await ExecuteIdQueryAsync<TEntity>(ast, ct);
    }

    return await ExecuteCollectionQueryAsync<TEntity>(ast, ct);
}

private bool IsIdOnlyQuery(FirestoreQueryExpression ast)
{
    // Lógica de decisión AQUÍ, no en el slice
    return ast.FilterResult != null
        && ast.FilterResult.AndClauses.Count == 1
        && ast.FilterResult.AndClauses[0].PropertyName == "Id"
        && ast.FilterResult.AndClauses[0].Operator == FirestoreOperator.EqualTo
        && ast.FilterResult.NestedOrGroups.Count == 0
        && ast.FilterResult.OrGroup == null;
}
```

---

## Problema Actual: Lógica en el Slice

En `FirestoreQueryExpression_Where.cs` líneas 46-96 hay lógica de decisión que debería estar en el Executor:

```csharp
// INCORRECTO - esto está en el Slice
if (filterResult.IsOrGroup)
{
    if (ast.IsIdOnlyQuery)  // ← Decisión de negocio
    {
        throw new InvalidOperationException(...);
    }
    ast.AddOrFilterGroup(filterResult.OrGroup!);
    return source.UpdateQueryExpression(ast);
}

// ... más lógica de decisión sobre IdOnlyQuery
if (clauses.Count == 1 && clauses[0].PropertyName == "Id")  // ← Decisión
{
    // ... crear IdOnlyQuery
}
```

---

## Problema Actual: Tipos Dispersos

`FirestoreFilterResult` tiene:
- `AndClauses`
- `OrGroup` (top-level OR puro)
- `NestedOrGroups` (OR dentro de AND)

Pero `IncludeInfo` y `FirestoreQueryExpression` tienen:
- `_filters` (List<FirestoreWhereClause>)
- `_orFilterGroups` (List<FirestoreOrFilterGroup>)

**No tienen `OrGroup`**, por eso se pierde cuando el translator devuelve un OR puro.

### Solución
`IncludeInfo` y `FirestoreQueryExpression` deben tener:
```csharp
public FirestoreFilterResult? FilterResult { get; private set; }
```

Y el translator simplemente asigna el resultado directamente, sin desempaquetar.

---

## Plan de Corrección

### Fase 1: Unificar almacenamiento de filtros
1. `FirestoreQueryExpression`: cambiar de `_filters` + `_orFilterGroups` a `FilterResult`
2. `IncludeInfo`: cambiar de `_filters` + `_orFilterGroups` a `FilterResult`
3. Actualizar tests

### Fase 2: Mover lógica de decisión al Executor
1. Eliminar lógica de `IsIdOnlyQuery` del slice `FirestoreQueryExpression_Where`
2. Mover la decisión de qué método llamar a `FirestoreQueryExecutor`
3. El slice solo almacena `FilterResult` en el AST

### Fase 3: Actualizar Executor
1. `ExecuteQueryAsync` inspecciona `ast.FilterResult` y decide:
   - Si es IdOnlyQuery → `GetDocumentAsync`
   - Si no → `Query` normal
2. Validaciones (ej: "no se puede OR con IdOnlyQuery") también van en el Executor

---

## Beneficios

1. **Tests más simples**: El translator solo traduce, fácil de testear unitariamente
2. **Sin duplicación**: La lógica de "cómo aplicar filtros" está en un solo lugar (Executor)
3. **Responsabilidades claras**: Cada capa hace una sola cosa
4. **Extensibilidad**: Añadir nuevos tipos de queries solo afecta al Executor

---

## Regla de Oro

> **Si estás escribiendo un `if` en un Slice, probablemente esa lógica pertenece al Executor.**

Los slices son coordinadores sin lógica. Solo:
1. Extraen datos del Request
2. Llaman al Translator
3. Almacenan el resultado en el AST
4. Devuelven el Source actualizado
