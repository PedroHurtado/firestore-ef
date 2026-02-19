# Bug: Firestore Provider no persiste cambios en navigation properties (References)

## Descripcion

Cuando una entidad trackeada por EF Core modifica una navigation property (reference), el provider de Firestore no detecta el cambio y no lo incluye en el UPDATE.

## Reproduccion

### Escenario: ExternalApp.AcceptInvitation

1. Se carga `ExternalApp` con tracking (via `IUpdate<ExternalApp, Guid>`)
2. Se carga `User` con tracking (via `IEntityLookup.GetRequiredAsync`)
3. El domain command asigna `externalApp.User = command.User`
4. Se llama `unitOfWork.SaveChangesAsync()`

### Estado del Change Tracker (correcto)

El tracker muestra la entidad con el User correctamente asociado:

```
ExternalApp
  Id = {63535e4b-af00-4ffb-8c53-0ef49b24d1d4}
  User = {Auth.Features.Users.Domain.UserAggregate.User}
    Id = {42c8927d-33a7-4ae3-a772-5dcd0f46ece1}
    Name = "Fudie Admin"
    Email = "admin@fudie.app"
  InvitationStatus = Accepted
  ApiKeyHash = "$2a$12$..."
  ...
```

### Query generada por el provider (incorrecta)

```
Firestore UPDATE: ExternalApps/63535e4b-af00-4ffb-8c53-0ef49b24d1d4
  Entity: ExternalApp (25,7ms)
  Data:
    {
      "ApiKeyHash": "$2a$12$...",
      "ApiKeyPrefix": "fud_CH8a",
      "ApiKeySalt": "$2a$12$...",
      "InvitationStatus": "Accepted",
      "_updatedAt": "2026-02-19T18:42:47Z"
    }
```

**Falta la referencia al User.** El campo `User` no aparece en el delta de cambios.

## Causa probable

`UpdateDocumentAsync` en `FirestoreDatabase.cs` solo itera propiedades escalares modificadas. No contempla cambios en navigation properties / references configuradas con `entity.Reference(x => x.User)`.

## Impacto

Cualquier entidad que modifique una reference despues de la creacion inicial no persistira el cambio. En este caso, `ExternalApp.AcceptInvitation` vincula un `User` que nunca se graba en Firestore.

## Ubicacion del bug

`Fudie.Firestore.EntityFrameworkCore/Storage/FirestoreDatabase.cs` -> `UpdateDocumentAsync`
