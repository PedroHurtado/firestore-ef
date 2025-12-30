using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Projections;
using Firestore.EntityFrameworkCore.Query.Resolved;
using System;
using System.Collections.Generic;
using Xunit;

namespace Fudie.Firestore.UnitTest.Query.Resolved
{
    public class ResolvedTypesTests
    {
        #region ResolvedWhereClause Tests

        [Fact]
        public void ResolvedWhereClause_CanBeCreated_WithStringValue()
        {
            var clause = new ResolvedWhereClause("Name", FirestoreOperator.EqualTo, "John");

            Assert.Equal("Name", clause.PropertyName);
            Assert.Equal(FirestoreOperator.EqualTo, clause.Operator);
            Assert.Equal("John", clause.Value);
            Assert.Null(clause.EnumType);
        }

        [Fact]
        public void ResolvedWhereClause_CanBeCreated_WithIntValue()
        {
            var clause = new ResolvedWhereClause("Age", FirestoreOperator.GreaterThan, 18);

            Assert.Equal("Age", clause.PropertyName);
            Assert.Equal(FirestoreOperator.GreaterThan, clause.Operator);
            Assert.Equal(18, clause.Value);
        }

        [Fact]
        public void ResolvedWhereClause_CanBeCreated_WithEnumType()
        {
            var clause = new ResolvedWhereClause("Status", FirestoreOperator.EqualTo, 1, typeof(DayOfWeek));

            Assert.Equal("Status", clause.PropertyName);
            Assert.Equal(typeof(DayOfWeek), clause.EnumType);
        }

        [Fact]
        public void ResolvedWhereClause_CanBeCreated_WithNullValue()
        {
            var clause = new ResolvedWhereClause("Name", FirestoreOperator.EqualTo, null);

            Assert.Null(clause.Value);
        }

        [Fact]
        public void ResolvedWhereClause_CanBeCreated_WithListValue_ForInOperator()
        {
            var values = new List<string> { "a", "b", "c" };
            var clause = new ResolvedWhereClause("Category", FirestoreOperator.In, values);

            Assert.Equal(FirestoreOperator.In, clause.Operator);
            Assert.Equal(values, clause.Value);
        }

        [Fact]
        public void ResolvedWhereClause_ToString_FormatsCorrectly()
        {
            var clause = new ResolvedWhereClause("Price", FirestoreOperator.GreaterThanOrEqualTo, 100m);

            Assert.Equal("Price >= 100", clause.ToString());
        }

        #endregion

        #region ResolvedOrFilterGroup Tests

        [Fact]
        public void ResolvedOrFilterGroup_CanBeCreated_WithMultipleClauses()
        {
            var clauses = new[]
            {
                new ResolvedWhereClause("Name", FirestoreOperator.EqualTo, "John"),
                new ResolvedWhereClause("Name", FirestoreOperator.EqualTo, "Jane")
            };

            var group = new ResolvedOrFilterGroup(clauses);

            Assert.Equal(2, group.Clauses.Count);
        }

        [Fact]
        public void ResolvedOrFilterGroup_ToString_FormatsCorrectly()
        {
            var clauses = new[]
            {
                new ResolvedWhereClause("Status", FirestoreOperator.EqualTo, "Active"),
                new ResolvedWhereClause("Status", FirestoreOperator.EqualTo, "Pending")
            };

            var group = new ResolvedOrFilterGroup(clauses);

            Assert.StartsWith("OR(", group.ToString());
        }

        #endregion

        #region ResolvedFilterResult Tests

        [Fact]
        public void ResolvedFilterResult_Empty_HasNoClausesOrGroups()
        {
            var result = ResolvedFilterResult.Empty;

            Assert.Empty(result.AndClauses);
            Assert.Null(result.OrGroup);
            Assert.Null(result.NestedOrGroups);
            Assert.False(result.IsOrGroup);
        }

        [Fact]
        public void ResolvedFilterResult_FromClause_CreatesAndClause()
        {
            var clause = new ResolvedWhereClause("Name", FirestoreOperator.EqualTo, "John");
            var result = ResolvedFilterResult.FromClause(clause);

            Assert.Single(result.AndClauses);
            Assert.False(result.IsOrGroup);
        }

