# Materializer Implementation Specification

## Objetivo

Implementar una clase que tome el resultado del `SnapshotShaper` (diccionarios jerárquicos) y los convierta en instancias CLR tipadas (entidades, DTOs, records, tipos anónimos).

## Contexto

El `SnapshotShaper` ya resolvió la estructura jerárquica de los datos. El Materializer solo necesita:
1. Descubrir cómo crear instancias del tipo destino (constructor + setters/backing fields)
2. Convertir valores de Firestore a tipos CLR
3. Cachear la estrategia de materialización por tipo para evitar reflexión repetida

## Input

- `ShapedResult shaped` - Resultado del SnapshotShaper con `Items: List<Dictionary<string, object?>>`
- `Type targetType` - Tipo CLR destino (entidad, DTO, record, tipo anónimo)

## Output

- `List<object>` - Instancias materializadas del targetType

## Consideraciones DDD

1. **Constructores protected/private**: Buscar todos los constructores, no solo públicos
2. **Backing fields**: Propiedades sin setter público usan backing field (`_categories` para `Categories`)
3. **Constructores parciales**: Constructor puede tener solo algunos parámetros, resto por setter/backing field
4. **Prioridad constructor**: Preferir constructores con más parámetros (más específicos)

## Dependencias

```csharp
public interface IFirestoreValueConverter
{
    object? ToFirestore(object? value, Type? enumType = null);
    object? FromFirestore(object? value, Type targetType);
}
```

El `IFirestoreValueConverter.FromFirestore` ya maneja conversiones como:
- `Timestamp` → `DateTime`
- `double` → `decimal`
- `string` → `enum`
- `DocumentReference` → `Guid` (si es necesario)

## Interfaz

```csharp
public interface IMaterializer
{
    List<object> Materialize(ShapedResult shaped, Type targetType);
}
```

## Records de Estrategia

```csharp
public record MaterializationStrategy(
    ConstructorInfo Constructor,
    IReadOnlyList<ConstructorParamMapping> ConstructorParams,
    IReadOnlyList<MemberMapping> MemberSetters
);

public record ConstructorParamMapping(
    string DictKey,
    int ParamIndex,
    Type TargetType
);

public record MemberMapping(
    string DictKey,
    MemberInfo Member,  // PropertyInfo o FieldInfo (backing field)
    Type TargetType
);
```

## Clase a Implementar

```csharp
public class Materializer : IMaterializer
{
    private static readonly ConcurrentDictionary<Type, Lazy<MaterializationStrategy>> _strategyCache = new();
    
    private readonly IFirestoreValueConverter _converter;

    public Materializer(IFirestoreValueConverter converter);

    public List<object> Materialize(ShapedResult shaped, Type targetType);
    
    private static MaterializationStrategy GetOrCreateStrategy(Type type);
    
    private static MaterializationStrategy DiscoverStrategy(Type type);
    
    private static ConstructorInfo FindBestConstructor(Type type);
    
    private static string GetBackingFieldName(string propertyName);
    
    private object ExecuteStrategy(Dictionary<string, object?> dict, MaterializationStrategy strategy);
    
    private object? MaterializeValue(object? value, Type targetType);
    
    private object MaterializeCollection(IList items, Type collectionType);
}
```

## Descripción de Métodos

### `Materialize(shaped, targetType)`
1. Para cada diccionario en `shaped.Items`
2. Llamar a `GetOrCreateStrategy(targetType)` para obtener estrategia cacheada
3. Llamar a `ExecuteStrategy(dict, strategy)` para crear instancia
4. Agregar a lista resultado
5. Retornar lista de instancias

### `GetOrCreateStrategy(type)`
1. Buscar en `_strategyCache` usando `GetOrAdd` con `Lazy<>`
2. El `Lazy<>` garantiza que `DiscoverStrategy` se ejecuta una sola vez por tipo
3. Retornar `strategy.Value`

### `DiscoverStrategy(type)`
1. Llamar a `FindBestConstructor(type)`
2. Crear lista de `ConstructorParamMapping`:
   - Para cada parámetro del constructor
   - DictKey = PascalCase del nombre del parámetro (ej: "name" → "Name")
   - ParamIndex = índice en el constructor
   - TargetType = tipo del parámetro
3. Obtener propiedades del tipo con `GetProperties()`
4. Para cada propiedad que NO está en los parámetros del constructor:
   - Si tiene setter público → crear `MemberMapping` con PropertyInfo
   - Si NO tiene setter → buscar backing field con `GetBackingFieldName`
     - Buscar campo con `GetField(backingFieldName, BindingFlags.NonPublic | BindingFlags.Instance)`
     - Si encuentra → crear `MemberMapping` con FieldInfo
5. Retornar `MaterializationStrategy(constructor, constructorParams, memberSetters)`

### `FindBestConstructor(type)`
1. Obtener todos los constructores:
   ```csharp
   type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
   ```
2. Ordenar por número de parámetros descendente
3. Retornar el primero (más parámetros = más específico)
4. Si no hay constructores → throw `InvalidOperationException`

