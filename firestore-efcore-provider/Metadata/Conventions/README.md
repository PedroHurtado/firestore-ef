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

2. **PropertyAdded**:
   - EnumToStringConvention
   - DecimalToDoubleConvention
   - TimestampConvention

3. **ComplexPropertyAdded**:
   - GeoPointConvention

4. **NavigationAdded**:
   - DocumentReferenceNamingConvention

5. **ModelFinalizing**:
   - ComplexTypeNavigationPropertyConvention

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
