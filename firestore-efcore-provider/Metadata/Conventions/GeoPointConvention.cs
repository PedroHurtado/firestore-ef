using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Firestore.EntityFrameworkCore.Metadata.Conventions;

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

        // Detección por ESTRUCTURA: buscar propiedades Latitude y Longitude (double)
        var latProperty = clrType.GetProperties()
            .FirstOrDefault(p =>
                (p.Name.Equals("Latitude", StringComparison.OrdinalIgnoreCase) ||
                 p.Name.Equals("Lat", StringComparison.OrdinalIgnoreCase) ||
                 p.Name.Equals("Latitud", StringComparison.OrdinalIgnoreCase)) &&
                (p.PropertyType == typeof(double) || p.PropertyType == typeof(double?)));

        var lonProperty = clrType.GetProperties()
            .FirstOrDefault(p =>
                (p.Name.Equals("Longitude", StringComparison.OrdinalIgnoreCase) ||
                 p.Name.Equals("Lng", StringComparison.OrdinalIgnoreCase) ||
                 p.Name.Equals("Lon", StringComparison.OrdinalIgnoreCase) ||
                 p.Name.Equals("Longitud", StringComparison.OrdinalIgnoreCase)) &&
                (p.PropertyType == typeof(double) || p.PropertyType == typeof(double?)));

        // Si no tiene ambas propiedades, no es GeoPoint
        if (latProperty == null || lonProperty == null)
            return;

        // Verificar si ya tiene la anotación de GeoPoint
        if (complexProperty.FindAnnotation("Firestore:IsGeoPoint") != null)
            return;

        // Aplicar la anotación de GeoPoint (nombre debe coincidir con FirestoreDocumentDeserializer)
        propertyBuilder.HasAnnotation("Firestore:IsGeoPoint", true);
    }
}
