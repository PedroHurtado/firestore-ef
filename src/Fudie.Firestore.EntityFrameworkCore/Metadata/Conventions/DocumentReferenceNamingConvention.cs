using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

public class DocumentReferenceNamingConvention : INavigationAddedConvention
{
    public void ProcessNavigationAdded(
        IConventionNavigationBuilder navigationBuilder,
        IConventionContext<IConventionNavigationBuilder> context)
    {
        var navigation = navigationBuilder.Metadata;

        // Verificar si ya tiene un nombre de campo DocumentReference configurado
        var existingFieldName = navigation.FindAnnotation("Firestore:DocumentReferenceFieldName");

        if (existingFieldName == null)
        {
            // Aplicar la convención: {PropertyName}Ref
            var fieldName = $"{navigation.Name}Ref";
            navigationBuilder.HasAnnotation("Firestore:DocumentReferenceFieldName", fieldName);
        }
    }
}
