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

---

## Solución implementada (Enero 2026)

### Resumen de cambios

Se implementaron **3 correcciones** para permitir patrones DDD con Value Objects:

| Archivo | Cambio | Problema resuelto |
|---------|--------|-------------------|
| `FirestoreConventionSetBuilder.cs` | Remover `ConstructorBindingConvention` | Problema 1 |
| `FirestoreConventionSetBuilder.cs` | Agregar `ComplexTypePropertyDiscoveryConvention` | Descubrir propiedades de records |
| `FirestoreModelValidator.cs` | Override selectivo de validaciones | Problema 2 |

### Fix 1: Remover ConstructorBindingConvention

En `FirestoreConventionSetBuilder.CreateConventionSet()`:

```csharp
// Remover ConstructorBindingConvention - Firestore tiene su propio Materializer
// que puede instanciar tipos con constructores protected usando reflection.
// Esta convención bloquea Value Objects DDD con constructor protected parametrizado.
conventionSet.ModelFinalizingConventions.RemoveAll(
    c => c is ConstructorBindingConvention);
```

### Fix 2: ComplexTypePropertyDiscoveryConvention

Al remover `ConstructorBindingConvention`, EF Core no puede descubrir automáticamente las propiedades de los ComplexTypes (records) porque no puede enlazar al constructor.

Se creó `ComplexTypePropertyDiscoveryConvention` que descubre manualmente las propiedades públicas de los records con getter:

```csharp
// En FirestoreConventionSetBuilder.cs
conventionSet.ComplexPropertyAddedConventions.Add(new ComplexTypePropertyDiscoveryConvention());
```

### Fix 3: FirestoreModelValidator selectivo

Se analizó `ModelValidator` de EF Core y se determinó qué validaciones son relevantes para Firestore:

**Validaciones MANTENIDAS** (útiles para cualquier proveedor):
- `ValidateIgnoredMembers`: Previene errores de configuración
- `ValidatePropertyMapping`: Nuestra versión que permite ComplexProperty nullable
- `ValidateOwnership`: Mapea a documentos embebidos
- `ValidateNonNullPrimaryKeys`: Firestore requiere IDs de documento
- `ValidateNoMutableKeys`: Los IDs de documento son inmutables
- `ValidateNoCycles`: Previene ciclos de serialización infinita
- `ValidateClrInheritance`: La herencia aplica a documentos
- `ValidateInheritanceMapping`: Discriminadores para polimorfismo
- `ValidateChangeTrackingStrategy`: El change tracking aún se usa
- `ValidateTypeMappings`: Útil para value converters
- `ValidatePrimitiveCollections`: ArrayOf se beneficia
- `LogShadowProperties`: Diagnóstico sin overhead

**Validaciones SALTADAS** (no aplican a bases de datos documentales):
- `ValidateRelationships`: Firestore usa DocumentReferences/SubCollections
- `ValidateNoShadowKeys`: Las shadow FKs no aplican a documentos
- `ValidateForeignKeys`: No hay FKs en bases de datos documentales
- `ValidateFieldMapping`: FirestoreDocumentDeserializer lo maneja
- `ValidateQueryFilters`: Implementación diferente en documentos
- `ValidateData`: No usamos HasData() seeding
- `ValidateTriggers`: Firestore Cloud Functions son externas

**Validaciones ESPECÍFICAS DE FIRESTORE**:
- `ValidateFirestoreConstraints`: Valida PKs, tipos de clave, sin claves compuestas
- `ValidateNoUnsupportedRelationships`: Bloquea relaciones N:M, 1:N, 1:1 no configuradas

### Referencia

Enfoque similar al de `CosmosModelValidator` en EF Core:
https://github.com/dotnet/efcore/blob/main/src/EFCore.Cosmos/Infrastructure/Internal/CosmosModelValidator.cs

### Fix 4: Registro correcto de FirestoreModelValidator

Se descubrió que el registro anterior de `FirestoreModelValidator` usaba `TryAdd`:

```csharp
// ANTES - NO funcionaba porque TryAddCoreServices ya registró ModelValidator
builder.TryAdd<IModelValidator, FirestoreModelValidator>()
```

`TryAdd` no sobrescribe registros existentes, por lo que `FirestoreModelValidator` **nunca se usaba** en producción. Se cambió a:

```csharp
// DESPUÉS - Sobrescribe el registro existente
serviceCollection.AddSingleton<IModelValidator, FirestoreModelValidator>();
```

Este cambio reveló que la validación `ValidateNoUnsupportedRelationships` nunca se había ejecutado. Para mantener compatibilidad hacia atrás, las relaciones no configuradas ahora generan **advertencias** en lugar de errores:

```csharp
// Antes: throw new NotSupportedException(...)
// Ahora: logger.Logger.LogWarning(message)
```

### Estado de tests

**395/406 tests pasaron** (11 skipped) en la suite completa de integración.

**53/53 tests pasaron** en `ProviderFixesTests`:

```bash
dotnet test tests/Fudie.Firestore.IntegrationTest --filter "FullyQualifiedName~ProviderFixes"

Passed!  - Failed: 0, Passed: 53, Skipped: 0, Total: 53
```

### Tests Comprehensivos de Conventions

Los tests en `ProviderFixesTests.cs` sirven como **SEED/PATRÓN** para todos los microservicios que utilicen el provider de Firestore. Cubren:

#### 1. Global Model Configuration
- ✅ `Model_ShouldUsePropertyAccessModeField` - PropertyAccessMode.Field para DDD

#### 2. Entity Type Registration (4 tests)
- ✅ `Model_ShouldHaveMenuEntityType`
- ✅ `Model_ShouldHaveMenuItemEntityType`
- ✅ `Model_ShouldHaveAllergenEntityType`
- ✅ `Model_ShouldHaveMenuCategoryEntityType`

