## Contexto

Revisa el documento `@.plans/review/plan-tdd-where.md`. Los ciclos del 1 al 11 están completados y confirmados.

**Continúa con la Fase 4: Null y Boolean**

---

## Flujo de trabajo TDD

**Para cada ciclo de la fase, sigue estos pasos:**

### 1. Escribir test de integración
- Crea el test de integración según el ciclo definido en el plan
- Ejecútalo para confirmar estado **RED**

### 2. Implementación

**Si GREEN (ya pasaba):**
- Commit del test
- Continúa al paso 3

**Si RED:**
- Implementa el código necesario
- Si es necesario, crea tests unitarios
- Itera hasta conseguir GREEN
- Commit de la implementación
- Continúa al paso 3

### 3. Verificación completa
```bash
dotnet build
dotnet test
```
- Ejecuta tests de regresión para asegurar que nada se ha roto

### 4. Documentación
- Actualiza el documento `plan-tdd-where.md` con:
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