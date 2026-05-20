namespace SpecForge.Domain.Application;

public sealed record ImplementationPhasePolicySnapshot(
    string PhaseId,
    string PolicyKey,
    string Summary,
    bool ExecutionAllowed,
    string? ExecutionBlockingReason,
    PhaseExecutionRequirements Permissions,
    IReadOnlyCollection<PhaseExecutionToolPermission> AllowedTools,
    IReadOnlyCollection<PhaseExecutionPathPolicy> WritablePaths,
    IReadOnlyCollection<PhaseExecutionPathPolicy> ForbiddenPaths,
    IReadOnlyCollection<PhaseExecutionEvidenceRequirement> EvidenceRequirements,
    IReadOnlyCollection<PhaseExecutionEligibilityRule> EligibilityRules);

public static class ImplementationPhasePolicySnapshotBuilder
{
    public static ImplementationPhasePolicySnapshot Build(
        PhaseExecutionReadiness readiness,
        PhaseExecutionPolicy policy) =>
        new(
            policy.PhaseId,
            policy.PolicyKey,
            policy.Summary,
            readiness.CanExecute,
            readiness.BlockingReason,
            policy.Permissions,
            policy.AllowedTools,
            policy.WritablePaths,
            policy.ForbiddenPaths,
            policy.EvidenceRequirements,
            policy.EligibilityRules);
}