        [Fact]
        public void ResolvedFilterResult_FromOrGroup_CreatesOrResult()
        {
            var orGroup = new ResolvedOrFilterGroup(new[]
            {
                new ResolvedWhereClause("A", FirestoreOperator.EqualTo, 1),
                new ResolvedWhereClause("B", FirestoreOperator.EqualTo, 2)
            });

            var result = ResolvedFilterResult.FromOrGroup(orGroup);

            Assert.True(result.IsOrGroup);
            Assert.NotNull(result.OrGroup);
            Assert.Empty(result.AndClauses);
        }

        [Fact]
        public void ResolvedFilterResult_WithNestedOrGroups_HasNestedOrGroupsTrue()
        {
            var nestedGroups = new[]
            {
                new ResolvedOrFilterGroup(new[] { new ResolvedWhereClause("X", FirestoreOperator.EqualTo, 1) })
            };

            var result = new ResolvedFilterResult(
                new[] { new ResolvedWhereClause("A", FirestoreOperator.EqualTo, 1) },
                null,
                nestedGroups);

            Assert.True(result.HasNestedOrGroups);
            Assert.False(result.IsOrGroup);
        }

        #endregion

        #region ResolvedOrderByClause Tests

        [Fact]
        public void ResolvedOrderByClause_DefaultsToAscending()
        {
            var clause = new ResolvedOrderByClause("Name");

            Assert.False(clause.Descending);
            Assert.Contains("ASC", clause.ToString());
        }

        [Fact]
        public void ResolvedOrderByClause_CanBeDescending()
        {
            var clause = new ResolvedOrderByClause("CreatedAt", Descending: true);

            Assert.True(clause.Descending);
            Assert.Contains("DESC", clause.ToString());
        }

        #endregion

        #region ResolvedPaginationInfo Tests

        [Fact]
        public void ResolvedPaginationInfo_None_HasNoPagination()
        {
            var pagination = ResolvedPaginationInfo.None;

            Assert.False(pagination.HasPagination);
            Assert.False(pagination.HasLimit);
            Assert.False(pagination.HasSkip);
            Assert.False(pagination.HasLimitToLast);
        }

        [Fact]
        public void ResolvedPaginationInfo_WithLimit_HasLimitTrue()
        {
            var pagination = new ResolvedPaginationInfo(Limit: 10);

            Assert.True(pagination.HasPagination);
            Assert.True(pagination.HasLimit);
            Assert.Equal(10, pagination.Limit);
        }

        [Fact]
        public void ResolvedPaginationInfo_WithSkip_HasSkipTrue()
        {
            var pagination = new ResolvedPaginationInfo(Skip: 5);

            Assert.True(pagination.HasPagination);
            Assert.True(pagination.HasSkip);
            Assert.Equal(5, pagination.Skip);
        }

        [Fact]
        public void ResolvedPaginationInfo_WithLimitToLast_HasLimitToLastTrue()
        {
            var pagination = new ResolvedPaginationInfo(LimitToLast: 3);

            Assert.True(pagination.HasPagination);
            Assert.True(pagination.HasLimitToLast);
            Assert.Equal(3, pagination.LimitToLast);
        }

        [Fact]
        public void ResolvedPaginationInfo_ToString_FormatsCorrectly()
        {
            var pagination = new ResolvedPaginationInfo(Limit: 10, Skip: 5);

            var str = pagination.ToString();
            Assert.Contains("Limit=10", str);
            Assert.Contains("Skip=5", str);
        }

        #endregion

        #region ResolvedCursor Tests

        [Fact]
        public void ResolvedCursor_WithDocumentIdOnly()
        {
            var cursor = new ResolvedCursor("doc123");

            Assert.Equal("doc123", cursor.DocumentId);
            Assert.Null(cursor.OrderByValues);
        }

        [Fact]
        public void ResolvedCursor_WithOrderByValues()
        {
            var cursor = new ResolvedCursor("doc123", new object?[] { "John", 25 });

            Assert.Equal("doc123", cursor.DocumentId);
            Assert.Equal(2, cursor.OrderByValues!.Count);
        }

