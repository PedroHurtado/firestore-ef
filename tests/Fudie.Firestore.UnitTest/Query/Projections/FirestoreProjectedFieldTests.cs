using Firestore.EntityFrameworkCore.Query.Projections;
using Xunit;

namespace Fudie.Firestore.UnitTest.Query.Projections
{
    /// <summary>
    /// Tests for FirestoreProjectedField class.
    /// Verifies field projection metadata is stored correctly.
    /// </summary>
    public class FirestoreProjectedFieldTests
    {
        [Fact]
        public void Constructor_SetsFieldPath()
        {
            var field = new FirestoreProjectedField("Name", "Name", typeof(string));

            Assert.Equal("Name", field.FieldPath);
        }

        [Fact]
        public void Constructor_SetsResultName()
        {
            var field = new FirestoreProjectedField("Direccion.Ciudad", "Ciudad", typeof(string));

            Assert.Equal("Ciudad", field.ResultName);
        }

        [Fact]
        public void Constructor_SetsFieldType()
        {
            var field = new FirestoreProjectedField("Price", "Price", typeof(decimal));

            Assert.Equal(typeof(decimal), field.FieldType);
        }

        [Fact]
        public void ConstructorParameterIndex_DefaultsToMinusOne()
        {
            var field = new FirestoreProjectedField("Id", "Id", typeof(int));

            Assert.Equal(-1, field.ConstructorParameterIndex);
        }

        [Fact]
        public void ConstructorParameterIndex_CanBeSetForRecords()
        {
            var field = new FirestoreProjectedField("Id", "Id", typeof(int), constructorParameterIndex: 0);

            Assert.Equal(0, field.ConstructorParameterIndex);
        }

        [Fact]
        public void FieldPath_SupportsNestedPaths()
        {
            var field = new FirestoreProjectedField("Direccion.Coordenadas.Latitud", "Latitud", typeof(double));

            Assert.Equal("Direccion.Coordenadas.Latitud", field.FieldPath);
        }

        [Fact]
        public void IsConstructorParameter_ReturnsTrueWhenIndexIsSet()
        {
            var field = new FirestoreProjectedField("Name", "Name", typeof(string), constructorParameterIndex: 1);

            Assert.True(field.IsConstructorParameter);
        }

        [Fact]
        public void IsConstructorParameter_ReturnsFalseWhenIndexIsMinusOne()
        {
            var field = new FirestoreProjectedField("Name", "Name", typeof(string));

            Assert.False(field.IsConstructorParameter);
        }
    }
}
