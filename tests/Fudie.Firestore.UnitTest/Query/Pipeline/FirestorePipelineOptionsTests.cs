using System;
using FluentAssertions;
using Fudie.Firestore.EntityFrameworkCore.Query.Pipeline;

namespace Fudie.Firestore.UnitTest.Query.Pipeline;

public class FirestorePipelineOptionsTests
{
    #region Property Tests

    [Fact]
    public void FirestorePipelineOptions_Has_EnableAstLogging_Property()
    {
        var property = typeof(FirestorePipelineOptions).GetProperty("EnableAstLogging");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(bool));
    }

    [Fact]
    public void FirestorePipelineOptions_Has_EnableQueryLogging_Property()
    {
        var property = typeof(FirestorePipelineOptions).GetProperty("EnableQueryLogging");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(bool));
    }

    [Fact]
    public void FirestorePipelineOptions_Has_EnableCaching_Property()
    {
        var property = typeof(FirestorePipelineOptions).GetProperty("EnableCaching");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(bool));
    }

    [Fact]
    public void FirestorePipelineOptions_Has_MaxRetries_Property()
    {
        var property = typeof(FirestorePipelineOptions).GetProperty("MaxRetries");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(int));
    }

    [Fact]
    public void FirestorePipelineOptions_Has_RetryInitialDelay_Property()
    {
        var property = typeof(FirestorePipelineOptions).GetProperty("RetryInitialDelay");

        property.Should().NotBeNull();
        property!.PropertyType.Should().Be(typeof(TimeSpan));
    }

    #endregion

    #region Default Values Tests

    [Fact]
    public void FirestorePipelineOptions_EnableAstLogging_Default_Is_False()
    {
        var options = new FirestorePipelineOptions();

        options.EnableAstLogging.Should().BeFalse();
    }

    [Fact]
    public void FirestorePipelineOptions_EnableQueryLogging_Default_Is_True()
    {
        var options = new FirestorePipelineOptions();

        options.EnableQueryLogging.Should().BeTrue();
    }

    [Fact]
    public void FirestorePipelineOptions_EnableCaching_Default_Is_False()
    {
        var options = new FirestorePipelineOptions();

        options.EnableCaching.Should().BeFalse();
    }

    [Fact]
    public void FirestorePipelineOptions_MaxRetries_Default_Is_3()
    {
        var options = new FirestorePipelineOptions();

        options.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void FirestorePipelineOptions_RetryInitialDelay_Default_Is_100ms()
    {
        var options = new FirestorePipelineOptions();

        options.RetryInitialDelay.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    #endregion

    #region Setter Tests

    [Fact]
    public void FirestorePipelineOptions_EnableAstLogging_Can_Be_Set()
    {
        var options = new FirestorePipelineOptions();

        options.EnableAstLogging = true;

        options.EnableAstLogging.Should().BeTrue();
    }

    [Fact]
    public void FirestorePipelineOptions_EnableQueryLogging_Can_Be_Set()
    {
        var options = new FirestorePipelineOptions();

        options.EnableQueryLogging = false;

        options.EnableQueryLogging.Should().BeFalse();
    }

    [Fact]
    public void FirestorePipelineOptions_EnableCaching_Can_Be_Set()
    {
        var options = new FirestorePipelineOptions();

        options.EnableCaching = true;

        options.EnableCaching.Should().BeTrue();
    }

    [Fact]
    public void FirestorePipelineOptions_MaxRetries_Can_Be_Set()
    {
        var options = new FirestorePipelineOptions();

        options.MaxRetries = 5;

        options.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void FirestorePipelineOptions_RetryInitialDelay_Can_Be_Set()
    {
        var options = new FirestorePipelineOptions();

        options.RetryInitialDelay = TimeSpan.FromSeconds(1);

        options.RetryInitialDelay.Should().Be(TimeSpan.FromSeconds(1));
    }

    #endregion
}