        [Fact]
        public void ResolvedCursor_ToString_FormatsCorrectly()
        {
            var cursor = new ResolvedCursor("doc123", new object?[] { "value1" });

            Assert.Contains("doc123", cursor.ToString());
            Assert.Contains("value1", cursor.ToString());
        }

        #endregion

        #region ResolvedInclude Tests

        [Fact]
        public void ResolvedInclude_BasicInclude_NoOperations()
        {
            var include = new ResolvedInclude(
                "Orders",
                IsCollection: true,
                FilterResults: Array.Empty<ResolvedFilterResult>(),
                OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
                Pagination: ResolvedPaginationInfo.None);

            Assert.Equal("Orders", include.NavigationName);
            Assert.True(include.IsCollection);
            Assert.False(include.HasOperations);
        }

        [Fact]
        public void ResolvedInclude_WithFilterResults_HasOperationsTrue()
        {
            var filterResult = ResolvedFilterResult.FromClause(
                new ResolvedWhereClause("Status", FirestoreOperator.EqualTo, "Active"));

            var include = new ResolvedInclude(
                "Orders",
                IsCollection: true,
                FilterResults: new[] { filterResult },
                OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
                Pagination: ResolvedPaginationInfo.None);

            Assert.True(include.HasOperations);
            Assert.Equal(1, include.TotalFilterCount);
        }

        [Fact]
        public void ResolvedInclude_WithPagination_HasOperationsTrue()
        {
            var include = new ResolvedInclude(
                "Orders",
                IsCollection: true,
                FilterResults: Array.Empty<ResolvedFilterResult>(),
                OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
                Pagination: new ResolvedPaginationInfo(Limit: 5));

            Assert.True(include.HasOperations);
        }

        [Fact]
        public void ResolvedInclude_TotalFilterCount_CountsAllClauses()
        {
            // FilterResult with 2 AND clauses
            var filterResult1 = new ResolvedFilterResult(
                new[]
                {
                    new ResolvedWhereClause("A", FirestoreOperator.EqualTo, 1),
                    new ResolvedWhereClause("B", FirestoreOperator.EqualTo, 2)
                });

            // FilterResult with OR group (2 clauses)
            var orGroup = new ResolvedOrFilterGroup(new[]
            {
                new ResolvedWhereClause("C", FirestoreOperator.EqualTo, 3),
                new ResolvedWhereClause("D", FirestoreOperator.EqualTo, 4)
            });
            var filterResult2 = ResolvedFilterResult.FromOrGroup(orGroup);

            var include = new ResolvedInclude(
                "Orders",
                IsCollection: true,
                FilterResults: new[] { filterResult1, filterResult2 },
                OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
                Pagination: ResolvedPaginationInfo.None);

            Assert.Equal(4, include.TotalFilterCount);
        }

        #endregion

        #region ResolvedProjectionDefinition Tests

        [Fact]
        public void ResolvedProjectionDefinition_EntityProjection_NoFields()
        {
            var projection = new ResolvedProjectionDefinition(
                ProjectionResultType.Entity,
                typeof(object),
                Fields: null,
                Subcollections: Array.Empty<ResolvedSubcollectionProjection>());

            Assert.Equal(ProjectionResultType.Entity, projection.ResultType);
            Assert.False(projection.HasFields);
            Assert.False(projection.HasSubcollections);
        }

        [Fact]
        public void ResolvedProjectionDefinition_WithFields_HasFieldsTrue()
        {
            var fields = new[] { new FirestoreProjectedField("Name", "Name", typeof(string)) };
            var projection = new ResolvedProjectionDefinition(
                ProjectionResultType.AnonymousType,
                typeof(object),
                Fields: fields,
                Subcollections: Array.Empty<ResolvedSubcollectionProjection>());

            Assert.True(projection.HasFields);
        }

        #endregion

        #region ResolvedSubcollectionProjection Tests

