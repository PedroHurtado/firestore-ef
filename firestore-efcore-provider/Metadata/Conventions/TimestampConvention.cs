using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Firestore.EntityFrameworkCore.Metadata.Conventions;

public class TimestampConvention : IPropertyAddedConvention
{
    private static readonly string[] TimestampPropertyNames =
    {
        "CreatedAt", "CreatedDate", "CreatedTime", "CreatedOn",
        "UpdatedAt", "UpdatedDate", "UpdatedTime", "UpdatedOn", "LastModified",
        "ModifiedAt", "ModifiedDate", "ModifiedTime", "ModifiedOn",
        "DeletedAt", "DeletedDate", "DeletedTime", "DeletedOn"
    };

    public void ProcessPropertyAdded(
        IConventionPropertyBuilder propertyBuilder,
        IConventionContext<IConventionPropertyBuilder> context)
    {
        var property = propertyBuilder.Metadata;

        // Verificar si es DateTime o DateTime?
        if (property.ClrType != typeof(DateTime) && property.ClrType != typeof(DateTime?))
            return;

        // Verificar si ya tiene una conversión configurada
        if (property.GetValueConverter() != null)
            return;

        // Verificar si el nombre coincide con nombres comunes de timestamp
        if (TimestampPropertyNames.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
        {
            // Firestore maneja DateTime automáticamente como Timestamp
            // Esta convention está preparada por si en el futuro necesitamos
            // aplicar alguna configuración específica a estos campos
        }
    }
}
