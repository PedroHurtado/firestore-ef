# Plan de Implementación: ArrayOf para Firestore Provider

## Resumen Ejecutivo

Este documento define el plan de implementación para el soporte de arrays en el provider de EF Core para Firestore, siguiendo la filosofía de **minimal configuration**.

---

## 1. Sintaxis Acordada

### 1.1 ArrayOf (embebido en documento)

| Caso | Sintaxis | Descripción |
|------|----------|-------------|
| Embedded simple | `ArrayOf(e => e.X)` | Array de ComplexType |
| Array GeoPoints | `ArrayOf(e => e.X).AsGeoPoints()` | Array de coordenadas |
| Array References | `ArrayOf(e => e.X).AsReferences()` | Array de referencias a otros documentos |
| Embedded con Reference | `ArrayOf(e => e.X, c => c.Reference(...))` | ComplexType que contiene una referencia |
| Embedded anidado | `ArrayOf(e => e.X, c => c.ArrayOf(...))` | ComplexType que contiene otro array |

### 1.2 Subcollection (ya implementado, extensión)

| Caso | Sintaxis |
|------|----------|
| Subcollection con Reference | `Subcollection(e => e.X, c => c.Reference(...))` |
| Subcollection con ArrayOf | `Subcollection(e => e.X, c => c.ArrayOf(...))` |

---

## 2. Modelos de Dominio de Ejemplo

### 2.1 Modelo Base para Tests

```csharp
// ==========================================
// ENTIDADES PRINCIPALES
// ==========================================

public class Restaurante
{
    public string Id { get; set; }
    public string Nombre { get; set; }
    
    // CASO 1: Array de Embedded simple
    public List<Horario> Horarios { get; set; }
    
    // CASO 2: Array de GeoPoints
    public List<Coordenada> ZonasCobertura { get; set; }
    
    // CASO 3: Array de References
    public List<Categoria> Categorias { get; set; }
    
    // CASO 4: Array de Embedded con Reference dentro
    public List<Certificacion> Certificaciones { get; set; }
    
    // CASO 5: Array de Embedded anidado
    public List<Menu> Menus { get; set; }
}

public class Categoria
{
    public string Id { get; set; }
    public string Nombre { get; set; }
}

public class Plato
{
    public string Id { get; set; }
    public string Nombre { get; set; }
    public decimal Precio { get; set; }
}

public class Certificador
{
    public string Id { get; set; }
    public string Nombre { get; set; }
    public string Pais { get; set; }
}

// ==========================================
// COMPLEX TYPES (Value Objects)
// ==========================================

public class Horario
{
    public string Dia { get; set; }
    public TimeSpan Apertura { get; set; }
    public TimeSpan Cierre { get; set; }
}

public class Coordenada
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class Certificacion
{
    public string Nombre { get; set; }
    public DateTime FechaObtencion { get; set; }
    public Certificador Certificador { get; set; }  // Reference
}

public class Menu
{
    public string Nombre { get; set; }
    public List<SeccionMenu> Secciones { get; set; }  // Array anidado
}

public class SeccionMenu
{
    public string Titulo { get; set; }
    public List<ItemMenu> Items { get; set; }  // Otro array anidado
}

public class ItemMenu
{
    public string Descripcion { get; set; }
    public Plato Plato { get; set; }  // Reference
}
```

