# Fix: Carencias del Provider de Firestore para EF Core

Este documento describe las limitaciones actuales del provider de Firestore que impiden el uso de patrones DDD estándar con Value Objects.

## Referencias

- **Issue EF Core**: https://github.com/dotnet/efcore/issues/32437
- **Issue ComplexProperty nullable**: https://github.com/dotnet/efcore/issues/31376

---

## Problema 1: ConstructorBindingConvention bloquea Value Objects con constructor protected

### Descripción

EF Core 8 aplica `ConstructorBindingConvention` durante `ModelFinalizing` para todos los ComplexTypes. Esta convención intenta encontrar un constructor válido para instanciar el tipo, pero **falla cuando el único constructor disponible es `protected` con parámetros**.

### Error producido

```
System.InvalidOperationException : No suitable constructor was found for entity type 'Menu.DepositPolicy#DepositPolicy'.
The following constructors had parameters that could not be bound to properties of the entity type:
    Cannot bind 'depositType', 'amount', 'percentage', 'minimumBillForDeposit', 'minimumGuestsForDeposit'
    in 'Menu.DepositPolicy#DepositPolicy(DepositType depositType, decimal amount, decimal? percentage, decimal? minimumBillForDeposit, int? minimumGuestsForDeposit)'
    Cannot bind 'original' in 'Menu.DepositPolicy#DepositPolicy(DepositPolicy original)'
Note that only mapped properties can be bound to constructor parameters. Navigations to related entities, including references to owned types, cannot be bound.
```

### Stack trace relevante

```
at Microsoft.EntityFrameworkCore.Metadata.Internal.ConstructorBindingFactory.GetBindings[T](...)
at Microsoft.EntityFrameworkCore.Metadata.Internal.ConstructorBindingFactory.GetBindings(IReadOnlyComplexType complexType, ...)
at Microsoft.EntityFrameworkCore.Metadata.Conventions.ConstructorBindingConvention.Process(ComplexType complexType)
at Microsoft.EntityFrameworkCore.Metadata.Conventions.ConstructorBindingConvention.ProcessModelFinalizing(...)
```

### Por qué es un problema para Firestore

El provider de Firestore tiene un **Materializer propio** que puede instanciar cualquier tipo usando reflection, sin necesidad de que EF Core configure un `ConstructorBinding`. La convención `ConstructorBindingConvention` **no aporta nada** al provider de Firestore y solo bloquea patrones DDD válidos.

### Patrón DDD afectado

Los Value Objects en DDD típicamente tienen:
- Constructor `protected` con parámetros para validación e inmutabilidad
- Factory methods estáticos (`Create()`) que validan y crean instancias

```csharp
public record DepositPolicy
{
    public DepositType DepositType { get; }
    public decimal Amount { get; }

    // ÚNICO constructor - protected con parámetros
    protected DepositPolicy(DepositType depositType, decimal amount)
    {
        DepositType = depositType;
        Amount = amount;
    }

    // Factory method con validación
    public static DepositPolicy Create(DepositType type, decimal amount)
    {
        // validaciones...
        return new(type, amount);
    }
}
```

### Workaround actual (no deseable)

Agregar un constructor `protected` sin parámetros solo para ORM:

```csharp
protected DepositPolicy() { } // Workaround - no queremos esto
```

Este workaround:
- Rompe la encapsulación del Value Object
- Permite crear instancias inválidas
- No debería ser necesario para un provider NoSQL

### Solución requerida

Remover o desactivar `ConstructorBindingConvention` en `FirestoreConventionSetBuilder`:

```csharp
public override ConventionSet CreateConventionSet()
{
    var conventionSet = base.CreateConventionSet();

    // Remover ConstructorBindingConvention - Firestore tiene su propio Materializer
    conventionSet.ModelFinalizingConventions.RemoveAll(
        c => c is ConstructorBindingConvention);

    return conventionSet;
}
```

**Nota del issue #32437**: Simplemente remover la convención puede crear un modelo inválido. Puede ser necesario implementar una `IModelFinalizingConvention` personalizada que configure instantiation bindings vacíos o que use APIs internas de EF Core.

---

## Problema 2: ComplexProperty nullable no soportado

### Descripción

EF Core 8 no permite configurar `ComplexProperty` como opcional (nullable). Esta es una limitación de diseño para SQL Server donde los ComplexTypes se mapean a columnas, pero **no aplica a Firestore** donde los documentos pueden tener campos opcionales de forma natural.

### Error producido

```
System.InvalidOperationException: Configuring the complex property 'Menu.DepositPolicy' as optional is not supported, call 'IsRequired()'.
See https://github.com/dotnet/efcore/issues/31376 for more information.
```

### Stack trace relevante

```
at Microsoft.EntityFrameworkCore.Infrastructure.ModelValidator.<ValidatePropertyMapping>g__Validate|7_0(...)
at Microsoft.EntityFrameworkCore.Infrastructure.ModelValidator.ValidatePropertyMapping(IModel model, ...)
at Microsoft.EntityFrameworkCore.Infrastructure.ModelValidator.Validate(IModel model, ...)
```

### Por qué es un problema para Firestore

En Firestore:
- Los documentos son JSON-like y pueden tener campos opcionales
- Un campo puede simplemente no existir en el documento
- No hay restricciones de esquema como en SQL

El patrón de tener un Value Object opcional es completamente válido:

```csharp
public class Menu
{
    public string Name { get; set; }

    // Política de depósito OPCIONAL - algunos menús no la tienen
    public DepositPolicy? DepositPolicy { get; set; }
}
```

### Solución requerida

Sobrescribir `ValidatePropertyMapping` en `FirestoreModelValidator` para no validar esta restricción:

```csharp
public class FirestoreModelValidator : ModelValidator
{
    protected override void ValidatePropertyMapping(
        IModel model,
        IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
    {
        // No llamar a base - evita la validación de ComplexProperty opcional
        // O implementar validación personalizada que excluya esta regla
    }
}
```

---

## Archivos a modificar en el Provider

| Archivo | Cambio |
|---------|--------|
| `FirestoreConventionSetBuilder.cs` | Remover/reemplazar `ConstructorBindingConvention` |
| `FirestoreModelValidator.cs` | Override de `ValidatePropertyMapping` |

---

## Tests de integración

Se han creado tests en:
```
tests/Fudie.Firestore.IntegrationTest/ProviderFixes/
├── ProviderFixesEntities.cs    # Value Objects con constructor protected
├── ProviderFixesDbContext.cs   # DbContexts que reproducen los problemas
└── ProviderFixesTests.cs       # Tests que deben pasar después del fix
```

### Tests que deben fallar ANTES del fix

1. `CreateModel_WithProtectedConstructorValueObject_AsComplexProperty_ShouldNotThrow` - Error de ConstructorBindingConvention
2. `CreateModel_WithNullableComplexProperty_ShouldNotThrow` - Error de ComplexProperty opcional
3. Tests de persistencia relacionados

### Tests en proyecto webapi

```
tests/Customer.UnitTests/Infrastructure/CustomerDbContextTests.cs
```

13 tests que validan la configuración del modelo con Value Objects reales.

---

## Verificación después de los fixes

1. Ejecutar tests de integración del provider:
   ```bash
   dotnet test tests/Fudie.Firestore.IntegrationTest --filter "FullyQualifiedName~ProviderFixesTests"
   ```

2. Ejecutar tests del proyecto webapi:
   ```bash
   dotnet test tests/Customer.UnitTests --filter "FullyQualifiedName~CustomerDbContextTests"
   ```

3. Verificar que los Value Objects **NO necesitan** constructor sin parámetros