        [Fact]
        public void ResolvedSubcollectionProjection_Basic()
        {
            var subcollection = new ResolvedSubcollectionProjection(
                "Orders",
                "Orders",
                "orders",
                FilterResults: Array.Empty<ResolvedFilterResult>(),
                OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
                Pagination: ResolvedPaginationInfo.None,
                Fields: null,
                Aggregation: null,
                AggregationPropertyName: null,
                NestedSubcollections: Array.Empty<ResolvedSubcollectionProjection>());

            Assert.Equal("Orders", subcollection.NavigationName);
            Assert.Equal("orders", subcollection.CollectionName);
            Assert.False(subcollection.IsAggregation);
        }

        [Fact]
        public void ResolvedSubcollectionProjection_WithAggregation_IsAggregationTrue()
        {
            var subcollection = new ResolvedSubcollectionProjection(
                "Orders",
                "OrderCount",
                "orders",
                FilterResults: Array.Empty<ResolvedFilterResult>(),
                OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
                Pagination: ResolvedPaginationInfo.None,
                Fields: null,
                Aggregation: FirestoreAggregationType.Count,
                AggregationPropertyName: null,
                NestedSubcollections: Array.Empty<ResolvedSubcollectionProjection>());

            Assert.True(subcollection.IsAggregation);
        }

        [Fact]
        public void ResolvedSubcollectionProjection_TotalFilterCount_CountsAllClauses()
        {
            var filterResult = new ResolvedFilterResult(
                new[]
                {
                    new ResolvedWhereClause("Status", FirestoreOperator.EqualTo, "Active"),
                    new ResolvedWhereClause("Amount", FirestoreOperator.GreaterThan, 100)
                });

            var subcollection = new ResolvedSubcollectionProjection(
                "Orders",
                "Orders",
                "orders",
                FilterResults: new[] { filterResult },
                OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
                Pagination: ResolvedPaginationInfo.None,
                Fields: null,
                Aggregation: null,
                AggregationPropertyName: null,
                NestedSubcollections: Array.Empty<ResolvedSubcollectionProjection>());

            Assert.Equal(2, subcollection.TotalFilterCount);
        }

        #endregion

        #region ResolvedFirestoreQuery Tests

        [Fact]
        public void ResolvedFirestoreQuery_Basic()
        {
            var query = new ResolvedFirestoreQuery(
                CollectionName: "customers",
                EntityClrType: typeof(object),
                FilterResults: Array.Empty<ResolvedFilterResult>(),
                OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
                Pagination: ResolvedPaginationInfo.None,
                StartAfterCursor: null,
                Includes: Array.Empty<ResolvedInclude>(),
                AggregationType: FirestoreAggregationType.None,
                AggregationPropertyName: null,
                AggregationResultType: null,
                Projection: null,
                ReturnDefault: false,
                ReturnType: null,
                IsTracking: true);

            Assert.Equal("customers", query.CollectionName);
            Assert.False(query.IsAggregation);
            Assert.False(query.HasProjection);
        }

        [Fact]
        public void ResolvedFirestoreQuery_CountQuery()
        {
            var query = new ResolvedFirestoreQuery(
                CollectionName: "customers",
                EntityClrType: typeof(object),
                FilterResults: Array.Empty<ResolvedFilterResult>(),
                OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
                Pagination: ResolvedPaginationInfo.None,
                StartAfterCursor: null,
                Includes: Array.Empty<ResolvedInclude>(),
                AggregationType: FirestoreAggregationType.Count,
                AggregationPropertyName: null,
                AggregationResultType: typeof(int),
                Projection: null,
                ReturnDefault: false,
                ReturnType: null,
                IsTracking: false);

            Assert.True(query.IsAggregation);
            Assert.True(query.IsCountQuery);
            Assert.False(query.IsAnyQuery);
        }

        [Fact]
        public void ResolvedFirestoreQuery_AnyQuery()
        {
            var query = new ResolvedFirestoreQuery(
                CollectionName: "customers",
                EntityClrType: typeof(object),
                FilterResults: Array.Empty<ResolvedFilterResult>(),
                OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
                Pagination: ResolvedPaginationInfo.None,
                StartAfterCursor: null,
                Includes: Array.Empty<ResolvedInclude>(),
                AggregationType: FirestoreAggregationType.Any,
                AggregationPropertyName: null,
                AggregationResultType: typeof(bool),
                Projection: null,
                ReturnDefault: false,
                ReturnType: null,
                IsTracking: false);

            Assert.True(query.IsAggregation);
            Assert.True(query.IsAnyQuery);
            Assert.False(query.IsCountQuery);
        }

