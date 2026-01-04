using Firestore.EntityFrameworkCore.Infrastructure;
using Firestore.EntityFrameworkCore.Query;
using Firestore.EntityFrameworkCore.Query.Pipeline;
using Firestore.EntityFrameworkCore.Query.Resolved;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Fudie.Firestore.UnitTest.Query.Pipeline.Services;

public class FirestoreIncludeLoaderTests
{
    private readonly Mock<IFirestoreCollectionManager> _mockCollectionManager;

    public FirestoreIncludeLoaderTests()
    {
        _mockCollectionManager = new Mock<IFirestoreCollectionManager>();
    }

    #region Class Structure Tests

    [Fact]
    public void FirestoreIncludeLoader_Implements_IIncludeLoader()
    {
        typeof(FirestoreIncludeLoader)
            .Should().Implement<IIncludeLoader>();
    }

    [Fact]
    public void FirestoreIncludeLoader_Has_Constructor_With_CollectionManager()
    {
        // FirestoreIncludeLoader requires IFirestoreCollectionManager for building paths
        var constructors = typeof(FirestoreIncludeLoader).GetConstructors();

        constructors.Should().HaveCount(1);
        var parameters = constructors[0].GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(IFirestoreCollectionManager));
    }

    [Fact]
    public void FirestoreIncludeLoader_Can_Be_Instantiated()
    {
        var loader = new FirestoreIncludeLoader(_mockCollectionManager.Object);

        loader.Should().NotBeNull();
    }

    #endregion

    #region LoadIncludeAsync Method Signature Tests

    [Fact]
    public void LoadIncludeAsync_Method_Exists_With_Correct_Signature()
    {
        var method = typeof(FirestoreIncludeLoader).GetMethod("LoadIncludeAsync");

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task));

        var parameters = method.GetParameters();
        parameters.Should().HaveCount(6);
        parameters[0].ParameterType.Should().Be(typeof(object));
        parameters[0].Name.Should().Be("entity");
        parameters[1].ParameterType.Should().Be(typeof(IEntityType));
        parameters[1].Name.Should().Be("entityType");
        parameters[2].ParameterType.Should().Be(typeof(ResolvedInclude));
        parameters[2].Name.Should().Be("resolvedInclude");
        parameters[3].ParameterType.Should().Be(typeof(IQueryPipelineMediator));
        parameters[3].Name.Should().Be("mediator");
        parameters[4].ParameterType.Should().Be(typeof(IFirestoreQueryContext));
        parameters[4].Name.Should().Be("queryContext");
        parameters[5].ParameterType.Should().Be(typeof(CancellationToken));
        parameters[5].Name.Should().Be("cancellationToken");
    }

    #endregion

    #region Behavior Documentation Tests

    [Fact]
    public void LoadIncludeAsync_Receives_Mediator_As_Parameter()
    {
        // Documents that FirestoreIncludeLoader receives IQueryPipelineMediator
        // as parameter to avoid circular DI dependencies.
        // This ensures tracking and proxy creation for included entities.

        var method = typeof(FirestoreIncludeLoader).GetMethod("LoadIncludeAsync");
        var parameters = method!.GetParameters();

        parameters[3].ParameterType.Should().Be(typeof(IQueryPipelineMediator),
            "IncludeLoader receives Mediator as parameter for sub-pipeline execution");
    }

    [Fact]
    public void LoadIncludeAsync_Receives_QueryContext_As_Parameter()
    {
        // Documents that FirestoreIncludeLoader receives IFirestoreQueryContext
        // as parameter when creating sub-pipeline contexts for includes.

        var method = typeof(FirestoreIncludeLoader).GetMethod("LoadIncludeAsync");
        var parameters = method!.GetParameters();

        parameters[4].ParameterType.Should().Be(typeof(IFirestoreQueryContext),
            "IncludeLoader receives QueryContext as parameter for sub-pipeline");
    }

    [Fact]
    public void LoadIncludeAsync_Accepts_ResolvedInclude_Not_Raw_Include()
    {
        // Documents that IncludeLoader works with ResolvedInclude (already resolved)
        // not raw Include expressions. The Resolver has already:
        // - Resolved collection paths
        // - Evaluated filter expressions
        // - Determined document IDs (if applicable)

        var method = typeof(FirestoreIncludeLoader).GetMethod("LoadIncludeAsync");
        var parameters = method!.GetParameters();

        parameters[2].ParameterType.Should().Be(typeof(ResolvedInclude),
            "IncludeLoader works with resolved includes, not raw expressions");
    }

    #endregion

    #region Collection vs Reference Documentation Tests

    [Fact]
    public void ResolvedInclude_Has_IsCollection_Property()
    {
        // Documents that IncludeLoader handles both collection and reference navigations
        // - Collection: WHERE FK == ParentPK (e.g., Menu.Categories)
        // - Reference: GetDocument by FK value (e.g., Category.Menu)

        typeof(ResolvedInclude).GetProperty("IsCollection")
            .Should().NotBeNull("IncludeLoader differentiates collection vs reference");
    }

    [Fact]
    public void ResolvedInclude_Has_DocumentId_For_Reference_Optimization()
    {
        // Documents that reference navigations can use GetDocument optimization
        // when the FK value is known

        typeof(ResolvedInclude).GetProperty("DocumentId")
            .Should().NotBeNull("Reference navigations can use GetDocument by FK");
    }

    #endregion

    #region Nested Includes Documentation Tests

    [Fact]
    public void ResolvedInclude_Has_NestedIncludes_For_ThenInclude()
    {
        // Documents that IncludeLoader supports ThenInclude via NestedIncludes
        // Example: .Include(m => m.Categories).ThenInclude(c => c.Items)

        typeof(ResolvedInclude).GetProperty("NestedIncludes")
            .Should().NotBeNull("IncludeLoader supports nested ThenInclude");
    }

    [Fact]
    public void LoadIncludeAsync_Processes_NestedIncludes_Recursively()
    {
        // Documents that IncludeLoader calls itself recursively for nested includes
        // Each level creates its own sub-pipeline execution

        var method = typeof(FirestoreIncludeLoader).GetMethod("LoadIncludeAsync");

        method.Should().NotBeNull(
            "LoadIncludeAsync should exist and process nested includes recursively");
    }

    #endregion
}
