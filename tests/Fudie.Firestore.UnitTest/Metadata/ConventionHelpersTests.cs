using Fudie.Firestore.EntityFrameworkCore.Metadata.Conventions;

namespace Fudie.Firestore.UnitTest.Metadata;

/// <summary>
/// Tests unitarios para ConventionHelpers.
/// Verifica la detección de patrones: PK, GeoPoint, colecciones, tipos primitivos.
/// </summary>
public class ConventionHelpersTests
{
    #region Test Types

    // Tipo con Id (tiene estructura de PK)
    private class EntityWithId
    {
        public string Id { get; set; } = default!;
        public string Name { get; set; } = default!;
    }

    // Tipo con {TypeName}Id (tiene estructura de PK)
    private class Product
    {
        public string ProductId { get; set; } = default!;
        public string Name { get; set; } = default!;
    }

    // Tipo sin Id (NO tiene estructura de PK)
    private class ValueObject
    {
        public string Name { get; set; } = default!;
        public int Value { get; set; }
    }

    // Tipo con Latitude/Longitude (GeoPoint)
    private class GeoLocation
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    // Tipo con Lat/Lng (GeoPoint alternativo)
    private class GeoPoint
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
    }

    // Tipo con Latitud/Longitud (GeoPoint español)
    private class PuntoGeografico
    {
        public double Latitud { get; set; }
        public double Longitud { get; set; }
    }

    // Tipo con Lat/Lng pero también Id (NO es GeoPoint puro)
    private class LocationEntity
    {
        public string Id { get; set; } = default!;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    // Tipo con solo Latitude (NO es GeoPoint)
    private class PartialGeo
    {
        public double Latitude { get; set; }
        public string Name { get; set; } = default!;
    }

    // Tipo con Latitude/Longitude pero tipo incorrecto (NO es GeoPoint)
    private class StringGeo
    {
        public string Latitude { get; set; } = default!;
        public string Longitude { get; set; } = default!;
    }

    #endregion

    #region HasPrimaryKeyStructure Tests

    [Fact]
    public void HasPrimaryKeyStructure_WithId_ReturnsTrue()
    {
        ConventionHelpers.HasPrimaryKeyStructure(typeof(EntityWithId))
            .Should().BeTrue();
    }

    [Fact]
    public void HasPrimaryKeyStructure_WithTypeNameId_ReturnsTrue()
    {
        ConventionHelpers.HasPrimaryKeyStructure(typeof(Product))
            .Should().BeTrue();
    }

    [Fact]
    public void HasPrimaryKeyStructure_WithoutId_ReturnsFalse()
    {
        ConventionHelpers.HasPrimaryKeyStructure(typeof(ValueObject))
            .Should().BeFalse();
    }

    [Fact]
    public void HasPrimaryKeyStructure_GeoLocation_ReturnsFalse()
    {
        ConventionHelpers.HasPrimaryKeyStructure(typeof(GeoLocation))
            .Should().BeFalse();
    }

    #endregion

    #region GetPrimaryKeyProperty Tests

    [Fact]
    public void GetPrimaryKeyProperty_WithId_ReturnsIdProperty()
    {
        var prop = ConventionHelpers.GetPrimaryKeyProperty(typeof(EntityWithId));
        prop.Should().NotBeNull();
        prop!.Name.Should().Be("Id");
    }

    [Fact]
    public void GetPrimaryKeyProperty_WithTypeNameId_ReturnsProperty()
    {
        var prop = ConventionHelpers.GetPrimaryKeyProperty(typeof(Product));
        prop.Should().NotBeNull();
        prop!.Name.Should().Be("ProductId");
    }

    [Fact]
    public void GetPrimaryKeyProperty_WithoutId_ReturnsNull()
    {
        var prop = ConventionHelpers.GetPrimaryKeyProperty(typeof(ValueObject));
        prop.Should().BeNull();
    }

    #endregion

    #region HasGeoPointStructure Tests

    [Fact]
    public void HasGeoPointStructure_WithLatitudeLongitude_ReturnsTrue()
    {
        ConventionHelpers.HasGeoPointStructure(typeof(GeoLocation))
            .Should().BeTrue();
    }

    [Fact]
    public void HasGeoPointStructure_WithLatLng_ReturnsTrue()
    {
        ConventionHelpers.HasGeoPointStructure(typeof(GeoPoint))
            .Should().BeTrue();
    }

    [Fact]
    public void HasGeoPointStructure_WithLatitudLongitud_ReturnsTrue()
    {
        ConventionHelpers.HasGeoPointStructure(typeof(PuntoGeografico))
            .Should().BeTrue();
    }

    [Fact]
    public void HasGeoPointStructure_WithOnlyLatitude_ReturnsFalse()
    {
        ConventionHelpers.HasGeoPointStructure(typeof(PartialGeo))
            .Should().BeFalse();
    }

    [Fact]
    public void HasGeoPointStructure_WithStringCoordinates_ReturnsFalse()
    {
        ConventionHelpers.HasGeoPointStructure(typeof(StringGeo))
            .Should().BeFalse();
    }

    [Fact]
    public void HasGeoPointStructure_EntityWithId_ReturnsTrue()
    {
        // HasGeoPointStructure solo verifica Lat/Lng, no verifica Id
        ConventionHelpers.HasGeoPointStructure(typeof(LocationEntity))
            .Should().BeTrue();
    }

    #endregion

    #region IsGeoPointType Tests

    [Fact]
    public void IsGeoPointType_PureGeoPoint_ReturnsTrue()
    {
        // Tiene Lat/Lng y NO tiene Id
        ConventionHelpers.IsGeoPointType(typeof(GeoLocation))
            .Should().BeTrue();
    }

    [Fact]
    public void IsGeoPointType_EntityWithGeoPoint_ReturnsFalse()
    {
        // Tiene Lat/Lng pero TAMBIÉN tiene Id
        ConventionHelpers.IsGeoPointType(typeof(LocationEntity))
            .Should().BeFalse();
    }

    [Fact]
    public void IsGeoPointType_ValueObject_ReturnsFalse()
    {
        // No tiene Lat/Lng
        ConventionHelpers.IsGeoPointType(typeof(ValueObject))
            .Should().BeFalse();
    }

    #endregion

    #region IsGenericCollection Tests

    [Fact]
    public void IsGenericCollection_List_ReturnsTrue()
    {
        ConventionHelpers.IsGenericCollection(typeof(List<string>))
            .Should().BeTrue();
    }

    [Fact]
    public void IsGenericCollection_IList_ReturnsTrue()
    {
        ConventionHelpers.IsGenericCollection(typeof(IList<int>))
            .Should().BeTrue();
    }

    [Fact]
    public void IsGenericCollection_ICollection_ReturnsTrue()
    {
        ConventionHelpers.IsGenericCollection(typeof(ICollection<double>))
            .Should().BeTrue();
    }

    [Fact]
    public void IsGenericCollection_IEnumerable_ReturnsTrue()
    {
        ConventionHelpers.IsGenericCollection(typeof(IEnumerable<object>))
            .Should().BeTrue();
    }

    [Fact]
    public void IsGenericCollection_String_ReturnsFalse()
    {
        ConventionHelpers.IsGenericCollection(typeof(string))
            .Should().BeFalse();
    }

    [Fact]
    public void IsGenericCollection_Array_ReturnsFalse()
    {
        // Arrays no son genéricos
        ConventionHelpers.IsGenericCollection(typeof(int[]))
            .Should().BeFalse();
    }

    [Fact]
    public void IsGenericCollection_Dictionary_ReturnsFalse()
    {
        ConventionHelpers.IsGenericCollection(typeof(Dictionary<string, int>))
            .Should().BeFalse();
    }

    #endregion

    #region GetCollectionElementType Tests

    [Fact]
    public void GetCollectionElementType_ListOfString_ReturnsString()
    {
        ConventionHelpers.GetCollectionElementType(typeof(List<string>))
            .Should().Be(typeof(string));
    }

    [Fact]
    public void GetCollectionElementType_ListOfEntity_ReturnsEntity()
    {
        ConventionHelpers.GetCollectionElementType(typeof(List<EntityWithId>))
            .Should().Be(typeof(EntityWithId));
    }

    [Fact]
    public void GetCollectionElementType_IEnumerableOfGeoPoint_ReturnsGeoPoint()
    {
        ConventionHelpers.GetCollectionElementType(typeof(IEnumerable<GeoLocation>))
            .Should().Be(typeof(GeoLocation));
    }

    [Fact]
    public void GetCollectionElementType_NonGeneric_ReturnsNull()
    {
        ConventionHelpers.GetCollectionElementType(typeof(string))
            .Should().BeNull();
    }

    #endregion

    #region IsPrimitiveOrSimpleType Tests

    [Fact]
    public void IsPrimitiveOrSimpleType_Int_ReturnsTrue()
    {
        ConventionHelpers.IsPrimitiveOrSimpleType(typeof(int))
            .Should().BeTrue();
    }

    [Fact]
    public void IsPrimitiveOrSimpleType_Double_ReturnsTrue()
    {
        ConventionHelpers.IsPrimitiveOrSimpleType(typeof(double))
            .Should().BeTrue();
    }

    [Fact]
    public void IsPrimitiveOrSimpleType_String_ReturnsTrue()
    {
        ConventionHelpers.IsPrimitiveOrSimpleType(typeof(string))
            .Should().BeTrue();
    }

    [Fact]
    public void IsPrimitiveOrSimpleType_Decimal_ReturnsTrue()
    {
        ConventionHelpers.IsPrimitiveOrSimpleType(typeof(decimal))
            .Should().BeTrue();
    }

    [Fact]
    public void IsPrimitiveOrSimpleType_DateTime_ReturnsTrue()
    {
        ConventionHelpers.IsPrimitiveOrSimpleType(typeof(DateTime))
            .Should().BeTrue();
    }

    [Fact]
    public void IsPrimitiveOrSimpleType_Guid_ReturnsTrue()
    {
        ConventionHelpers.IsPrimitiveOrSimpleType(typeof(Guid))
            .Should().BeTrue();
    }

    [Fact]
    public void IsPrimitiveOrSimpleType_NullableInt_ReturnsTrue()
    {
        ConventionHelpers.IsPrimitiveOrSimpleType(typeof(int?))
            .Should().BeTrue();
    }

    [Fact]
    public void IsPrimitiveOrSimpleType_Enum_ReturnsTrue()
    {
        ConventionHelpers.IsPrimitiveOrSimpleType(typeof(DayOfWeek))
            .Should().BeTrue();
    }

    [Fact]
    public void IsPrimitiveOrSimpleType_Class_ReturnsFalse()
    {
        ConventionHelpers.IsPrimitiveOrSimpleType(typeof(ValueObject))
            .Should().BeFalse();
    }

    [Fact]
    public void IsPrimitiveOrSimpleType_List_ReturnsFalse()
    {
        ConventionHelpers.IsPrimitiveOrSimpleType(typeof(List<int>))
            .Should().BeFalse();
    }

    #endregion
}