        [Fact]
        public void ResolvedFirestoreQuery_WithFilterResultsAndPagination()
        {
            var filterResult = new ResolvedFilterResult(
                new[]
                {
                    new ResolvedWhereClause("Status", FirestoreOperator.EqualTo, "Active"),
                    new ResolvedWhereClause("Age", FirestoreOperator.GreaterThan, 18)
                });

            var query = new ResolvedFirestoreQuery(
                CollectionName: "customers",
                EntityClrType: typeof(object),
                FilterResults: new[] { filterResult },
                OrderByClauses: new[] { new ResolvedOrderByClause("Name") },
                Pagination: new ResolvedPaginationInfo(Limit: 10, Skip: 5),
                StartAfterCursor: null,
                Includes: Array.Empty<ResolvedInclude>(),
                AggregationType: FirestoreAggregationType.None,
                AggregationPropertyName: null,
                AggregationResultType: null,
                Projection: null,
                ReturnDefault: false,
                ReturnType: null,
                IsTracking: true);

            Assert.Equal(2, query.TotalFilterCount);
            Assert.Single(query.OrderByClauses);
            Assert.Equal(10, query.Pagination.Limit);
            Assert.Equal(5, query.Pagination.Skip);
        }

        [Fact]
        public void ResolvedFirestoreQuery_ToString_IncludesKeyInfo()
        {
            var filterResult = ResolvedFilterResult.FromClause(
                new ResolvedWhereClause("Price", FirestoreOperator.GreaterThan, 100));

            var query = new ResolvedFirestoreQuery(
                CollectionName: "products",
                EntityClrType: typeof(object),
                FilterResults: new[] { filterResult },
                OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
                Pagination: new ResolvedPaginationInfo(Limit: 20),
                StartAfterCursor: null,
                Includes: Array.Empty<ResolvedInclude>(),
                AggregationType: FirestoreAggregationType.None,
                AggregationPropertyName: null,
                AggregationResultType: null,
                Projection: null,
                ReturnDefault: false,
                ReturnType: null,
                IsTracking: true);

            var str = query.ToString();
            Assert.Contains("products", str);
            Assert.Contains("Filters", str);
            Assert.Contains("Limit: 20", str);
        }

        [Fact]
        public void ResolvedFirestoreQuery_TotalFilterCount_CountsAllFilterResults()
        {
            // FilterResult 1: 2 AND clauses
            var filterResult1 = new ResolvedFilterResult(
                new[]
                {
                    new ResolvedWhereClause("A", FirestoreOperator.EqualTo, 1),
                    new ResolvedWhereClause("B", FirestoreOperator.EqualTo, 2)
                });

            // FilterResult 2: OR group with 3 clauses
            var orGroup = new ResolvedOrFilterGroup(new[]
            {
                new ResolvedWhereClause("C", FirestoreOperator.EqualTo, 3),
                new ResolvedWhereClause("D", FirestoreOperator.EqualTo, 4),
                new ResolvedWhereClause("E", FirestoreOperator.EqualTo, 5)
            });
            var filterResult2 = ResolvedFilterResult.FromOrGroup(orGroup);

            var query = new ResolvedFirestoreQuery(
                CollectionName: "test",
                EntityClrType: typeof(object),
                FilterResults: new[] { filterResult1, filterResult2 },
                OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
                Pagination: ResolvedPaginationInfo.None,
                StartAfterCursor: null,
                Includes: Array.Empty<ResolvedInclude>(),
                AggregationType: FirestoreAggregationType.None,
                AggregationPropertyName: null,
                AggregationResultType: null,
                Projection: null,
                ReturnDefault: false,
                ReturnType: null,
                IsTracking: true);

            Assert.Equal(5, query.TotalFilterCount);
        }

        #endregion
    }
}
