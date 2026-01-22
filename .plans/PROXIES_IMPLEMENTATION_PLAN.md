# Plan de Implementación: Proxies y Lazy Loading

## Estado actual

Los tests de lazy loading y explicit loading están en SKIP porque falta infraestructura crítica.

Tests afectados:
- `LazyLoading_Reference_ShouldLoadWhenAccessed`
- `ExplicitLoading_Reference_ShouldLoadWhenRequested`

---

## Problema raíz

Cuando se guarda un `ArticuloLazy` con `Categoria`, Firestore almacena:
```
Categoria: "CategoriaLazies/cat-xxx" (DocumentReference)
```

Cuando se lee el artículo:
1. Se deserializa el documento
2. Se trackea la entidad
3. **La shadow property `CategoriaId` queda vacía**

EF Core necesita `CategoriaId` poblado para que `entry.Reference().LoadAsync()` funcione.

---

## Implementación requerida

### Fase 1: Explicit Loading (sin proxies)

**Objetivo:** Hacer funcionar `entry.Reference().LoadAsync()`

**Pasos:**

#### 1. Crear ShadowFkHandler

Nuevo handler que se ejecuta DESPUÉS de ConvertHandler y ANTES de TrackingHandler.

```csharp
public class ShadowFkHandler : QueryPipelineHandlerBase
{
    protected override QueryKind[] ApplicableKinds => new[] { QueryKind.Entity };

    protected override async Task<PipelineResult> HandleCoreAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken cancellationToken)
    {
        var result = await next(context, cancellationToken);

        if (result is PipelineResult.Materialized materialized)
        {
            PopulateShadowFks(materialized.Items, context);
        }

        return result;
    }

    private void PopulateShadowFks(IReadOnlyList<object> entities, PipelineContext context)
    {
        var model = context.QueryContext.Model;
        var entityType = model.FindEntityType(context.EntityType!);
        if (entityType == null) return;

        var allSnapshots = context.GetMetadata(PipelineMetadataKeys.AllSnapshots);
        if (allSnapshots == null) return;

        foreach (var entity in entities)
        {
            // Obtener el snapshot original de esta entidad
            // Extraer DocumentReferences de los campos
            // Mapear a shadow FK properties
            // Guardar en metadata para que TrackingHandler las use
        }
    }
}
```

#### 2. Modificar TrackingHandler

Después de `stateManager.GetOrCreateEntry()`, poblar shadow properties:

```csharp
var entry = stateManager.GetOrCreateEntry(entity, entityType);
entry.SetEntityState(EntityState.Unchanged);

// Poblar shadow FK properties
var shadowFks = context.GetMetadata<Dictionary<object, Dictionary<string, object>>>("ShadowFks");
if (shadowFks?.TryGetValue(entity, out var fks) == true)
{
    foreach (var (propertyName, value) in fks)
    {
        var property = entityType.FindProperty(propertyName);
        if (property != null && property.IsShadowProperty())
        {
            entry.SetProperty(property, value, isMaterialization: false);
        }
    }
}

result.Add(entity);
```

#### 3. Orden del pipeline

```
ConvertHandler → ShadowFkHandler → TrackingHandler → ProxyHandler
```

#### 4. Test de verificación

El test `ExplicitLoading_Reference_ShouldLoadWhenRequested` debe pasar.

---

### Fase 2: Lazy Loading (con proxies)

**Objetivo:** Hacer funcionar el acceso automático a navegaciones via proxies.

**Requisitos:**
- Fase 1 completada
- ILazyLoader implementado

**Pasos:**

#### 1. Implementar ILazyLoader

```csharp
public class FirestoreLazyLoader : ILazyLoader
{
    private readonly DbContext _context;

    public FirestoreLazyLoader(DbContext context)
    {
        _context = context;
    }

    public void Load(object entity, string navigationName)
    {
        var entry = _context.Entry(entity);
        var navigation = entry.Navigation(navigationName);
        
        if (!navigation.IsLoaded)
        {
            navigation.Load();
        }
    }

    public async Task LoadAsync(
        object entity, 
        string navigationName,
        CancellationToken cancellationToken = default)
    {
        var entry = _context.Entry(entity);
        var navigation = entry.Navigation(navigationName);
        
        if (!navigation.IsLoaded)
        {
            await navigation.LoadAsync(cancellationToken);
        }
    }
}
```

