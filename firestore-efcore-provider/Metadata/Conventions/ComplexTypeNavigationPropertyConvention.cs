using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Firestore.EntityFrameworkCore.Metadata.Conventions;

public class ComplexTypeNavigationPropertyConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        var model = modelBuilder.Metadata;

        foreach (var entityType in model.GetEntityTypes())
        {
            foreach (var complexProperty in entityType.GetComplexProperties())
            {
                IgnoreNavigationsInComplexType(complexProperty.ComplexType, model);
            }
        }
    }

    private void IgnoreNavigationsInComplexType(IConventionComplexType complexType, IConventionModel model)
    {
        var clrType = complexType.ClrType;

        // Buscar todas las propiedades que son navigation properties
        foreach (var propertyInfo in clrType.GetProperties())
        {
            var propertyType = propertyInfo.PropertyType;

            // Verificar si es una entidad (navigation property)
            if (IsEntityType(propertyType, model))
            {
                // Ignorar la propiedad en el complex type si existe
                var existingProperty = complexType.FindProperty(propertyInfo.Name);

                if (existingProperty != null)
                {
                    complexType.RemoveProperty(existingProperty);
                }
            }

            // Verificar si es una colección de entidades
            if (propertyType.IsGenericType)
            {
                var genericDef = propertyType.GetGenericTypeDefinition();
                if (genericDef == typeof(ICollection<>) ||
                    genericDef == typeof(IEnumerable<>) ||
                    genericDef == typeof(List<>))
                {
                    var elementType = propertyType.GetGenericArguments()[0];
                    if (IsEntityType(elementType, model))
                    {
                        var existingProperty = complexType.FindProperty(propertyInfo.Name);

                        if (existingProperty != null)
                        {
                            complexType.RemoveProperty(existingProperty);
                        }
                    }
                }
            }
        }

        // Recursivamente procesar complex properties anidados
        foreach (var nestedComplexProperty in complexType.GetComplexProperties())
        {
            IgnoreNavigationsInComplexType(nestedComplexProperty.ComplexType, model);
        }
    }

    private static bool IsEntityType(Type type, IConventionModel model)
    {
        return model.FindEntityType(type) != null;
    }
}