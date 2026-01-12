using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

public class CollectionNamingConvention : IEntityTypeAddedConvention
{
    public void ProcessEntityTypeAdded(
        IConventionEntityTypeBuilder entityTypeBuilder,
        IConventionContext<IConventionEntityTypeBuilder> context)
    {
        var entityType = entityTypeBuilder.Metadata;

        // Solo aplicar si no tiene un nombre de tabla/colección configurado explícitamente
        var tableName = entityType.GetTableName();

        // Si el nombre de tabla es igual al nombre de la entidad o es null, aplicar pluralización
        if (tableName == null || tableName == entityType.ClrType.Name)
        {
            // Pluralizar el nombre de la entidad
            var pluralizedName = entityType.ClrType.Name.Pluralize();
            entityTypeBuilder.ToTable(pluralizedName);
        }
    }
}
