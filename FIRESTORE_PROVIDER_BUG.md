# Bug: FirestoreWhereTranslator does not resolve Reference navigation properties

## Context

`Membership` has a `User` property configured as a Firestore Reference:

```csharp
entity.Reference(x => x.User);
```

## LINQ Query

```csharp
query.Query<Membership>()
    .IgnoreQueryFilters()
    .Include(m => m.Role)
    .Where(m => m.User!.Id == userId)
    .FirstOrDefaultAsync();
```

## Generated Firestore Query

```
Query: Memberships
  .Where(Inner.Id == "6c5ad094-8cc9-4d54-b512-292e45eec115")
  .Limit(1)
  .Include(User) [Reference]
    → Query: Users
  .Include(Role) [Reference]
    → Query: TenantRoles
  0 doc(s) (7,7ms)
```

## Problem

`FirestoreWhereTranslator.cs` translates `m.User!.Id == userId` as `Inner.Id == "guid"` instead of resolving the Reference field.

In Firestore, `User` is stored as a DocumentReference (`Users/6c5ad094-8cc9-4d54-b512-292e45eec115`). The translator should compare against the reference path, not attempt to navigate into the referenced document's `Id` field.

The query returns 0 documents even though the referenced document exists, causing the application to always miss membership data when creating sessions.

## Expected Behavior

The translator should detect that `User` is a Reference property and convert `m.User!.Id == userId` into a comparison against the reference value: `Users/{userId}`.

## Affected File

`FirestoreWhereTranslator.cs`
