using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

/// <summary>
/// Convention que detecta ComplexTypes con estructura de GeoPoint (Latitude + Longitude).
/// La detección es POR ESTRUCTURA, no por nombre - cualquier tipo con propiedades
/// Latitude y Longitude (ambas double) será tratado como GeoPoint.
/// </summary>
public class GeoPointConvention : IComplexPropertyAddedConvention
{
    public void ProcessComplexPropertyAdded(
        IConventionComplexPropertyBuilder propertyBuilder,
        IConventionContext<IConventionComplexPropertyBuilder> context)
    {
        var complexProperty = propertyBuilder.Metadata;
        var complexType = complexProperty.ComplexType;
        var clrType = complexType.ClrType;

        // Usar helper centralizado para detectar estructura GeoPoint
        if (!ConventionHelpers.HasGeoPointStructure(clrType))
            return;

        // Verificar si ya tiene la anotación de GeoPoint
        if (complexProperty.FindAnnotation("Firestore:IsGeoPoint") != null)
            return;

        // Aplicar la anotación de GeoPoint
        propertyBuilder.HasAnnotation("Firestore:IsGeoPoint", true);
    }
}
