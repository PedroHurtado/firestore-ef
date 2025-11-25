using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Firestore.EntityFrameworkCore.Metadata.Conventions;

public class GeoPointConvention : IComplexPropertyAddedConvention
{
    private static readonly string[] GeoPointPropertyNames =
    {
        "Location", "Coordinates", "Position", "GeoLocation",
        "GeoPosition", "LatLng", "LatLon", "Coords"
    };

    public void ProcessComplexPropertyAdded(
        IConventionComplexPropertyBuilder propertyBuilder,
        IConventionContext<IConventionComplexPropertyBuilder> context)
    {
        var complexProperty = propertyBuilder.Metadata;

        // Verificar si el nombre coincide con nombres comunes de coordenadas
        if (!GeoPointPropertyNames.Contains(complexProperty.Name, StringComparer.OrdinalIgnoreCase))
            return;

        var complexType = complexProperty.ComplexType;
        var clrType = complexType.ClrType;

        // Verificar si tiene propiedades Latitude y Longitude (o variantes)
        var hasLatitude = clrType.GetProperties()
            .Any(p => p.Name.Equals("Latitude", StringComparison.OrdinalIgnoreCase) ||
                     p.Name.Equals("Lat", StringComparison.OrdinalIgnoreCase));

        var hasLongitude = clrType.GetProperties()
            .Any(p => p.Name.Equals("Longitude", StringComparison.OrdinalIgnoreCase) ||
                     p.Name.Equals("Lng", StringComparison.OrdinalIgnoreCase) ||
                     p.Name.Equals("Lon", StringComparison.OrdinalIgnoreCase));

        if (!hasLatitude || !hasLongitude)
            return;

        // Verificar si ya tiene la anotación de GeoPoint
        if (complexProperty.FindAnnotation("Firestore:GeoPoint") != null)
            return;

        // Aplicar la anotación de GeoPoint
        propertyBuilder.HasAnnotation("Firestore:GeoPoint", true);

        // Buscar y anotar las propiedades Latitude y Longitude
        var latProperty = complexType.GetProperties()
            .FirstOrDefault(p => p.Name.Equals("Latitude", StringComparison.OrdinalIgnoreCase) ||
                               p.Name.Equals("Lat", StringComparison.OrdinalIgnoreCase));

        var lngProperty = complexType.GetProperties()
            .FirstOrDefault(p => p.Name.Equals("Longitude", StringComparison.OrdinalIgnoreCase) ||
                               p.Name.Equals("Lng", StringComparison.OrdinalIgnoreCase) ||
                               p.Name.Equals("Lon", StringComparison.OrdinalIgnoreCase));

        if (latProperty != null)
        {
            ((IMutableProperty)latProperty).SetAnnotation("Firestore:GeoPoint:Latitude", true);
        }

        if (lngProperty != null)
        {
            ((IMutableProperty)lngProperty).SetAnnotation("Firestore:GeoPoint:Longitude", true);
        }
    }
}
