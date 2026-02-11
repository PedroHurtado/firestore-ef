# Bug: Firestore Provider - Include con Reference Nullable

## Contexto

Al ejecutar una query con `Include` sobre una navigation property nullable (`User?`) combinada con una non-nullable (`Role`), el provider de Firestore lanza `NotImplementedException` en `TranslateJoin`.

## Modelo: Membership

```csharp
public partial class Membership : AggregateRoot<Guid>
{
    public Guid TenantId { get; protected set; }
    public User? User { get; protected set; }              // NULLABLE - Reference
    public TenantRole Role { get; protected set; } = default!; // NON-NULLABLE - Reference
    public bool IsActive { get; protected set; }
    public string InvitationEmail { get; protected set; } = string.Empty;
    public InvitationStatus InvitationStatus { get; protected set; }
}
```

## DbContext Configuration

```csharp
public class AuthDbContext(DbContextOptions<AuthDbContext> options, Guid tenantId) :
    DbContext(options), IEntityLookup, IQuery, IChangeTracker, IUnitOfWork
{
    public DbSet<Membership> Memberships => Set<Membership>();

    public IQueryable<T> Query<T>() where T : class, IEntity
    {
        return Set<T>().AsQueryable().AsNoTracking();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Membership>(entity =>
        {
            entity.HasQueryFilter(x => x.TenantId == tenantId);
            entity.Reference(x => x.Role);  // non-nullable reference
            entity.Reference(x => x.User);  // nullable reference
        });
    }
}
```

## Query que produce el error

```csharp
var memberships = await query.Query<Membership>()
    .Include(x => x.User)    // nullable User?
    .Include(x => x.Role)    // non-nullable TenantRole
    .OrderBy(x => x.InvitationEmail)
    .ToListAsync();
```

## Exception

```
System.NotImplementedException: The method or operation is not implemented.
  at Fudie.Firestore.EntityFrameworkCore.Query.Visitors
    .FirestoreQueryableMethodTranslatingExpressionVisitor
    .TranslateJoin(...) line 214
  at Microsoft.EntityFrameworkCore.Query
    .QueryableMethodTranslatingExpressionVisitor.VisitMethodCall
```

## Hipotesis

El provider tiene tests para Include con References, pero no se probaron combinaciones de **Reference nullable + Reference non-nullable** en la misma entidad. EF Core traduce el Include de una navigation property nullable como un LEFT JOIN, lo que llega al metodo `TranslateJoin` del visitor en lugar del path habitual de Include. El metodo `TranslateJoin` no esta implementado (lanza `NotImplementedException`).

## Clase a investigar

`FirestoreQueryableMethodTranslatingExpressionVisitor` - metodo `TranslateJoin` (linea 214).

## Plan

1. Crear un test de integracion en el proyecto Firestore que reproduzca el escenario: entidad con dos References configuradas via `entity.Reference()`, una nullable y otra non-nullable, y ejecutar una query con `.Include()` en ambas.
2. Confirmar que el test falla con `NotImplementedException` en `TranslateJoin`.
3. Implementar o corregir `TranslateJoin` en el visitor para soportar este caso (o redirigir al path correcto de Include).