### 2.2 Configuración Fluent API

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Restaurante>(entity =>
    {
        // CASO 1: Embedded simple - Sin config (convention detecta)
        entity.ArrayOf(e => e.Horarios);
        
        // CASO 2: GeoPoints
        entity.ArrayOf(e => e.ZonasCobertura).AsGeoPoints();
        
        // CASO 3: References
        entity.ArrayOf(e => e.Categorias).AsReferences();
        
        // CASO 4: Embedded con Reference
        entity.ArrayOf(e => e.Certificaciones, c => 
        {
            c.Reference(x => x.Certificador);
        });
        
        // CASO 5: Embedded anidado con Reference al final
        entity.ArrayOf(e => e.Menus, menu =>
        {
            menu.ArrayOf(m => m.Secciones, seccion =>
            {
                seccion.ArrayOf(s => s.Items, item =>
                {
                    item.Reference(i => i.Plato);
                });
            });
        });
    });
}
```

---

## 3. Estructura de Datos en Firestore

### 3.1 CASO 1: Array de Embedded Simple

**C#:**
```csharp
var restaurante = new Restaurante
{
    Id = "rest-001",
    Horarios = new List<Horario>
    {
        new Horario { Dia = "Lunes", Apertura = TimeSpan.FromHours(9), Cierre = TimeSpan.FromHours(22) },
        new Horario { Dia = "Martes", Apertura = TimeSpan.FromHours(9), Cierre = TimeSpan.FromHours(22) }
    }
};
```

**Firestore:**
```json
// /restaurantes/rest-001
{
    "nombre": "La Tasca",
    "horarios": [
        { "dia": "Lunes", "apertura": "09:00:00", "cierre": "22:00:00" },
        { "dia": "Martes", "apertura": "09:00:00", "cierre": "22:00:00" }
    ]
}
```

---

### 3.2 CASO 2: Array de GeoPoints

**C#:**
```csharp
var restaurante = new Restaurante
{
    Id = "rest-001",
    ZonasCobertura = new List<Coordenada>
    {
        new Coordenada { Latitude = 40.4168, Longitude = -3.7038 },
        new Coordenada { Latitude = 40.4200, Longitude = -3.7100 }
    }
};
```

**Firestore:**
```json
// /restaurantes/rest-001
{
    "nombre": "La Tasca",
    "zonasCobertura": [
        GeoPoint(40.4168, -3.7038),
        GeoPoint(40.4200, -3.7100)
    ]
}
```

**Nota:** `GeoPoint` es un tipo nativo de Firestore, no un objeto JSON.

---

### 3.3 CASO 3: Array de References

**C#:**
```csharp
var restaurante = new Restaurante
{
    Id = "rest-001",
    Categorias = new List<Categoria>
    {
        new Categoria { Id = "cat-001", Nombre = "Italiana" },
        new Categoria { Id = "cat-002", Nombre = "Mediterránea" }
    }
};
```

**Firestore:**
```json
// /categorias/cat-001
{ "nombre": "Italiana" }

// /categorias/cat-002
{ "nombre": "Mediterránea" }

// /restaurantes/rest-001
{
    "nombre": "La Tasca",
    "categoriasRefs": [
        Reference(/categorias/cat-001),
        Reference(/categorias/cat-002)
    ]
}
```

**Nota:** `Reference` es un tipo nativo de Firestore. El campo se renombra con sufijo `Refs` por convention.

---

### 3.4 CASO 4: Array de Embedded con Reference

**C#:**
```csharp
var restaurante = new Restaurante
{
    Id = "rest-001",
    Certificaciones = new List<Certificacion>
    {
        new Certificacion 
        { 
            Nombre = "ISO 9001", 
            FechaObtencion = new DateTime(2023, 1, 15),
            Certificador = new Certificador { Id = "cert-001" }
        }
    }
};
```

**Firestore:**
```json
// /certificadores/cert-001
{ "nombre": "Bureau Veritas", "pais": "Francia" }

// /restaurantes/rest-001
{
    "nombre": "La Tasca",
    "certificaciones": [
        {
            "nombre": "ISO 9001",
            "fechaObtencion": Timestamp(2023-01-15),
            "certificadorRef": Reference(/certificadores/cert-001)
        }
    ]
}
```

---

### 3.5 CASO 5: Array de Embedded Anidado

**C#:**
```csharp
var restaurante = new Restaurante
{
    Id = "rest-001",
    Menus = new List<Menu>
    {
        new Menu
        {
            Nombre = "Carta Principal",
            Secciones = new List<SeccionMenu>
            {
                new SeccionMenu
                {
                    Titulo = "Entrantes",
                    Items = new List<ItemMenu>
                    {
                        new ItemMenu 
                        { 
                            Descripcion = "Ración completa",
                            Plato = new Plato { Id = "plato-001" }
                        }
                    }
                }
            }
        }
    }
};
```

**Firestore:**
```json
// /platos/plato-001
{ "nombre": "Patatas Bravas", "precio": 8.50 }

