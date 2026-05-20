using SpecForge.Domain.Application;
using SpecForge.Domain.Persistence;

namespace SpecForge.Domain.Tests;

public sealed class SemanticGraphLifecycleTests
{
    [Fact]
    public void Describe_ReturnsRepositoryGlobalAndUserStoryImpactLifecycleContract()
    {
        var userStoryPaths = UserStoryFilePaths.FromWorkspaceRoot("/repo", "workflow", "US-0001");

        var contract = SemanticGraphLifecycleCatalog.Describe("/repo", userStoryPaths);

        Assert.Equal("semantic-code-graph-lifecycle/v1", contract.ContractKey);

        Assert.Equal("repository-global", contract.GlobalGraph.Scope);
        Assert.Equal("/repo/.specs/cache/graphs/global-graph.json", contract.GlobalGraph.ArtifactPath);
        Assert.Equal("/repo/.specs/cache/graphs/global-graph.meta.json", contract.GlobalGraph.MetadataPath);
        Assert.Equal("/repo/.specs/cache/graphs/graph-build-log.jsonl", contract.GlobalGraph.AuxiliaryPath);
        Assert.Equal("create-if-missing|rebuild-from-zero", contract.GlobalGraph.BuildMode);
        Assert.True(contract.GlobalGraph.OverwriteRequiresConfirmation);

        Assert.Equal("user-story-impact", contract.ImpactGraph.Scope);
        Assert.Equal("/repo/.specs/us/US-0001/context/impact-graph.json", contract.ImpactGraph.ArtifactPath);
        Assert.Equal("/repo/.specs/us/US-0001/context/impact-graph.meta.json", contract.ImpactGraph.MetadataPath);
        Assert.Equal("/repo/.specs/us/US-0001/context/impact-summary.md", contract.ImpactGraph.AuxiliaryPath);
        Assert.Equal("materialize-from-global|materialize-from-fallback", contract.ImpactGraph.BuildMode);
        Assert.False(contract.ImpactGraph.OverwriteRequiresConfirmation);

        Assert.Equal("reuse", contract.FreshnessPolicy.FreshBehavior);
        Assert.Equal("refresh-incrementally", contract.FreshnessPolicy.StaleBehavior);
        Assert.True(contract.FallbackPolicy.WorkflowMayProceedWithoutGraph);
        Assert.Equal("mini-graph-pack", contract.FallbackPolicy.FallbackArtifactKind);
        Assert.Contains(".specs/us/<US>/context/", contract.OwnershipBoundary.WorkflowRuntimeResponsibility, StringComparison.Ordinal);
    }

    [Fact]
    public void SemanticGraphFilePaths_FromWorkspaceRoot_UsesRepositoryCacheDirectory()
    {
        var paths = SemanticGraphFilePaths.FromWorkspaceRoot("/repo");

        Assert.Equal("/repo/.specs/cache/graphs", paths.GraphsDirectoryPath);
        Assert.Equal("/repo/.specs/cache/graphs/global-graph.json", paths.GlobalGraphPath);
        Assert.Equal("/repo/.specs/cache/graphs/global-graph.meta.json", paths.GlobalGraphMetadataPath);
        Assert.Equal("/repo/.specs/cache/graphs/graph-build-log.jsonl", paths.GraphBuildLogPath);
        Assert.Equal("/repo/.specs/cache/graphs/graph-cost-ledger.json", paths.GraphCostLedgerPath);
    }
}
