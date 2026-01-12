# Firestore Conventions

Este directorio contiene todas las conventions que se aplican automáticamente al modelo de EF Core para Firestore, siguiendo el patrón de `ConventionSetBuilder` de Entity Framework Core.

## Arquitectura

El sistema de conventions utiliza el patrón de EF Core:

1. **FirestoreConventionSetBuilder** - Extiende `ProviderConventionSetBuilder` y registra todas las conventions
2. **Conventions individuales** - Cada una implementa interfaces específicas de EF Core (`IEntityTypeAddedConvention`, `IPropertyAddedConvention`, etc.)
3. Las conventions se ejecutan automáticamente en diferentes fases de la construcción del modelo

## Conventions Implementadas

### 1. PrimaryKeyConvention
- **Tipo**: `IEntityTypeAddedConvention`
- **Cuándo se ejecuta**: Al agregar una entidad al modelo
- **Qué hace**: Detecta automáticamente propiedades llamadas `Id` o `{EntityName}Id` como clave primaria

### 2. CollectionNamingConvention
- **Tipo**: `IEntityTypeAddedConvention`
- **Cuándo se ejecuta**: Al agregar una entidad al modelo
- **Qué hace**: Pluraliza automáticamente el nombre de la entidad para el nombre de la colección
  - `Producto` → `Productos`
  - `Cliente` → `Clientes`
- **Requisito**: Paquete NuGet `Humanizer.Core`

### 3. EnumToStringConvention
- **Tipo**: `IPropertyAddedConvention`
- **Cuándo se ejecuta**: Al agregar una propiedad al modelo
- **Qué hace**: Convierte automáticamente todas las propiedades enum a string

### 4. DecimalToDoubleConvention
- **Tipo**: `IPropertyAddedConvention`
- **Cuándo se ejecuta**: Al agregar una propiedad al modelo
- **Qué hace**: Convierte automáticamente decimal a double (Firestore no soporta decimal nativamente)

### 5. TimestampConvention
- **Tipo**: `IPropertyAddedConvention`
- **Cuándo se ejecuta**: Al agregar una propiedad al modelo
- **Qué hace**: Detecta propiedades DateTime con nombres como:
  - `CreatedAt`, `CreatedDate`, `CreatedOn`
  - `UpdatedAt`, `UpdatedDate`, `UpdatedOn`
  - `ModifiedAt`, `DeletedAt`, etc.
- **Nota**: Actualmente preparada para futuras configuraciones específicas

### 6. GeoPointConvention
- **Tipo**: `IComplexPropertyAddedConvention`
- **Cuándo se ejecuta**: Al agregar una complex property al modelo
- **Qué hace**: Detecta automáticamente propiedades que parecen coordenadas:
  - Nombres: `Location`, `Coordinates`, `Position`, `GeoLocation`, etc.
  - Debe tener propiedades `Latitude`/`Lat` y `Longitude`/`Lng`/`Lon`
  - Aplica anotaciones `Firestore:GeoPoint`

### 7. DocumentReferenceNamingConvention
- **Tipo**: `INavigationAddedConvention`
- **Cuándo se ejecuta**: Al agregar una navigation property al modelo
- **Qué hace**: Estandariza el naming de los campos DocumentReference como `{PropertyName}Ref`

### 8. ComplexTypeNavigationPropertyConvention
- **Tipo**: `IModelFinalizingConvention`
- **Cuándo se ejecuta**: Al finalizar la construcción del modelo
- **Qué hace**: Ignora automáticamente navigation properties dentro de ComplexTypes (value objects)
  - Detecta propiedades que son entidades
  - Detecta colecciones de entidades (`ICollection<>`, `IEnumerable<>`, `List<>`)
  - Las elimina del complex type para evitar errores

## Conventions de ArrayOf

Las siguientes conventions manejan arrays de elementos en documentos Firestore. Los arrays en Firestore pueden contener diferentes tipos de datos y estas conventions los detectan y configuran automáticamente.

### 9. ArrayOfConvention
- **Tipo**: `IEntityTypeAddedConvention`, `IModelFinalizingConvention`
- **Cuándo se ejecuta**: Al agregar una entidad y al finalizar el modelo
- **Qué hace**: Auto-detecta propiedades `List<T>`, `HashSet<T>`, `ICollection<T>` y aplica la configuración ArrayOf apropiada
- **Tipos soportados**:
  - **ArrayOf GeoPoint**: `List<T>` donde T tiene propiedades `Latitude`/`Longitude` sin Id
  - **ArrayOf Embedded**: `List<T>` donde T es un ComplexType/ValueObject (clase sin Id)
  - **ArrayOf Reference**: `List<T>` donde T es una entidad registrada en el modelo
