using SpecForge.Domain.Workflow;

namespace SpecForge.Domain.Application;

public sealed record PhaseExecutionEnvelope(
    string PhaseId,
    string EnvelopeKey,
    string ExecutionMode,
    string SandboxMode,
    IReadOnlyCollection<PhaseExecutionEnvelopeToolPermission> ToolPermissions,
    IReadOnlyCollection<PhaseExecutionEnvelopeWriteScope> WriteScopes,
    IReadOnlyCollection<PhaseExecutionEnvelopeBoundary> RepositoryBoundaries,
    PhaseExecutionEnvelopeBudget Budget);

public sealed record PhaseExecutionEnvelopeToolPermission(
    string Actor,
    string Tool,
    string Access,
    string Enforcement);

public sealed record PhaseExecutionEnvelopeWriteScope(
    string Actor,
    string Path,
    string Access,
    string Enforcement);

public sealed record PhaseExecutionEnvelopeBoundary(
    string Kind,
    string Path,
    string Access,
    string Summary);

public sealed record PhaseExecutionEnvelopeBudget(
    string ComputeTier,
    string TokenBudget,
    string TimeBudget,
    string MutationBudget,
    string Notes);

public static class PhaseExecutionEnvelopeCatalog
{
    private const string EnvelopeKey = "shared-execution-envelope/v1";

    public static PhaseExecutionEnvelope Describe(
        PhaseId phaseId,
        PhaseExecutionPolicy policy,
        PhaseExecutionReadiness? readiness = null)
    {
        var effectiveReadiness = readiness ?? new PhaseExecutionReadiness(
            phaseId,
            CanExecute: true,
            RequiredPermissions: policy.Permissions);
        var executionMode = ResolveExecutionMode(policy, effectiveReadiness);
        var sandboxMode = ResolveSandboxMode(phaseId, executionMode);

        return new PhaseExecutionEnvelope(
            PhaseId: WorkflowPresentation.ToPhaseSlug(phaseId),
            EnvelopeKey,
            ExecutionMode: executionMode,
            SandboxMode: sandboxMode,
            ToolPermissions: BuildToolPermissions(policy, executionMode),
            WriteScopes: BuildWriteScopes(policy, executionMode),
            RepositoryBoundaries: BuildRepositoryBoundaries(policy),
            Budget: BuildBudget(phaseId, policy, executionMode));
    }

    private static string ResolveExecutionMode(
        PhaseExecutionPolicy policy,
        PhaseExecutionReadiness readiness)
    {
        if (!policy.Permissions.ModelExecutionRequired)
        {
            return "workflow-entry";
        }

        return readiness.AssignedModelSecurity?.NativeCliRequired == true
            ? "native-cli"
            : "managed-provider";
    }

    private static string ResolveSandboxMode(PhaseId phaseId, string executionMode)
    {
        if (executionMode == "workflow-entry")
        {
            return "runtime-managed";
        }

        if (executionMode != "native-cli")
        {
            return "provider-managed";
        }

        return phaseId is PhaseId.Implementation or PhaseId.Review
            ? "workspace-write"
            : "read-only";
    }

    private static IReadOnlyCollection<PhaseExecutionEnvelopeToolPermission> BuildToolPermissions(
        PhaseExecutionPolicy policy,
        string executionMode)
    {
        var tools = new List<PhaseExecutionEnvelopeToolPermission>();

        tools.AddRange(policy.AllowedTools
            .Select(item => new PhaseExecutionEnvelopeToolPermission(
                item.Tool == "phase-artifact-persist" ? "specforge-runtime" : "phase-agent",
                item.Tool,
                item.Access,
                item.Enforcement)));

        if (executionMode == "managed-provider")
        {
            tools.Add(new PhaseExecutionEnvelopeToolPermission(
                "specforge-runtime",
                "context-materialization",
                "read",
                "enforced"));
        }

        return tools
            .GroupBy(static item => $"{item.Actor}|{item.Tool}|{item.Access}|{item.Enforcement}", StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();
    }

    private static IReadOnlyCollection<PhaseExecutionEnvelopeWriteScope> BuildWriteScopes(
        PhaseExecutionPolicy policy,
        string executionMode)
    {
        return policy.WritablePaths
            .Where(item => executionMode == "native-cli" || item.Actor == "specforge-runtime")
            .Select(item => new PhaseExecutionEnvelopeWriteScope(
                item.Actor,
                item.Path,
                item.Access,
                item.Enforcement))
            .ToArray();
    }

    private static IReadOnlyCollection<PhaseExecutionEnvelopeBoundary> BuildRepositoryBoundaries(
        PhaseExecutionPolicy policy)
    {
        var boundaries = new List<PhaseExecutionEnvelopeBoundary>
        {
            new(
                "workspace-root",
                "<workspace-root>",
                "scoped",
                "Execution is scoped to the current workspace root and user-story runtime state.")
        };

        boundaries.AddRange(policy.ForbiddenPaths.Select(item =>
            new PhaseExecutionEnvelopeBoundary(
                "forbidden-path",
                item.Path,
                item.Access,
                item.Reason)));

        return boundaries;
    }

    private static PhaseExecutionEnvelopeBudget BuildBudget(
        PhaseId phaseId,
        PhaseExecutionPolicy policy,
        string executionMode)
    {
        return phaseId switch
        {
            PhaseId.Capture => new PhaseExecutionEnvelopeBudget(
                ComputeTier: "minimal",
                TokenBudget: "none",
                TimeBudget: "short",
                MutationBudget: "workflow-metadata-only",
                Notes: "Capture only materializes initial workflow state and does not run a phase model."),
            PhaseId.Implementation => new PhaseExecutionEnvelopeBudget(
                ComputeTier: "extended",
                TokenBudget: "high",
                TimeBudget: "long",
                MutationBudget: executionMode == "native-cli" ? "phase-scoped-repository-mutation" : "artifact-only",
                Notes: "Implementation has the highest mutation budget because it may produce repository changes plus phase evidence."),
            PhaseId.Review => new PhaseExecutionEnvelopeBudget(
                ComputeTier: "extended",
                TokenBudget: "high",
                TimeBudget: "long",
                MutationBudget: executionMode == "native-cli" ? "phase-scoped-review-mutation" : "artifact-only",
                Notes: "Review can require deeper repository inspection and may run under a native CLI envelope in write-capable mode today."),
            PhaseId.TechnicalDesign or PhaseId.ReleaseApproval => new PhaseExecutionEnvelopeBudget(
                ComputeTier: "elevated",
                TokenBudget: "medium",
                TimeBudget: "standard",
                MutationBudget: "artifact-only",
                Notes: "These phases need broader reasoning context but should not mutate repository files outside workflow artifacts."),
            _ => new PhaseExecutionEnvelopeBudget(
                ComputeTier: "standard",
                TokenBudget: policy.Permissions.ModelExecutionRequired ? "medium" : "none",
                TimeBudget: "standard",
                MutationBudget: policy.Permissions.WorkspaceWriteAccess ? "phase-scoped" : "artifact-only",
                Notes: "Declared budget only for now; stronger enforcement lands in later waves.")
        };
    }
}
