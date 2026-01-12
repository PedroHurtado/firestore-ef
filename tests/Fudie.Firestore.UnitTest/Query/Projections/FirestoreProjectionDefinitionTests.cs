using Fudie.Firestore.EntityFrameworkCore.Query.Projections;
using Xunit;

namespace Fudie.Firestore.UnitTest.Query.Projections
{
    /// <summary>
    /// Tests for FirestoreProjectionDefinition class.
    /// Verifies projection definition is constructed correctly for all 8 Select cases.
    /// </summary>
    public class FirestoreProjectionDefinitionTests
    {
        #region Case 1: e => e (Entity)

        [Fact]
        public void EntityProjection_HasEntityResultType()
        {
            var projection = FirestoreProjectionDefinition.CreateEntityProjection(typeof(TestEntity));

            Assert.Equal(ProjectionResultType.Entity, projection.ResultType);
        }

        [Fact]
        public void EntityProjection_HasNullFields()
        {
            var projection = FirestoreProjectionDefinition.CreateEntityProjection(typeof(TestEntity));

            Assert.Null(projection.Fields);
        }

        [Fact]
        public void EntityProjection_HasEmptySubcollections()
        {
            var projection = FirestoreProjectionDefinition.CreateEntityProjection(typeof(TestEntity));

            Assert.Empty(projection.Subcollections);
        }

        #endregion

        #region Case 2: e => e.Name (SingleField)

        [Fact]
        public void SingleFieldProjection_HasSingleFieldResultType()
        {
            var field = new FirestoreProjectedField("Name", "Name", typeof(string));
            var projection = FirestoreProjectionDefinition.CreateSingleFieldProjection(typeof(string), field);

            Assert.Equal(ProjectionResultType.SingleField, projection.ResultType);
        }

        [Fact]
        public void SingleFieldProjection_HasOneField()
        {
            var field = new FirestoreProjectedField("Name", "Name", typeof(string));
            var projection = FirestoreProjectionDefinition.CreateSingleFieldProjection(typeof(string), field);

            Assert.Single(projection.Fields!);
            Assert.Equal("Name", projection.Fields![0].FieldPath);
        }

        #endregion

        #region Case 3: e => new { e.Id, e.Name } (AnonymousType)

        [Fact]
        public void AnonymousTypeProjection_HasAnonymousTypeResultType()
        {
            var fields = new List<FirestoreProjectedField>
            {
                new("Id", "Id", typeof(int)),
                new("Name", "Name", typeof(string))
            };
            var projection = FirestoreProjectionDefinition.CreateAnonymousTypeProjection(typeof(object), fields);

            Assert.Equal(ProjectionResultType.AnonymousType, projection.ResultType);
        }

        [Fact]
        public void AnonymousTypeProjection_HasMultipleFields()
        {
            var fields = new List<FirestoreProjectedField>
            {
                new("Id", "Id", typeof(int)),
                new("Name", "Name", typeof(string)),
                new("Price", "Price", typeof(decimal))
            };
            var projection = FirestoreProjectionDefinition.CreateAnonymousTypeProjection(typeof(object), fields);

            Assert.Equal(3, projection.Fields!.Count);
        }

        #endregion

        #region Case 4: e => new Dto { Id = e.Id } (DtoClass)

        [Fact]
        public void DtoClassProjection_HasDtoClassResultType()
        {
            var fields = new List<FirestoreProjectedField>
            {
                new("Id", "Id", typeof(int))
            };
            var projection = FirestoreProjectionDefinition.CreateDtoClassProjection(typeof(TestDto), fields);

            Assert.Equal(ProjectionResultType.DtoClass, projection.ResultType);
        }

        [Fact]
        public void DtoClassProjection_SetsClrType()
        {
            var fields = new List<FirestoreProjectedField>
            {
                new("Id", "Id", typeof(int))
            };
            var projection = FirestoreProjectionDefinition.CreateDtoClassProjection(typeof(TestDto), fields);

            Assert.Equal(typeof(TestDto), projection.ClrType);
        }

        #endregion

        #region Case 5: e => new Record(e.Id, e.Name) (Record)

        [Fact]
        public void RecordProjection_HasRecordResultType()
        {
            var fields = new List<FirestoreProjectedField>
            {
                new("Id", "Id", typeof(int), constructorParameterIndex: 0),
                new("Name", "Name", typeof(string), constructorParameterIndex: 1)
            };
            var projection = FirestoreProjectionDefinition.CreateRecordProjection(typeof(TestRecord), fields);

            Assert.Equal(ProjectionResultType.Record, projection.ResultType);
        }

        [Fact]
        public void RecordProjection_FieldsHaveConstructorParameterIndices()
        {
            var fields = new List<FirestoreProjectedField>
            {
                new("Id", "Id", typeof(int), constructorParameterIndex: 0),
                new("Name", "Name", typeof(string), constructorParameterIndex: 1)
            };
            var projection = FirestoreProjectionDefinition.CreateRecordProjection(typeof(TestRecord), fields);

            Assert.Equal(0, projection.Fields![0].ConstructorParameterIndex);
            Assert.Equal(1, projection.Fields![1].ConstructorParameterIndex);
        }

        #endregion

        #region Case 6: e => e.Direccion (ComplexType - SingleField)

        [Fact]
        public void ComplexTypeProjection_IsSingleFieldResultType()
        {
            var field = new FirestoreProjectedField("Direccion", "Direccion", typeof(TestDireccion));
            var projection = FirestoreProjectionDefinition.CreateSingleFieldProjection(typeof(TestDireccion), field);

            Assert.Equal(ProjectionResultType.SingleField, projection.ResultType);
        }

        #endregion

        #region Case 7 & 8: Subcollections

        [Fact]
        public void ProjectionWithSubcollection_HasSubcollections()
        {
            var subcollection = new FirestoreSubcollectionProjection("Pedidos", "Pedidos", "Pedidos", true, typeof(TestEntity));
            var projection = FirestoreProjectionDefinition.CreateEntityProjection(typeof(TestEntity));
            projection.Subcollections.Add(subcollection);

            Assert.Single(projection.Subcollections);
            Assert.Equal("Pedidos", projection.Subcollections[0].NavigationName);
        }

        [Fact]
        public void AnonymousTypeWithSubcollection_HasBothFieldsAndSubcollections()
        {
            var fields = new List<FirestoreProjectedField>
            {
                new("Name", "Name", typeof(string))
            };
            var subcollection = new FirestoreSubcollectionProjection("Pedidos", "Items", "Pedidos", true, typeof(TestEntity));

            var projection = FirestoreProjectionDefinition.CreateAnonymousTypeProjection(typeof(object), fields);
            projection.Subcollections.Add(subcollection);

            Assert.Single(projection.Fields!);
            Assert.Single(projection.Subcollections);
            Assert.Equal("Items", projection.Subcollections[0].ResultName);
        }

        #endregion

        #region Helper types

        private class TestEntity
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        private class TestDto
        {
            public int Id { get; set; }
        }

        private record TestRecord(int Id, string Name);

        private class TestDireccion
        {
            public string Ciudad { get; set; } = string.Empty;
        }

        #endregion
    }
}
