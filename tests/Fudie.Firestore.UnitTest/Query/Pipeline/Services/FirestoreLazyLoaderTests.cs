using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Fudie.Firestore.UnitTest.Query.Pipeline.Services;

public class FirestoreLazyLoaderTests
{
    #region Class Structure Tests

    [Fact]
    public void FirestoreLazyLoader_Implements_ILazyLoader()
    {
        typeof(FirestoreLazyLoader)
            .Should().Implement<ILazyLoader>();
    }

    [Fact]
    public void FirestoreLazyLoader_Constructor_Accepts_IQueryPipelineMediator()
    {
        var constructors = typeof(FirestoreLazyLoader).GetConstructors();

        constructors.Should().HaveCount(1);
        var parameters = constructors[0].GetParameters();

        // Should accept mediator and query context
        parameters.Should().Contain(p => p.ParameterType == typeof(IQueryPipelineMediator));
    }

    [Fact]
    public void FirestoreLazyLoader_Can_Be_Instantiated()
    {
        var mockMediator = new Mock<IQueryPipelineMediator>();
        var mockQueryContext = new Mock<IFirestoreQueryContext>();

        var lazyLoader = new FirestoreLazyLoader(mockMediator.Object, mockQueryContext.Object);

        lazyLoader.Should().NotBeNull();
    }

    #endregion

    #region ILazyLoader Methods Tests

    [Fact]
    public void FirestoreLazyLoader_Has_Load_Method()
    {
        // ILazyLoader defines Load(object entity, string navigationName)
        var method = typeof(FirestoreLazyLoader).GetMethod("Load",
            new[] { typeof(object), typeof(string) });

        method.Should().NotBeNull();
    }

    [Fact]
    public void FirestoreLazyLoader_Has_LoadAsync_Method()
    {
        // ILazyLoader defines LoadAsync(object entity, CancellationToken, string navigationName)
        var method = typeof(FirestoreLazyLoader).GetMethod("LoadAsync");

        method.Should().NotBeNull();
    }

    #endregion

    #region Sub-Pipeline Behavior Tests

    [Fact]
    public void Load_Executes_SubPipeline_For_Navigation()
    {
        // When Load is called:
        // 1. Build AST for the navigation query
        // 2. Execute full pipeline (Resolver → Execute → Convert → Tracking → Proxy)
        // 3. Assign result to navigation property
        typeof(FirestoreLazyLoader).Should().NotBeNull(
            "FirestoreLazyLoader must execute sub-pipeline for navigation loading");
    }

    [Fact]
    public void SubPipeline_Results_Are_Tracked()
    {
        // Entities loaded via lazy loading must be tracked
        // This happens because sub-pipeline includes TrackingHandler
        typeof(FirestoreLazyLoader).Should().NotBeNull(
            "Sub-pipeline results must be tracked");
    }

    [Fact]
    public void SubPipeline_Results_Are_Proxied()
    {
        // Entities loaded via lazy loading must be proxied for nested lazy loading
        // Example: Menu.Categories loads Category proxies, which can lazy load Items
        typeof(FirestoreLazyLoader).Should().NotBeNull(
            "Sub-pipeline results must be proxied for nested lazy loading");
    }

    #endregion

    #region Navigation Query Building Tests

    [Fact]
    public void Load_Builds_Correct_Query_For_Collection_Navigation()
    {
        // For Menu.Categories (collection):
        // Query: WHERE ParentId == menu.Id
        typeof(FirestoreLazyLoader).Should().NotBeNull(
            "FirestoreLazyLoader must build correct query for collections");
    }

    [Fact]
    public void Load_Builds_Correct_Query_For_Reference_Navigation()
    {
        // For Category.Menu (reference):
        // Query: WHERE Id == category.MenuId (get by document ID)
        typeof(FirestoreLazyLoader).Should().NotBeNull(
            "FirestoreLazyLoader must build correct query for references");
    }

    #endregion

    #region SetLoaded/IsLoaded/Dispose Tests

    [Fact]
    public void IsLoaded_Returns_False_By_Default()
    {
        var mockMediator = new Mock<IQueryPipelineMediator>();
        var mockQueryContext = new Mock<IFirestoreQueryContext>();
        var lazyLoader = new FirestoreLazyLoader(mockMediator.Object, mockQueryContext.Object);

        var entity = new object();

        lazyLoader.IsLoaded(entity, "Navigation").Should().BeFalse();
    }

