using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Query;
using Firestore.EntityFrameworkCore.Query.Ast;
using Firestore.EntityFrameworkCore.Query.Resolved;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Xunit;

namespace Fudie.Firestore.UnitTest.Query.Resolved
{
    public class FirestoreAstResolverTests
    {
        #region Test Entities

        public class Menu
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public ICollection<Category> Categories { get; set; } = new List<Category>();
        }

        public class Category
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string MenuId { get; set; } = "";
            public ICollection<Item> Items { get; set; } = new List<Item>();
        }

        public class Item
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public decimal Price { get; set; }
            public string CategoryId { get; set; } = "";
        }

        private class TestDbContext : DbContext
        {
            public DbSet<Menu> Menus { get; set; } = null!;
            public DbSet<Category> Categories { get; set; } = null!;
            public DbSet<Item> Items { get; set; } = null!;

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseInMemoryDatabase("TestDb_" + Guid.NewGuid());
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<Menu>()
                    .HasMany(m => m.Categories)
                    .WithOne()
                    .HasForeignKey(c => c.MenuId);

                modelBuilder.Entity<Category>()
                    .HasMany(c => c.Items)
                    .WithOne()
                    .HasForeignKey(i => i.CategoryId);
            }
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_Throws_On_Null_QueryContext()
        {
            var collectionManagerMock = new Mock<IFirestoreCollectionManager>();

            Assert.Throws<ArgumentNullException>(() =>
                new FirestoreAstResolver(null!, collectionManagerMock.Object));
        }

        [Fact]
        public void Constructor_Throws_On_Null_CollectionManager()
        {
            var queryContextMock = new Mock<IFirestoreQueryContext>();

            Assert.Throws<ArgumentNullException>(() =>
                new FirestoreAstResolver(queryContextMock.Object, null!));
        }

        #endregion

        #region Resolve Tests

        [Fact]
        public void Resolve_Throws_On_Null_Ast()
        {
            var queryContextMock = new Mock<IFirestoreQueryContext>();
            var collectionManagerMock = new Mock<IFirestoreCollectionManager>();

            var resolver = new FirestoreAstResolver(
                queryContextMock.Object,
                collectionManagerMock.Object);

            Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null!));
        }

        [Fact]
        public void Resolve_BasicQuery_ReturnsCollectionPath()
        {
            using var context = new TestDbContext();
            var entityType = context.Model.FindEntityType(typeof(Menu))!;

            var queryContextMock = new Mock<IFirestoreQueryContext>();
            queryContextMock.Setup(x => x.ParameterValues)
                .Returns(new Dictionary<string, object?>());

            var collectionManagerMock = new Mock<IFirestoreCollectionManager>();
            collectionManagerMock
                .Setup(x => x.GetCollectionName(typeof(Menu)))
                .Returns("menus");

            var resolver = new FirestoreAstResolver(
                queryContextMock.Object,
                collectionManagerMock.Object);

            var ast = new FirestoreQueryExpression(entityType, "menus");

            var result = resolver.Resolve(ast);

            Assert.Equal("menus", result.CollectionPath);
            Assert.Equal(typeof(Menu), result.EntityClrType);
            Assert.Null(result.DocumentId);
            Assert.False(result.IsDocumentQuery);
        }

        [Fact]
        public void Resolve_WithIdValueExpression_ResolvesDocumentId()
        {
            using var context = new TestDbContext();
            var entityType = context.Model.FindEntityType(typeof(Menu))!;

            var queryContextMock = new Mock<IFirestoreQueryContext>();
            queryContextMock.Setup(x => x.ParameterValues)
                .Returns(new Dictionary<string, object?>());

            var collectionManagerMock = new Mock<IFirestoreCollectionManager>();
            collectionManagerMock
                .Setup(x => x.GetCollectionName(typeof(Menu)))
                .Returns("menus");

            var resolver = new FirestoreAstResolver(
                queryContextMock.Object,
                collectionManagerMock.Object);

            var ast = new FirestoreQueryExpression(entityType, "menus");
            ast.WithIdValueExpression(Expression.Constant("menu-123"));

            var result = resolver.Resolve(ast);

            Assert.Equal("menu-123", result.DocumentId);
            Assert.True(result.IsDocumentQuery);
        }

        [Fact]
        public void Resolve_WithParameterizedIdValue_EvaluatesFromContext()
        {
            using var context = new TestDbContext();
            var entityType = context.Model.FindEntityType(typeof(Menu))!;

            var queryContextMock = new Mock<IFirestoreQueryContext>();
            queryContextMock.Setup(x => x.ParameterValues)
                .Returns(new Dictionary<string, object?> { { "__menuId_0", "menu-456" } });

            var collectionManagerMock = new Mock<IFirestoreCollectionManager>();
            collectionManagerMock
                .Setup(x => x.GetCollectionName(typeof(Menu)))
                .Returns("menus");

            var resolver = new FirestoreAstResolver(
                queryContextMock.Object,
                collectionManagerMock.Object);

            var ast = new FirestoreQueryExpression(entityType, "menus");
            var paramExpr = Expression.Parameter(typeof(string), "__menuId_0");
            ast.WithIdValueExpression(paramExpr);

            var result = resolver.Resolve(ast);

            Assert.Equal("menu-456", result.DocumentId);
        }

        #endregion

        #region Filter Resolution Tests

        [Fact]
        public void Resolve_WithConstantFilter_ResolvesValue()
        {
            using var context = new TestDbContext();
            var entityType = context.Model.FindEntityType(typeof(Menu))!;

            var queryContextMock = new Mock<IFirestoreQueryContext>();
            queryContextMock.Setup(x => x.ParameterValues)
                .Returns(new Dictionary<string, object?>());

            var collectionManagerMock = new Mock<IFirestoreCollectionManager>();
            collectionManagerMock
                .Setup(x => x.GetCollectionName(typeof(Menu)))
                .Returns("menus");

            var resolver = new FirestoreAstResolver(
                queryContextMock.Object,
                collectionManagerMock.Object);

            var whereClause = new FirestoreWhereClause(
                "Name",
                FirestoreOperator.EqualTo,
                Expression.Constant("Test Menu"));

            var filterResult = FirestoreFilterResult.FromClause(whereClause);

            var ast = new FirestoreQueryExpression(entityType, "menus");
            ast.AddFilterResult(filterResult);

            var result = resolver.Resolve(ast);

            Assert.Single(result.FilterResults);
            Assert.Single(result.FilterResults[0].AndClauses);
            Assert.Equal("Name", result.FilterResults[0].AndClauses[0].PropertyName);
            Assert.Equal("Test Menu", result.FilterResults[0].AndClauses[0].Value);
        }

        [Fact]
        public void Resolve_WithParameterizedFilter_EvaluatesFromContext()
        {
            using var context = new TestDbContext();
            var entityType = context.Model.FindEntityType(typeof(Menu))!;

            var queryContextMock = new Mock<IFirestoreQueryContext>();
            queryContextMock.Setup(x => x.ParameterValues)
                .Returns(new Dictionary<string, object?> { { "__name_0", "Parameterized Name" } });

            var collectionManagerMock = new Mock<IFirestoreCollectionManager>();
            collectionManagerMock
                .Setup(x => x.GetCollectionName(typeof(Menu)))
                .Returns("menus");

            var resolver = new FirestoreAstResolver(
                queryContextMock.Object,
                collectionManagerMock.Object);

            var paramExpr = Expression.Parameter(typeof(string), "__name_0");
            var whereClause = new FirestoreWhereClause(
                "Name",
                FirestoreOperator.EqualTo,
                paramExpr);

            var filterResult = FirestoreFilterResult.FromClause(whereClause);

            var ast = new FirestoreQueryExpression(entityType, "menus");
            ast.AddFilterResult(filterResult);

            var result = resolver.Resolve(ast);

            Assert.Equal("Parameterized Name", result.FilterResults[0].AndClauses[0].Value);
        }

        #endregion

        #region Pagination Resolution Tests

        [Fact]
        public void Resolve_WithConstantLimit_ResolvesValue()
        {
            using var context = new TestDbContext();
            var entityType = context.Model.FindEntityType(typeof(Menu))!;

            var queryContextMock = new Mock<IFirestoreQueryContext>();
            queryContextMock.Setup(x => x.ParameterValues)
                .Returns(new Dictionary<string, object?>());

            var collectionManagerMock = new Mock<IFirestoreCollectionManager>();
            collectionManagerMock
                .Setup(x => x.GetCollectionName(typeof(Menu)))
                .Returns("menus");

            var resolver = new FirestoreAstResolver(
                queryContextMock.Object,
                collectionManagerMock.Object);

            var ast = new FirestoreQueryExpression(entityType, "menus");
            ast.WithLimit(10);

            var result = resolver.Resolve(ast);

            Assert.Equal(10, result.Pagination.Limit);
        }

        [Fact]
        public void Resolve_WithParameterizedLimit_EvaluatesFromContext()
        {
            using var context = new TestDbContext();
            var entityType = context.Model.FindEntityType(typeof(Menu))!;

            var queryContextMock = new Mock<IFirestoreQueryContext>();
            queryContextMock.Setup(x => x.ParameterValues)
                .Returns(new Dictionary<string, object?> { { "__limit_0", 25 } });

            var collectionManagerMock = new Mock<IFirestoreCollectionManager>();
            collectionManagerMock
                .Setup(x => x.GetCollectionName(typeof(Menu)))
                .Returns("menus");

            var resolver = new FirestoreAstResolver(
                queryContextMock.Object,
                collectionManagerMock.Object);

            var ast = new FirestoreQueryExpression(entityType, "menus");
            var paramExpr = Expression.Parameter(typeof(int), "__limit_0");
            ast.WithLimitExpression(paramExpr);

            var result = resolver.Resolve(ast);

            Assert.Equal(25, result.Pagination.Limit);
        }

        [Fact]
        public void Resolve_WithConstantSkip_ResolvesValue()
        {
            using var context = new TestDbContext();
            var entityType = context.Model.FindEntityType(typeof(Menu))!;

            var queryContextMock = new Mock<IFirestoreQueryContext>();
            queryContextMock.Setup(x => x.ParameterValues)
                .Returns(new Dictionary<string, object?>());

            var collectionManagerMock = new Mock<IFirestoreCollectionManager>();
            collectionManagerMock
                .Setup(x => x.GetCollectionName(typeof(Menu)))
                .Returns("menus");

            var resolver = new FirestoreAstResolver(
                queryContextMock.Object,
                collectionManagerMock.Object);

            var ast = new FirestoreQueryExpression(entityType, "menus");
            ast.WithSkip(5);

            var result = resolver.Resolve(ast);

            Assert.Equal(5, result.Pagination.Skip);
        }

        #endregion

        #region OrderBy Resolution Tests

        [Fact]
        public void Resolve_WithOrderBy_ResolvesToOrderByClauses()
        {
            using var context = new TestDbContext();
            var entityType = context.Model.FindEntityType(typeof(Menu))!;

            var queryContextMock = new Mock<IFirestoreQueryContext>();
            queryContextMock.Setup(x => x.ParameterValues)
                .Returns(new Dictionary<string, object?>());

            var collectionManagerMock = new Mock<IFirestoreCollectionManager>();
            collectionManagerMock
                .Setup(x => x.GetCollectionName(typeof(Menu)))
                .Returns("menus");

            var resolver = new FirestoreAstResolver(
                queryContextMock.Object,
                collectionManagerMock.Object);

            var ast = new FirestoreQueryExpression(entityType, "menus");
            ast.AddOrderBy(new FirestoreOrderByClause("Name", false));
            ast.AddOrderBy(new FirestoreOrderByClause("Id", true));

            var result = resolver.Resolve(ast);

            Assert.Equal(2, result.OrderByClauses.Count);
            Assert.Equal("Name", result.OrderByClauses[0].PropertyName);
            Assert.False(result.OrderByClauses[0].Descending);
            Assert.Equal("Id", result.OrderByClauses[1].PropertyName);
            Assert.True(result.OrderByClauses[1].Descending);
        }

        #endregion

        #region Include Resolution Tests

        [Fact]
        public void Resolve_Include_ResolvesNavigationAndCollectionName()
        {
            using var context = new TestDbContext();
            var menuEntityType = context.Model.FindEntityType(typeof(Menu))!;

            var queryContextMock = new Mock<IFirestoreQueryContext>();
            queryContextMock.Setup(x => x.ParameterValues)
                .Returns(new Dictionary<string, object?>());

            var collectionManagerMock = new Mock<IFirestoreCollectionManager>();
            collectionManagerMock
                .Setup(x => x.GetCollectionName(typeof(Menu)))
                .Returns("menus");
            collectionManagerMock
                .Setup(x => x.GetCollectionName(typeof(Category)))
                .Returns("categories");

            var resolver = new FirestoreAstResolver(
                queryContextMock.Object,
                collectionManagerMock.Object);

            var ast = new FirestoreQueryExpression(menuEntityType, "menus");
            ast.AddInclude("Categories", true, "categories", typeof(Category));

            var result = resolver.Resolve(ast);

            Assert.Single(result.Includes);
            Assert.Equal("Categories", result.Includes[0].NavigationName);
            Assert.Equal("categories", result.Includes[0].CollectionPath);
            Assert.Equal(typeof(Category), result.Includes[0].TargetEntityType);
            Assert.True(result.Includes[0].IsCollection);
        }

        [Fact]
        public void Resolve_NestedInclude_ResolvesNestedNavigations()
        {
            using var context = new TestDbContext();
            var menuEntityType = context.Model.FindEntityType(typeof(Menu))!;

            var queryContextMock = new Mock<IFirestoreQueryContext>();
            queryContextMock.Setup(x => x.ParameterValues)
                .Returns(new Dictionary<string, object?>());

            var collectionManagerMock = new Mock<IFirestoreCollectionManager>();
            collectionManagerMock
                .Setup(x => x.GetCollectionName(typeof(Menu)))
                .Returns("menus");
            collectionManagerMock
                .Setup(x => x.GetCollectionName(typeof(Category)))
                .Returns("categories");
            collectionManagerMock
                .Setup(x => x.GetCollectionName(typeof(Item)))
                .Returns("items");

            var resolver = new FirestoreAstResolver(
                queryContextMock.Object,
                collectionManagerMock.Object);

            var ast = new FirestoreQueryExpression(menuEntityType, "menus");
            ast.AddInclude("Categories", true, "categories", typeof(Category));
            ast.AddInclude("Items", true, "items", typeof(Item)); // This belongs to Category

            var result = resolver.Resolve(ast);

            // Should have Categories include with nested Items
            Assert.Single(result.Includes);
            var categoriesInclude = result.Includes[0];
            Assert.Equal("categories", categoriesInclude.CollectionPath);
            Assert.Equal(typeof(Category), categoriesInclude.TargetEntityType);

            // Nested Items include
            Assert.Single(categoriesInclude.NestedIncludes);
            var itemsInclude = categoriesInclude.NestedIncludes[0];
            Assert.Equal("Items", itemsInclude.NavigationName);
            Assert.Equal("items", itemsInclude.CollectionPath);
            Assert.Equal(typeof(Item), itemsInclude.TargetEntityType);
        }

        #endregion

        #region Aggregation Resolution Tests

        [Fact]
        public void Resolve_CountQuery_PreservesAggregationType()
        {
            using var context = new TestDbContext();
            var entityType = context.Model.FindEntityType(typeof(Menu))!;

            var queryContextMock = new Mock<IFirestoreQueryContext>();
            queryContextMock.Setup(x => x.ParameterValues)
                .Returns(new Dictionary<string, object?>());

            var collectionManagerMock = new Mock<IFirestoreCollectionManager>();
            collectionManagerMock
                .Setup(x => x.GetCollectionName(typeof(Menu)))
                .Returns("menus");

            var resolver = new FirestoreAstResolver(
                queryContextMock.Object,
                collectionManagerMock.Object);

            var ast = new FirestoreQueryExpression(entityType, "menus");
            ast.WithCount();

            var result = resolver.Resolve(ast);

            Assert.True(result.IsCountQuery);
            Assert.Equal(FirestoreAggregationType.Count, result.AggregationType);
        }

        [Fact]
        public void Resolve_SumQuery_PreservesAggregationDetails()
        {
            using var context = new TestDbContext();
            var entityType = context.Model.FindEntityType(typeof(Item))!;

            var queryContextMock = new Mock<IFirestoreQueryContext>();
            queryContextMock.Setup(x => x.ParameterValues)
                .Returns(new Dictionary<string, object?>());

            var collectionManagerMock = new Mock<IFirestoreCollectionManager>();
            collectionManagerMock
                .Setup(x => x.GetCollectionName(typeof(Item)))
                .Returns("items");

            var resolver = new FirestoreAstResolver(
                queryContextMock.Object,
                collectionManagerMock.Object);

            var ast = new FirestoreQueryExpression(entityType, "items");
            ast.WithSum("Price", typeof(decimal));

            var result = resolver.Resolve(ast);

            Assert.True(result.IsAggregation);
            Assert.Equal(FirestoreAggregationType.Sum, result.AggregationType);
            Assert.Equal("Price", result.AggregationPropertyName);
            Assert.Equal(typeof(decimal), result.AggregationResultType);
        }

        #endregion

        #region ReturnDefault Resolution Tests

        [Fact]
        public void Resolve_FirstOrDefault_PreservesReturnDefault()
        {
            using var context = new TestDbContext();
            var entityType = context.Model.FindEntityType(typeof(Menu))!;

            var queryContextMock = new Mock<IFirestoreQueryContext>();
            queryContextMock.Setup(x => x.ParameterValues)
                .Returns(new Dictionary<string, object?>());

            var collectionManagerMock = new Mock<IFirestoreCollectionManager>();
            collectionManagerMock
                .Setup(x => x.GetCollectionName(typeof(Menu)))
                .Returns("menus");

            var resolver = new FirestoreAstResolver(
                queryContextMock.Object,
                collectionManagerMock.Object);

            var ast = new FirestoreQueryExpression(entityType, "menus");
            ast.WithReturnDefault(true, typeof(Menu));
            ast.WithLimit(1);

            var result = resolver.Resolve(ast);

            Assert.True(result.ReturnDefault);
            Assert.Equal(typeof(Menu), result.ReturnType);
        }

        #endregion

        #region Cursor Resolution Tests

        [Fact]
        public void Resolve_WithCursor_PreservesCursorInfo()
        {
            using var context = new TestDbContext();
            var entityType = context.Model.FindEntityType(typeof(Menu))!;

            var queryContextMock = new Mock<IFirestoreQueryContext>();
            queryContextMock.Setup(x => x.ParameterValues)
                .Returns(new Dictionary<string, object?>());

            var collectionManagerMock = new Mock<IFirestoreCollectionManager>();
            collectionManagerMock
                .Setup(x => x.GetCollectionName(typeof(Menu)))
                .Returns("menus");

            var resolver = new FirestoreAstResolver(
                queryContextMock.Object,
                collectionManagerMock.Object);

            var cursor = new FirestoreCursor("last-doc-123", new object?[] { "LastName", 42 });

            var ast = new FirestoreQueryExpression(entityType, "menus");
            ast.WithStartAfter(cursor);

            var result = resolver.Resolve(ast);

            Assert.NotNull(result.StartAfterCursor);
            Assert.Equal("last-doc-123", result.StartAfterCursor.DocumentId);
            Assert.Equal(2, result.StartAfterCursor.OrderByValues!.Count);
        }

        #endregion
    }
}