- **Comportamiento**:
  1. En `EntityTypeAdded`: Detecta y marca GeoPoints y Embedded, ignora la propiedad para evitar errores de EF Core
  2. En `ModelFinalizing`: Detecta References (cuando las entidades ya están registradas), limpia entidades descubiertas incorrectamente

#### Ejemplo de detección automática:
```csharp
// DbContext SIN configuración - todo auto-detectado por conventions
public class MiDbContext : DbContext
{
    public DbSet<Oficina> Oficinas => Set<Oficina>();
    // Sin OnModelCreating - las conventions detectan todo automáticamente
}

public class Oficina
{
    public string Id { get; set; }

    // ArrayOf Embedded (auto-detectado: Direccion no tiene Id)
    public List<Direccion> Direcciones { get; set; }

    // ArrayOf GeoPoint (auto-detectado: tiene Lat/Lng sin Id)
    public List<Ubicacion> Ubicaciones { get; set; }
}

public class Direccion { public string Calle { get; set; } /* sin Id */ }
public class Ubicacion { public double Latitude { get; set; } public double Longitude { get; set; } }
```

#### Configuración explícita con Fluent API:

Cuando necesitas control explícito o la auto-detección no es suficiente, usa `ArrayOf()` en `OnModelCreating`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Tienda>(entity =>
    {
        // ArrayOf Embedded (explícito)
        entity.ArrayOf(e => e.Horarios);

        // ArrayOf GeoPoints
        entity.ArrayOf(e => e.ZonasCobertura).AsGeoPoints();

        // ArrayOf References
        entity.ArrayOf(e => e.Etiquetas).AsReferences();
    });
}
```

#### Configuración con elementos anidados (References dentro de Embedded):

Para configurar referencias dentro de los elementos embebidos:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Restaurante>(entity =>
    {
        // CASO 4: Embedded con Reference dentro
        entity.ArrayOf(e => e.Certificaciones, cert =>
        {
            cert.Reference(c => c.Certificador);
        });

        // CASO 5: Embedded anidado con Reference al final del path
        // Restaurante → Menus → Secciones → Items → Plato (Reference)
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

#### ¿Qué requiere configuración explícita?

| Caso | Auto-detectado | Requiere config |
|------|----------------|-----------------|
| `List<T>` donde T no tiene Id | ✅ Embedded | - |
| `List<T>` donde T tiene Lat/Lng | ✅ GeoPoint | - |
| `List<T>` donde T tiene DbSet | ✅ Reference | - |
| Reference dentro de Embedded | ❌ | `ArrayOf(x, c => c.Reference(...))` |
| Arrays anidados con Reference | ❌ | Configuración anidada completa |

#### Métodos disponibles en ArrayOfBuilder:
- `.AsGeoPoints()` - Configura como array de GeoPoints nativos de Firestore
- `.AsReferences()` - Configura como array de DocumentReferences

#### Métodos disponibles en ArrayOfElementBuilder:
- `.Reference(x => x.Property)` - Configura una propiedad como DocumentReference
- `.ArrayOf(x => x.Property, configure)` - Configura un array anidado dentro del elemento

### 10. ListDecimalToDoubleArrayConvention
- **Tipo**: `IPropertyAddedConvention`
- **Cuándo se ejecuta**: Al agregar una propiedad al modelo
- **Qué hace**: Convierte automáticamente `List<decimal>` y `List<decimal?>` a `List<double>` (Firestore no soporta decimal)

### 11. ListEnumToStringArrayConvention
- **Tipo**: `IPropertyAddedConvention`
- **Cuándo se ejecuta**: Al agregar una propiedad al modelo
- **Qué hace**: Convierte automáticamente `List<TEnum>` y `List<TEnum?>` a `List<string>`

### ArrayOfAnnotations (Helper)
Clase estática que define las anotaciones y extension methods para ArrayOf:
- `Firestore:ArrayOf:Type:{PropertyName}` - Tipo de array (Embedded, GeoPoint, Reference)
- `Firestore:ArrayOf:ElementClrType:{PropertyName}` - Tipo CLR del elemento

Extension methods disponibles:
- `entityType.IsArrayOf(propertyName)` - Verifica si es ArrayOf
- `entityType.IsArrayOfEmbedded(propertyName)` - Verifica si es ArrayOf Embedded
- `entityType.IsArrayOfGeoPoint(propertyName)` - Verifica si es ArrayOf GeoPoint
- `entityType.IsArrayOfReference(propertyName)` - Verifica si es ArrayOf Reference
- `entityType.GetArrayOfType(propertyName)` - Obtiene el tipo de ArrayOf
- `entityType.GetArrayOfElementClrType(propertyName)` - Obtiene el tipo CLR del elemento

### ConventionHelpers (Helper)
Clase estática con métodos de utilidad reutilizables:
- `HasPrimaryKeyStructure(type)` - Verifica si tiene propiedad Id o {TypeName}Id
- `IsGeoPointType(type)` - Verifica si es GeoPoint puro (Lat/Lng sin Id)
- `IsGenericCollection(type)` - Verifica si es colección genérica
- `GetCollectionElementType(type)` - Obtiene el tipo de elemento
- `IsPrimitiveOrSimpleType(type)` - Verifica si es tipo primitivo/simple

## Integración

Las conventions ya están integradas en `FirestoreConventionSetBuilder`. Para que funcionen, solo necesitas asegurarte de que tu provider esté usando este `ConventionSetBuilder`:

```csharp
// En tu método de extensión para configurar Firestore
services.AddDbContext<MiDbContext>(options =>
{
    options.UseFirestore(projectId);
});
```

El `FirestoreConventionSetBuilder` se registra automáticamente cuando usas `.UseFirestore()`.

## Orden de Ejecución

Las conventions se ejecutan en el siguiente orden según la fase de construcción del modelo:

1. **EntityTypeAdded**:
   - PrimaryKeyConvention
   - CollectionNamingConvention
   - ArrayOfConvention (detecta GeoPoints y Embedded, ignora propiedades)

2. **PropertyAdded**:
   - EnumToStringConvention
   - DecimalToDoubleConvention
   - TimestampConvention
   - ListEnumToStringArrayConvention
   - ListDecimalToDoubleArrayConvention

3. **ComplexPropertyAdded**:
   - GeoPointConvention
   - ComplexTypeNavigationPropertyConvention

4. **NavigationAdded**:
   - DocumentReferenceNamingConvention

5. **ModelFinalizing**:
   - ArrayOfConvention (detecta References, limpia entidades incorrectas)

## Requisitos

### Paquetes NuGet necesarios:
```bash
dotnet add package Humanizer.Core
```

## Ejemplos

### Antes de las conventions:
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Producto>(entity =>
    {
        entity.HasKey(e => e.Id);
        entity.ToTable("Productos");
        entity.Property(e => e.Precio).HasConversion<double>();
        entity.Property(e => e.Estado).HasConversion<string>();
        
        entity.ComplexProperty(e => e.Direccion, direccion =>
        {
            direccion.Ignore(d => d.Ciudad); // Si Ciudad es una navigation property
            
            direccion.ComplexProperty(d => d.Coordenadas, coords =>
            {
                // Configurar GeoPoint manualmente
            });
        });
    });
}
```

