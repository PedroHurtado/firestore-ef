using Fudie.Firestore.EntityFrameworkCore.Query.Ast;
using System.Linq.Expressions;
using Xunit;

namespace Fudie.Firestore.UnitTest.Query.Ast
{
    /// <summary>
    /// Unit tests for FirestorePaginationInfo.
    /// </summary>
    public class FirestorePaginationInfoTests
    {
        #region Constructor and Initial State

        [Fact]
        public void NewInstance_HasNoValues()
        {
            // Arrange & Act
            var pagination = new FirestorePaginationInfo();

            // Assert
            Assert.Null(pagination.Limit);
            Assert.Null(pagination.LimitExpression);
            Assert.Null(pagination.LimitToLast);
            Assert.Null(pagination.LimitToLastExpression);
            Assert.Null(pagination.Skip);
            Assert.Null(pagination.SkipExpression);
        }

        [Fact]
        public void NewInstance_HasPagination_IsFalse()
        {
            // Arrange & Act
            var pagination = new FirestorePaginationInfo();

            // Assert
            Assert.False(pagination.HasPagination);
        }

        #endregion

        #region Limit (Take)

        [Fact]
        public void WithLimit_SetsLimitValue()
        {
            // Arrange
            var pagination = new FirestorePaginationInfo();

            // Act
            pagination.WithLimit(10);

            // Assert
            Assert.Equal(10, pagination.Limit);
            Assert.Null(pagination.LimitExpression);
        }

        [Fact]
        public void WithLimit_ClearsLimitExpression()
        {
            // Arrange
            var pagination = new FirestorePaginationInfo();
            Expression<Func<int>> expr = () => 5;
            pagination.WithLimitExpression(expr);

            // Act
            pagination.WithLimit(10);

            // Assert
            Assert.Equal(10, pagination.Limit);
            Assert.Null(pagination.LimitExpression);
        }

        [Fact]
        public void WithLimitExpression_SetsExpression()
        {
            // Arrange
            var pagination = new FirestorePaginationInfo();
            Expression<Func<int>> expr = () => 5;

            // Act
            pagination.WithLimitExpression(expr);

            // Assert
            Assert.Null(pagination.Limit);
            Assert.NotNull(pagination.LimitExpression);
        }

        [Fact]
        public void WithLimitExpression_ClearsLimitValue()
        {
            // Arrange
            var pagination = new FirestorePaginationInfo();
            pagination.WithLimit(10);
            Expression<Func<int>> expr = () => 5;

            // Act
            pagination.WithLimitExpression(expr);

            // Assert
            Assert.Null(pagination.Limit);
            Assert.NotNull(pagination.LimitExpression);
        }

        [Fact]
        public void HasLimit_TrueWhenLimitSet()
        {
            // Arrange
            var pagination = new FirestorePaginationInfo();
            pagination.WithLimit(10);

            // Assert
            Assert.True(pagination.HasLimit);
        }

        [Fact]
        public void HasLimit_TrueWhenLimitExpressionSet()
        {
            // Arrange
            var pagination = new FirestorePaginationInfo();
            Expression<Func<int>> expr = () => 5;
            pagination.WithLimitExpression(expr);

            // Assert
            Assert.True(pagination.HasLimit);
        }

        [Fact]
        public void HasLimit_FalseWhenNotSet()
        {
            // Arrange
            var pagination = new FirestorePaginationInfo();

            // Assert
            Assert.False(pagination.HasLimit);
        }

        #endregion

        #region LimitToLast (TakeLast)

        [Fact]
        public void WithLimitToLast_SetsValue()
        {
            // Arrange
            var pagination = new FirestorePaginationInfo();

            // Act
            pagination.WithLimitToLast(5);

            // Assert
            Assert.Equal(5, pagination.LimitToLast);
            Assert.Null(pagination.LimitToLastExpression);
        }

        [Fact]
        public void WithLimitToLastExpression_SetsExpression()
        {
            // Arrange
            var pagination = new FirestorePaginationInfo();
            Expression<Func<int>> expr = () => 3;

            // Act
            pagination.WithLimitToLastExpression(expr);

            // Assert
            Assert.Null(pagination.LimitToLast);
            Assert.NotNull(pagination.LimitToLastExpression);
        }

        [Fact]
        public void HasLimitToLast_TrueWhenSet()
        {
            // Arrange
            var pagination = new FirestorePaginationInfo();
            pagination.WithLimitToLast(5);

            // Assert
            Assert.True(pagination.HasLimitToLast);
        }

