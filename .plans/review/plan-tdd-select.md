# Plan TDD: Select (Proyecciones) - EF Core Firestore Provider

**Fecha:** 2025-12-15

---

## Cotejo: LINQ Select → Firestore

### Proyecciones de Campos (Soportado)

| LINQ | Firestore | Soporte |
|------|-----------|---------|
| `.Select(x => x.Nombre)` | `.Select("Nombre")` | ✅ |
| `.Select(x => new { x.Nombre, x.Precio })` | `.Select("Nombre", "Precio")` | ✅ |
| `.Select(x => new Dto { Nombre = x.Nombre })` | `.Select("Nombre")` + mapeo | ✅ |

### ComplexTypes (Soportado)

| LINQ | Firestore | Soporte |
|------|-----------|---------|
| `.Select(x => x.Direccion)` | `.Select("Direccion")` | ✅ |
| `.Select(x => x.Direccion.Calle)` | `.Select("Direccion.Calle")` | ✅ |
| `.Select(x => new { x.Direccion.Calle, x.Direccion.Ciudad })` | `.Select("Direccion.Calle", "Direccion.Ciudad")` | ✅ |

### Subcollections (Soportado con N+1)

| LINQ | Firestore | Soporte |
|------|-----------|---------|
| `.Select(c => new { c.Nombre, c.Pedidos })` | Query root + query subcollection | ✅ N+1 |
| `.Select(c => new { c.Nombre, Pedidos = c.Pedidos.Select(p => p.Total) })` | Query root + query subcollection proyectada | ✅ N+1 |
| `.Select(c => new { c.Nombre, Pedidos = c.Pedidos.Where(...) })` | Query root + query subcollection filtrada | ✅ N+1 |
| `.Select(c => new { c.Nombre, Pedidos = c.Pedidos.Where(...).OrderBy(...).Take(n) })` | Query root + query subcollection completa | ✅ N+1 |

### Cálculos y Transformaciones (No soportado - Client-side)

| LINQ | Firestore | Soporte |
|------|-----------|---------|
| `.Select(x => x.Precio * x.Cantidad)` | No | ❌ Client-side |
| `.Select(x => x.Precio * 1.21m)` | No | ❌ Client-side |
| `.Select(x => x.Nombre.ToUpper())` | No | ❌ Client-side |
| `.Select(x => x.Nombre.Substring(0, 10))` | No | ❌ Client-side |
| `.Select(x => $"{x.Nombre} - {x.Codigo}")` | No | ❌ Client-side |
| `.Select(x => x.Activo ? "Sí" : "No")` | No | ❌ Client-side |

### Agregaciones en Proyección (Parcial)

| LINQ | Firestore | Soporte |
|------|-----------|---------|
| `.Select(x => x.Items.Count())` | Count aggregation | ⚠️ Posible |
| `.Select(x => x.Items.Sum(i => i.Precio))` | No | ❌ Client-side |

### No Soportable

| LINQ | Razón |
|------|-------|
| `.Select(x => context.OtraEntidad.First(...))` | Subquery a otra colección |

---

## Paso 0: Diagnóstico

Antes de empezar, verificar qué funciona hoy:

| Operación | ¿Funciona? |
|-----------|------------|
| `.Select(x => x.Campo)` | |
| `.Select(x => new { x.A, x.B })` | |
| `.Select(x => new Dto { ... })` | |
| `.Select(x => x.ComplexType)` | |
| `.Select(x => x.ComplexType.Campo)` | |
| `.Select(c => c.Subcollection)` | |
| `.Select(c => c.Subcollection.Where(...))` | |
| `.Select(c => c.Subcollection.Select(...))` | |

---

## Ciclos TDD

### Fase 1: Campos Simples ✅ (Commit: 0e3bda9)

| Ciclo | Comportamiento | Estado |
|-------|----------------|--------|
| 1 | Select campo único | ✅ |
| 2 | Select múltiples campos (tipo anónimo) | ✅ |
| 3 | Select a DTO (clase) | ✅ |
| 3b | Select a DTO (record) | ✅ |

### Fase 2: ComplexTypes ✅ (Commit: 5bcfd30)

| Ciclo | Comportamiento | Estado |
|-------|----------------|--------|
| 4 | Select ComplexType completo | ✅ |
| 5 | Select campo de ComplexType | ✅ |
| 6 | Select múltiples campos de ComplexType | ✅ |
| 6b | Select a record desde ComplexType | ✅ |

### Fase 3: Combinación Where + Select ✅ (Commit: 5bcfd30)

| Ciclo | Comportamiento | Estado |
|-------|----------------|--------|
| 7 | Where + Select campos | ✅ |
| 7b | Where + Select tipo anónimo | ✅ |
| 8 | Where + Select a DTO (clase) | ✅ |
| 8b | Where + Select a DTO (record) | ✅ |
| 9 | Where + OrderBy + Select | ✅ |
| 9b | Where + OrderByDescending + Select | ✅ |
| 10 | Where + OrderBy + Take + Select | ✅ |
| 10b | Where + OrderBy + Skip + Take + Select | ✅ |

### Fase 4: Subcollections en Select

| Ciclo | Comportamiento | Estado | Commit |
|-------|----------------|--------|--------|
| 11 | Select con subcollection completa | ✅ | 88e3049 |
| 12 | Select con subcollection proyectada | ✅ | 88e3049 |
| 13 | Select con subcollection filtrada | ✅ | 88e3049 |
| 14 | Select con subcollection filtrada + ordenada | ✅ | 88e3049 |
| 15 | Select con subcollection filtrada + ordenada + limitada | ✅ | 88e3049 |
| 16 | Select con múltiples subcollections (navegaciones anidadas) | ✅ | 3dc0eb8 |

**Nota Ciclo 16:** Implementado soporte para navegaciones de nivel 2+ (`p.Lineas.Count()`).
El `SubcollectionAccessDetector` ahora detecta navegaciones anidadas buscando en los tipos target
de las navegaciones ya detectadas.

### Fase 5: Query Completa (integración final)

| Ciclo | Comportamiento |
|-------|----------------|
| 17 | Where root + Select campos root + subcollection con Where + Select + OrderBy + Take |

### Fase 6: No soportados (decidir)

| Operación | Decisión |
|-----------|----------|
| Cálculos (`x.A * x.B`) | ¿Client-side o NotSupportedException? |
| Transformaciones string | ¿Client-side o NotSupportedException? |
| Condicionales ternarios | ¿Client-side o NotSupportedException? |

---

## Reglas

1. Diagnóstico primero
2. Un ciclo = un comportamiento
3. Test se escribe cuando toca, no antes
4. Cada ciclo = commits (RED, GREEN, REFACTOR si aplica)
5. Si pasa en verde → siguiente ciclo

---

## Notas de Implementación

### Ejecución de Subcollections

```
Query Principal → Resultados Root
       ↓
  Por cada root:
       ↓
  [Paralelo] Query Subcollection 1, Query Subcollection 2, ...
       ↓
  Ensamblar DTO
```

### Campos a Extraer del Select

El provider debe parsear el árbol de expresión y extraer:

- **Root:** Lista de campos/paths a proyectar
- **Por cada subcollection:** Where, OrderBy, Take, Select internos

### Performance

- N+1 es inevitable pero paralelo
- Solo campos necesarios viajan por red
- Filtros se aplican en Firestore, no en memoria
