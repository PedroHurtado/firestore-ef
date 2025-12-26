using Firestore.EntityFrameworkCore.Query;
using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Projections;
using Xunit;

namespace Fudie.Firestore.UnitTest.Query.Projections
{
    /// <summary>
    /// Tests for FirestoreSubcollectionProjection class.
    /// Verifies subcollection projection with filters, ordering, and limits.
    /// </summary>
    public class FirestoreSubcollectionProjectionTests
    {
        #region Basic Properties

        [Fact]
        public void Constructor_SetsNavigationName()
        {
            var subcollection = new FirestoreSubcollectionProjection("Pedidos", "Pedidos", "Pedidos");

            Assert.Equal("Pedidos", subcollection.NavigationName);
        }

        [Fact]
        public void Constructor_SetsResultName()
        {
            var subcollection = new FirestoreSubcollectionProjection("Pedidos", "TopPedidos", "Pedidos");

            Assert.Equal("TopPedidos", subcollection.ResultName);
        }

        [Fact]
        public void Constructor_SetsCollectionName()
        {
            var subcollection = new FirestoreSubcollectionProjection("Pedidos", "Pedidos", "orders");

            Assert.Equal("orders", subcollection.CollectionName);
        }

        [Fact]
        public void Filters_DefaultsToEmptyList()
        {
            var subcollection = new FirestoreSubcollectionProjection("Pedidos", "Pedidos", "Pedidos");

            Assert.Empty(subcollection.Filters);
        }

        [Fact]
        public void OrderByClauses_DefaultsToEmptyList()
        {
            var subcollection = new FirestoreSubcollectionProjection("Pedidos", "Pedidos", "Pedidos");

            Assert.Empty(subcollection.OrderByClauses);
        }

        [Fact]
        public void Limit_DefaultsToNull()
        {
            var subcollection = new FirestoreSubcollectionProjection("Pedidos", "Pedidos", "Pedidos");

            Assert.Null(subcollection.Limit);
        }

        [Fact]
        public void Fields_DefaultsToNull()
        {
            var subcollection = new FirestoreSubcollectionProjection("Pedidos", "Pedidos", "Pedidos");

            Assert.Null(subcollection.Fields);
        }

        #endregion

        #region Filters (reutiliza FirestoreWhereClause)

        [Fact]
        public void AddFilter_AddsFirestoreWhereClause()
        {
            var subcollection = new FirestoreSubcollectionProjection("Pedidos", "Pedidos", "Pedidos");
            var filter = new FirestoreWhereClause("Estado", FirestoreOperator.EqualTo, System.Linq.Expressions.Expression.Constant(1));

            subcollection.Filters.Add(filter);

            Assert.Single(subcollection.Filters);
            Assert.Equal("Estado", subcollection.Filters[0].PropertyName);
        }

        [Fact]
        public void Filters_CanHaveMultipleClauses()
        {
            var subcollection = new FirestoreSubcollectionProjection("Pedidos", "Pedidos", "Pedidos");
            subcollection.Filters.Add(new FirestoreWhereClause("Estado", FirestoreOperator.EqualTo, System.Linq.Expressions.Expression.Constant(1)));
            subcollection.Filters.Add(new FirestoreWhereClause("Total", FirestoreOperator.GreaterThan, System.Linq.Expressions.Expression.Constant(100m)));

            Assert.Equal(2, subcollection.Filters.Count);
        }

        #endregion

        #region OrderBy (reutiliza FirestoreOrderByClause)

        [Fact]
        public void AddOrderBy_AddsFirestoreOrderByClause()
        {
            var subcollection = new FirestoreSubcollectionProjection("Pedidos", "Pedidos", "Pedidos");
            var orderBy = new FirestoreOrderByClause("Total", descending: true);

            subcollection.OrderByClauses.Add(orderBy);

            Assert.Single(subcollection.OrderByClauses);
            Assert.Equal("Total", subcollection.OrderByClauses[0].PropertyName);
            Assert.True(subcollection.OrderByClauses[0].Descending);
        }

        #endregion

        #region Limit (Take)

        [Fact]
        public void Limit_CanBeSet()
        {
            var subcollection = new FirestoreSubcollectionProjection("Pedidos", "Pedidos", "Pedidos");
            subcollection.Limit = 5;

            Assert.Equal(5, subcollection.Limit);
        }