        [Fact]
        public void HasLimitToLast_FalseWhenNotSet()
        {
            // Arrange
            var pagination = new FirestorePaginationInfo();

            // Assert
            Assert.False(pagination.HasLimitToLast);
        }

        #endregion

        #region Skip (Offset)

        [Fact]
        public void WithSkip_SetsValue()
        {
            // Arrange
            var pagination = new FirestorePaginationInfo();

            // Act
            pagination.WithSkip(20);

            // Assert
            Assert.Equal(20, pagination.Skip);
            Assert.Null(pagination.SkipExpression);
        }

        [Fact]
        public void WithSkipExpression_SetsExpression()
        {
            // Arrange
            var pagination = new FirestorePaginationInfo();
            Expression<Func<int>> expr = () => 10;

            // Act
            pagination.WithSkipExpression(expr);

            // Assert
            Assert.Null(pagination.Skip);
            Assert.NotNull(pagination.SkipExpression);
        }

        [Fact]
        public void HasSkip_TrueWhenSet()
        {
            // Arrange
            var pagination = new FirestorePaginationInfo();
            pagination.WithSkip(20);

            // Assert
            Assert.True(pagination.HasSkip);
        }

        [Fact]
        public void HasSkip_FalseWhenNotSet()
        {
            // Arrange
            var pagination = new FirestorePaginationInfo();

            // Assert
            Assert.False(pagination.HasSkip);
        }

        #endregion

        #region HasPagination

        [Fact]
        public void HasPagination_TrueWhenLimitSet()
        {
            // Arrange
            var pagination = new FirestorePaginationInfo();
            pagination.WithLimit(10);

            // Assert
            Assert.True(pagination.HasPagination);
        }

        [Fact]
        public void HasPagination_TrueWhenSkipSet()
        {
            // Arrange
            var pagination = new FirestorePaginationInfo();
            pagination.WithSkip(5);

            // Assert
            Assert.True(pagination.HasPagination);
        }

        [Fact]
        public void HasPagination_TrueWhenLimitToLastSet()
        {
            // Arrange
            var pagination = new FirestorePaginationInfo();
            pagination.WithLimitToLast(3);

            // Assert
            Assert.True(pagination.HasPagination);
        }

        [Fact]
        public void HasPagination_TrueWhenMultipleSet()
        {
            // Arrange
            var pagination = new FirestorePaginationInfo();
            pagination.WithLimit(10).WithSkip(5);

            // Assert
            Assert.True(pagination.HasPagination);
        }

        #endregion

        #region Fluent API

        [Fact]
        public void FluentApi_ReturnsSameInstance()
        {
            // Arrange
            var pagination = new FirestorePaginationInfo();

            // Act
            var result = pagination
                .WithLimit(10)
                .WithSkip(5)
                .WithLimitToLast(3);

            // Assert
            Assert.Same(pagination, result);
        }

        #endregion

        #region ToString

        [Fact]
        public void ToString_EmptyWhenNoValues()
        {
            // Arrange
            var pagination = new FirestorePaginationInfo();

            // Act
            var result = pagination.ToString();

            // Assert
            Assert.Equal("None", result);
        }

        [Fact]
        public void ToString_ShowsLimit()
        {
            // Arrange
            var pagination = new FirestorePaginationInfo();
            pagination.WithLimit(10);

            // Act
            var result = pagination.ToString();

            // Assert
            Assert.Contains("Limit=10", result);
        }

        [Fact]
        public void ToString_ShowsSkip()
        {
            // Arrange
            var pagination = new FirestorePaginationInfo();
            pagination.WithSkip(5);

            // Act
            var result = pagination.ToString();

            // Assert
            Assert.Contains("Offset=5", result);
        }

        [Fact]
        public void ToString_ShowsLimitExpression()
        {
            // Arrange
            var pagination = new FirestorePaginationInfo();
            Expression<Func<int>> expr = () => 10;
            pagination.WithLimitExpression(expr);

            // Act
            var result = pagination.ToString();

            // Assert
            Assert.Contains("Limit=<expr>", result);
        }

        [Fact]
        public void ToString_ShowsMultipleValues()
        {
            // Arrange
            var pagination = new FirestorePaginationInfo();
            pagination.WithLimit(10).WithSkip(5);

            // Act
            var result = pagination.ToString();

            // Assert
            Assert.Contains("Limit=10", result);
            Assert.Contains("Offset=5", result);
        }

        #endregion
    }
}
