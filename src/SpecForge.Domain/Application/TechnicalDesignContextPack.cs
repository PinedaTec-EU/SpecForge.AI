using System.Text.Json;
using SpecForge.Domain.Persistence;

namespace SpecForge.Domain.Application;

public sealed record TechnicalDesignContextPack(
    IReadOnlyCollection<RefinementSkillSelectionItem> SelectedSkills,
    RefinementGraphScopeRequest? GraphScopeRequest,
    string? ImpactGraphState,
    string? ImpactSummaryPath,
    bool GraphEnabled,
    bool GraphAvailable,
    bool FallbackUsed,
    IReadOnlyCollection<TechnicalDesignGraphExpansion> GraphBackedExpansions,
    IReadOnlyCollection<TechnicalDesignGraphQueryEvidence> GraphQueryEvidence,
    IReadOnlyCollection<string> Warnings);

public sealed record TechnicalDesignGraphExpansion(
    string Path,
    string Reason,
    string Source,
    string? ProjectPath = null,
    string? Sha256 = null);

public sealed record TechnicalDesignGraphQueryEvidence(
    string QueryKind,
    string Purpose,
    string Actor,
    string Tooling,
    string? ModelProfile,
    string SourceGraphUsed,
    string FreshnessState,
    bool FallbackUsed,
    int LatencyMs,
    SemanticGraphTokenUsage? TokenUsage,
    IReadOnlyCollection<string> IncludedFiles,
    IReadOnlyCollection<string> IncludedNodes,
    IReadOnlyCollection<string> InclusionReasons,
    IReadOnlyCollection<string> Warnings);

internal static class TechnicalDesignContextPackBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static async Task<TechnicalDesignContextPack> BuildAsync(
        string workspaceRoot,
        string usId,
        UserStoryFilePaths paths,
        PhaseExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(usId);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(context);

        var controls = SemanticGraphRuntimeControls.ResolveFromEnvironment();
        var warnings = new List<string>();
        var graphScopeRequest = await TryReadGraphScopeRequestAsync(paths.GraphScopeRequestPath, cancellationToken);
        var selectedSkills = BuildSelectedSkills(workspaceRoot, context, graphScopeRequest);
        var graphState = controls.UseSemanticGraphWhenAvailable ? "missing" : "disabled";
        var graphAvailable = false;
        var fallbackUsed = false;
        string? impactSummaryPath = null;
        IReadOnlyCollection<TechnicalDesignGraphExpansion> expansions = [];
        var queryEvidence = new List<TechnicalDesignGraphQueryEvidence>();

        if (controls.UseSemanticGraphWhenAvailable)
        {
            if (graphScopeRequest is null)
            {
                warnings.Add("Graph scope request is missing, so technical-design will continue without graph-backed narrowing.");
            }
            else
            {
                var status = SemanticGraphOperations.DescribeStatus(workspaceRoot, usId);
                var effectiveImpactState = status.ImpactGraph?.State ?? "missing";
                if (controls.AllowGraphBuildRefreshForTouchedUserStoryScope
                    && effectiveImpactState is "missing" or "stale-refreshable")
                {
                    var materializeResult = await SemanticGraphOperations.MaterializeImpactGraphAsync(
                        workspaceRoot,
                        new SemanticGraphImpactOperationRequest(
                            UsId: usId,
                            Actor: "workflow-runtime",
                            Reason: "Prepare technical-design context pack from the current refinement graph scope request.",
                            TriggerSurface: "workflow-runtime"),
                        cancellationToken);
                    warnings.AddRange(materializeResult.Warnings);
                    if (!materializeResult.Executed && materializeResult.BlockedReasons.Count > 0)
                    {
                        warnings.AddRange(materializeResult.BlockedReasons);
                    }

                    status = SemanticGraphOperations.DescribeStatus(workspaceRoot, usId);
                }

                queryEvidence.Add(MapQueryEvidence(
                    purpose: "Inspect graph readiness before technical-design narrowing begins.",
                    queryResult: SemanticGraphOperations.ExecuteQuery(
                        workspaceRoot,
                        new SemanticGraphQueryRequest(
                            QueryKind: "status",
                            Actor: "workflow-runtime",
                            UsId: usId,
                            Phase: "technical-design",
                            Reason: "Inspect graph readiness for the technical-design context pack.",
                            TriggerSurface: "workflow-runtime"))));

                graphState = status.ImpactGraph?.State ?? effectiveImpactState;
                graphAvailable = status.ImpactGraph?.Exists == true;
                impactSummaryPath = File.Exists(paths.ImpactGraphSummaryPath)
                    ? PhaseExecutionReceiptStore.NormalizePath(paths.ImpactGraphSummaryPath)
                    : null;

                var impactGraph = await TryReadImpactGraphAsync(paths.ImpactGraphPath, cancellationToken);
                if (impactGraph is not null)
                {
                    fallbackUsed = string.Equals(impactGraph.DerivationMode, "fallback-derived", StringComparison.Ordinal);
                    expansions = impactGraph.IncludedFiles
                        .Select(item => new TechnicalDesignGraphExpansion(
                            item.Path,
                            item.Reason,
                            item.Source,
                            item.ProjectPath,
                            item.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                                ? null
                                : PhaseExecutionReceiptStore.TryComputeFileSha256(
                                    Path.Combine(workspaceRoot, item.Path.Replace('/', Path.DirectorySeparatorChar)))))
                        .ToArray();
                    warnings.AddRange(impactGraph.Warnings);
                    foreach (var seedFile in graphScopeRequest.SeedFiles
                                 .Where(static item => !string.IsNullOrWhiteSpace(item.Path))
                                 .Take(3))
                    {
                        var queryResult = SemanticGraphOperations.ExecuteQuery(
                            workspaceRoot,
                            new SemanticGraphQueryRequest(
                                QueryKind: "why-included:file",
                                Actor: "workflow-runtime",
                                UsId: usId,
                                Phase: "technical-design",
                                Reason: $"Justify why `{seedFile.Path}` is part of the current design scope.",
                                FilePath: seedFile.Path,
                                TriggerSurface: "workflow-runtime"));
                        queryEvidence.Add(MapQueryEvidence(
                            purpose: $"Justify why `{seedFile.Path}` belongs in the technical-design scope.",
                            queryResult: queryResult));
                    }
                }
                else if (graphAvailable)
                {
                    warnings.Add("Impact graph metadata exists, but the artifact could not be read for the technical-design context pack.");
                }
            }
        }

        return new TechnicalDesignContextPack(
            SelectedSkills: selectedSkills,
            GraphScopeRequest: graphScopeRequest,
            ImpactGraphState: graphState,
            ImpactSummaryPath: impactSummaryPath,
            GraphEnabled: controls.UseSemanticGraphWhenAvailable,
            GraphAvailable: graphAvailable,
            FallbackUsed: fallbackUsed,
            GraphBackedExpansions: expansions,
            GraphQueryEvidence: queryEvidence,
            Warnings: warnings
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .ToArray());
    }

    private static TechnicalDesignGraphQueryEvidence MapQueryEvidence(
        string purpose,
        SemanticGraphQueryResult queryResult) =>
        new(
            QueryKind: queryResult.QueryKind,
            Purpose: purpose,
            Actor: "workflow-runtime",
            Tooling: "semantic-graph",
            ModelProfile: null,
            SourceGraphUsed: queryResult.SourceGraphUsed,
            FreshnessState: queryResult.FreshnessState,
            FallbackUsed: queryResult.FallbackUsed,
            LatencyMs: queryResult.LatencyMs,
            TokenUsage: null,
            IncludedFiles: queryResult.IncludedFiles,
            IncludedNodes: queryResult.IncludedNodes,
            InclusionReasons: queryResult.InclusionReasons,
            Warnings: queryResult.Warnings);

    private static IReadOnlyCollection<RefinementSkillSelectionItem> BuildSelectedSkills(
        string workspaceRoot,
        PhaseExecutionContext context,
        RefinementGraphScopeRequest? graphScopeRequest)
    {
        var skillPreselection = RefinementSkillPreselectionBuilder.Build(
            workspaceRoot,
            context,
            graphScopeRequest?.UnresolvedScopeQuestions ?? []);

        return skillPreselection.RequiredSkills
            .Concat(skillPreselection.CandidateSkills)
            .GroupBy(static item => item.SkillPath, StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();
    }

    private static async Task<RefinementGraphScopeRequest?> TryReadGraphScopeRequestAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<RefinementGraphScopeRequest>(stream, SerializerOptions, cancellationToken);
    }

    private static async Task<SemanticImpactGraphArtifact?> TryReadImpactGraphAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<SemanticImpactGraphArtifact>(stream, SerializerOptions, cancellationToken);
    }
}
