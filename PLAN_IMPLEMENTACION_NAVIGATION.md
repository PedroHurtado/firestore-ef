# Plan de Implementación: Navigation Expansion (Include/ThenInclude)

**Fecha:** 1 de Diciembre de 2025
**Objetivo:** Implementar soporte para `Include` y `ThenInclude` en el provider de Firestore.
**Contexto:** El usuario necesita cargar grafos de objetos complejos (`Cliente -> Pedidos -> Lineas -> Producto`) que están modelados como Subcolecciones y Referencias.

---

## 1. Arquitectura de Solución

A diferencia de bases de datos relacionales que usan SQL JOINs, Firestore requiere una estrategia de **"Query + Fetch"**.

### Estrategia: "Late Materialization Stitching"
1.  **Translation Phase:** Detectar y almacenar los paths de navegación solicitados (`Include` / `ThenInclude`) en el `FirestoreQueryExpression`.
2.  **Execution Phase:** Ejecutar la query principal para obtener los documentos raíz (ej: `Clientes`).
3.  **Materialization Phase (Shaper):**
    *   Al materializar cada entidad raíz, el `FirestoreDocumentDeserializer` (o un componente auxiliar) consultará la lista de `PendingIncludes`.
    *   Para cada navegación pendiente, se ejecutará una operación de lectura adicional (Get Subcollection o Get Document).
    *   Los resultados se asignarán a las propiedades de navegación de la entidad.

---

## 2. Componentes Afectados

### A. `FirestoreQueryExpression` (Estado: Parcialmente Implementado)
*   **Responsabilidad:** Almacenar el árbol de navegaciones a cargar.
*   **Cambios:**
    *   Asegurar que `PendingIncludes` pueda representar una estructura jerárquica (no solo una lista plana), o usar una lista plana de paths (ej: `Pedidos`, `Pedidos.Lineas`, `Pedidos.Lineas.Producto`).
    *   *Nota:* EF Core ya nos da `IReadOnlyNavigation`, que tiene info sobre el target.

### B. `FirestoreQueryableMethodTranslatingExpressionVisitor` (Estado: En Progreso)
*   **Responsabilidad:** Interceptar llamadas a `Include` y `ThenInclude`.
*   **Cambios:**
    *   Completar la detección de `Include` (ya iniciada).
    *   Implementar soporte para `ThenInclude`. Esto es crítico para `Cliente -> Pedidos -> Lineas`.
    *   Validar que las navegaciones sean válidas (existan en el modelo).

### C. `FirestoreShapedQueryCompilingExpressionVisitor` (Estado: Pendiente)
*   **Responsabilidad:** Inyectar la lógica de carga de relaciones en el Shaper.
*   **Cambios:**
    *   En `VisitShapedQuery`, pasar la lista de `PendingIncludes` al `FirestoreQueryingEnumerable` o directamente al delegado de materialización.
    *   Generar código que llame a `IncludePopulator.PopulateAsync(...)` después de deserializar la entidad principal.

### D. `IncludePopulator` (Nuevo Componente)
*   **Responsabilidad:** Ejecutar las lecturas adicionales.
*   **Lógica:**
    ```csharp
    public async Task PopulateAsync(object entity, DocumentSnapshot doc, List<IReadOnlyNavigation> includes)
    {
        foreach (var nav in includes)
        {
            if (nav.IsSubCollection())
            {
                // Cargar Subcolección (1:N)
                // Query: doc.Reference.Collection(nav.Name).GetSnapshotAsync()
                // Deserializar hijos y asignar a la lista
                // RECURSIVIDAD: Llamar a PopulateAsync para los hijos (para soportar ThenInclude)
            }
            else if (nav.IsReference()) // Ej: Linea.Producto
            {
                // Cargar Referencia (N:1)
                // Leer campo Reference del doc
                // GetSnapshotAsync() del documento referenciado
                // Deserializar y asignar
            }
        }
    }
    ```

---

## 3. Plan de Pasos Detallado

### Paso 1: Completar `FirestoreQueryableMethodTranslatingExpressionVisitor`
*   **Tarea:** Asegurar que `Include` y `ThenInclude` pueblen correctamente `FirestoreQueryExpression.PendingIncludes`.
*   **Reto:** `ThenInclude` es un método de extensión que actúa sobre el resultado de `Include`. Necesitamos rastrear el "camino" de la navegación.
*   **Solución:** EF Core maneja esto internamente con `IncludeExpression`. Necesitamos visitar correctamente estas expresiones en el `ShapedQueryCompilingExpressionVisitor` (donde realmente se procesan los includes en EF Core moderno), no solo en el TranslatingVisitor.
    *   *Corrección:* En EF Core 8, los `Include` a menudo se quedan como `IncludeExpression` en el ShaperExpression. El `FirestoreShapedQueryCompilingExpressionVisitor` es el lugar correcto para extraerlos.

### Paso 2: Implementar Carga de Subcolecciones (1:N)
*   **Escenario:** `Cliente -> Pedidos`
*   **Lógica:**
    1.  Obtener referencia a la subcolección: `doc.Reference.Collection("Pedidos")`.
    2.  Ejecutar `GetSnapshotAsync()`.
    3.  Deserializar cada documento a `Pedido`.
    4.  Instanciar `List<Pedido>` y asignarlo a `cliente.Pedidos`.

### Paso 3: Implementar Carga Recursiva (ThenInclude)
*   **Escenario:** `Cliente -> Pedidos -> Lineas`
*   **Lógica:**
    1.  Al cargar los `Pedidos`, detectar si hay includes pendientes para `Pedido` (ej: `Lineas`).
    2.  Para cada `Pedido` cargado, repetir el proceso: cargar su subcolección `Lineas`.

### Paso 4: Implementar Carga de Referencias (N:1)
*   **Escenario:** `Linea -> Producto`
*   **Lógica:**
    1.  Al cargar `Linea`, leer el campo `Producto` (que debe ser un `DocumentReference` o un ID).
    2.  Si es `DocumentReference`: `await ref.GetSnapshotAsync()`.
    3.  Deserializar `Producto` y asignar a `linea.Producto`.

---

## 4. Consideraciones de Rendimiento (N+1)

*   **Problema:** Cargar `Pedidos` para 10 `Clientes` implica 1 query inicial + 10 queries de subcolecciones.
*   **Mitigación (Fase 2):**
    *   Para referencias (N:1), usar `WhereIn` para cargar todos los productos referenciados en un solo lote por página.
    *   Para subcolecciones (1:N), Firestore no soporta "batch get" de subcolecciones de diferentes padres fácilmente sin Collection Groups (que pueden traer datos no deseados). Por ahora, aceptaremos N+1 para subcolecciones como limitación del diseño NoSQL jerárquico.

---

## 5. Implementación Inmediata (Para pasar el test)

Nos centraremos en hacer pasar:
```csharp
context.Clientes
    .Include(c => c.Pedidos)
        .ThenInclude(p => p.Lineas)
            .ThenInclude(l => l.Producto)
```

1.  **Modificar `FirestoreShapedQueryCompilingExpressionVisitor.cs`**:
    *   Mejorar `IncludeDetectorVisitor` para capturar la estructura completa del árbol de includes.
    *   Implementar `LoadIncludes` con soporte recursivo.
2.  **Modificar `FirestoreDocumentDeserializer.cs`**:
    *   Asegurar que pueda deserializar referencias (DocumentReference -> Entidad).