        #endregion

        #region Fields (proyección de subcollection)

        [Fact]
        public void Fields_CanBeSetForProjectedSubcollection()
        {
            var subcollection = new FirestoreSubcollectionProjection("Pedidos", "Pedidos", "Pedidos");
            subcollection.Fields = new List<FirestoreProjectedField>
            {
                new("Total", "Total", typeof(decimal))
            };

            Assert.Single(subcollection.Fields);
            Assert.Equal("Total", subcollection.Fields[0].FieldPath);
        }

        #endregion

        #region Aggregation

        [Fact]
        public void Aggregation_DefaultsToNull()
        {
            var subcollection = new FirestoreSubcollectionProjection("Pedidos", "Pedidos", "Pedidos");

            Assert.Null(subcollection.Aggregation);
        }

        [Fact]
        public void Aggregation_CanBeSetToCount()
        {
            var subcollection = new FirestoreSubcollectionProjection("Lineas", "CantidadLineas", "Lineas");
            subcollection.Aggregation = FirestoreAggregationType.Count;

            Assert.Equal(FirestoreAggregationType.Count, subcollection.Aggregation);
        }

        [Fact]
        public void Aggregation_CanHavePropertyNameForSum()
        {
            var subcollection = new FirestoreSubcollectionProjection("Lineas", "TotalLineas", "Lineas");
            subcollection.Aggregation = FirestoreAggregationType.Sum;
            subcollection.AggregationPropertyName = "Cantidad";

            Assert.Equal(FirestoreAggregationType.Sum, subcollection.Aggregation);
            Assert.Equal("Cantidad", subcollection.AggregationPropertyName);
        }

        #endregion

        #region Nested Subcollections

        [Fact]
        public void NestedSubcollections_DefaultsToEmptyList()
        {
            var subcollection = new FirestoreSubcollectionProjection("Pedidos", "Pedidos", "Pedidos");

            Assert.Empty(subcollection.NestedSubcollections);
        }

        [Fact]
        public void NestedSubcollections_CanContainChildSubcollections()
        {
            var pedidos = new FirestoreSubcollectionProjection("Pedidos", "Pedidos", "Pedidos");
            var lineas = new FirestoreSubcollectionProjection("Lineas", "Lineas", "Lineas");

            pedidos.NestedSubcollections.Add(lineas);

            Assert.Single(pedidos.NestedSubcollections);
            Assert.Equal("Lineas", pedidos.NestedSubcollections[0].NavigationName);
        }

        #endregion

        #region Complex Scenarios

        [Fact]
        public void ComplexScenario_FilteredOrderedLimitedSubcollection()
        {
            // Simula: e.Pedidos.Where(p => p.Estado == Confirmado).OrderByDescending(p => p.Total).Take(2)
            var subcollection = new FirestoreSubcollectionProjection("Pedidos", "TopPedidos", "Pedidos");

            subcollection.Filters.Add(new FirestoreWhereClause(
                "Estado",
                FirestoreOperator.EqualTo,
                System.Linq.Expressions.Expression.Constant("Confirmado")));

            subcollection.OrderByClauses.Add(new FirestoreOrderByClause("Total", descending: true));

            subcollection.Limit = 2;

            Assert.Single(subcollection.Filters);
            Assert.Single(subcollection.OrderByClauses);
            Assert.Equal(2, subcollection.Limit);
        }

        [Fact]
        public void ComplexScenario_NestedSubcollectionWithAggregation()
        {
            // Simula: e.Pedidos.Select(p => new { p.NumeroOrden, CantidadLineas = p.Lineas.Count() })
            var pedidos = new FirestoreSubcollectionProjection("Pedidos", "Pedidos", "Pedidos");
            pedidos.Fields = new List<FirestoreProjectedField>
            {
                new("NumeroOrden", "NumeroOrden", typeof(string))
            };

            var lineas = new FirestoreSubcollectionProjection("Lineas", "CantidadLineas", "Lineas");
            lineas.Aggregation = FirestoreAggregationType.Count;

            pedidos.NestedSubcollections.Add(lineas);

            Assert.Single(pedidos.Fields);
            Assert.Single(pedidos.NestedSubcollections);
            Assert.Equal(FirestoreAggregationType.Count, pedidos.NestedSubcollections[0].Aggregation);
        }

        #endregion
    }
}