// /restaurantes/rest-001
{
    "nombre": "La Tasca",
    "menus": [
        {
            "nombre": "Carta Principal",
            "secciones": [
                {
                    "titulo": "Entrantes",
                    "items": [
                        {
                            "descripcion": "Ración completa",
                            "platoRef": Reference(/platos/plato-001)
                        }
                    ]
                }
            ]
        }
    ]
}
```

---

## 4. Conventions Aplicables

### 4.1 Conventions Automáticas (Minimal Config)

| Convention | Detecta | Aplica |
|------------|---------|--------|
| `ArrayOfComplexTypeConvention` | `List<T>` donde T no es Entity ni tiene Lat/Lng | `ArrayOf` embedded |
| `ArrayOfGeoPointConvention` | `List<T>` donde T tiene `Latitude`/`Longitude` | `AsGeoPoints()` |
| `ReferenceInComplexTypeConvention` | Propiedad tipo Entity dentro de ComplexType | Anotación de Reference |

### 4.2 Configuración Explícita Requerida

| Caso | Por qué no se puede auto-detectar |
|------|-----------------------------------|
| `AsReferences()` | `List<Entity>` es ambiguo: ¿References o Subcollection? |
| Arrays anidados complejos | Demasiada profundidad para inferir correctamente |

### 4.3 Tabla de Decisión para Convention

```
¿Es List<T>?
    │
    ├─ NO → No aplica
    │
    └─ SÍ → ¿T es Entity registrada?
              │
              ├─ SÍ → REQUIERE CONFIG EXPLÍCITA
              │       (puede ser Reference[] o Subcollection)
              │
              └─ NO → ¿T tiene Latitude + Longitude?
                        │
                        ├─ SÍ → Auto-aplicar AsGeoPoints()
                        │
                        └─ NO → Auto-aplicar ArrayOf embedded
```

---

## 5. Fases de Implementación

### FASE 1: Infraestructura Base

**Objetivo:** Crear la estructura de extensiones y builders sin funcionalidad.

**Entregables:**

1. `ArrayOfEntityTypeBuilderExtensions` - Métodos de extensión vacíos
2. `ArrayOfBuilder<TEntity, TElement>` - Builder con fluent API
3. `ArrayOfElementBuilder<TElement>` - Builder para configuración interna

**Código esqueleto:**

```csharp
// ArrayOfEntityTypeBuilderExtensions.cs
public static class ArrayOfEntityTypeBuilderExtensions
{
    public static ArrayOfBuilder<TEntity, TElement> ArrayOf<TEntity, TElement>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, IEnumerable<TElement>>> propertyExpression)
        where TEntity : class
        where TElement : class
    {
        // TODO: Fase 2
        return new ArrayOfBuilder<TEntity, TElement>(builder, propertyExpression);
    }
    
    public static ArrayOfBuilder<TEntity, TElement> ArrayOf<TEntity, TElement>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, IEnumerable<TElement>>> propertyExpression,
        Action<ArrayOfElementBuilder<TElement>> configure)
        where TEntity : class
        where TElement : class
    {
        // TODO: Fase 2
        var arrayBuilder = new ArrayOfBuilder<TEntity, TElement>(builder, propertyExpression);
        configure(new ArrayOfElementBuilder<TElement>());
        return arrayBuilder;
    }
}

// ArrayOfBuilder.cs
public class ArrayOfBuilder<TEntity, TElement>
    where TEntity : class
    where TElement : class
{
    public ArrayOfBuilder<TEntity, TElement> AsGeoPoints()
    {
        // TODO: Fase 3
        return this;
    }
    
    public ArrayOfBuilder<TEntity, TElement> AsReferences()
    {
        // TODO: Fase 4
        return this;
    }
}