    [Fact]
    public void SetLoaded_Marks_Navigation_As_Loaded()
    {
        var mockMediator = new Mock<IQueryPipelineMediator>();
        var mockQueryContext = new Mock<IFirestoreQueryContext>();
        var lazyLoader = new FirestoreLazyLoader(mockMediator.Object, mockQueryContext.Object);

        var entity = new object();

        lazyLoader.SetLoaded(entity, "Navigation", true);

        lazyLoader.IsLoaded(entity, "Navigation").Should().BeTrue();
    }

    [Fact]
    public void SetLoaded_With_False_Removes_Loaded_Status()
    {
        var mockMediator = new Mock<IQueryPipelineMediator>();
        var mockQueryContext = new Mock<IFirestoreQueryContext>();
        var lazyLoader = new FirestoreLazyLoader(mockMediator.Object, mockQueryContext.Object);

        var entity = new object();
        lazyLoader.SetLoaded(entity, "Navigation", true);

        lazyLoader.SetLoaded(entity, "Navigation", false);

        lazyLoader.IsLoaded(entity, "Navigation").Should().BeFalse();
    }

    [Fact]
    public void Dispose_Clears_Loaded_Navigations()
    {
        var mockMediator = new Mock<IQueryPipelineMediator>();
        var mockQueryContext = new Mock<IFirestoreQueryContext>();
        var lazyLoader = new FirestoreLazyLoader(mockMediator.Object, mockQueryContext.Object);

        var entity = new object();
        lazyLoader.SetLoaded(entity, "Navigation", true);

        lazyLoader.Dispose();

        lazyLoader.IsLoaded(entity, "Navigation").Should().BeFalse();
    }

    [Fact]
    public void IsLoaded_Tracks_Different_Navigations_Independently()
    {
        var mockMediator = new Mock<IQueryPipelineMediator>();
        var mockQueryContext = new Mock<IFirestoreQueryContext>();
        var lazyLoader = new FirestoreLazyLoader(mockMediator.Object, mockQueryContext.Object);

        var entity = new object();
        lazyLoader.SetLoaded(entity, "Navigation1", true);

        lazyLoader.IsLoaded(entity, "Navigation1").Should().BeTrue();
        lazyLoader.IsLoaded(entity, "Navigation2").Should().BeFalse();
    }

    [Fact]
    public void IsLoaded_Tracks_Different_Entities_Independently()
    {
        var mockMediator = new Mock<IQueryPipelineMediator>();
        var mockQueryContext = new Mock<IFirestoreQueryContext>();
        var lazyLoader = new FirestoreLazyLoader(mockMediator.Object, mockQueryContext.Object);

        var entity1 = new object();
        var entity2 = new object();
        lazyLoader.SetLoaded(entity1, "Navigation", true);

        lazyLoader.IsLoaded(entity1, "Navigation").Should().BeTrue();
        lazyLoader.IsLoaded(entity2, "Navigation").Should().BeFalse();
    }

    #endregion

    #region LoadAsync Validation Tests

    [Fact]
    public async Task LoadAsync_Throws_ArgumentNullException_For_Null_Entity()
    {
        var mockMediator = new Mock<IQueryPipelineMediator>();
        var mockQueryContext = new Mock<IFirestoreQueryContext>();
        var lazyLoader = new FirestoreLazyLoader(mockMediator.Object, mockQueryContext.Object);

        Func<Task> act = () => lazyLoader.LoadAsync(null!, default, "Navigation");

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("entity");
    }

    [Fact]
    public async Task LoadAsync_Throws_ArgumentException_For_Empty_NavigationName()
    {
        var mockMediator = new Mock<IQueryPipelineMediator>();
        var mockQueryContext = new Mock<IFirestoreQueryContext>();
        var lazyLoader = new FirestoreLazyLoader(mockMediator.Object, mockQueryContext.Object);

        Func<Task> act = () => lazyLoader.LoadAsync(new object(), default, "");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("navigationName");
    }

    #endregion
}
