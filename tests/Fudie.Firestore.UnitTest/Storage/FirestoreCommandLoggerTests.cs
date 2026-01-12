using Fudie.Firestore.EntityFrameworkCore.Diagnostics;
using Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;
using Fudie.Firestore.EntityFrameworkCore.Storage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics.Internal;
using Microsoft.Extensions.Logging;
using Moq;

namespace Fudie.Firestore.UnitTest.Storage;

/// <summary>
/// Tests for FirestoreCommandLogger implementation.
/// Verifies logging behavior for Insert, Update, Delete operations.
/// </summary>
public class FirestoreCommandLoggerTests
{
    private class TestEntity { public string Id { get; set; } = string.Empty; }

    #region LogInsert Tests

    [Fact]
    public void LogInsert_WhenQueryLogLevelNone_DoesNotLog()
    {
        // Arrange
        var mockLogger = new Mock<IDiagnosticsLogger<DbLoggerCategory.Database.Command>>();
        var options = new FirestorePipelineOptions { QueryLogLevel = QueryLogLevel.None };
        var commandLogger = new FirestoreCommandLogger(mockLogger.Object, options);

        // Act
        commandLogger.LogInsert("collection", "doc1", typeof(TestEntity), TimeSpan.FromMilliseconds(100));

        // Assert - No logging should occur when QueryLogLevel is None
        mockLogger.Verify(l => l.ShouldLog(It.IsAny<EventDefinition<string>>()), Times.Never);
    }

    [Fact]
    public void LogInsert_WhenQueryLogLevelCount_CallsLogger()
    {
        // Arrange
        var loggingDefinitions = new FirestoreLoggingDefinitions();
        var mockLoggerOptions = new Mock<ILoggingOptions>();
        mockLoggerOptions.Setup(o => o.IsSensitiveDataLoggingEnabled).Returns(false);

        var mockLogger = new Mock<IDiagnosticsLogger<DbLoggerCategory.Database.Command>>();
        mockLogger.Setup(l => l.Definitions).Returns(loggingDefinitions);
        mockLogger.Setup(l => l.Options).Returns(mockLoggerOptions.Object);
        mockLogger.Setup(l => l.ShouldLog(It.IsAny<EventDefinition<string>>())).Returns(false);
        mockLogger.Setup(l => l.NeedsEventData(
            It.IsAny<EventDefinition<string>>(),
            out It.Ref<bool>.IsAny,
            out It.Ref<bool>.IsAny))
            .Returns(false);

        var options = new FirestorePipelineOptions { QueryLogLevel = QueryLogLevel.Count };
        var commandLogger = new FirestoreCommandLogger(mockLogger.Object, options);

        // Act
        commandLogger.LogInsert("collection", "doc1", typeof(TestEntity), TimeSpan.FromMilliseconds(100));

        // Assert - Logger methods should be called when QueryLogLevel is not None
        mockLogger.Verify(l => l.Definitions, Times.AtLeastOnce);
    }

    #endregion

    #region LogUpdate Tests

    [Fact]
    public void LogUpdate_WhenQueryLogLevelNone_DoesNotLog()
    {
        // Arrange
        var mockLogger = new Mock<IDiagnosticsLogger<DbLoggerCategory.Database.Command>>();
        var options = new FirestorePipelineOptions { QueryLogLevel = QueryLogLevel.None };
        var commandLogger = new FirestoreCommandLogger(mockLogger.Object, options);

        // Act
        commandLogger.LogUpdate("collection", "doc1", typeof(TestEntity), TimeSpan.FromMilliseconds(50));

        // Assert - No logging should occur when QueryLogLevel is None
        mockLogger.Verify(l => l.ShouldLog(It.IsAny<EventDefinition<string>>()), Times.Never);
    }

