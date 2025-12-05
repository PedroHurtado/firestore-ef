namespace Fudie.Firestore.UnitTest.Infrastructure;

public class FirestoreOptionsExtensionTest
{
    [Fact]
    public void Default_Values_Are_Set_Correctly()
    {
        var extension = new FirestoreOptionsExtension();

        extension.ProjectId.Should().BeNull();
        extension.CredentialsPath.Should().BeNull();
        extension.DatabaseId.Should().Be("(default)");
        extension.MaxRetryAttempts.Should().Be(3);
        extension.CommandTimeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void WithProjectId_Creates_New_Instance_With_ProjectId()
    {
        var original = new FirestoreOptionsExtension();

        var modified = original.WithProjectId("my-project");

        modified.ProjectId.Should().Be("my-project");
        original.ProjectId.Should().BeNull("original should be unchanged");
    }

    [Fact]
    public void WithCredentialsPath_Creates_New_Instance_With_CredentialsPath()
    {
        var original = new FirestoreOptionsExtension();

        var modified = original.WithCredentialsPath("/path/to/credentials.json");

        modified.CredentialsPath.Should().Be("/path/to/credentials.json");
        original.CredentialsPath.Should().BeNull("original should be unchanged");
    }

    [Fact]
    public void WithCredentialsPath_Accepts_Null()
    {
        var original = new FirestoreOptionsExtension()
            .WithCredentialsPath("/some/path");

        var modified = original.WithCredentialsPath(null);

        modified.CredentialsPath.Should().BeNull();
    }

    [Fact]
    public void WithDatabaseId_Creates_New_Instance_With_DatabaseId()
    {
        var original = new FirestoreOptionsExtension();

        var modified = original.WithDatabaseId("my-database");

        modified.DatabaseId.Should().Be("my-database");
        original.DatabaseId.Should().Be("(default)", "original should be unchanged");
    }

    [Fact]
    public void WithDatabaseId_Uses_Default_When_Null()
    {
        var original = new FirestoreOptionsExtension()
            .WithDatabaseId("custom-db");

        var modified = original.WithDatabaseId(null);

        modified.DatabaseId.Should().Be("(default)");
    }

    [Fact]
    public void WithMaxRetryAttempts_Creates_New_Instance()
    {
        var original = new FirestoreOptionsExtension();

        var modified = original.WithMaxRetryAttempts(5);

        modified.MaxRetryAttempts.Should().Be(5);
        original.MaxRetryAttempts.Should().Be(3, "original should be unchanged");
    }

    [Fact]
    public void WithCommandTimeout_Creates_New_Instance()
    {
        var original = new FirestoreOptionsExtension();

        var modified = original.WithCommandTimeout(TimeSpan.FromMinutes(2));

        modified.CommandTimeout.Should().Be(TimeSpan.FromMinutes(2));
        original.CommandTimeout.Should().Be(TimeSpan.FromSeconds(30), "original should be unchanged");
    }

    [Fact]
    public void Chained_Modifications_Work_Correctly()
    {
        var extension = new FirestoreOptionsExtension()
            .WithProjectId("my-project")
            .WithCredentialsPath("/path/to/creds.json")
            .WithDatabaseId("my-db")
            .WithMaxRetryAttempts(5)
            .WithCommandTimeout(TimeSpan.FromMinutes(1));

        extension.ProjectId.Should().Be("my-project");
        extension.CredentialsPath.Should().Be("/path/to/creds.json");
        extension.DatabaseId.Should().Be("my-db");
        extension.MaxRetryAttempts.Should().Be(5);
        extension.CommandTimeout.Should().Be(TimeSpan.FromMinutes(1));
    }

    #region Validate Tests

    [Fact]
    public void Validate_Throws_When_ProjectId_Is_Null()
    {
        var extension = new FirestoreOptionsExtension();
        var options = new Mock<IDbContextOptions>().Object;

        var action = () => extension.Validate(options);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*ProjectId*requerido*");
    }

    [Fact]
    public void Validate_Throws_When_ProjectId_Is_Empty()
    {
        var extension = new FirestoreOptionsExtension()
            .WithProjectId("");
        var options = new Mock<IDbContextOptions>().Object;

        var action = () => extension.Validate(options);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*ProjectId*requerido*");
    }

    [Fact]
    public void Validate_Throws_When_ProjectId_Is_Whitespace()
    {
        var extension = new FirestoreOptionsExtension()
            .WithProjectId("   ");
        var options = new Mock<IDbContextOptions>().Object;

        var action = () => extension.Validate(options);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*ProjectId*requerido*");
    }

    [Fact]
    public void Validate_Throws_When_MaxRetryAttempts_Is_Negative()
    {
        var extension = new FirestoreOptionsExtension()
            .WithProjectId("valid-project")
            .WithMaxRetryAttempts(-1);
        var options = new Mock<IDbContextOptions>().Object;

        var action = () => extension.Validate(options);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*MaxRetryAttempts*mayor o igual a 0*");
    }

    [Fact]
    public void Validate_Throws_When_CommandTimeout_Is_Zero()
    {
        var extension = new FirestoreOptionsExtension()
            .WithProjectId("valid-project")
            .WithCommandTimeout(TimeSpan.Zero);
        var options = new Mock<IDbContextOptions>().Object;

        var action = () => extension.Validate(options);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*CommandTimeout*mayor que cero*");
    }

    [Fact]
    public void Validate_Throws_When_CommandTimeout_Is_Negative()
    {
        var extension = new FirestoreOptionsExtension()
            .WithProjectId("valid-project")
            .WithCommandTimeout(TimeSpan.FromSeconds(-1));
        var options = new Mock<IDbContextOptions>().Object;

        var action = () => extension.Validate(options);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*CommandTimeout*mayor que cero*");
    }

    [Fact]
    public void Validate_Succeeds_With_Valid_Configuration()
    {
        var extension = new FirestoreOptionsExtension()
            .WithProjectId("valid-project");
        var options = new Mock<IDbContextOptions>().Object;

        var action = () => extension.Validate(options);

        action.Should().NotThrow();
    }

    [Fact]
    public void Validate_Allows_Zero_MaxRetryAttempts()
    {
        var extension = new FirestoreOptionsExtension()
            .WithProjectId("valid-project")
            .WithMaxRetryAttempts(0);
        var options = new Mock<IDbContextOptions>().Object;

        var action = () => extension.Validate(options);

        action.Should().NotThrow();
    }

    #endregion

    #region Info Tests

    [Fact]
    public void Info_Returns_ExtensionInfo()
    {
        var extension = new FirestoreOptionsExtension()
            .WithProjectId("test-project");

        var info = extension.Info;

        info.Should().NotBeNull();
        info.IsDatabaseProvider.Should().BeTrue();
    }

    [Fact]
    public void Info_LogFragment_Contains_ProjectId()
    {
        var extension = new FirestoreOptionsExtension()
            .WithProjectId("my-project");

        var logFragment = extension.Info.LogFragment;

        logFragment.Should().Contain("ProjectId=my-project");
    }

    [Fact]
    public void Info_LogFragment_Contains_DatabaseId_When_Not_Default()
    {
        var extension = new FirestoreOptionsExtension()
            .WithProjectId("my-project")
            .WithDatabaseId("custom-db");

        var logFragment = extension.Info.LogFragment;

        logFragment.Should().Contain("DatabaseId=custom-db");
    }

    [Fact]
    public void Info_LogFragment_Omits_DatabaseId_When_Default()
    {
        var extension = new FirestoreOptionsExtension()
            .WithProjectId("my-project");

        var logFragment = extension.Info.LogFragment;

        logFragment.Should().NotContain("DatabaseId=");
    }

    [Fact]
    public void Info_LogFragment_Contains_MaxRetryAttempts_When_Not_Default()
    {
        var extension = new FirestoreOptionsExtension()
            .WithProjectId("my-project")
            .WithMaxRetryAttempts(5);

        var logFragment = extension.Info.LogFragment;

        logFragment.Should().Contain("MaxRetryAttempts=5");
    }

    [Fact]
    public void Info_GetServiceProviderHashCode_Returns_Consistent_Value()
    {
        var extension = new FirestoreOptionsExtension()
            .WithProjectId("my-project")
            .WithDatabaseId("my-db");

        var hash1 = extension.Info.GetServiceProviderHashCode();
        var hash2 = extension.Info.GetServiceProviderHashCode();

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void Info_GetServiceProviderHashCode_Differs_For_Different_ProjectIds()
    {
        var extension1 = new FirestoreOptionsExtension()
            .WithProjectId("project-1");
        var extension2 = new FirestoreOptionsExtension()
            .WithProjectId("project-2");

        var hash1 = extension1.Info.GetServiceProviderHashCode();
        var hash2 = extension2.Info.GetServiceProviderHashCode();

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Info_ShouldUseSameServiceProvider_Returns_True_For_Same_Config()
    {
        var extension1 = new FirestoreOptionsExtension()
            .WithProjectId("my-project")
            .WithDatabaseId("my-db")
            .WithCredentialsPath("/path/to/creds");
        var extension2 = new FirestoreOptionsExtension()
            .WithProjectId("my-project")
            .WithDatabaseId("my-db")
            .WithCredentialsPath("/path/to/creds");

        var result = extension1.Info.ShouldUseSameServiceProvider(extension2.Info);

        result.Should().BeTrue();
    }

    [Fact]
    public void Info_ShouldUseSameServiceProvider_Returns_False_For_Different_ProjectId()
    {
        var extension1 = new FirestoreOptionsExtension()
            .WithProjectId("project-1");
        var extension2 = new FirestoreOptionsExtension()
            .WithProjectId("project-2");

        var result = extension1.Info.ShouldUseSameServiceProvider(extension2.Info);

        result.Should().BeFalse();
    }

    [Fact]
    public void Info_PopulateDebugInfo_Fills_Dictionary()
    {
        var extension = new FirestoreOptionsExtension()
            .WithProjectId("my-project")
            .WithDatabaseId("my-db")
            .WithMaxRetryAttempts(5)
            .WithCommandTimeout(TimeSpan.FromMinutes(2));
        var debugInfo = new Dictionary<string, string>();

        extension.Info.PopulateDebugInfo(debugInfo);

        debugInfo.Should().ContainKey("Firestore:ProjectId");
        debugInfo.Should().ContainKey("Firestore:DatabaseId").WhoseValue.Should().Be("my-db");
        debugInfo.Should().ContainKey("Firestore:MaxRetryAttempts").WhoseValue.Should().Be("5");
        debugInfo.Should().ContainKey("Firestore:CommandTimeout");
    }

    #endregion

    #region ApplyServices Tests

    [Fact]
    public void ApplyServices_Registers_Firestore_Services()
    {
        var extension = new FirestoreOptionsExtension();
        var services = new ServiceCollection();

        extension.ApplyServices(services);

        // Verify essential services are registered
        services.Should().Contain(sd => sd.ServiceType == typeof(IDatabase));
        services.Should().Contain(sd => sd.ServiceType == typeof(IDatabaseProvider));
        services.Should().Contain(sd => sd.ServiceType == typeof(ITypeMappingSource));
    }

    #endregion
}