#### 3. PrimaryKeyConvention (3 tests)
- ✅ `PrimaryKeyConvention_Menu_ShouldHaveIdAsPrimaryKey`
- ✅ `PrimaryKeyConvention_MenuItem_ShouldHaveIdAsPrimaryKey` (Guid)
- ✅ `PrimaryKeyConvention_Allergen_ShouldHaveIdAsPrimaryKey` (string)

#### 4. CollectionNamingConvention (4 tests)
- ✅ `CollectionNamingConvention_Menu_ShouldHavePluralizedTableName` (Menus)
- ✅ `CollectionNamingConvention_MenuItem_ShouldHavePluralizedTableName` (MenuItems)
- ✅ `CollectionNamingConvention_Allergen_ShouldHavePluralizedTableName` (Allergens)
- ✅ `CollectionNamingConvention_MenuCategory_ShouldHavePluralizedTableName` (MenuCategories)

#### 5. EnumToStringConvention (2 tests)
- ✅ `EnumToStringConvention_DepositPolicy_DepositType_ShouldHaveStringConverter`
- ✅ `EnumToStringConvention_PriceOption_PortionType_ShouldHaveStringConverter`

#### 6. DecimalToDoubleConvention (3 tests)
- ✅ `DecimalToDoubleConvention_DepositPolicy_Amount_ShouldHaveDoubleConverter`
- ✅ `DecimalToDoubleConvention_NullableDecimal_ShouldHaveNullableDoubleConverter`
- ✅ `DecimalToDoubleConvention_NutritionalInfo_Protein_ShouldHaveDoubleConverter`

#### 7. Query Filter Tests (3 tests)
- ✅ `Menu_ShouldHaveQueryFilter` (multi-tenancy)
- ✅ `MenuItem_ShouldHaveQueryFilter` (multi-tenancy)
- ✅ `Allergen_ShouldNotHaveQueryFilter` (catálogo global)

#### 8. ComplexProperty (Value Object) Tests (6 tests)
- ✅ `Menu_ShouldHaveDepositPolicyAsComplexProperty`
- ✅ `Menu_DepositPolicy_ShouldBeNullable` (Value Object opcional)
- ✅ `MenuItem_ShouldHaveDepositOverrideAsComplexProperty`
- ✅ `MenuItem_DepositOverride_ShouldBeNullable`
- ✅ `MenuItem_ShouldHaveNutritionalInfoAsComplexProperty`
- ✅ `MenuItem_NutritionalInfo_ShouldBeNullable`

#### 9. ComplexTypePropertyDiscoveryConvention (3 tests)
- ✅ `ComplexTypePropertyDiscovery_DepositPolicy_ShouldDiscoverAllProperties` (5 props)
- ✅ `ComplexTypePropertyDiscovery_NutritionalInfo_ShouldDiscoverAllProperties` (8 props)
- ✅ `ComplexTypePropertyDiscovery_ItemDepositOverride_ShouldDiscoverAllProperties` (2 props)

#### 10. SubCollection Annotation Tests (4 tests)
- ✅ `Menu_ShouldHaveCategoriesNavigation`
- ✅ `Menu_Categories_ShouldBeMarkedAsSubCollection` (Firestore:SubCollection=true)
- ✅ `Menu_Categories_ShouldBeCollection`
- ✅ `Menu_Categories_ShouldBeFirestoreConfigured`

#### 11. ArrayOf Embedded Tests (3 tests)
- ✅ `MenuItem_PriceOptions_ShouldBeArrayOf`
- ✅ `MenuItem_PriceOptions_ShouldBeArrayOfEmbedded`
- ✅ `MenuItem_PriceOptions_ShouldHaveCorrectElementType` (PriceOption)

#### 12. ArrayOf Reference Tests (3 tests)
- ✅ `MenuItem_Allergens_ShouldBeArrayOf`
- ✅ `MenuItem_Allergens_ShouldBeArrayOfReference`
- ✅ `MenuItem_Allergens_ShouldHaveCorrectElementType` (Allergen)

#### 13. ArrayOf Embedded with Nested Reference Tests (3 tests)
- ✅ `MenuCategory_Items_ShouldBeArrayOf`
- ✅ `MenuCategory_Items_ShouldBeArrayOfEmbedded`
- ✅ `MenuCategory_Items_ShouldHaveCorrectElementType` (CategoryItem)

#### 14. DocumentReferenceNamingConvention Tests (1 test)
- ✅ `DocumentReferenceNaming_Categories_ShouldHaveFieldNameAnnotation` ({Property}Ref)

#### 15. DbSet Exposure Tests (3 tests)
- ✅ `DbContext_ShouldExposeMenusDbSet`
- ✅ `DbContext_ShouldExposeMenuItemsDbSet`
- ✅ `DbContext_ShouldExposeAllergensDbSet`

#### 16. Model Validation Tests (2 tests)
- ✅ `Model_ShouldNotThrowForNullableComplexProperty` (FirestoreModelValidator)
- ✅ `Model_ShouldNotThrowForProtectedConstructorValueObjects` (ConstructorBindingConvention removed)

#### 17. Integration Tests (4 tests)
- ✅ `Integration_Menu_ShouldHaveCompleteConfiguration`
- ✅ `Integration_MenuItem_ShouldHaveCompleteConfiguration`
- ✅ `Integration_AllEntityTypes_ShouldHavePrimaryKey`
- ✅ `Integration_AllNavigations_ShouldBeFirestoreConfigured`

#### 18. Regression Tests (1 test)
- ✅ `Regression_ModelShouldBuildWithoutExceptions`