### `GetBackingFieldName(propertyName)`
1. Convención: `"_" + char.ToLower(propertyName[0]) + propertyName[1..]`
2. Ejemplo: "Categories" → "_categories"

### `ExecuteStrategy(dict, strategy)`
1. Crear array de argumentos: `new object?[strategy.Constructor.GetParameters().Length]`
2. Para cada `ConstructorParamMapping` en `strategy.ConstructorParams`:
   - Obtener valor de `dict[mapping.DictKey]` (o null si no existe)
   - Llamar a `MaterializeValue(value, mapping.TargetType)`
   - Asignar a `args[mapping.ParamIndex]`
3. Crear instancia: `strategy.Constructor.Invoke(args)`
4. Para cada `MemberMapping` en `strategy.MemberSetters`:
   - Obtener valor de `dict[mapping.DictKey]` (o null si no existe)
   - Llamar a `MaterializeValue(value, mapping.TargetType)`
   - Si es `PropertyInfo` → `((PropertyInfo)mapping.Member).SetValue(instance, value)`
   - Si es `FieldInfo` → `((FieldInfo)mapping.Member).SetValue(instance, value)`
5. Retornar instancia

### `MaterializeValue(value, targetType)`
1. Si `value == null` → retornar null
2. Si `value` es `Dictionary<string, object?>`:
   - Es ComplexType o entidad anidada
   - Llamar a `GetOrCreateStrategy(targetType)`
   - Llamar a `ExecuteStrategy((Dictionary<string, object?>)value, strategy)`
   - Retornar instancia
3. Si `value` es `IList` y contiene diccionarios:
   - Es colección de entidades/ComplexTypes
   - Llamar a `MaterializeCollection((IList)value, targetType)`
   - Retornar colección
4. Si no → es tipo simple:
   - Llamar a `_converter.FromFirestore(value, targetType)`
   - Retornar valor convertido

### `MaterializeCollection(items, collectionType)`
1. Determinar tipo de elemento:
   - Si `collectionType` es `List<T>` o `IList<T>` o `IReadOnlyCollection<T>` o `IEnumerable<T>`
   - Obtener `T` con `collectionType.GetGenericArguments()[0]`
2. Crear lista concreta: `typeof(List<>).MakeGenericType(elementType)` → `Activator.CreateInstance()`
3. Obtener método `Add` de la lista
4. Para cada item en `items`:
   - Si es `Dictionary<string, object?>` → `MaterializeValue(item, elementType)`
   - Agregar a lista con `Add`
5. Retornar lista

## Registro de Servicios

Reemplazar:
```csharp
.TryAddScoped<IFirestoreDocumentDeserializer, FirestoreDocumentDeserializer>()
.TryAddScoped<IProjectionMaterializer, ProjectionMaterializer>()
```

Por:
```csharp
.TryAddSingleton<IMaterializer, Materializer>()
```

## Ejemplo de Flujo

```
Input:
  targetType = typeof(Menu)
  dict = {
    "Id": "abc123",
    "Name": "Carta de Verano",
    "Categories": [
      { "Id": "cat1", "Name": "Entrantes", "Items": [...] }
    ]
  }

DiscoverStrategy(Menu):
  Constructor: Menu(Guid id, string name)
  ConstructorParams: [
    { DictKey: "Id", ParamIndex: 0, Type: Guid },
    { DictKey: "Name", ParamIndex: 1, Type: string }
  ]
  MemberSetters: [
    { DictKey: "Categories", Member: FieldInfo(_categories), Type: List<MenuCategory> }
  ]

ExecuteStrategy:
  1. args[0] = MaterializeValue("abc123", Guid) → Guid.Parse("abc123")
  2. args[1] = MaterializeValue("Carta de Verano", string) → "Carta de Verano"
  3. instance = new Menu(args[0], args[1])
  4. categories = MaterializeCollection([...], List<MenuCategory>)
  5. _categories.SetValue(instance, categories)
  6. return instance
```

## Tests

Crear tests para:
1. Tipo con constructor sin parámetros y propiedades públicas
2. Tipo con constructor con parámetros (DDD)
3. Tipo con backing fields (`IReadOnlyCollection<T>`)
4. Tipo con constructor parcial (algunos params, otros por setter)
5. ComplexType anidado (DepositPolicy)
6. Colección de entidades (Categories)
7. Record (inmutable)
8. Tipo anónimo
9. Entidad con navegación que tiene navegación (Menu → Category → Items → MenuItem)
10. Cache: verificar que `DiscoverStrategy` se llama solo una vez por tipo

## Notas de Implementación

1. **BindingFlags**: Usar siempre `BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance` para encontrar miembros protected/private
2. **Null handling**: El diccionario puede no tener todas las keys, tratar como null
3. **Convención PascalCase**: Los parámetros del constructor en C# son camelCase, las keys del diccionario son PascalCase
4. **Performance**: La cache estática hace que la reflexión solo ocurra una vez por tipo en toda la vida de la aplicación