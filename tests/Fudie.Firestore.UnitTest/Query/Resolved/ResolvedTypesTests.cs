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
        public void ResolvedWhereClause_CanBeCreated_WithEnumValueAsString()
        {
            // Enum values are pre-converted to strings by the Resolver
            var clause = new ResolvedWhereClause("Status", FirestoreOperator.EqualTo, "Monday");

            Assert.Equal("Status", clause.PropertyName);
            Assert.Equal("Monday", clause.Value);
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

            // Format: (clause1 || clause2)
            Assert.Contains("||", group.ToString());
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
            // ASC is implicit - only shows property name
            Assert.Equal("Name", clause.ToString());
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
            Assert.Contains(".Limit(10)", str);
            Assert.Contains(".Offset(5)", str);
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
                NavigationName: "Orders",
                IsCollection: true,
                TargetEntityType: typeof(object),
                CollectionPath: "orders",
                DocumentId: null,
                FilterResults: Array.Empty<ResolvedFilterResult>(),
                OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
                Pagination: ResolvedPaginationInfo.None,
                NestedIncludes: Array.Empty<ResolvedInclude>());

            Assert.Equal("Orders", include.NavigationName);
            Assert.True(include.IsCollection);
            Assert.Equal("orders", include.CollectionPath);
            Assert.False(include.IsDocumentQuery);
            Assert.False(include.HasOperations);
        }

        [Fact]
        public void ResolvedInclude_WithDocumentId_IsDocumentQueryTrue()
        {
            var include = new ResolvedInclude(
                NavigationName: "Category",
                IsCollection: true,
                TargetEntityType: typeof(object),
                CollectionPath: "categories",
                DocumentId: "cat-456",
                FilterResults: Array.Empty<ResolvedFilterResult>(),
                OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
                Pagination: ResolvedPaginationInfo.None,
                NestedIncludes: Array.Empty<ResolvedInclude>());

            Assert.True(include.IsDocumentQuery);
            Assert.Equal("cat-456", include.DocumentId);
        }

        [Fact]
        public void ResolvedInclude_WithFilterResults_HasOperationsTrue()
        {
            var filterResult = ResolvedFilterResult.FromClause(
                new ResolvedWhereClause("Status", FirestoreOperator.EqualTo, "Active"));

            var include = new ResolvedInclude(
                NavigationName: "Orders",
                IsCollection: true,
                TargetEntityType: typeof(object),
                CollectionPath: "orders",
                DocumentId: null,
                FilterResults: new[] { filterResult },
                OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
                Pagination: ResolvedPaginationInfo.None,
                NestedIncludes: Array.Empty<ResolvedInclude>());

            Assert.True(include.HasOperations);
            Assert.Equal(1, include.TotalFilterCount);
        }

        [Fact]
        public void ResolvedInclude_WithPagination_HasOperationsTrue()
        {
            var include = new ResolvedInclude(
                NavigationName: "Orders",
                IsCollection: true,
                TargetEntityType: typeof(object),
                CollectionPath: "orders",
                DocumentId: null,
                FilterResults: Array.Empty<ResolvedFilterResult>(),
                OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
                Pagination: new ResolvedPaginationInfo(Limit: 5),
                NestedIncludes: Array.Empty<ResolvedInclude>());

            Assert.True(include.HasOperations);
        }

        [Fact]
        public void ResolvedInclude_WithNestedIncludes()
        {
            var nestedInclude = new ResolvedInclude(
                NavigationName: "Items",
                IsCollection: true,
                TargetEntityType: typeof(object),
                CollectionPath: "items",
                DocumentId: null,
                FilterResults: Array.Empty<ResolvedFilterResult>(),
                OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
                Pagination: ResolvedPaginationInfo.None,
                NestedIncludes: Array.Empty<ResolvedInclude>());

            var include = new ResolvedInclude(
                NavigationName: "Categories",
                IsCollection: true,
                TargetEntityType: typeof(object),
                CollectionPath: "categories",
                DocumentId: null,
                FilterResults: Array.Empty<ResolvedFilterResult>(),
                OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
                Pagination: ResolvedPaginationInfo.None,
                NestedIncludes: new[] { nestedInclude });

            Assert.Single(include.NestedIncludes);
            Assert.Equal("Items", include.NestedIncludes[0].NavigationName);
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
                NavigationName: "Orders",
                ResultName: "Orders",
                TargetEntityType: typeof(object),
                CollectionPath: "orders",
                DocumentId: null,
                FilterResults: Array.Empty<ResolvedFilterResult>(),
                OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
                Pagination: ResolvedPaginationInfo.None,
                Fields: null,
                Aggregation: null,
                AggregationPropertyName: null,
                NestedSubcollections: Array.Empty<ResolvedSubcollectionProjection>());

            Assert.Equal("Orders", subcollection.NavigationName);
            Assert.Equal("orders", subcollection.CollectionPath);
            Assert.False(subcollection.IsDocumentQuery);
            Assert.False(subcollection.IsAggregation);
        }

        [Fact]
        public void ResolvedSubcollectionProjection_WithDocumentId_IsDocumentQueryTrue()
        {
            var subcollection = new ResolvedSubcollectionProjection(
                NavigationName: "Order",
                ResultName: "Order",
                TargetEntityType: typeof(object),
                CollectionPath: "orders",
                DocumentId: "order-123",
                FilterResults: Array.Empty<ResolvedFilterResult>(),
                OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
                Pagination: ResolvedPaginationInfo.None,
                Fields: null,
                Aggregation: null,
                AggregationPropertyName: null,
                NestedSubcollections: Array.Empty<ResolvedSubcollectionProjection>());

            Assert.True(subcollection.IsDocumentQuery);
            Assert.Equal("order-123", subcollection.DocumentId);
        }

        [Fact]
        public void ResolvedSubcollectionProjection_WithAggregation_IsAggregationTrue()
        {
            var subcollection = new ResolvedSubcollectionProjection(
                NavigationName: "Orders",
                ResultName: "OrderCount",
                TargetEntityType: typeof(object),
                CollectionPath: "orders",
                DocumentId: null,
                FilterResults: Array.Empty<ResolvedFilterResult>(),
                OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
                Pagination: ResolvedPaginationInfo.None,
                Fields: null,
                Aggregation: FirestoreAggregationType.Count,
                AggregationPropertyName: null,
                NestedSubcollections: Array.Empty<ResolvedSubcollectionProjection>());

            Assert.True(subcollection.IsAggregation);
        }

        #endregion

        #region ResolvedFirestoreQuery Tests

        [Fact]
        public void ResolvedFirestoreQuery_Basic_CollectionQuery()
        {
            var query = new ResolvedFirestoreQuery(
                CollectionPath: "customers",
                EntityClrType: typeof(object),
                DocumentId: null,
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
                ReturnType: null);

            Assert.Equal("customers", query.CollectionPath);
            Assert.False(query.IsDocumentQuery);
            Assert.False(query.IsAggregation);
            Assert.False(query.HasProjection);
        }

        [Fact]
        public void ResolvedFirestoreQuery_WithDocumentId_IsDocumentQueryTrue()
        {
            var query = new ResolvedFirestoreQuery(
                CollectionPath: "customers",
                EntityClrType: typeof(object),
                DocumentId: "cust-123",
                FilterResults: Array.Empty<ResolvedFilterResult>(),
                OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
                Pagination: ResolvedPaginationInfo.None,
                StartAfterCursor: null,
                Includes: Array.Empty<ResolvedInclude>(),
                AggregationType: FirestoreAggregationType.None,
                AggregationPropertyName: null,
                AggregationResultType: null,
                Projection: null,
                ReturnDefault: true,
                ReturnType: typeof(object));

            Assert.True(query.IsDocumentQuery);
            Assert.Equal("cust-123", query.DocumentId);
        }

        [Fact]
        public void ResolvedFirestoreQuery_CountQuery()
        {
            var query = new ResolvedFirestoreQuery(
                CollectionPath: "customers",
                EntityClrType: typeof(object),
                DocumentId: null,
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
                ReturnType: null);

            Assert.True(query.IsAggregation);
            Assert.True(query.IsCountQuery);
            Assert.False(query.IsAnyQuery);
        }

        [Fact]
        public void ResolvedFirestoreQuery_AnyQuery()
        {
            var query = new ResolvedFirestoreQuery(
                CollectionPath: "customers",
                EntityClrType: typeof(object),
                DocumentId: null,
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
                ReturnType: null);

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
                CollectionPath: "customers",
                EntityClrType: typeof(object),
                DocumentId: null,
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
                ReturnType: null);

            Assert.Equal(2, query.TotalFilterCount);
            Assert.Single(query.OrderByClauses);
            Assert.Equal(10, query.Pagination.Limit);
            Assert.Equal(5, query.Pagination.Skip);
        }

        [Fact]
        public void ResolvedFirestoreQuery_ToString_CollectionQuery()
        {
            var filterResult = ResolvedFilterResult.FromClause(
                new ResolvedWhereClause("Price", FirestoreOperator.GreaterThan, 100));

            var query = new ResolvedFirestoreQuery(
                CollectionPath: "products",
                EntityClrType: typeof(object),
                DocumentId: null,
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
                ReturnType: null);

            var str = query.ToString();
            Assert.Contains("Query: products", str);
            Assert.Contains("Where", str);
            Assert.Contains(".Limit(20)", str);
        }

        [Fact]
        public void ResolvedFirestoreQuery_ToString_DocumentQuery()
        {
            var query = new ResolvedFirestoreQuery(
                CollectionPath: "menus",
                EntityClrType: typeof(object),
                DocumentId: "menu-123",
                FilterResults: Array.Empty<ResolvedFilterResult>(),
                OrderByClauses: Array.Empty<ResolvedOrderByClause>(),
                Pagination: ResolvedPaginationInfo.None,
                StartAfterCursor: null,
                Includes: Array.Empty<ResolvedInclude>(),
                AggregationType: FirestoreAggregationType.None,
                AggregationPropertyName: null,
                AggregationResultType: null,
                Projection: null,
                ReturnDefault: true,
                ReturnType: typeof(object));

            var str = query.ToString();
            Assert.Contains("Document: menus/menu-123", str);
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
                CollectionPath: "test",
                EntityClrType: typeof(object),
                DocumentId: null,
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
                ReturnType: null);

            Assert.Equal(5, query.TotalFilterCount);
        }

        #endregion
    }
}
