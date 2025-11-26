using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Firestore.EntityFrameworkCore.Extensions;

public static class FirestorePropertyBuilderExtensions
{
    // ============= GEOPOINT (ComplexProperty) =============
    
    /// <summary>
    /// Marca un ComplexProperty como GeoPoint de Firestore.
    /// Necesario porque GeoPoint requiere configuración especial para detectar Latitude/Longitude.
    /// </summary>
    public static ComplexPropertyBuilder HasGeoPoint(this ComplexPropertyBuilder propertyBuilder)
    {
        propertyBuilder.Metadata.SetAnnotation("Firestore:IsGeoPoint", true);
        return propertyBuilder;
    }

    // ============= FIN =============
    // Todo lo demás es detección automática:
    // - Si una propiedad dentro de un ComplexType es una entidad con DbSet → DocumentReference (automático)
    // - Si una propiedad dentro de un ComplexType es otro ComplexType → Embebido (automático)
    // - Si una navegación de entidad es a otra entidad con DbSet → DocumentReference (automático)
    // - Si una navegación de entidad es List<Entidad> con DbSet → Array[DocumentReference] (automático)
}