# Informe: Validación Arquitectónica del Provider Firestore

## Contexto

Se realizó una revisión arquitectónica comparando el enfoque de materialización del provider Firestore contra el enfoque "ortodoxo" usado por InMemory y otros providers relacionales.

## Dos Enfoques de Materialización

### Enfoque Ortodoxo (InMemory/SQL Server)
```
DocumentSnapshot
       ↓
   ValueBuffer (object[] con índices fijos)
       ↓
   EntityShaperExpression (generado por EF Core)
       ↓
   InjectEntityMaterializers() transforma el shaper
       ↓
   Código generado automático que:
       - Lee valores por índice del ValueBuffer
       - Llama a QueryContext.StartTracking()
       - Maneja identity resolution
       - Hidrata shadow properties
       - Crea proxies si están habilitados
       ↓
   Entidad tracked
```

### Enfoque Firestore (Actual)
```
DocumentSnapshot
       ↓
   Deserializer.DeserializeEntity<T>() 
       ↓
   dbContext.Attach(entity)
       ↓
   SetShadowForeignKeys()
       ↓
   Entidad tracked
```

## Análisis de Riesgos

### Riesgos Eliminados (No Aplican a Firestore)

| Escenario | Razón de Exclusión |
|-----------|-------------------|
| Identity Resolution en JOINs | Firestore es NoSQL, no hay JOINs |
| Subqueries complejas | No soportadas en Firestore |
| Proyecciones multi-tabla | No hay JOINs |
| Cartesian explosion | No hay JOINs |

### Riesgos Evaluados

| Escenario | Riesgo | Estado | Notas |
|-----------|--------|--------|-------|
| Value Converters | ALTO | ✅ **VERIFICADO** | Enums → String funcionan correctamente |
| Lazy Loading Proxies | MEDIO | ✅ **VERIFICADO** | Corregido y funcionando |
| Explicit Loading | MEDIO | ✅ **VERIFICADO** | Tests pasan |
| Eager Loading (Include) | MEDIO | ✅ **VERIFICADO** | Tests pasan |
| Change Tracking | MEDIO | ✅ **FUNCIONA** | `Attach()` + snapshots funcionan |
| Shadow FK Properties | ALTO | ✅ **VERIFICADO** | `SetShadowForeignKeys()` funciona |
| Query Filters | **ALTO** | ⏳ **PENDIENTE** | Requerido, no implementado aún |
| Herencia (TPH/TPT/TPC) | MEDIO | ➖ **NO REQUERIDO** | Fuera de scope |
| Owned Types | MEDIO | ➖ **NO REQUERIDO** | Fuera de scope |
| Interceptors/Eventos | BAJO | ⚠️ **NO VERIFICADO** | Probablemente no dispara `MaterializationInterceptor` |

## Decisión Arquitectónica

### ¿Es válido el enfoque heterodoxo?

**SÍ**, por las siguientes razones:

1. **Firestore no es relacional** — Los escenarios más complejos que justifican el pipeline ortodoxo (JOINs, subqueries, identity resolution multi-tabla) no aplican.

2. **Funcionalidad equivalente** — `Attach()` + `SetShadowForeignKeys()` logra el mismo resultado que `InjectEntityMaterializers()` + `ValueBuffer` para el scope de Firestore.

3. **Features críticas funcionan:**
   - ✅ CRUD completo
   - ✅ Eager Loading (Include/ThenInclude)
   - ✅ Explicit Loading
   - ✅ Lazy Loading con Proxies
   - ✅ Value Converters
   - ✅ Shadow Properties
   - ✅ Change Tracking

4. **Migrar al enfoque ortodoxo requeriría:**
   - Reestructurar toda la deserialización
   - Mapear DocumentSnapshot → ValueBuffer con índices exactos
   - Cambio arquitectónico significativo sin beneficio funcional claro

### Limitaciones Aceptadas

| Feature | Estado | Justificación |
|---------|--------|---------------|
| Herencia (TPH/TPT/TPC) | No soportado | Fuera de scope del proyecto |
| Owned Types | No soportado | Fuera de scope del proyecto |
| MaterializationInterceptor | Probablemente no funciona | Bajo impacto, documentar |

## Trabajo Pendiente

### Query Filters (Prioridad Alta)

Los Query Filters son necesarios para:
- Soft-delete (`IsDeleted`)
- Multi-tenancy (`TenantId`)

**Dónde implementar:** `FirestoreQueryableMethodTranslatingExpressionVisitor`

**Cómo funciona:** EF Core registra filtros en el modelo:
```csharp
modelBuilder.Entity<Articulo>().HasQueryFilter(a => !a.IsDeleted);
```

El `QueryableMethodTranslatingExpressionVisitor` debe inyectar automáticamente el filtro como un `.Where()` adicional en cada query.

**Referencia:** Investigar cómo InMemory aplica `QueryFilter` en su visitor.

## Conclusión

El enfoque arquitectónico del provider Firestore es **válido y apropiado** para su contexto NoSQL. No es una "chapuza" sino una adaptación pragmática que:

1. Respeta las limitaciones de Firestore
2. Aprovecha las capacidades de EF Core donde tiene sentido
3. Implementa manualmente donde el pipeline estándar no aplica

**Recomendación:** Documentar explícitamente en el README/docs que:
- El provider usa materialización custom (no ValueBuffer)
- Herencia y Owned Types no están soportados
- Query Filters están en roadmap

## Próximos Pasos

1. ⏳ Implementar Query Filters
2. 📝 Documentar limitaciones conocidas
3. 🧪 Añadir tests negativos para features no soportados (errores claros)