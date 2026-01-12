using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

public class ComplexTypeNavigationPropertyConvention : IComplexPropertyAddedConvention
{
    public void ProcessComplexPropertyAdded(
        IConventionComplexPropertyBuilder propertyBuilder,
        IConventionContext<IConventionComplexPropertyBuilder> context)
    {
        var complexType = propertyBuilder.Metadata.ComplexType;
        var model = propertyBuilder.Metadata.DeclaringType.Model;
        
        IgnoreNavigationsInComplexType(complexType, model);
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
                // Ignorar la propiedad usando el builder
                complexType.Builder.Ignore(propertyInfo.Name);
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
                        complexType.Builder.Ignore(propertyInfo.Name);
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

    private bool IsEntityType(Type type, IConventionModel model)
    {
        return model.FindEntityType(type) != null;
    }
}
