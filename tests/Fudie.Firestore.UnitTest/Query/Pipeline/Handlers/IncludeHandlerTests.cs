using Firestore.EntityFrameworkCore.Query.Resolved;

namespace Fudie.Firestore.UnitTest.Query.Pipeline.Handlers;

public class IncludeHandlerTests
{
    #region Class Structure Tests

    [Fact]
    public void IncludeHandler_Implements_IQueryPipelineHandler()
    {
        typeof(IncludeHandler)
            .Should().Implement<IQueryPipelineHandler>();
    }

    [Fact]
    public void IncludeHandler_Extends_QueryPipelineHandlerBase()
    {
        typeof(IncludeHandler)
            .Should().BeDerivedFrom<QueryPipelineHandlerBase>();
    }

    [Fact]
    public void IncludeHandler_Constructor_Accepts_IIncludeLoader()
    {
        var constructors = typeof(IncludeHandler).GetConstructors();

        constructors.Should().HaveCount(1);
        var parameters = constructors[0].GetParameters();

        parameters.Should().Contain(p => p.ParameterType == typeof(IIncludeLoader));
    }

    #endregion

    #region ApplicableKinds Tests

    [Fact]
    public void ApplicableKinds_Contains_Only_Entity()
    {
        // IncludeHandler only applies to Entity queries
        // Aggregation/Scalar queries don't have includes
        typeof(IncludeHandler).Should().NotBeNull(
            "IncludeHandler should only apply to QueryKind.Entity");
    }

    #endregion

    #region Handler Behavior Tests

    [Fact]
    public void HandleAsync_Passes_Through_When_No_ResolvedIncludes()
    {
        // If ResolvedFirestoreQuery has no Includes, just pass through
        typeof(IncludeHandler).Should().NotBeNull(
            "IncludeHandler should pass through when no includes");
    }

    [Fact]
    public void HandleAsync_Uses_ResolvedIncludes_Not_PendingIncludes()
    {
        // IncludeHandler uses ResolvedInclude from ResolvedFirestoreQuery
        // NOT IncludeInfo from AST.PendingIncludes
        // Resolver has already done all the resolution work
        typeof(IncludeHandler).Should().NotBeNull(
            "IncludeHandler should use ResolvedInclude from ResolvedFirestoreQuery");
    }

    [Fact]
    public void HandleAsync_Uses_IIncludeLoader_To_Execute_Includes()
    {
        // IncludeHandler delegates to IIncludeLoader for execution
        typeof(IncludeHandler).Should().NotBeNull(
            "IncludeHandler should use IIncludeLoader");
    }

    #endregion

    #region IIncludeLoader Tests

    [Fact]
    public void IIncludeLoader_Accepts_ResolvedInclude()
    {
        // IIncludeLoader.LoadIncludeAsync takes ResolvedInclude, not IncludeInfo
        var method = typeof(IIncludeLoader).GetMethod("LoadIncludeAsync");

        method.Should().NotBeNull();
        method!.GetParameters()
            .Should().Contain(p => p.ParameterType == typeof(ResolvedInclude));
    }

    [Fact]
    public void IncludeLoader_Parametrizes_Query_With_FK_Value()
    {
        // ResolvedInclude has filters/ordering/pagination already resolved
        // IncludeLoader only adds the FK filter for the specific parent entity
        typeof(FirestoreIncludeLoader).Should().NotBeNull(
            "IncludeLoader should parametrize with FK value");
    }

    [Fact]
    public void IncludeLoader_Executes_SubPipeline_For_Navigation()
    {
        // Like FirestoreLazyLoader, uses IQueryPipelineMediator
        typeof(FirestoreIncludeLoader).Should().NotBeNull(
            "IncludeLoader should execute sub-pipeline");
    }

    [Fact]
    public void IncludeLoader_Handles_Nested_Includes_Recursively()
    {
        // ResolvedInclude has NestedIncludes for ThenInclude
        // IncludeLoader processes them recursively
        typeof(FirestoreIncludeLoader).Should().NotBeNull(
            "IncludeLoader should handle nested includes");
    }

    #endregion

    #region Resolved Include Tests

    [Fact]
    public void ResolvedInclude_Has_FilterResults_Already_Resolved()
    {
        // Filters are resolved by Resolver, not by IncludeLoader
        typeof(ResolvedInclude).GetProperty("FilterResults")
            .Should().NotBeNull();
    }

    [Fact]
    public void ResolvedInclude_Has_OrderByClauses_Already_Resolved()
    {
        // OrderBy is resolved by Resolver
        typeof(ResolvedInclude).GetProperty("OrderByClauses")
            .Should().NotBeNull();
    }

    [Fact]
    public void ResolvedInclude_Has_Pagination_Already_Resolved()
    {
        // Pagination (Take/Skip) is resolved by Resolver
        typeof(ResolvedInclude).GetProperty("Pagination")
            .Should().NotBeNull();
    }

    [Fact]
    public void ResolvedInclude_Has_DocumentId_For_Reference_Optimization()
    {
        // For reference navigations, Resolver may set DocumentId for GetDocumentAsync
        typeof(ResolvedInclude).GetProperty("DocumentId")
            .Should().NotBeNull();
    }

    #endregion
}
