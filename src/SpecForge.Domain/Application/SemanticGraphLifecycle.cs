using SpecForge.Domain.Persistence;

namespace SpecForge.Domain.Application;

public sealed record SemanticGraphLifecycleContract(
    string ContractKey,
    SemanticGraphArtifactLifecycle GlobalGraph,
    SemanticGraphArtifactLifecycle ImpactGraph,
    SemanticGraphFreshnessPolicy FreshnessPolicy,
    SemanticGraphFallbackPolicy FallbackPolicy,
    SemanticGraphOwnershipBoundary OwnershipBoundary);

public sealed record SemanticGraphArtifactLifecycle(
    string Scope,
    string ArtifactPath,
    string MetadataPath,
    string? AuxiliaryPath,
    string BuildMode,
    string ReuseMode,
    bool OverwriteRequiresConfirmation);

public sealed record SemanticGraphFreshnessPolicy(
    string FreshBehavior,
    string StaleBehavior,
    string MissingBehavior,
    string RefreshPreference);

public sealed record SemanticGraphFallbackPolicy(
    bool WorkflowMayProceedWithoutGraph,
    string FallbackArtifactKind,
    string MissingGraphBehavior,
    string ExtractionFailureBehavior);

public sealed record SemanticGraphOwnershipBoundary(
    string WorkflowRuntimeResponsibility,
    string GraphServiceResponsibility,
    string PhaseConsumerResponsibility);

public static class SemanticGraphLifecycleCatalog
{
    public const string ContractKey = "semantic-code-graph-lifecycle/v1";

    public static SemanticGraphLifecycleContract Describe(
        string workspaceRoot,
        UserStoryFilePaths userStoryPaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(userStoryPaths);

        var repositoryPaths = SemanticGraphFilePaths.FromWorkspaceRoot(workspaceRoot);

        return new SemanticGraphLifecycleContract(
            ContractKey,
            GlobalGraph: new SemanticGraphArtifactLifecycle(
                Scope: "repository-global",
                ArtifactPath: Normalize(repositoryPaths.GlobalGraphPath),
                MetadataPath: Normalize(repositoryPaths.GlobalGraphMetadataPath),
                AuxiliaryPath: Normalize(repositoryPaths.GraphBuildLogPath),
                BuildMode: "create-if-missing|rebuild-from-zero",
                ReuseMode: "reuse-if-fresh|refresh-if-stale",
                OverwriteRequiresConfirmation: true),
            ImpactGraph: new SemanticGraphArtifactLifecycle(
                Scope: "user-story-impact",
                ArtifactPath: Normalize(userStoryPaths.ImpactGraphPath),
                MetadataPath: Normalize(userStoryPaths.ImpactGraphMetadataPath),
                AuxiliaryPath: Normalize(userStoryPaths.ImpactGraphSummaryPath),
                BuildMode: "materialize-from-global|materialize-from-fallback",
                ReuseMode: "reuse-if-inputs-match|refresh-on-scope-change",
                OverwriteRequiresConfirmation: false),
            FreshnessPolicy: new SemanticGraphFreshnessPolicy(
                FreshBehavior: "reuse",
                StaleBehavior: "refresh-incrementally",
                MissingBehavior: "build-or-fallback",
                RefreshPreference: "prefer-incremental-refresh-over-full-rebuild"),
            FallbackPolicy: new SemanticGraphFallbackPolicy(
                WorkflowMayProceedWithoutGraph: true,
                FallbackArtifactKind: "mini-graph-pack",
                MissingGraphBehavior: "allow-phase-fallback-when-graph-unavailable",
                ExtractionFailureBehavior: "record-fallback-and-continue"),
            OwnershipBoundary: new SemanticGraphOwnershipBoundary(
                WorkflowRuntimeResponsibility: "Stores graph lifecycle metadata, decides whether phases can reuse graph outputs, and persists user-story graph artifacts under `.specs/us/<US>/context/`.",
                GraphServiceResponsibility: "Builds, refreshes, and queries the repository-global graph plus user-story impact graphs according to the declared lifecycle contract.",
                PhaseConsumerResponsibility: "Consumes bounded graph outputs and must not mutate global graph state directly from phase execution."));
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
}
