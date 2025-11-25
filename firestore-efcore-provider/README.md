# Firestore Entity Framework Core Provider

Proveedor de Entity Framework Core para Google Cloud Firestore.

## Clases Implementadas

### Infrastructure
- FirestoreOptionsExtension
- FirestoreDbContextOptionsBuilder
- FirestoreDbContextOptionsExtensions
- FirestoreServiceCollectionExtensions
- FirestoreClientWrapper
- FirestoreIdGenerator
- FirestoreDocumentSerializer
- FirestoreCollectionManager

### Storage
- FirestoreDatabase
- FirestoreDatabaseProvider
- FirestoreDatabaseCreator
- FirestoreTransactionManager
- FirestoreTransaction
- FirestoreTypeMappingSource
- FirestoreExecutionStrategy

### Update
- FirestoreUpdateSqlGenerator
- FirestoreModificationCommandBatch

### Metadata
- FirestoreModelValidator
- FirestoreConventionSetBuilder

### Query
- FirestoreQueryContext
- FirestoreQueryCompilationContext

### Extensions
- FirestoreTransactionExtensions

## Instalación de Dependencias

```bash
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Google.Cloud.Firestore
```

## Uso

```csharp
services.AddDbContext<MiContexto>(options =>
    options.UseFirestore("mi-proyecto-firebase", "path/to/credentials.json"));
```

## Estado Actual

✅ SaveChanges funcional
❌ Queries LINQ pendientes (requiere implementación completa del Query Pipeline)
