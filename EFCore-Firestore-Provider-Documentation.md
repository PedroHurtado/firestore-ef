# EF Core Provider para Google Cloud Firestore

**Estado del Proyecto:** En desarrollo activo - Fase de escritura completada  
**Última actualización:** 23 de noviembre de 2025

---

## 📋 Índice

1. [Resumen Ejecutivo](#resumen-ejecutivo)
2. [Estado Actual](#estado-actual)
3. [Arquitectura](#arquitectura)
4. [Tipos Soportados](#tipos-soportados)
5. [Ejemplos de Uso](#ejemplos-de-uso)
6. [Pendientes](#pendientes)
7. [Notas Técnicas](#notas-técnicas)

---

## 🎯 Resumen Ejecutivo

Provider personalizado de Entity Framework Core que permite usar Google Cloud Firestore (base de datos NoSQL de documentos) con el modelo relacional de EF Core. El objetivo es permitir a desarrolladores .NET usar patrones familiares de EF Core y LINQ contra Firestore, manteniendo principios de Domain-Driven Design.

**Filosofía del proyecto:**
- ✅ Dominio limpio, sin dependencias de SDKs externos
- ✅ Configuración explícita y clara
- ✅ Comportamiento automático en el provider (no en el código del usuario)
- ❌ No sobrecomplificar con patrones innecesarios

---

## ✅ Estado Actual

### Funcionalidades Completadas

#### Operaciones CRUD
- ✅ **Escritura completa:** Insert, Update, Delete
- ✅ **Transacciones:** Soporte nativo de Firestore
- ✅ **Generación de IDs:** Automática cuando no se proporciona
- ✅ **Timestamps:** `_createdAt` y `_updatedAt` automáticos

#### Mapeo de Tipos Primitivos
- ✅ String, Number, Boolean, Timestamp
- ✅ **Decimal → Double** (conversión automática)
- ✅ **Enum → String** (conversión automática)
- ✅ **Colecciones primitivas:** `List<int>`, `List<decimal>`, `List<enum>`

#### Complex Types (Value Objects)
- ✅ Complex Properties simples
- ✅ **List<ComplexType>** → Array de maps en Firestore
- ✅ Anidamiento recursivo de complex types

#### Tipos Especiales de Firestore

##### 1. GeoPoint (Coordenadas Geográficas)
```csharp
// Dominio limpio
public record Ubicacion(double Latitude, double Longitude);

// Configuración
entity.ComplexProperty(e => e.Ubicacion).HasGeoPoint();

// Resultado en Firestore
Ubicacion: GeoPoint(40.4168, -3.7038)
```

##### 2. DocumentReference (Referencias entre entidades)
```csharp
// DDD puro - entidad completa en el dominio
public class Producto
{
    public Cliente Propietario { get; set; }
}

// Configuración
entity.HasReference(p => p.Propietario);
// O con propiedad específica:
entity.HasReference(p => p.Propietario, c => c.Email);

// Resultado en Firestore
Propietario: DocumentReference("clientes/cliente-001")
```

---

## 🏗️ Arquitectura

### Componentes Principales

```
Firestore.EntityFrameworkCore/
├── Infrastructure/
│   ├── FirestoreOptionsExtension.cs          # Configuración del provider
│   ├── IFirestoreClientWrapper.cs            # Abstracción del SDK de Google
│   └── FirestoreClientWrapper.cs             # Implementación del wrapper
│
├── Storage/
│   ├── FirestoreDatabase.cs                  # Núcleo: CRUD y serialización
│   ├── FirestoreTypeMappingSource.cs         # Mapeo de tipos CLR ↔ Firestore
│   └── FirestoreTransactionManager.cs        # Gestión de transacciones
│
├── Metadata/
│   ├── IFirestoreCollectionManager.cs        # Nombres de colecciones
│   └── IFirestoreIdGenerator.cs              # Generación de IDs
│
└── Extensions/
    ├── FirestoreDbContextOptionsExtensions.cs # UseFirestore()
    └── FirestorePropertyBuilderExtensions.cs  # HasGeoPoint(), HasReference()
```

### Flujo de Serialización

```
Entity (C#)
    ↓
SerializeEntityFromEntry()
    ↓
├── SerializeProperties() ────────────→ Primitivos y colecciones
│   └── ApplyConverter() ──────────→ decimal→double, enum→string
│
├── SerializeComplexProperties() ──→ Value Objects
│   ├── HasGeoPoint? ──────────────→ Google.Cloud.Firestore.GeoPoint
│   ├── HasReference? ─────────────→ DocumentReference
│   ├── IEnumerable? ──────────────→ Array de maps
│   └── Simple ComplexType ────────→ Map
│
└── SerializeEntityReferences() ───→ Navegaciones DDD
    └── entity.HasReference() ─────→ DocumentReference
        ↓
Dictionary<string, object>
    ↓
Firestore SDK
```

---

## 📦 Tipos Soportados

### Tabla de Compatibilidad

| Tipo C# | Tipo Firestore | Conversión | Estado |
|---------|----------------|------------|--------|
| `string` | string | Directa | ✅ |
| `int`, `long`, `float` | number | Directa | ✅ |
| `double` | number | Directa | ✅ |
| `decimal` | number | decimal → double | ✅ |
| `bool` | boolean | Directa | ✅ |
| `DateTime` | timestamp | Directa | ✅ |
| `enum` | string | enum.ToString() | ✅ |
| `byte[]` | bytes | Directa | ⚠️ No testeado |
| `List<T>` (primitivos) | array | Con conversión de elementos | ✅ |
| Complex Type | map | Recursivo | ✅ |
| `List<ComplexType>` | array de maps | Iteración + serialización | ✅ |
| Entidad con `HasGeoPoint()` | geopoint | Latitude/Longitude → GeoPoint | ✅ |
| Entidad con `HasReference()` | reference | Extrae Id → DocumentReference | ✅ |
| `List<Entity>` | array de references | ❌ Pendiente | 🔴 |
| Nested reference (en VO) | reference (en map) | ❌ Pendiente | 🔴 |

---

## 💻 Ejemplos de Uso

### Configuración Inicial

```csharp
services.AddDbContext<MiContexto>(options =>
    options.UseFirestore("project-id", "credentials.json")
);
```

### Modelo de Dominio

```csharp
// Enums → String automáticamente
public enum CategoriaProducto { Electronica, Ropa, Alimentos }

// Value Objects (Complex Types)
public record Direccion
{
    public required string Calle { get; init; }
    public required string Ciudad { get; init; }
    public required string CodigoPostal { get; init; }
}

// GeoPoint
public record Ubicacion(double Latitude, double Longitude);

// Entidades
[Table("productos")]
public class Producto
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }    
    public decimal Precio { get; set; }                    // → double
    public CategoriaProducto Categoria { get; set; }       // → string
    public required Direccion DireccionAlmacen { get; set; } // → map
    public required List<decimal> DataDecimal { get; set; } // → array de doubles
    public required Cliente Propietario { get; set; }       // → DocumentReference
}

[Table("clientes")]
public class Cliente
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public required List<Direccion> Direcciones { get; set; } // → array de maps
    public required Ubicacion Ubicacion { get; set; }         // → GeoPoint
}
```

### Configuración del Modelo

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Producto>(entity =>
    {
        entity.HasKey(e => e.Id);
        
        // Complex Properties
        entity.ComplexProperty(p => p.DireccionAlmacen);
        
        // Referencias DDD
        entity.HasReference(p => p.Propietario);
        // O con propiedad específica:
        // entity.HasReference(p => p.Propietario, c => c.Email);
        
        // Conversiones manuales (opcional para colecciones)
        entity.Property(p => p.DataDecimal).HasConversion(
            v => string.Join(',', v),
            v => new List<decimal>()
        );
    });

    modelBuilder.Entity<Cliente>(entity =>
    {
        entity.HasKey(e => e.Id);
        entity.ComplexProperty(e => e.Direcciones);
        entity.ComplexProperty(e => e.Ubicacion).HasGeoPoint();
    });
}
```

### Uso en Código

```csharp
// Crear cliente
var cliente = new Cliente
{
    Id = "cliente-001",
    Nombre = "Juan Pérez",
    Email = "juan@example.com",
    Direcciones = [
        new Direccion { Calle = "Calle Principal 123", Ciudad = "Madrid", CodigoPostal = "28001" },
        new Direccion { Calle = "Avenida Secundaria 456", Ciudad = "Madrid", CodigoPostal = "28002" }
    ],
    Ubicacion = new Ubicacion(40.4168, -3.7038)
};

context.Clientes.Add(cliente);
await context.SaveChangesAsync();

// Crear producto con referencia
var producto = new Producto
{
    Id = "prod-001",
    Nombre = "Laptop Dell",
    Precio = 999.99m,
    Categoria = CategoriaProducto.Electronica,
    DireccionAlmacen = new Direccion { Calle = "Camino de Murcia, 90", Ciudad = "Cieza", CodigoPostal = "30530" },
    DataDecimal = [1.1m, 1.2m, 1.3m],
    Propietario = cliente  // ✅ DDD puro
};

context.Productos.Add(producto);
await context.SaveChangesAsync();
```

### Resultado en Firestore

```
clientes/cliente-001:
{
  Nombre: "Juan Pérez",
  Email: "juan@example.com",
  Ubicacion: GeoPoint(40.4168, -3.7038),
  Direcciones: [
    { Calle: "Calle Principal 123", Ciudad: "Madrid", CodigoPostal: "28001" },
    { Calle: "Avenida Secundaria 456", Ciudad: "Madrid", CodigoPostal: "28002" }
  ],
  _createdAt: Timestamp,
  _updatedAt: Timestamp
}

productos/prod-001:
{
  Nombre: "Laptop Dell",
  Precio: 999.99,
  Categoria: "Electronica",
  DireccionAlmacen: { Calle: "Camino de Murcia, 90", Ciudad: "Cieza", CodigoPostal: "30530" },
  DataDecimal: [1.1, 1.2, 1.3],
  Propietario: DocumentReference("clientes/cliente-001"),
  _createdAt: Timestamp,
  _updatedAt: Timestamp
}
```

---

## 🔴 Pendientes

### Prioridad Alta (Funcionalidad Crítica)

#### 1. ⚠️ **Suprimir Id del Diccionario Serializado**
**Problema:**  
Actualmente el `Id` se serializa dentro del documento, pero en Firestore el ID es el identificador del documento, no debe estar dentro del contenido.

```
❌ MAL (actual):
productos/prod-001: { Id: "prod-001", Nombre: "Laptop", ... }

✅ BIEN (esperado):
productos/prod-001: { Nombre: "Laptop", ... }
```

**Solución:**
En `SerializeProperties()`, filtrar la propiedad clave primaria:
```csharp
foreach (var property in typeBase.GetProperties())
{
    // ✅ Saltar la clave primaria
    if (property.IsPrimaryKey()) continue;
    
    var value = valueGetter(property);
    // ... resto
}
```

---

#### 2. 📚 **List<Entity> como Array de DocumentReference**
**Caso de uso:**
```csharp
public class Producto
{
    public List<Cliente> Proveedores { get; set; }  // Múltiples referencias
}

// Configuración deseada
entity.HasReference(p => p.Proveedores);

// Resultado esperado en Firestore
Proveedores: [
  DocumentReference("clientes/cliente-001"),
  DocumentReference("clientes/cliente-002"),
  DocumentReference("clientes/cliente-003")
]
```

**Implementación necesaria:**
1. Detectar en `HasReference()` si la navegación es colección
2. En `SerializeEntityReferences()`, iterar la colección
3. Extraer ID de cada entidad
4. Crear array de `DocumentReference`

---

#### 3. 🔗 **Nested References (Referencias dentro de Value Objects)**
**Caso de uso:**
```csharp
public class Direccion  // Value Object
{
    public string Calle { get; set; }
    public string Ciudad { get; set; }
    public Pais PaisRef { get; set; }  // ¡Referencia dentro de un VO!
}

public class Producto
{
    public Direccion DireccionAlmacen { get; set; }
}

// Configuración deseada
entity.ComplexProperty(p => p.DireccionAlmacen, cp =>
{
    cp.HasReference(d => d.PaisRef);
});

// Resultado esperado en Firestore
DireccionAlmacen: {
  Calle: "...",
  Ciudad: "...",
  PaisRef: DocumentReference("paises/ES")
}
```

**Complejidad:**  
Las referencias actualmente solo funcionan a nivel de entidad, no dentro de complex types. Se necesita:
1. Permitir `HasReference()` en `ComplexPropertyBuilder`
2. Propagar anotaciones a través de la jerarquía de complex types
3. En `SerializeComplexType()`, detectar y procesar referencias anidadas

---

#### 4. 🔧 **Conventions Automáticas para Conversiones**
**Problema:**  
Actualmente el usuario debe configurar conversiones manualmente para cada propiedad:

```csharp
// ❌ Repetitivo y tedioso
entity.Property(p => p.DataDecimal).HasConversion(
    v => string.Join(',', v),
    v => new List<decimal>()
);

entity.Property(p => p.DataEnum).HasConversion(
    v => string.Join(',', v),
    v => new List<CategoriaProducto>()
);

entity.Property(p => p.Categoria).HasConversion<string>();
entity.Property(p => p.Precio).HasConversion<double>();
```

**Objetivo:**  
El provider debe aplicar automáticamente las conversiones necesarias:

```csharp
// ✅ Sin configuración manual - automático por conventions
modelBuilder.Entity<Producto>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.ComplexProperty(p => p.DireccionAlmacen);
    // ✅ NO necesitas HasConversion, el provider lo hace automáticamente
});
```

**Implementación necesaria:**

1. **Crear clases de Convention:**
```csharp
public class DecimalToDoubleConvention : IPropertyAddedConvention
{
    public void ProcessPropertyAdded(
        IConventionPropertyBuilder propertyBuilder,
        IConventionContext<IConventionPropertyBuilder> context)
    {
        if (propertyBuilder.Metadata.ClrType == typeof(decimal))
        {
            propertyBuilder.HasConversion<double>();
        }
    }
}

public class EnumToStringConvention : IPropertyAddedConvention
{
    public void ProcessPropertyAdded(
        IConventionPropertyBuilder propertyBuilder,
        IConventionContext<IConventionPropertyBuilder> context)
    {
        if (propertyBuilder.Metadata.ClrType.IsEnum)
        {
            propertyBuilder.HasConversion<string>();
        }
    }
}

public class CollectionConvention : IPropertyAddedConvention
{
    public void ProcessPropertyAdded(
        IConventionPropertyBuilder propertyBuilder,
        IConventionContext<IConventionPropertyBuilder> context)
    {
        var type = propertyBuilder.Metadata.ClrType;
        
        // List<decimal> → HasConversion con manejo especial
        if (IsListOfType(type, typeof(decimal)))
        {
            // Aplicar conversión automática
        }
        
        // List<enum> → HasConversion con manejo especial
        if (IsListOfEnum(type))
        {
            // Aplicar conversión automática
        }
    }
}
```

2. **Registrar conventions en el provider:**
```csharp
public class FirestoreConventionSetBuilder : IConventionSetBuilder
{
    public ConventionSet CreateConventionSet()
    {
        var conventionSet = _sqliteConventionSetBuilder.CreateConventionSet();
        
        // Agregar nuestras conventions personalizadas
        conventionSet.PropertyAddedConventions.Add(new DecimalToDoubleConvention());
        conventionSet.PropertyAddedConventions.Add(new EnumToStringConvention());
        conventionSet.PropertyAddedConventions.Add(new CollectionConvention());
        
        return conventionSet;
    }
}
```

3. **Registrar en el provider:**
```csharp
// En FirestoreOptionsExtension
services.TryAddSingleton<IConventionSetBuilder, FirestoreConventionSetBuilder>();
```

**Beneficios:**
- ✅ Código más limpio y menos repetitivo
- ✅ Comportamiento consistente automático
- ✅ Reducción de errores por configuración olvidada
- ✅ Mejor experiencia de desarrollador

**Prioridad:** Alta - Mejora significativamente la usabilidad del provider

---

### Prioridad Media

#### 5. 🗂️ **Subcollections (Colecciones Jerárquicas)**
Firestore permite estructuras como:
```
clientes/cliente-001/pedidos/pedido-001
clientes/cliente-001/pedidos/pedido-002
```

**Desafíos:**
- Modelar en EF Core (no tiene concepto nativo de subcolecciones)
- Decidir sintaxis de configuración
- Implementar rutas jerárquicas en serialización y queries

**Posibles enfoques:**
```csharp
// Opción A: Atributo
[Subcollection("pedidos")]
public List<Pedido> Pedidos { get; set; }

// Opción B: Fluent API
entity.HasSubcollection(c => c.Pedidos, "pedidos");
```

---

#### 6. 📖 **Lectura y Deserialización**
Actualmente solo está implementada la escritura. Falta:
- Leer documentos desde Firestore
- Deserializar a entidades C#
- Aplicar conversiones inversas (double → decimal, string → enum, etc.)
- Reconstruir complex types y referencias

**Componentes a implementar:**
- `FirestoreQueryProvider`
- `FirestoreQueryable<T>`
- Métodos de deserialización inversos a la serialización actual

---

#### 7. 🔍 **LINQ Query Pipeline**
Traducir queries LINQ a Firestore queries:
```csharp
var productos = await context.Productos
    .Where(p => p.Categoria == CategoriaProducto.Electronica)
    .Where(p => p.Precio > 100)
    .OrderBy(p => p.Nombre)
    .ToListAsync();

// Debe traducirse a:
db.Collection("productos")
  .WhereEqualTo("Categoria", "Electronica")
  .WhereGreaterThan("Precio", 100)
  .OrderBy("Nombre")
```

**Limitaciones de Firestore a considerar:**
- No soporta `OR` compuestos (necesita queries separados + merge en memoria)
- Límites en operadores: máximo un rango por query
- Índices requeridos para ciertos tipos de queries

---

### Prioridad Baja (Mejoras)

- **Validación de GeoPoint:** Validar rangos de lat/lon al serializar
- **Soporte para Nullable<GeoPoint>:** Coordenadas opcionales
- **Batching optimizado:** Agrupar múltiples operaciones en un WriteBatch
- **Logging mejorado:** Diagnósticos más detallados
- **Tests unitarios:** Cobertura completa
- **Documentación:** Guía de usuario completa

---

## 📝 Notas Técnicas

### Decisiones de Diseño

#### 1. Complex Properties vs. Owned Types
**Decisión:** Usar `ComplexProperty` en lugar de `OwnsOne`  
**Razón:** `OwnsOne` crea colecciones no deseadas en Firestore. `ComplexProperty` mapea directamente a maps/objetos embebidos.

#### 2. Referencias como Navegaciones Ignoradas
**Decisión:** Usar `builder.Ignore()` + annotations para referencias  
**Razón:** Evita que EF Core intente crear navegaciones reales, pero mantiene metadata para serialización.

#### 3. Conversión de Expresiones
**Problema:** `Ignore()` requiere `Expression<Func<T, object?>>` pero recibimos `Expression<Func<T, TRelated?>>`  
**Solución:** Construir expresión con `Expression.Convert()`:
```csharp
var parameter = navigationExpression.Parameters[0];
var convertedBody = Expression.Convert(navigationExpression.Body, typeof(object));
var convertedExpression = Expression.Lambda<Func<TEntity, object?>>(convertedBody, parameter);
builder.Ignore(convertedExpression);
```

#### 4. No usar `_firestoreDb` directamente
**Decisión:** Siempre usar `_firestoreClient.Database`  
**Razón:** El wrapper proporciona abstracción y facilita testing/mocking.

---

### Limitaciones Conocidas

1. **No hay soporte para claves compuestas** - Firestore usa IDs simples como identificadores de documento
2. **Escritura únicamente** - Lectura/queries pendientes
3. **Referencias solo a nivel de entidad** - Nested references pendientes
4. **Sin validación de esquema** - Firestore es schemaless, validaciones deben ir en el dominio
5. **Transacciones limitadas a 500 documentos** - Limitación de Firestore

---

## 🚀 Próximos Pasos

### Para la próxima sesión:

1. **Suprimir Id del diccionario** (rápido, crítico)
2. **List<Entity> como referencias** (alta prioridad)
3. **Nested references** (complejidad media)
4. **Conventions automáticas** (mejora de usabilidad significativa)
5. **Diseño de subcollections** (requiere decisiones arquitectónicas)

### Orden sugerido:
```
Sesión 1: Suprimir Id + List<Entity> referencias
Sesión 2: Nested references + Conventions básicas (decimal, enum)
Sesión 3: Conventions avanzadas (colecciones) + Diseño de subcollections
Sesión 4+: Lectura y deserialización
Sesión 5+: Query pipeline
```

---

## 📚 Referencias

- [Firestore Data Model](https://firebase.google.com/docs/firestore/data-model)
- [EF Core Custom Providers](https://learn.microsoft.com/en-us/ef/core/providers/writing-a-provider)
- [Google.Cloud.Firestore SDK](https://cloud.google.com/dotnet/docs/reference/Google.Cloud.Firestore/latest)

---

**Última actualización:** 23 de noviembre de 2025  
**Versión del documento:** 1.0  
**Estado:** En desarrollo activo - Escritura completada, lectura pendiente
