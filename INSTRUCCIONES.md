# Firestore Entity Framework Core Provider

## 📦 Contenido del ZIP

El archivo `firestore-efcore-provider.zip` contiene **todas las clases implementadas** para el proveedor de Firestore para Entity Framework Core.

### Estructura del Proyecto

```
Firestore.EntityFrameworkCore/
├── Infrastructure/
│   ├── FirestoreOptionsExtension.cs
│   ├── FirestoreDbContextOptionsBuilder.cs
│   ├── FirestoreDbContextOptionsExtensions.cs
│   ├── FirestoreServiceCollectionExtensions.cs
│   └── Internal/
│       ├── FirestoreClientWrapper.cs
│       ├── FirestoreIdGenerator.cs
│       ├── FirestoreDocumentSerializer.cs
│       └── FirestoreCollectionManager.cs
├── Storage/
│   ├── FirestoreDatabase.cs
│   ├── FirestoreDatabaseProvider.cs
│   ├── FirestoreDatabaseCreator.cs
│   ├── FirestoreTransactionManager.cs
│   ├── FirestoreTransaction.cs
│   ├── FirestoreTypeMappingSource.cs
│   └── FirestoreExecutionStrategy.cs
├── Update/
│   ├── FirestoreUpdateSqlGenerator.cs
│   └── FirestoreModificationCommandBatch.cs
├── Metadata/
│   ├── FirestoreModelValidator.cs
│   └── Conventions/
│       └── FirestoreConventionSetBuilder.cs
├── Query/
│   ├── FirestoreQueryContext.cs
│   └── FirestoreQueryCompilationContext.cs
├── Extensions/
│   └── FirestoreTransactionExtensions.cs
├── Firestore.EntityFrameworkCore.csproj
├── README.md
├── EJEMPLO_USO.cs
└── LICENSE
```

## 🚀 Inicio Rápido

### 1. Extraer el ZIP
Extrae el contenido del ZIP en tu directorio de solución.

### 2. Instalar Dependencias
```bash
cd Firestore.EntityFrameworkCore
dotnet restore
```

### 3. Compilar el Proyecto
```bash
dotnet build
```

### 4. Usar en tu Proyecto

Referencia el proyecto o empaqueta como NuGet:

```bash
dotnet pack
```

Luego en tu proyecto:

```bash
dotnet add package Firestore.EntityFrameworkCore --source ./path/to/packages
```

## 📝 Uso Básico

```csharp
using Microsoft.EntityFrameworkCore;
using Firestore.EntityFrameworkCore.Infrastructure;

public class MiContexto : DbContext
{
    public DbSet<Producto> Productos { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseFirestore(
            "mi-proyecto-firebase",
            "path/to/credentials.json");
    }
}

// Uso
using var context = new MiContexto();

context.Productos.Add(new Producto 
{ 
    Nombre = "Laptop", 
    Precio = 999.99m 
});

await context.SaveChangesAsync();
```

## ✅ Funcionalidades Implementadas

### ✅ SaveChanges Completo
- Inserción de entidades
- Actualización de entidades
- Eliminación de entidades
- Generación automática de IDs
- Serialización de entidades
- Manejo de timestamps

### ✅ Transacciones
- BeginTransaction
- Commit
- Rollback
- Transacciones automáticas con extensiones

### ✅ Configuración
- ProjectId
- CredentialsPath
- DatabaseId
- MaxRetryAttempts
- CommandTimeout

### ✅ Validaciones
- Validación de claves primarias
- Validación de tipos soportados
- Validación de configuración

### ✅ Conversión de Tipos
- string, int, long, double, float, bool
- DateTime (UTC automático)
- Decimal → Double
- Enum → String
- Guid → String

## ❌ Funcionalidades Pendientes

### Queries LINQ
El Query Pipeline completo aún no está implementado. Por ahora:

❌ `context.Productos.Where(p => p.Precio > 100)` - No funciona
❌ `context.Productos.ToList()` - No funciona
❌ `context.Productos.FirstOrDefault()` - No funciona

**Para consultas**, necesitas:
1. Implementar el Query Pipeline completo (8 clases adicionales)
2. O usar el SDK de Firestore directamente:

```csharp
var db = context.GetFirestoreDatabase();
var productos = await db.Collection("productos")
    .WhereGreaterThan("Precio", 100)
    .GetSnapshotAsync();
```

## 🔧 Próximos Pasos

Para completar el proveedor, necesitas implementar:

1. **Query Pipeline** (para soporte LINQ completo):
   - FirestoreQueryableMethodTranslatingExpressionVisitor
   - FirestoreShapedQueryCompilingExpressionVisitor
   - FirestoreExpressionTranslatingExpressionVisitor
   - Y más...

2. **Características Avanzadas**:
   - Subcollections
   - Índices compuestos
   - Listeners en tiempo real
   - Cache local

## 📚 Recursos

- [Entity Framework Core Docs](https://docs.microsoft.com/ef/core/)
- [Firestore Documentation](https://cloud.google.com/firestore/docs)
- [Google.Cloud.Firestore NuGet](https://www.nuget.org/packages/Google.Cloud.Firestore/)

## 📄 Licencia

MIT License - Ver archivo LICENSE

## 🤝 Contribuciones

Este es un proyecto base. Siéntete libre de:
- Completar el Query Pipeline
- Agregar tests unitarios
- Mejorar el rendimiento
- Agregar características adicionales

## ⚠️ Notas Importantes

1. **Firestore es NoSQL**: No esperes todas las características de SQL
2. **Sin JOINs**: Las relaciones deben manejarse manualmente
3. **Límites de Firestore**: 500 operaciones por transacción
4. **Costos**: Ten en cuenta los costos de lectura/escritura de Firestore

## 📞 Soporte

Para preguntas o issues, consulta:
- Firebase Console para configuración
- Logs de EF Core: `optionsBuilder.LogTo(Console.WriteLine)`

---

**¡Éxito con tu proyecto!** 🚀
