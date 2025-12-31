using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Projections;
using Firestore.EntityFrameworkCore.Query.Translators;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Xunit;

namespace Fudie.Firestore.UnitTest.Query.Translators
{
    /// <summary>
    /// Unit tests for ProjectionExtractionVisitor.
    /// Tests extraction of projection information from lambda expressions.
    /// </summary>
    public class ProjectionExtractionVisitorTests
    {
        private static IFirestoreCollectionManager CreateCollectionManagerMock()
        {
            var mock = new Mock<IFirestoreCollectionManager>();
            mock.Setup(m => m.GetCollectionName(It.IsAny<Type>()))
                .Returns((Type t) => t.Name.ToLower() + "s");
            return mock.Object;
        }

        private static ProjectionExtractionVisitor CreateVisitor()
            => new ProjectionExtractionVisitor(CreateCollectionManagerMock());
        #region Case 1: Identity projection (e => e)

        [Fact]
        public void Extract_IdentityProjection_ReturnsNull()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntity, TestEntity>> selector = e => e;

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Extract_TypeConversionProjection_ReturnsNull()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestDerivedEntity, TestEntity>> selector = e => e;

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region Case 2: SingleField projection (e => e.Name)

        [Fact]
        public void Extract_SingleField_ReturnsSingleFieldProjection()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntity, string>> selector = e => e.Name;

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ProjectionResultType.SingleField, result.ResultType);
            Assert.Equal(typeof(string), result.ClrType);
        }

        [Fact]
        public void Extract_SingleField_HasCorrectFieldPath()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntity, string>> selector = e => e.Name;

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.NotNull(result?.Fields);
            Assert.Single(result.Fields);
            Assert.Equal("Name", result.Fields[0].FieldPath);
            Assert.Equal("Name", result.Fields[0].ResultName);
        }

        [Fact]
        public void Extract_SingleField_IntProperty_ReturnsCorrectType()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntity, int>> selector = e => e.Quantity;

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(typeof(int), result.ClrType);
            Assert.Equal("Quantity", result.Fields![0].FieldPath);
        }

        [Fact]
        public void Extract_SingleField_DecimalProperty_ReturnsCorrectType()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntity, decimal>> selector = e => e.Price;

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(typeof(decimal), result.ClrType);
        }

        #endregion

        #region Case 3: AnonymousType projection (e => new { e.Id, e.Name })

        [Fact]
        public void Extract_AnonymousType_ReturnsAnonymousTypeProjection()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntity, object>> selector = e => new { e.Id, e.Name };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ProjectionResultType.AnonymousType, result.ResultType);
        }

        [Fact]
        public void Extract_AnonymousType_HasAllFields()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntity, object>> selector = e => new { e.Id, e.Name, e.Price };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.NotNull(result?.Fields);
            Assert.Equal(3, result.Fields.Count);
        }

        [Fact]
        public void Extract_AnonymousType_FieldsHaveCorrectNames()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntity, object>> selector = e => new { e.Id, e.Name };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.Contains(result!.Fields!, f => f.ResultName == "Id");
            Assert.Contains(result.Fields!, f => f.ResultName == "Name");
        }

        [Fact]
        public void Extract_AnonymousType_WithAlias_UsesAliasAsResultName()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntity, object>> selector = e => new { Identifier = e.Id, ProductName = e.Name };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.Contains(result!.Fields!, f => f.ResultName == "Identifier" && f.FieldPath == "Id");
            Assert.Contains(result.Fields!, f => f.ResultName == "ProductName" && f.FieldPath == "Name");
        }

        #endregion

        #region Case 4: DtoClass projection (e => new Dto { Id = e.Id })

        [Fact]
        public void Extract_DtoClass_ReturnsDtoClassProjection()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntity, TestDto>> selector = e => new TestDto { Id = e.Id, Name = e.Name };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ProjectionResultType.DtoClass, result.ResultType);
            Assert.Equal(typeof(TestDto), result.ClrType);
        }

        [Fact]
        public void Extract_DtoClass_HasAllAssignedFields()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntity, TestDto>> selector = e => new TestDto { Id = e.Id, Name = e.Name, Price = e.Price };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.NotNull(result?.Fields);
            Assert.Equal(3, result.Fields.Count);
        }

        [Fact]
        public void Extract_DtoClass_FieldsHaveCorrectMapping()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntity, TestDto>> selector = e => new TestDto { Id = e.Id, Name = e.Name };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.Contains(result!.Fields!, f => f.ResultName == "Id" && f.FieldPath == "Id");
            Assert.Contains(result.Fields!, f => f.ResultName == "Name" && f.FieldPath == "Name");
        }

        #endregion

        #region Case 5: Record projection (e => new Record(e.Id, e.Name))

        [Fact]
        public void Extract_Record_ReturnsRecordProjection()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntity, TestRecord>> selector = e => new TestRecord(e.Id, e.Name, e.Price);

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ProjectionResultType.Record, result.ResultType);
            Assert.Equal(typeof(TestRecord), result.ClrType);
        }

        [Fact]
        public void Extract_Record_FieldsHaveConstructorIndices()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntity, TestRecord>> selector = e => new TestRecord(e.Id, e.Name, e.Price);

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.NotNull(result?.Fields);
            Assert.Equal(3, result.Fields.Count);
            Assert.Contains(result.Fields, f => f.ConstructorParameterIndex == 0);
            Assert.Contains(result.Fields, f => f.ConstructorParameterIndex == 1);
            Assert.Contains(result.Fields, f => f.ConstructorParameterIndex == 2);
        }

        [Fact]
        public void Extract_Record_FieldsAreMarkedAsConstructorParameters()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntity, TestRecord>> selector = e => new TestRecord(e.Id, e.Name, e.Price);

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.All(result!.Fields!, f => Assert.True(f.IsConstructorParameter));
        }

        #endregion

        #region Case 6: ComplexType projection (e => e.Direccion)

        [Fact]
        public void Extract_ComplexType_ReturnsSingleFieldProjection()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithComplexType, TestDireccion>> selector = e => e.Direccion;

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ProjectionResultType.SingleField, result.ResultType);
            Assert.Equal(typeof(TestDireccion), result.ClrType);
        }

        [Fact]
        public void Extract_ComplexType_HasCorrectFieldPath()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithComplexType, TestDireccion>> selector = e => e.Direccion;

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.Single(result!.Fields!);
            Assert.Equal("Direccion", result.Fields![0].FieldPath);
        }

        #endregion

        #region Case 7: Nested field projection (e => e.Direccion.Ciudad)

        [Fact]
        public void Extract_NestedField_HasDottedFieldPath()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithComplexType, string>> selector = e => e.Direccion.Ciudad;

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ProjectionResultType.SingleField, result.ResultType);
            Assert.Equal("Direccion.Ciudad", result.Fields![0].FieldPath);
        }

        [Fact]
        public void Extract_NestedField_ResultNameIsLastSegment()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithComplexType, string>> selector = e => e.Direccion.Ciudad;

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.Equal("Ciudad", result!.Fields![0].ResultName);
        }

        [Fact]
        public void Extract_DeeplyNestedField_HasFullPath()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithComplexType, double>> selector = e => e.Direccion.Coordenadas.Latitud;

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.Equal("Direccion.Coordenadas.Latitud", result!.Fields![0].FieldPath);
        }

        [Fact]
        public void Extract_AnonymousType_WithNestedField_HasCorrectPaths()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithComplexType, object>> selector = e => new
            {
                e.Nombre,
                Ciudad = e.Direccion.Ciudad
            };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.Contains(result!.Fields!, f => f.FieldPath == "Nombre");
            Assert.Contains(result.Fields!, f => f.FieldPath == "Direccion.Ciudad" && f.ResultName == "Ciudad");
        }

        #endregion

        #region Case 8: Subcollection projection (e => new { e.Nombre, e.Pedidos })

        [Fact]
        public void Extract_WithSubcollection_DetectsSubcollection()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithSubcollection, object>> selector = e => new { e.Nombre, e.Pedidos };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.Subcollections);
            Assert.Equal("Pedidos", result.Subcollections[0].NavigationName);
        }

        [Fact]
        public void Extract_WithSubcollection_HasBothFieldsAndSubcollections()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithSubcollection, object>> selector = e => new { e.Nombre, e.Pedidos };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.Single(result!.Fields!);
            Assert.Equal("Nombre", result.Fields![0].FieldPath);
            Assert.Single(result.Subcollections);
        }

        [Fact]
        public void Extract_SubcollectionWithAlias_UsesAliasAsResultName()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithSubcollection, object>> selector = e => new { e.Nombre, Orders = e.Pedidos };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.Equal("Orders", result!.Subcollections[0].ResultName);
            Assert.Equal("Pedidos", result.Subcollections[0].NavigationName);
        }

        #endregion

        #region Case 9: Subcollection with Where

        [Fact]
        public void Extract_SubcollectionWithWhere_ExtractsFilters()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithSubcollection, object>> selector = e => new
            {
                e.Nombre,
                PedidosActivos = e.Pedidos.Where(p => p.Estado == "Activo")
            };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.Single(result!.Subcollections);
            Assert.NotEmpty(result.Subcollections[0].Filters);
        }

        [Fact]
        public void Extract_SubcollectionWithWhere_FilterHasCorrectProperty()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithSubcollection, object>> selector = e => new
            {
                PedidosActivos = e.Pedidos.Where(p => p.Estado == "Activo")
            };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            var filter = result!.Subcollections[0].Filters[0];
            Assert.Equal("Estado", filter.PropertyName);
        }

        #endregion

        #region Case 10: Subcollection with OrderBy

        [Fact]
        public void Extract_SubcollectionWithOrderBy_ExtractsOrdering()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithSubcollection, object>> selector = e => new
            {
                PedidosOrdenados = e.Pedidos.OrderBy(p => p.Total)
            };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.Single(result!.Subcollections);
            Assert.Single(result.Subcollections[0].OrderByClauses);
            Assert.Equal("Total", result.Subcollections[0].OrderByClauses[0].PropertyName);
        }

        [Fact]
        public void Extract_SubcollectionWithOrderByDescending_HasDescendingOrder()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithSubcollection, object>> selector = e => new
            {
                PedidosOrdenados = e.Pedidos.OrderByDescending(p => p.Total)
            };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.True(result!.Subcollections[0].OrderByClauses[0].Descending);
        }

        [Fact]
        public void Extract_SubcollectionWithThenBy_HasMultipleOrderClauses()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithSubcollection, object>> selector = e => new
            {
                Pedidos = e.Pedidos.OrderBy(p => p.Estado).ThenBy(p => p.Total)
            };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.Equal(2, result!.Subcollections[0].OrderByClauses.Count);
        }

        #endregion

        #region Case 11: Subcollection with Take

        [Fact]
        public void Extract_SubcollectionWithTake_ExtractsLimit()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithSubcollection, object>> selector = e => new
            {
                TopPedidos = e.Pedidos.Take(5)
            };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.Equal(5, result!.Subcollections[0].Limit);
        }

        #endregion

        #region Case 12: Subcollection with combined operations

        [Fact]
        public void Extract_SubcollectionWithWhereOrderByTake_ExtractsAllOperations()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithSubcollection, object>> selector = e => new
            {
                TopPedidosActivos = e.Pedidos
                    .Where(p => p.Estado == "Activo")
                    .OrderByDescending(p => p.Total)
                    .Take(3)
            };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            var subcollection = result!.Subcollections[0];
            Assert.NotEmpty(subcollection.Filters);
            Assert.Single(subcollection.OrderByClauses);
            Assert.True(subcollection.OrderByClauses[0].Descending);
            Assert.Equal(3, subcollection.Limit);
        }

        #endregion

        #region Case 13: Subcollection with nested Select

        [Fact]
        public void Extract_SubcollectionWithSelect_ExtractsNestedProjection()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithSubcollection, object>> selector = e => new
            {
                Totales = e.Pedidos.Select(p => p.Total)
            };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.Single(result!.Subcollections);
            Assert.NotNull(result.Subcollections[0].Fields);
            Assert.Single(result.Subcollections[0].Fields!);
        }

        [Fact]
        public void Extract_SubcollectionWithSelectAnonymous_ExtractsMultipleFields()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithSubcollection, object>> selector = e => new
            {
                PedidosResumen = e.Pedidos.Select(p => new { p.NumeroOrden, p.Total })
            };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            var fields = result!.Subcollections[0].Fields;
            Assert.NotNull(fields);
            Assert.Equal(2, fields.Count);
        }

        #endregion

        #region Case 14: Subcollection aggregations

        [Fact]
        public void Extract_SubcollectionWithCount_DetectsAggregation()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithSubcollection, object>> selector = e => new
            {
                CantidadPedidos = e.Pedidos.Count()
            };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.Single(result!.Subcollections);
            Assert.Equal(FirestoreAggregationType.Count, result.Subcollections[0].Aggregation);
        }

        [Fact]
        public void Extract_SubcollectionWithSum_DetectsAggregationWithProperty()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithSubcollection, object>> selector = e => new
            {
                TotalVentas = e.Pedidos.Sum(p => p.Total)
            };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            var subcollection = result!.Subcollections[0];
            Assert.Equal(FirestoreAggregationType.Sum, subcollection.Aggregation);
            Assert.Equal("Total", subcollection.AggregationPropertyName);
        }

        [Fact]
        public void Extract_SubcollectionWithAverage_DetectsAggregation()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithSubcollection, object>> selector = e => new
            {
                PromedioVentas = e.Pedidos.Average(p => p.Total)
            };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.Equal(FirestoreAggregationType.Average, result!.Subcollections[0].Aggregation);
        }

        [Fact]
        public void Extract_SubcollectionWithMin_DetectsAggregation()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithSubcollection, object>> selector = e => new
            {
                MinimoTotal = e.Pedidos.Min(p => p.Total)
            };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.Equal(FirestoreAggregationType.Min, result!.Subcollections[0].Aggregation);
        }

        [Fact]
        public void Extract_SubcollectionWithMax_DetectsAggregation()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithSubcollection, object>> selector = e => new
            {
                MaximoTotal = e.Pedidos.Max(p => p.Total)
            };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.Equal(FirestoreAggregationType.Max, result!.Subcollections[0].Aggregation);
        }

        #endregion

        #region Edge cases

        [Fact]
        public void Extract_NullSelector_ReturnsNull()
        {
            // Arrange
            var visitor = CreateVisitor();

            // Act
            var result = visitor.Extract(null!);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Extract_EmptyAnonymousType_ReturnsEmptyFields()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntity, object>> selector = e => new { };

            // Act
            var result = visitor.Extract(selector);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Fields!);
        }

        #endregion

        #region Unsupported expressions - should throw NotSupportedException

        [Fact]
        public void Extract_BinaryExpression_ThrowsNotSupportedException()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntity, decimal>> selector = e => e.Price * 1.21m;

            // Act & Assert
            Assert.Throws<NotSupportedException>(() => visitor.Extract(selector));
        }

        [Fact]
        public void Extract_ConstantInProjection_ThrowsNotSupportedException()
        {
            // Arrange
            var visitor = CreateVisitor();
            var fecha = DateTime.Now;
            Expression<Func<TestEntity, object>> selector = e => new { e.Name, Fecha = fecha };

            // Act & Assert
            Assert.Throws<NotSupportedException>(() => visitor.Extract(selector));
        }

        [Fact]
        public void Extract_MethodCallOnProperty_ThrowsNotSupportedException()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntity, string>> selector = e => e.Name.ToUpper();

            // Act & Assert
            Assert.Throws<NotSupportedException>(() => visitor.Extract(selector));
        }

        [Fact]
        public void Extract_ConditionalExpression_ThrowsNotSupportedException()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntity, string>> selector = e => e.Quantity > 0 ? "Disponible" : "Agotado";

            // Act & Assert
            Assert.Throws<NotSupportedException>(() => visitor.Extract(selector));
        }

        [Fact]
        public void Extract_SubcollectionWithSkip_ThrowsNotSupportedException()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithSubcollection, object>> selector = e => new
            {
                Pedidos = e.Pedidos.Skip(5)
            };

            // Act & Assert
            Assert.Throws<NotSupportedException>(() => visitor.Extract(selector));
        }

        [Fact]
        public void Extract_SubcollectionWithFirstOrDefault_ThrowsNotSupportedException()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithSubcollection, object>> selector = e => new
            {
                PrimerPedido = e.Pedidos.FirstOrDefault()
            };

            // Act & Assert
            Assert.Throws<NotSupportedException>(() => visitor.Extract(selector));
        }

        [Fact]
        public void Extract_SubcollectionWithAny_ThrowsNotSupportedException()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithSubcollection, object>> selector = e => new
            {
                TienePedidosGrandes = e.Pedidos.Any(p => p.Total > 100)
            };

            // Act & Assert
            Assert.Throws<NotSupportedException>(() => visitor.Extract(selector));
        }

        [Fact]
        public void Extract_SubcollectionWithAll_ThrowsNotSupportedException()
        {
            // Arrange
            var visitor = CreateVisitor();
            Expression<Func<TestEntityWithSubcollection, object>> selector = e => new
            {
                TodosActivos = e.Pedidos.All(p => p.Estado == "Activo")
            };

            // Act & Assert
            Assert.Throws<NotSupportedException>(() => visitor.Extract(selector));
        }

        #endregion

        #region Test helper types

        private class TestEntity
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal Price { get; set; }
        }

        private class TestDerivedEntity : TestEntity
        {
            public string Extra { get; set; } = string.Empty;
        }

        private class TestDto
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public decimal Price { get; set; }
        }

        private record TestRecord(string Id, string Name, decimal Price);

        private class TestDireccion
        {
            public string Ciudad { get; set; } = string.Empty;
            public string CodigoPostal { get; set; } = string.Empty;
            public TestCoordenadas Coordenadas { get; set; } = new();
        }

        private class TestCoordenadas
        {
            public double Latitud { get; set; }
            public double Longitud { get; set; }
        }

        private class TestEntityWithComplexType
        {
            public string Id { get; set; } = string.Empty;
            public string Nombre { get; set; } = string.Empty;
            public TestDireccion Direccion { get; set; } = new();
        }

        private class TestPedido
        {
            public string Id { get; set; } = string.Empty;
            public string NumeroOrden { get; set; } = string.Empty;
            public decimal Total { get; set; }
            public string Estado { get; set; } = string.Empty;
        }

        private class TestEntityWithSubcollection
        {
            public string Id { get; set; } = string.Empty;
            public string Nombre { get; set; } = string.Empty;
            public List<TestPedido> Pedidos { get; set; } = new();
        }

        #endregion
    }
}