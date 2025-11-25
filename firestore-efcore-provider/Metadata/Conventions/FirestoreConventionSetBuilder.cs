using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace Firestore.EntityFrameworkCore.Metadata.Conventions;

public class FirestoreConventionSetBuilder : ProviderConventionSetBuilder
{
    public FirestoreConventionSetBuilder(ProviderConventionSetBuilderDependencies dependencies)
        : base(dependencies)
    {
    }

    public override ConventionSet CreateConventionSet()
    {
        var conventionSet = base.CreateConventionSet();

        // Agregar conventions que se ejecutan cuando se agrega una entidad
        conventionSet.EntityTypeAddedConventions.Add(new PrimaryKeyConvention());
        conventionSet.EntityTypeAddedConventions.Add(new CollectionNamingConvention());

        // Agregar conventions que se ejecutan cuando se agrega una propiedad
        conventionSet.PropertyAddedConventions.Add(new EnumToStringConvention());
        conventionSet.PropertyAddedConventions.Add(new DecimalToDoubleConvention());
        conventionSet.PropertyAddedConventions.Add(new TimestampConvention());

        // Agregar conventions que se ejecutan cuando se agrega una complex property
        conventionSet.ComplexPropertyAddedConventions.Add(new GeoPointConvention());

        // Agregar conventions que se ejecutan cuando se agrega una navigation
        conventionSet.NavigationAddedConventions.Add(new DocumentReferenceNamingConvention());

        // Agregar conventions que se ejecutan al finalizar el modelo
        conventionSet.ModelFinalizingConventions.Add(new ComplexTypeNavigationPropertyConvention());

        return conventionSet;
    }
}
