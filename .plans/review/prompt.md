## Contexto 
    @./plans/.plans/refactor/ast-executor-refactor.md

    Ejemplo de microdomain

    FirestoreQueryExpression_OrderBy.cs

    Ejemplo de Translator

    FirestoreOrderByTranslator.cs

    Contenido a refactorizar

    FirestoreQueryableMethodTranslatingExpressionVisitor.CS



Enpieza con la siguiente fase

### Fase 1: Campos Simples

### 1.5 FirestoreIncludeTranslator y IncludeMicrodomain


**Qué traduce:** `Include`, `ThenInclude`, Filtered Includes

**Commit:**

---

## Flujo de trabajo TDD

**Para cada fase, sigue estos pasos:**

### 1. Escribir test de unitarios
- Crea los test unitarios el ciclo definido en el plan
- Ejecútalo para confirmar estado **RED**
- Te esperas a revisar Test por parte mia

### 2. Implementación

**Si GREEN (ya pasaba):**
- Commit de la fase

**Si RED:**
- Implementa el código necesario
- Si es necesario, crea tests unitarios
- Itera hasta conseguir GREEN
- Espera revision mia
- Commit de la implementación


### 3. Verificación completa
```bash
dotnet build
dotnet test
```
- Ejecuta tests de regresión para asegurar que nada se ha roto

### 4. Documentación
- Actualiza el documento `ast-executor-refactor` con:
  - Marca la tarea como completada
  - Anota el ID del commit asociado
- Commitea el documento actualizado

### 5. Cierre de ciclo
- Proporciona un breve resumen de lo implementado
- Solicita confirmación para continuar con el siguiente ciclo

---

**REGLAS DE COMMITS:**
- Los mensajes NO deben contener referencias a Claude, IA, "Generated with", ni co-autorías
- Usar mensajes descriptivos del cambio técnico realizado