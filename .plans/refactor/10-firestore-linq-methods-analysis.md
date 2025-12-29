# Análisis de Métodos LINQ para EF Core Provider de Firestore

## Resumen del Estado Actual

- **Métodos implementados:** 17
- **Métodos pendientes:** 16

---

## Métodos Implementados ✅

| Método | Estado |
|--------|--------|
| `TranslateAny` | ✅ Implementado |
| `TranslateAverage` | ✅ Implementado |
| `TranslateCount` | ✅ Implementado |
| `TranslateDefaultIfEmpty` | ✅ Implementado |
| `TranslateFirstOrDefault` | ✅ Implementado |
| `TranslateLeftJoin` | ✅ Implementado |
| `TranslateMax` | ✅ Implementado |
| `TranslateMin` | ✅ Implementado |
| `TranslateOrderBy` | ✅ Implementado |
| `TranslateSelect` | ✅ Implementado |
| `TranslateSingleOrDefault` | ✅ Implementado |
| `TranslateSkip` | ✅ Implementado |
| `TranslateSum` | ✅ Implementado |
| `TranslateTake` | ✅ Implementado |
| `TranslateTakeLast` | ✅ Implementado (privado) |
| `TranslateThenBy` | ✅ Implementado |
| `TranslateWhere` | ✅ Implementado |

---

## Métodos Pendientes - Análisis de Viabilidad

### Probablemente Soportables en Firestore

| Método | Viabilidad | Estrategia de Implementación |
|--------|------------|------------------------------|
| `Contains` | ✅ Nativo | Firestore soporta `whereIn` (máximo 30 valores por query) |
| `LongCount` | ✅ Fácil | Idéntico a `Count`, solo cambia el tipo de retorno a `long` |
| `LastOrDefault` | ✅ Posible | Invertir `OrderBy` + `FirstOrDefault` |
| `ElementAtOrDefault` | ⚠️ Posible | `Skip(n).FirstOrDefault()` - funcional pero costoso en documentos |
| `Distinct` | ⚠️ Parcial | Solo en campos individuales, requiere evaluación en cliente |
| `All` | ⚠️ Cliente | Transformar a `!Any(x => !predicate)` - evaluación en cliente |

### Difíciles o Imposibles en Firestore

| Método | Viabilidad | Razón |
|--------|------------|-------|
| `GroupBy` | ❌ No soportado | Firestore no soporta agrupaciones server-side |
| `Join` | ❌ No soportado | Firestore es NoSQL, no hay JOINs nativos |
| `GroupJoin` | ❌ No soportado | Requiere JOIN que Firestore no soporta |
| `SelectMany` | ⚠️ Solo cliente | Flatten de colecciones solo posible en cliente |
| `Except` | ❌ No soportado | Operaciones de conjuntos no disponibles en Firestore |
| `Intersect` | ❌ No soportado | Operaciones de conjuntos no disponibles en Firestore |
| `Union` | ⚠️ Solo cliente | Combinar resultados de múltiples queries en cliente |
| `Concat` | ⚠️ Solo cliente | Similar a Union, combinar queries en cliente |
| `Reverse` | ⚠️ Solo cliente | Firestore no tiene operador reverse nativo |
| `SkipWhile` | ❌ No soportado | Firestore no soporta predicados posicionales |
| `TakeWhile` | ❌ No soportado | Firestore no soporta predicados posicionales |
| `OfType` | ⚠️ Depende | Viabilidad depende de estrategia de herencia/discriminador |
| `Cast` | ⚠️ Depende | Similar a OfType, depende del modelo de datos |

---

## Leyenda

| Símbolo | Significado |
|---------|-------------|
| ✅ | Soportado nativamente o fácil de implementar |
| ⚠️ | Posible con limitaciones o evaluación en cliente |
| ❌ | No soportado por limitaciones de Firestore |

---

## Recomendaciones

### Prioridad Alta (Implementar)
1. **`Contains`** - Muy usado en queries, Firestore lo soporta con `whereIn`
2. **`LongCount`** - Trivial, reutiliza lógica de `Count`
3. **`LastOrDefault`** - Útil y factible invirtiendo orden

### Prioridad Media (Evaluar necesidad)
4. **`Distinct`** - Implementar con evaluación en cliente
5. **`ElementAtOrDefault`** - Poco común pero posible

### Prioridad Baja (Documentar como no soportado)
- `GroupBy`, `Join`, `GroupJoin` - Limitación fundamental de NoSQL
- `SkipWhile`, `TakeWhile` - Raramente usados, imposibles en Firestore
- `Except`, `Intersect`, `Union` - Operaciones de conjuntos no aplicables

---

## Notas Técnicas

### Limitaciones de Firestore a Considerar

1. **whereIn**: Máximo 30 valores por consulta
2. **Ordenamiento**: Requiere índices compuestos para múltiples campos
3. **Desigualdades**: Solo se permite en un campo por query
4. **No hay JOINs**: Las relaciones deben manejarse con subcollections o referencias
5. **No hay agregaciones complejas**: COUNT, SUM, AVG requieren extensiones o cliente

### Estrategia para Métodos "Solo Cliente"

Para métodos que requieren evaluación en cliente, considerar:
- Lanzar `InvalidOperationException` con mensaje claro
- Implementar con `AsEnumerable()` implícito y warning
- Documentar en la API pública del provider
