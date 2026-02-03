using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

public class FirestoreConventionSetBuilder : ProviderConventionSetBuilder
{
    public FirestoreConventionSetBuilder(ProviderConventionSetBuilderDependencies dependencies)
        : base(dependencies)
    {
    }

    public override ConventionSet CreateConventionSet()
    {
        var conventionSet = base.CreateConventionSet();

        // Remover ConstructorBindingConvention - Firestore tiene su propio Materializer
        // que puede instanciar tipos con constructores protected usando reflection.
        // Esta convención bloquea Value Objects DDD con constructor protected parametrizado.
        conventionSet.ModelFinalizingConventions.RemoveAll(
            c => c is ConstructorBindingConvention);

        // Agregar conventions que se ejecutan cuando se agrega una entidad
        var arrayOfConvention = new ArrayOfConvention();
        var mapOfConvention = new MapOfConvention();
        conventionSet.EntityTypeAddedConventions.Add(new PrimaryKeyConvention());
        conventionSet.EntityTypeAddedConventions.Add(new CollectionNamingConvention());
        conventionSet.EntityTypeAddedConventions.Add(arrayOfConvention);
        conventionSet.EntityTypeAddedConventions.Add(mapOfConvention);

        // Agregar conventions que se ejecutan al finalizar el modelo
        conventionSet.ModelFinalizingConventions.Add(arrayOfConvention);
        conventionSet.ModelFinalizingConventions.Add(mapOfConvention);

        // BackingFieldConvention debe ejecutarse DESPUÉS de ArrayOfConvention
        // para detectar backing fields de TODAS las colecciones (ArrayOf + SubCollections)
        conventionSet.ModelFinalizingConventions.Add(new BackingFieldConvention());

        // Agregar conventions que se ejecutan cuando se agrega una propiedad
        conventionSet.PropertyAddedConventions.Add(new EnumToStringConvention());
        conventionSet.PropertyAddedConventions.Add(new DecimalToDoubleConvention());
        conventionSet.PropertyAddedConventions.Add(new TimestampConvention());

        // Agregar conventions que se ejecutan cuando se agrega una complex property
        // ComplexTypePropertyDiscoveryConvention debe ejecutarse PRIMERO para descubrir propiedades
        // de records con constructores protected (que ConstructorBindingConvention no puede manejar)
        conventionSet.ComplexPropertyAddedConventions.Add(new ComplexTypePropertyDiscoveryConvention());
        conventionSet.ComplexPropertyAddedConventions.Add(new GeoPointConvention());
        conventionSet.ComplexPropertyAddedConventions.Add(new ComplexTypeNavigationPropertyConvention());

        // Agregar conventions que se ejecutan cuando se agrega una navigation
        conventionSet.NavigationAddedConventions.Add(new DocumentReferenceNamingConvention());

        return conventionSet;
    }
}