// ArrayOfElementBuilder.cs
public class ArrayOfElementBuilder<TElement>
    where TElement : class
{
    public ArrayOfElementBuilder<TElement> Reference<TRef>(
        Expression<Func<TElement, TRef>> propertyExpression)
        where TRef : class
    {
        // TODO: Fase 4
        return this;
    }
    
    public ArrayOfElementBuilder<TElement> ArrayOf<TNested>(
        Expression<Func<TElement, IEnumerable<TNested>>> propertyExpression)
        where TNested : class
    {
        // TODO: Fase 5
        return this;
    }
    
    public ArrayOfElementBuilder<TElement> ArrayOf<TNested>(
        Expression<Func<TElement, IEnumerable<TNested>>> propertyExpression,
        Action<ArrayOfElementBuilder<TNested>> configure)
        where TNested : class
    {
        // TODO: Fase 5
        return this;
    }
}
```

---

### FASE 2: ArrayOf Embedded Simple

**Objetivo:** Implementar `ArrayOf(e => e.X)` para ComplexTypes simples.

**Funcionalidad:**
- Detectar la propiedad List<T>
- Registrar anotación `Firestore:ArrayOf:Embedded`
- Almacenar metadata en el modelo

**Anotaciones a registrar:**

```csharp
public static class ArrayOfAnnotations
{
    public const string Prefix = "Firestore:ArrayOf:";
    public const string Type = Prefix + "Type";           // "Embedded" | "GeoPoint" | "Reference"
    public const string ElementType = Prefix + "ElementType";
    public const string NestedConfig = Prefix + "NestedConfig";
}
```

---

### FASE 3: ArrayOf GeoPoints

**Objetivo:** Implementar `ArrayOf(e => e.X).AsGeoPoints()`.

**Funcionalidad:**
- Convertir List<T> a array de GeoPoint nativo de Firestore
- T debe tener propiedades Latitude/Longitude (o Lat/Lng)

**Serialización:**

```csharp
// Entrada C#
List<Coordenada> { new(40.41, -3.70), new(40.42, -3.71) }

// Salida Firestore
[ GeoPoint(40.41, -3.70), GeoPoint(40.42, -3.71) ]
```

---

### FASE 4: ArrayOf References

**Objetivo:** Implementar `ArrayOf(e => e.X).AsReferences()` y `ArrayOf(e => e.X, c => c.Reference(...))`.

**Funcionalidad:**
- `AsReferences()`: Convertir List<Entity> a array de DocumentReference
- `Reference(...)`: Marcar propiedad dentro de ComplexType como Reference

**Serialización AsReferences:**

```csharp
// Entrada C#
List<Categoria> { new() { Id = "cat-001" }, new() { Id = "cat-002" } }

// Salida Firestore (campo renombrado a categoriasRefs por convention)
[ Reference(/categorias/cat-001), Reference(/categorias/cat-002) ]
```

**Serialización Reference en Embedded:**

```csharp
// Entrada C#
List<Certificacion> 
{ 
    new() { Nombre = "ISO", Certificador = new() { Id = "cert-001" } } 
}

// Salida Firestore
[
    {
        "nombre": "ISO",
        "certificadorRef": Reference(/certificadores/cert-001)
    }
]
```

---

### FASE 5: ArrayOf Anidado

**Objetivo:** Implementar `ArrayOf(e => e.X, c => c.ArrayOf(...))` recursivo.

**Funcionalidad:**
- Permitir configuración anidada de arrays
- Soportar profundidad arbitraria
- Combinar con Reference en cualquier nivel

**Serialización:**

```csharp
// Entrada C#
List<Menu>
{
    new Menu
    {
        Nombre = "Carta",
        Secciones = new List<SeccionMenu>
        {
            new SeccionMenu
            {
                Titulo = "Entrantes",
                Items = new List<ItemMenu>
                {
                    new ItemMenu { Descripcion = "Ración", Plato = new() { Id = "p-001" } }
                }
            }
        }
    }
}