    [Fact]
    public void LogUpdate_WhenQueryLogLevelIds_CallsLogger()
    {
        // Arrange
        var loggingDefinitions = new FirestoreLoggingDefinitions();
        var mockLoggerOptions = new Mock<ILoggingOptions>();
        mockLoggerOptions.Setup(o => o.IsSensitiveDataLoggingEnabled).Returns(false);

        var mockLogger = new Mock<IDiagnosticsLogger<DbLoggerCategory.Database.Command>>();
        mockLogger.Setup(l => l.Definitions).Returns(loggingDefinitions);
        mockLogger.Setup(l => l.Options).Returns(mockLoggerOptions.Object);
        mockLogger.Setup(l => l.ShouldLog(It.IsAny<EventDefinition<string>>())).Returns(false);
        mockLogger.Setup(l => l.NeedsEventData(
            It.IsAny<EventDefinition<string>>(),
            out It.Ref<bool>.IsAny,
            out It.Ref<bool>.IsAny))
            .Returns(false);

        var options = new FirestorePipelineOptions { QueryLogLevel = QueryLogLevel.Ids };
        var commandLogger = new FirestoreCommandLogger(mockLogger.Object, options);

        // Act
        commandLogger.LogUpdate("collection", "doc1", typeof(TestEntity), TimeSpan.FromMilliseconds(50));

        // Assert
        mockLogger.Verify(l => l.Definitions, Times.AtLeastOnce);
    }

    #endregion

    #region LogDelete Tests

    [Fact]
    public void LogDelete_WhenQueryLogLevelNone_DoesNotLog()
    {
        // Arrange
        var mockLogger = new Mock<IDiagnosticsLogger<DbLoggerCategory.Database.Command>>();
        var options = new FirestorePipelineOptions { QueryLogLevel = QueryLogLevel.None };
        var commandLogger = new FirestoreCommandLogger(mockLogger.Object, options);

        // Act
        commandLogger.LogDelete("collection", "doc1", typeof(TestEntity), TimeSpan.FromMilliseconds(75));

        // Assert - No logging should occur when QueryLogLevel is None
        mockLogger.Verify(l => l.ShouldLog(It.IsAny<EventDefinition<string>>()), Times.Never);
    }

    [Fact]
    public void LogDelete_WhenQueryLogLevelFull_CallsLogger()
    {
        // Arrange
        var loggingDefinitions = new FirestoreLoggingDefinitions();
        var mockLoggerOptions = new Mock<ILoggingOptions>();
        mockLoggerOptions.Setup(o => o.IsSensitiveDataLoggingEnabled).Returns(false);

        var mockLogger = new Mock<IDiagnosticsLogger<DbLoggerCategory.Database.Command>>();
        mockLogger.Setup(l => l.Definitions).Returns(loggingDefinitions);
        mockLogger.Setup(l => l.Options).Returns(mockLoggerOptions.Object);
        mockLogger.Setup(l => l.ShouldLog(It.IsAny<EventDefinition<string>>())).Returns(false);
        mockLogger.Setup(l => l.NeedsEventData(
            It.IsAny<EventDefinition<string>>(),
            out It.Ref<bool>.IsAny,
            out It.Ref<bool>.IsAny))
            .Returns(false);

        var options = new FirestorePipelineOptions { QueryLogLevel = QueryLogLevel.Full };
        var commandLogger = new FirestoreCommandLogger(mockLogger.Object, options);

        // Act
        commandLogger.LogDelete("collection", "doc1", typeof(TestEntity), TimeSpan.FromMilliseconds(75));

        // Assert
        mockLogger.Verify(l => l.Definitions, Times.AtLeastOnce);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_SetsLoggerAndOptions()
    {
        // Arrange
        var mockLogger = new Mock<IDiagnosticsLogger<DbLoggerCategory.Database.Command>>();
        var options = new FirestorePipelineOptions { QueryLogLevel = QueryLogLevel.Count };

        // Act
        var commandLogger = new FirestoreCommandLogger(mockLogger.Object, options);

        // Assert - The object is created without exceptions
        commandLogger.Should().NotBeNull();
    }

    #endregion
}
