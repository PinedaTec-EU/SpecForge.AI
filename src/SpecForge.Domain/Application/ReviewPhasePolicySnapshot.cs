namespace SpecForge.Domain.Application;

public sealed record ReviewPhasePolicySnapshot(
    string PhaseId,
    string PolicyKey,
    string Summary,
    bool ExecutionAllowed,
    string? ExecutionBlockingReason,
    PhaseExecutionRequirements Permissions,
    IReadOnlyCollection<PhaseExecutionEvidenceRequirement> EvidenceRequirements,
    IReadOnlyCollection<PhaseExecutionEligibilityRule> EligibilityRules,
    string ActiveEvidencePolicy,
    string? LatestGateVerdict,
    bool? LatestHasBlockingFindings,
    bool ForceApprovalRequiresReason,
    IReadOnlyCollection<ReviewEvidencePolicyRule> EvidenceRules,
    IReadOnlyCollection<ReviewPhaseOverrideCondition> OverrideConditions);

public static class ReviewPhasePolicySnapshotBuilder
{
    public static ReviewPhasePolicySnapshot Build(
        PhaseExecutionReadiness readiness,
        PhaseExecutionPolicy policy,
        ReviewPhasePolicyDetails reviewPolicyDetails) =>
        new(
            policy.PhaseId,
            policy.PolicyKey,
            policy.Summary,
            readiness.CanExecute,
            readiness.BlockingReason,
            policy.Permissions,
            policy.EvidenceRequirements,
            policy.EligibilityRules,
            reviewPolicyDetails.ActiveEvidencePolicy,
            reviewPolicyDetails.LatestGateVerdict,
            reviewPolicyDetails.LatestHasBlockingFindings,
            reviewPolicyDetails.ForceApprovalRequiresReason,
            reviewPolicyDetails.EvidenceRules,
            reviewPolicyDetails.OverrideConditions);
}