#### 2. Crear EfCoreProxyFactoryAdapter

```csharp
internal sealed class EfCoreProxyFactoryAdapter : IProxyFactory
{
    private readonly Microsoft.EntityFrameworkCore.Proxies.Internal.IProxyFactory _inner;
    private readonly Func<ILazyLoader> _lazyLoaderFactory;

    public EfCoreProxyFactoryAdapter(
        Microsoft.EntityFrameworkCore.Proxies.Internal.IProxyFactory inner,
        Func<ILazyLoader> lazyLoaderFactory)
    {
        _inner = inner;
        _lazyLoaderFactory = lazyLoaderFactory;
    }

    public Type GetProxyType(IEntityType entityType)
        => _inner.CreateProxyType(entityType);

    public object CreateProxy(IEntityType entityType)
        => _inner.Create(entityType, _lazyLoaderFactory());
}
```

#### 3. Implementar ProxyHandler

```csharp
public class ProxyHandler : QueryPipelineHandlerBase
{
    private readonly IProxyFactory? _proxyFactory;

    public ProxyHandler(IProxyFactory? proxyFactory)
    {
        _proxyFactory = proxyFactory;
    }

    protected override QueryKind[] ApplicableKinds => new[] { QueryKind.Entity };

    protected override async Task<PipelineResult> HandleCoreAsync(
        PipelineContext context,
        PipelineDelegate next,
        CancellationToken cancellationToken)
    {
        var result = await next(context, cancellationToken);

        if (_proxyFactory == null)
            return result;

        if (result is PipelineResult.Materialized materialized)
        {
            var proxies = ConvertToProxies(materialized.Items, context);
            return new PipelineResult.Materialized(proxies, materialized.Context);
        }

        return result;
    }

    private IReadOnlyList<object> ConvertToProxies(
        IReadOnlyList<object> entities,
        PipelineContext context)
    {
        var entityType = context.QueryContext.Model.FindEntityType(context.EntityType!);
        if (entityType == null)
            return entities;

        var result = new List<object>(entities.Count);
        foreach (var entity in entities)
        {
            var proxy = _proxyFactory!.CreateProxy(entityType);
            CopyProperties(entity, proxy, entityType);
            result.Add(proxy);
        }
        return result;
    }

    private static void CopyProperties(object source, object target, IEntityType entityType)
    {
        foreach (var property in entityType.GetProperties())
        {
            var value = property.GetGetter().GetClrValue(source);
            property.GetSetter().SetClrValue(target, value);
        }

        foreach (var navigation in entityType.GetNavigations())
        {
            var value = navigation.GetGetter().GetClrValue(source);
            if (value != null)
            {
                navigation.PropertyInfo?.SetValue(target, value);
            }
        }
    }
}
```

#### 4. Registrar en DI

Solo cuando proxies están configurados en DbContextOptions.

#### 5. Test de verificación

El test `LazyLoading_Reference_ShouldLoadWhenAccessed` debe pasar.

---

## Archivos a modificar/crear

### Fase 1
- [ ] Crear `ShadowFkHandler.cs`
- [ ] Modificar `TrackingHandler.cs`
- [ ] Añadir key a `PipelineMetadataKeys.cs`
- [ ] Actualizar orden en registro de pipeline

### Fase 2
- [ ] Crear `FirestoreLazyLoader.cs`
- [ ] Crear `EfCoreProxyFactoryAdapter.cs`
- [ ] Modificar `ProxyHandler.cs`
- [ ] Registrar en DI

---

## NO hacer

- NO modificar el deserializer para manejar proxies
- NO crear lógica de proxies en ConvertHandler
- NO usar APIs internas de EF Core sin adaptador
- NO implementar Fase 2 sin completar Fase 1

---

## Dependencias

El explicit loading depende de que EF Core pueda resolver la query de carga. Verificar que:

1. `entry.Reference("Categoria").LoadAsync()` genera una query
2. Esa query pasa por el pipeline normal
3. El resultado se asigna correctamente a la navegación

Si EF Core no genera la query correctamente, puede ser necesario implementar `INavigationLoader` custom.
