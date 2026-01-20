# SnapshotShaper Implementation Specification

## Objetivo

Implementar una clase que tome una lista plana de `DocumentSnapshot` de Firestore y los convierta en una estructura jerárquica de diccionarios basándose en el AST de la query (`ResolvedFirestoreQuery`).

## Contexto

Cuando se ejecuta una query con Includes en el provider de EF Core para Firestore, se recuperan múltiples documentos en una lista plana. Por ejemplo, una query de `Menu` con `Include(Categories).ThenInclude(Items).ThenInclude(MenuItem).ThenInclude(Allergens)` puede retornar 12 snapshots planos que deben convertirse en 1 objeto `Menu` con su árbol completo.

## Input

- `ResolvedFirestoreQuery query` - El AST resuelto que contiene la estructura de la query
- `IReadOnlyList<DocumentSnapshot> snapshots` - Lista plana de todos los documentos recuperados

## Output

- `List<Dictionary<string, object?>>` - Lista de diccionarios con estructura jerárquica (uno por cada root)

## Interfaz

```csharp
public interface ISnapshotShaper
{
    List<Dictionary<string, object?>> Shape(ResolvedFirestoreQuery query, IReadOnlyList<DocumentSnapshot> snapshots);
}
```

## Clase a Implementar

```csharp
public class SnapshotShaper : ISnapshotShaper
{
    private Dictionary<string, List<SnapshotInfo>> _byCollection;
    private Dictionary<string, List<SnapshotInfo>> _byParentPath;

    public List<Dictionary<string, object?>> Shape(ResolvedFirestoreQuery query, IReadOnlyList<DocumentSnapshot> snapshots);
    private void IndexSnapshots(IReadOnlyList<DocumentSnapshot> snapshots);
    private List<SnapshotInfo> FindRoots(ResolvedFirestoreQuery query);
    private Dictionary<string, object?> ShapeNode(SnapshotInfo info, IReadOnlyList<ResolvedInclude> includes);
    private List<Dictionary<string, object?>> ResolveSubCollection(string parentPath, ResolvedInclude include);
    private object? ResolveReference(DocumentSnapshot snapshot, ResolvedInclude include);
}

public record SnapshotInfo(
    DocumentSnapshot Snapshot,
    string CollectionPath,
    string DocumentId,
    string? ParentDocumentPath);
```

## Descripción de Métodos

### `Shape(query, snapshots)`
1. Llamar a `IndexSnapshots` para indexar todos los snapshots
2. Llamar a `FindRoots` para obtener los snapshots raíz
3. Para cada root, llamar a `ShapeNode` con `query.Includes`
4. Retornar la lista de diccionarios

### `IndexSnapshots(snapshots)`
1. Para cada snapshot, parsear `Reference.Path` para extraer:
   - `CollectionPath` (penúltimo segmento después de `/documents/`)
   - `DocumentId` (último segmento)
   - `ParentDocumentPath` (segmentos anteriores, o null si es root collection)
2. Crear `SnapshotInfo` con estos datos
3. Agregar a `_byCollection` agrupando por `CollectionPath`
4. Agregar a `_byParentPath` agrupando por `ParentDocumentPath` (si no es null)

**Ejemplo de parseo de path:**
```
Path: "projects/demo-project/databases/(default)/documents/Menus/abc123/MenuCategories/cat1"
Resultado:
  - CollectionPath: "MenuCategories"
  - DocumentId: "cat1"
  - ParentDocumentPath: "Menus/abc123"

Path: "projects/demo-project/databases/(default)/documents/Menus/abc123"
Resultado:
  - CollectionPath: "Menus"
  - DocumentId: "abc123"
  - ParentDocumentPath: null
```

### `FindRoots(query)`
1. Buscar en `_byCollection` los snapshots donde `CollectionPath == query.CollectionPath`
2. Si `query.DocumentId` no es null, filtrar por ese ID específico
3. Retornar la lista de `SnapshotInfo` encontrados

### `ShapeNode(info, includes)`
1. Convertir snapshot a diccionario con `ToDictionary()`
2. Construir el path del nodo actual: `"{CollectionPath}/{DocumentId}"`
3. Para cada include en includes:
   - Si `include.IsCollection`: llamar a `ResolveSubCollection(nodePath, include)` y asignar resultado a `dict[include.NavigationName]`
   - Si `include.IsReference`: llamar a `ResolveReference(snapshot, include)` y asignar resultado a `dict[include.NavigationName]`
4. Retornar el diccionario con las navegaciones resueltas

### `ResolveSubCollection(parentPath, include)`
1. Buscar en `_byParentPath[parentPath]` los snapshots hijos
2. Filtrar donde `CollectionPath == include.CollectionPath`
3. Para cada hijo encontrado, llamar a `ShapeNode(hijo, include.NestedIncludes)` (recursivo)
4. Retornar lista de diccionarios

### `ResolveReference(snapshot, include)`
1. Leer del snapshot el valor de la propiedad FK
2. Si es `DocumentReference` single:
   - Extraer el ID del documento referenciado
   - Buscar en `_byCollection[include.CollectionPath]` por ese ID
   - Retornar `ToDictionary()` del encontrado, o null si no existe
3. Si es `List<DocumentReference>` (array de referencias):
   - Para cada referencia, extraer ID y buscar en `_byCollection`
   - Retornar `List<Dictionary<string, object?>>` con los encontrados

## Estructura del AST Relevante

Revisar el archivo `ResolvedFirestoreQuery.cs` para ver la estructura completa. Los campos clave son:

- `ResolvedFirestoreQuery.CollectionPath` - Path de la colección raíz
- `ResolvedFirestoreQuery.DocumentId` - ID específico si es GetDocument
- `ResolvedFirestoreQuery.Includes` - Lista de `ResolvedInclude`

- `ResolvedInclude.NavigationName` - Nombre de la propiedad de navegación
- `ResolvedInclude.IsCollection` - true = SubCollection, false = Reference
- `ResolvedInclude.CollectionPath` - Path de la colección destino
- `ResolvedInclude.NestedIncludes` - Includes anidados (recursivo)

## Notas de Implementación

1. **Referencias en memoria**: Si un documento ya fue recuperado (existe en `_byCollection`), no se vuelve a pedir. El shaper solo trabaja con los snapshots que ya tiene.

2. **SubCollections vs References**:
   - SubCollections: Se relacionan por path jerárquico (padre/hijo)
   - References: Se relacionan por DocumentReference almacenado en una propiedad

3. **Sin materialización**: Este shaper NO materializa a entidades CLR. Solo produce diccionarios. La materialización es responsabilidad de otro pipeline.

4. **Proyecciones**: La misma lógica aplica para proyecciones. La única diferencia es que Firestore ya devuelve solo los campos solicitados en el Select.

## Tests

Crear tests para:
1. Query simple sin includes
2. Query con una SubCollection
3. Query con una Reference
4. Query con SubCollections anidadas
5. Query con Reference que tiene array de references
6. Query con múltiples roots (ToList)