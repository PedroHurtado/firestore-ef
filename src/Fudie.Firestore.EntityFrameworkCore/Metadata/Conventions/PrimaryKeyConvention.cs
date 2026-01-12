using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

public class PrimaryKeyConvention : IEntityTypeAddedConvention
{
    public void ProcessEntityTypeAdded(
        IConventionEntityTypeBuilder entityTypeBuilder,
        IConventionContext<IConventionEntityTypeBuilder> context)
    {
        var entityType = entityTypeBuilder.Metadata;

        // Si ya tiene una clave primaria configurada, no hacer nada
        if (entityType.FindPrimaryKey() != null)
            return;

        // Buscar propiedades llamadas "Id" o "{EntityName}Id"
        var entityName = entityType.ClrType.Name;
        var idProperty = entityType.FindProperty("Id")
            ?? entityType.FindProperty($"{entityName}Id");

        if (idProperty != null)
        {
            // Pasar la propiedad misma, no el nombre
            entityTypeBuilder.PrimaryKey([idProperty]);
        }
    }
}