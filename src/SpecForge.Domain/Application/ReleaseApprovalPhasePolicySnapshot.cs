namespace SpecForge.Domain.Application;

public sealed record ReleaseApprovalPhasePolicySnapshot(
    string PhaseId,
    string PolicyKey,
    string Summary,
    string Status,
    bool ExecutionAllowed,
    string? ExecutionBlockingReason,
    PhaseExecutionRequirements Permissions,
    IReadOnlyCollection<PhaseExecutionEvidenceRequirement> EvidenceRequirements,
    IReadOnlyCollection<PhaseExecutionEligibilityRule> EligibilityRules,
    bool ApprovalAvailableNow,
    string? ApprovalBlockingReason,
    string? LatestReviewVerdict,
    bool LatestReviewWasForceApproved,
    bool HasReleaseArtifact,
    bool HasReleaseEvidencePack,
    bool HasImplementationEvidence,
    bool HasReviewGateResult,
    bool HasBranchContext,
    bool HasTimelineContext,
    string? CurrentWorkspaceHeadSha,
    string? ApprovedReviewCommitSha,
    bool? ReviewCommitMatchesWorkspaceHead,
    IReadOnlyCollection<ReleaseApprovalEvidenceRule> EvidenceRules,
    IReadOnlyCollection<ReleaseApprovalPolicyCondition> ExecutionConditions,
    IReadOnlyCollection<ReleaseApprovalPolicyCondition> ApprovalConditions);

public static class ReleaseApprovalPhasePolicySnapshotBuilder
{
    public static ReleaseApprovalPhasePolicySnapshot Build(
        PhaseExecutionReadiness readiness,
        PhaseExecutionPolicy policy,
        ReleaseApprovalPolicyDetails releaseApprovalPolicy) =>
        new(
            policy.PhaseId,
            policy.PolicyKey,
            policy.Summary,
            releaseApprovalPolicy.Status,
            readiness.CanExecute,
            readiness.BlockingReason,
            policy.Permissions,
            policy.EvidenceRequirements,
            policy.EligibilityRules,
            releaseApprovalPolicy.ApprovalAvailableNow,
            releaseApprovalPolicy.ApprovalBlockingReason,
            releaseApprovalPolicy.LatestReviewVerdict,
            releaseApprovalPolicy.LatestReviewWasForceApproved,
            releaseApprovalPolicy.HasReleaseArtifact,
            releaseApprovalPolicy.HasReleaseEvidencePack,
            releaseApprovalPolicy.HasImplementationEvidence,
            releaseApprovalPolicy.HasReviewGateResult,
            releaseApprovalPolicy.HasBranchContext,
            releaseApprovalPolicy.HasTimelineContext,
            releaseApprovalPolicy.CurrentWorkspaceHeadSha,
            releaseApprovalPolicy.ApprovedReviewCommitSha,
            releaseApprovalPolicy.ReviewCommitMatchesWorkspaceHead,
            releaseApprovalPolicy.EvidenceRules,
            releaseApprovalPolicy.ExecutionConditions,
            releaseApprovalPolicy.ApprovalConditions);
}
