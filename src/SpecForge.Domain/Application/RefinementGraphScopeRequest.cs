namespace SpecForge.Domain.Application;

public sealed record RefinementGraphScopeRequest(
    int Depth,
    IReadOnlyCollection<RefinementGraphSeedNode> SeedNodes,
    IReadOnlyCollection<PhaseExecutionArtifactInput> SeedFiles,
    IReadOnlyCollection<string> UnresolvedScopeQuestions);

public sealed record RefinementGraphSeedNode(
    string Id,
    string Label,
    string Reason);

public static class RefinementGraphScopeRequestBuilder
{
    public static RefinementGraphScopeRequest Build(
        PhaseExecutionContext context,
        string refinementArtifactPath,
        IReadOnlyCollection<string> pendingQuestions,
        RefinementSkillPreselection skillPreselection)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(skillPreselection);

        var unresolvedScopeQuestions = pendingQuestions
            .Where(static question => !string.IsNullOrWhiteSpace(question))
            .Select(static question => question.Trim())
            .ToArray();
        var seedNodes = BuildSeedNodes(skillPreselection, unresolvedScopeQuestions, context.ContextFilePaths.Count);
        var seedFiles = BuildSeedFiles(context, refinementArtifactPath);
        var depth = ResolveDepth(unresolvedScopeQuestions.Length, skillPreselection.CandidateSkills.Count, context.ContextFilePaths.Count);

        return new RefinementGraphScopeRequest(
            depth,
            seedNodes,
            seedFiles,
            unresolvedScopeQuestions);
    }

    private static IReadOnlyCollection<RefinementGraphSeedNode> BuildSeedNodes(
        RefinementSkillPreselection skillPreselection,
        IReadOnlyCollection<string> unresolvedScopeQuestions,
        int contextFileCount)
    {
        var nodes = new List<RefinementGraphSeedNode>
        {
            new(
                "user-story-intent",
                "User Story Intent",
                "This is the primary business intent that technical design must preserve."),
            new(
                "refinement-decision",
                "Refinement Decision",
                unresolvedScopeQuestions.Count == 0
                    ? "Refinement is currently ready for downstream design handoff."
                    : "Refinement still has unresolved questions that technical design should treat as scope uncertainty.")
        };

        if (contextFileCount > 0)
        {
            nodes.Add(new RefinementGraphSeedNode(
                "repository-context",
                "Repository Context",
                "Attached context files already narrow the repository area that technical design should inspect first."));
        }

        foreach (var candidate in skillPreselection.CandidateSkills)
        {
            var skillPath = candidate.SkillPath;
            if (skillPath.Contains("/hexagonal/", StringComparison.Ordinal))
            {
                nodes.Add(new RefinementGraphSeedNode(
                    "adapter-boundaries",
                    "Adapter Boundaries",
                    "Hexagonal signals suggest technical design should map controller, provider, or port boundaries early."));
            }
            else if (skillPath.Contains("/ddd/", StringComparison.Ordinal))
            {
                nodes.Add(new RefinementGraphSeedNode(
                    "domain-model",
                    "Domain Model",
                    "DDD signals suggest technical design should inspect aggregates, invariants, and ownership boundaries."));
            }
            else if (skillPath.Contains("/domain-events/", StringComparison.Ordinal))
            {
                nodes.Add(new RefinementGraphSeedNode(
                    "domain-events",
                    "Domain Events",
                    "Event-oriented signals suggest technical design should inspect triggers, notifications, and integration edges."));
            }
            else if (skillPath.Contains("/terraform/", StringComparison.Ordinal))
            {
                nodes.Add(new RefinementGraphSeedNode(
                    "infrastructure-scope",
                    "Infrastructure Scope",
                    "Infrastructure signals suggest technical design should inspect deployment or environment wiring."));
            }
        }

        return nodes
            .GroupBy(static item => item.Id, StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();
    }

    private static IReadOnlyCollection<PhaseExecutionArtifactInput> BuildSeedFiles(
        PhaseExecutionContext context,
        string refinementArtifactPath)
    {
        var files = new List<PhaseExecutionArtifactInput>
        {
            new(
                PhaseExecutionReceiptStore.NormalizePath(context.UserStoryPath),
                PhaseExecutionReceiptStore.TryComputeFileSha256(context.UserStoryPath),
                "capture"),
            new(
                PhaseExecutionReceiptStore.NormalizePath(refinementArtifactPath),
                PhaseExecutionReceiptStore.TryComputeFileSha256(refinementArtifactPath),
                "refinement")
        };

        files.AddRange(context.PreviousArtifactPaths
            .OrderBy(static item => item.Key)
            .Select(static item => new PhaseExecutionArtifactInput(
                PhaseExecutionReceiptStore.NormalizePath(item.Value),
                PhaseExecutionReceiptStore.TryComputeFileSha256(item.Value),
                WorkflowPresentation.ToPhaseSlug(item.Key))));

        files.AddRange(context.ContextFilePaths
            .OrderBy(static path => path, StringComparer.Ordinal)
            .Select(static path => new PhaseExecutionArtifactInput(
                PhaseExecutionReceiptStore.NormalizePath(path),
                PhaseExecutionReceiptStore.TryComputeFileSha256(path))));

        return files
            .GroupBy(static item => item.Path, StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();
    }

    private static int ResolveDepth(int unresolvedQuestionCount, int candidateSkillCount, int contextFileCount)
    {
        var depth = 1;
        if (unresolvedQuestionCount >= 2 || candidateSkillCount >= 1 || contextFileCount >= 2)
        {
            depth++;
        }

        if (unresolvedQuestionCount >= 4 || candidateSkillCount >= 3 || contextFileCount >= 5)
        {
            depth++;
        }

        return Math.Clamp(depth, 1, 3);
    }
}