// Salida Firestore
[
    {
        "nombre": "Carta",
        "secciones": [
            {
                "titulo": "Entrantes",
                "items": [
                    {
                        "descripcion": "Ración",
                        "platoRef": Reference(/platos/p-001)
                    }
                ]
            }
        ]
    }
]
```

---

### FASE 6: Conventions

**Objetivo:** Auto-detectar casos obvios para minimal configuration.

**Conventions a implementar:**

**ArrayOfComplexTypeConvention:**

```csharp
// Auto-detecta: List<T> donde T es ComplexType (no Entity, no tiene Lat/Lng)
// Aplica: ArrayOf embedded automáticamente
```

**ArrayOfGeoPointConvention:**

```csharp
// Auto-detecta: List<T> donde T tiene Latitude + Longitude
// Aplica: AsGeoPoints() automáticamente
```

---

### FASE 7: Integración con Subcollection

**Objetivo:** Extender Subcollection existente para soportar ArrayOf y Reference dentro.

**Sintaxis:**

```csharp
entity.Subcollection(e => e.Pedidos, pedido =>
{
    pedido.ArrayOf(p => p.Lineas, linea =>
    {
        linea.Reference(l => l.Producto);
    });
});
```

---

## 6. Resumen de Fases

| Fase | Descripción | Dependencias |
|------|-------------|--------------|
| 1 | Infraestructura Base | Ninguna |
| 2 | ArrayOf Embedded Simple | Fase 1 |
| 3 | ArrayOf GeoPoints | Fase 2 |
| 4 | ArrayOf References | Fase 2 |
| 5 | ArrayOf Anidado | Fase 2, 4 |
| 6 | Conventions | Fase 2, 3 |
| 7 | Integración Subcollection | Fase 2, 4, 5 |

---

## 7. Criterios de Aceptación por Fase

### Fase 1
- [ ] Código compila sin errores
- [ ] Sintaxis fluent funciona (aunque no hace nada)

### Fase 2
- [ ] CRUD completo de arrays embedded
- [ ] Serialización/deserialización correcta

### Fase 3
- [ ] GeoPoints se guardan como tipo nativo
- [ ] Lectura reconstruye objetos C#

### Fase 4
- [ ] References se guardan como DocumentReference
- [ ] Lazy loading funciona (si aplica)
- [ ] Campo renombrado con sufijo Refs

### Fase 5
- [ ] Anidamiento de 3+ niveles funciona
- [ ] Combinación con References funciona

### Fase 6
- [ ] ComplexType[] detectado automáticamente
- [ ] GeoPoint[] detectado automáticamente
- [ ] No conflicto con config explícita

### Fase 7
- [ ] Subcollection + ArrayOf funciona
- [ ] Subcollection + Reference funciona

---

## 8. Notas para Claude Code

1. **Una fase a la vez** - No avanzar a la siguiente hasta que la actual tenga tests verdes
2. **Tests primero** - Escribir tests antes de implementación (TDD)
3. **No modificar lo existente** - Las conventions actuales deben seguir funcionando
4. **Commits atómicos** - Un commit por funcionalidad pequeña

---

## 9. Estimación de Archivos por Fase

### FASE 1: Infraestructura Base

**Archivos nuevos:**

| Archivo | Ubicación | Descripción |
|---------|-----------|-------------|
| `ArrayOfBuilder.cs` | `Metadata/Builders/` | Builder principal con fluent API |
| `ArrayOfElementBuilder.cs` | `Metadata/Builders/` | Builder para configuración interna de elementos |
| `ArrayOfEntityTypeBuilderExtensions.cs` | `Metadata/Builders/` | Métodos de extensión para `EntityTypeBuilder<T>` |
| `ArrayOfAnnotations.cs` | `Metadata/Conventions/` | Constantes de anotaciones y métodos helper |

**Tests:**

| Archivo | Ubicación |
|---------|-----------|
| `ArrayOfBuilderTests.cs` | `tests/Fudie.Firestore.UnitTest/Metadata/` |
| `ArrayOfEntityTypeBuilderExtensionsTests.cs` | `tests/Fudie.Firestore.UnitTest/Extensions/` |

---

### FASE 2: ArrayOf Embedded Simple

**Archivos a modificar:**

| Archivo | Acción | Descripción |
|---------|--------|-------------|
| `ArrayOfBuilder.cs` | Modificar | Agregar lógica para registrar anotaciones embedded |
| `ArrayOfAnnotations.cs` | Modificar | Agregar métodos helper para leer anotaciones |
| `FirestoreDocumentSerializer.cs` | Modificar | Serialización de arrays embedded |
| `FirestoreDocumentDeserializer.cs` | Modificar | Deserialización de arrays embedded |

**Tests:**

| Archivo | Ubicación |
|---------|-----------|
| `ArrayOfEmbeddedSerializationTests.cs` | `tests/Fudie.Firestore.UnitTest/Storage/` |
| `ArrayOfEmbeddedIntegrationTests.cs` | `tests/Fudie.Firestore.IntegrationTest/ArrayOf/` |

---

### FASE 3: ArrayOf GeoPoints

**Archivos a modificar:**

| Archivo | Acción | Descripción |
|---------|--------|-------------|
| `ArrayOfBuilder.cs` | Modificar | Implementar `AsGeoPoints()` |
| `FirestoreDocumentSerializer.cs` | Modificar | Serialización a GeoPoint nativo |
| `FirestoreDocumentDeserializer.cs` | Modificar | Deserialización desde GeoPoint |

**Tests:**

| Archivo | Ubicación |
|---------|-----------|
| `ArrayOfGeoPointsTests.cs` | `tests/Fudie.Firestore.UnitTest/Storage/` |
| `ArrayOfGeoPointsIntegrationTests.cs` | `tests/Fudie.Firestore.IntegrationTest/ArrayOf/` |

---

### FASE 4: ArrayOf References

**Archivos a modificar:**

| Archivo | Acción | Descripción |
|---------|--------|-------------|
| `ArrayOfBuilder.cs` | Modificar | Implementar `AsReferences()` |
| `ArrayOfElementBuilder.cs` | Modificar | Implementar `Reference()` |
| `FirestoreDocumentSerializer.cs` | Modificar | Serialización a DocumentReference |
| `FirestoreDocumentDeserializer.cs` | Modificar | Deserialización desde DocumentReference |

**Tests:**

| Archivo | Ubicación |
|---------|-----------|
| `ArrayOfReferencesTests.cs` | `tests/Fudie.Firestore.UnitTest/Storage/` |
| `ArrayOfReferencesIntegrationTests.cs` | `tests/Fudie.Firestore.IntegrationTest/ArrayOf/` |

---

### FASE 5: ArrayOf Anidado

**Archivos a modificar:**

| Archivo | Acción | Descripción |
|---------|--------|-------------|
| `ArrayOfElementBuilder.cs` | Modificar | Implementar `ArrayOf()` recursivo |
| `FirestoreDocumentSerializer.cs` | Modificar | Serialización recursiva |
| `FirestoreDocumentDeserializer.cs` | Modificar | Deserialización recursiva |

**Tests:**

| Archivo | Ubicación |
|---------|-----------|
| `ArrayOfNestedTests.cs` | `tests/Fudie.Firestore.UnitTest/Storage/` |
| `ArrayOfNestedIntegrationTests.cs` | `tests/Fudie.Firestore.IntegrationTest/ArrayOf/` |

---

### FASE 6: Conventions

**Archivos nuevos:**

| Archivo | Ubicación | Descripción |
|---------|-----------|-------------|
| `ArrayOfComplexTypeConvention.cs` | `Metadata/Conventions/` | Auto-detecta `List<ComplexType>` |
| `ArrayOfGeoPointConvention.cs` | `Metadata/Conventions/` | Auto-detecta `List<T>` con Lat/Lng |

**Archivos a modificar:**

| Archivo | Acción | Descripción |
|---------|--------|-------------|
| `FirestoreConventionSetBuilder.cs` | Modificar | Registrar las nuevas conventions |

**Tests:**

| Archivo | Ubicación |
|---------|-----------|
| `ArrayOfComplexTypeConventionTests.cs` | `tests/Fudie.Firestore.UnitTest/Conventions/` |
| `ArrayOfGeoPointConventionTests.cs` | `tests/Fudie.Firestore.UnitTest/Conventions/` |

---

### FASE 7: Integración con Subcollection

**Archivos a modificar:**

| Archivo | Acción | Descripción |
|---------|--------|-------------|
| `SubCollectionBuilder.cs` | Modificar | Agregar soporte para `ArrayOf()` y `Reference()` |

**Tests:**

| Archivo | Ubicación |
|---------|-----------|
| `SubCollectionWithArrayOfTests.cs` | `tests/Fudie.Firestore.IntegrationTest/SubCollections/` |

---

### Resumen Total de Archivos

| Carpeta | Archivos Nuevos | Archivos Modificados |
|---------|-----------------|----------------------|
| `Metadata/Builders/` | 3 | 1 (SubCollectionBuilder) |
| `Metadata/Conventions/` | 3 | 1 (FirestoreConventionSetBuilder) |
| `Infrastructure/Internal/` | 0 | 2 (Serializer, Deserializer) |
| `tests/.../UnitTest/` | ~8 | 0 |
| `tests/.../IntegrationTest/ArrayOf/` | ~5 | 0 |
| **TOTAL** | **~19** | **~4** |
