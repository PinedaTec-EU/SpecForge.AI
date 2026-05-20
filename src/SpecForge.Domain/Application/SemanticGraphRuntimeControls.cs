namespace SpecForge.Domain.Application;

public sealed record SemanticGraphRuntimeControls(
    bool UseSemanticGraphWhenAvailable,
    bool AllowGraphBuildRefreshForTouchedUserStoryScope)
{
    public static SemanticGraphRuntimeControls Default => new(
        UseSemanticGraphWhenAvailable: true,
        AllowGraphBuildRefreshForTouchedUserStoryScope: false);

    public string DescribeDefaultBehavior() =>
        UseSemanticGraphWhenAvailable
            ? AllowGraphBuildRefreshForTouchedUserStoryScope
                ? "Reuse semantic graph artifacts when available and allow graph materialization or refresh for the touched user-story scope."
                : "Reuse semantic graph artifacts when available, but do not create or refresh graph state automatically for the touched user-story scope."
            : "Ignore semantic graph artifacts during workflow runtime and rely on fallback context expansion only.";
}