### Después de las conventions:
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // ¡Ya no necesitas configurar nada de esto!
    // Las conventions lo hacen automáticamente:
    // ✓ Id detectado como clave primaria
    // ✓ Colección "Productos" pluralizada
    // ✓ Precio (decimal) convertido a double
    // ✓ Estado (enum) convertido a string
    // ✓ Navigation properties en Direccion ignoradas
    // ✓ Coordenadas detectadas como GeoPoint
}
```

## Deshabilitación

Si necesitas deshabilitar una convention específica, puedes modificar `FirestoreConventionSetBuilder.cs` y comentar la línea correspondiente:

```csharp
public override ConventionSet CreateConventionSet()
{
    var conventionSet = base.CreateConventionSet();

    // Deshabilitar CollectionNamingConvention comentando esta línea:
    // conventionSet.EntityTypeAddedConventions.Add(new CollectionNamingConvention());

    return conventionSet;
}
```

## Extensión

Para agregar tu propia convention:

1. Implementa la interfaz apropiada (`IEntityTypeAddedConvention`, `IPropertyAddedConvention`, etc.)
2. Agrégala al `ConventionSet` en `FirestoreConventionSetBuilder.CreateConventionSet()`

Ejemplo:
```csharp
public class MiCustomConvention : IPropertyAddedConvention
{
    public void ProcessPropertyAdded(
        IConventionPropertyBuilder propertyBuilder,
        IConventionContext<IConventionPropertyBuilder> context)
    {
        // Tu lógica aquí
    }
}

// En FirestoreConventionSetBuilder:
conventionSet.PropertyAddedConventions.Add(new MiCustomConvention());
```
