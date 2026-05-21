using System.Text.RegularExpressions;
using SpecForge.Domain.Persistence;
using SpecForge.Domain.Workflow;

namespace SpecForge.Domain.Application;

public sealed record ReleaseApprovalPolicyDetails(
    string Status,
    bool ExecutionEligibleNow,
    string? ExecutionBlockingReason,
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

public sealed record ReleaseApprovalEvidenceRule(
    string EvidenceKind,
    bool IsRequired,
    string CurrentStatusMessage);

public sealed record ReleaseApprovalPolicyCondition(
    string Id,
    string Description,
    string Status,
    bool IsCurrentlySatisfied,
    string? BlockingReason = null,
    string? CurrentStatusMessage = null);

internal sealed record ReleaseApprovalExecutionGuardResult(
    string? LatestReviewVerdict,
    bool LatestReviewWasForceApproved,
    string? CurrentWorkspaceHeadSha,
    string? ApprovedReviewCommitSha,
    bool? ReviewCommitMatchesWorkspaceHead,
    bool CanExecute,
    string? BlockingReason);

public static class ReleaseApprovalPolicyDetailsBuilder
{
    public static ReleaseApprovalPolicyDetails Build(
        string workspaceRoot,
        UserStoryFilePaths paths,
        bool isCurrentReleaseApprovalPhase,
        ReleaseApprovalEvidencePack? releaseApprovalEvidencePack,
        IReadOnlyCollection<TimelineEventDetails> timelineEvents)
    {
        var executionGuard = EvaluateExecutionGuard(workspaceRoot, paths, timelineEvents);
        var hasReleaseArtifact = !string.IsNullOrWhiteSpace(paths.GetLatestExistingPhaseArtifactPath(PhaseId.ReleaseApproval));
        var hasReleaseEvidencePack = releaseApprovalEvidencePack is not null;
        var hasImplementationEvidence = releaseApprovalEvidencePack?.SupportingArtifacts.Any(static item =>
            item.Kind is "implementation-evidence-markdown" or "implementation-evidence-json") == true;
        var hasReviewGateResult = !string.IsNullOrWhiteSpace(releaseApprovalEvidencePack?.ReviewVerdict);
        var hasBranchContext = releaseApprovalEvidencePack?.SupportingArtifacts.Any(static item => item.Kind == "branch-context") == true;
        var hasTimelineContext = releaseApprovalEvidencePack?.SupportingArtifacts.Any(static item => item.Kind == "timeline-context") == true;
        var approvalBlockingReason = ResolveApprovalBlockingReason(
            isCurrentReleaseApprovalPhase,
            hasReleaseArtifact,
            hasReleaseEvidencePack,
            hasImplementationEvidence,
            hasReviewGateResult,
            hasBranchContext,
            hasTimelineContext);
        var approvalAvailableNow = approvalBlockingReason is null;
        var status = approvalAvailableNow
            ? "ready"
            : executionGuard.CanExecute
                ? "blocked"
                : "attention";

        return new ReleaseApprovalPolicyDetails(
            Status: status,
            ExecutionEligibleNow: executionGuard.CanExecute,
            ExecutionBlockingReason: executionGuard.BlockingReason,
            ApprovalAvailableNow: approvalAvailableNow,
            ApprovalBlockingReason: approvalBlockingReason,
            LatestReviewVerdict: executionGuard.LatestReviewVerdict,
            LatestReviewWasForceApproved: executionGuard.LatestReviewWasForceApproved,
            HasReleaseArtifact: hasReleaseArtifact,
            HasReleaseEvidencePack: hasReleaseEvidencePack,
            HasImplementationEvidence: hasImplementationEvidence,
            HasReviewGateResult: hasReviewGateResult,
            HasBranchContext: hasBranchContext,
            HasTimelineContext: hasTimelineContext,
            CurrentWorkspaceHeadSha: executionGuard.CurrentWorkspaceHeadSha,
            ApprovedReviewCommitSha: executionGuard.ApprovedReviewCommitSha,
            ReviewCommitMatchesWorkspaceHead: executionGuard.ReviewCommitMatchesWorkspaceHead,
            EvidenceRules: BuildEvidenceRules(
                hasReleaseEvidencePack,
                hasImplementationEvidence,
                hasReviewGateResult,
                hasBranchContext,
                hasTimelineContext),
            ExecutionConditions: BuildExecutionConditions(executionGuard),
            ApprovalConditions: BuildApprovalConditions(
                isCurrentReleaseApprovalPhase,
                hasReleaseArtifact,
                hasReleaseEvidencePack,
                hasImplementationEvidence,
                hasReviewGateResult,
                hasBranchContext,
                hasTimelineContext,
                approvalBlockingReason));
    }

    internal static ReleaseApprovalExecutionGuardResult EvaluateExecutionGuard(
        string workspaceRoot,
        UserStoryFilePaths paths,
        IReadOnlyCollection<TimelineEventDetails> timelineEvents)
    {
        var reviewPath = paths.GetLatestExistingPhaseArtifactPath(PhaseId.Review);
        if (string.IsNullOrWhiteSpace(reviewPath) || !File.Exists(reviewPath))
        {
            return new ReleaseApprovalExecutionGuardResult(
                LatestReviewVerdict: null,
                LatestReviewWasForceApproved: TryReadLastForceApprovalDecision(timelineEvents) is not null,
                CurrentWorkspaceHeadSha: PhaseExecutionReceiptStore.TryReadGitHeadSha(workspaceRoot),
                ApprovedReviewCommitSha: TryReadLatestReviewPhaseCommitSha(timelineEvents),
                ReviewCommitMatchesWorkspaceHead: null,
                CanExecute: false,
                BlockingReason: "release_approval_review_artifact_missing");
        }

        var latestReviewVerdict = WorkflowRunner.TryReadReviewResult(File.ReadAllText(reviewPath));
        var forceApprovalDecision = TryReadLastForceApprovalDecision(timelineEvents);
        var latestReviewWasForceApproved = forceApprovalDecision is not null;
        if (!string.Equals(latestReviewVerdict, "pass", StringComparison.OrdinalIgnoreCase) && !latestReviewWasForceApproved)
        {
            return new ReleaseApprovalExecutionGuardResult(
                latestReviewVerdict,
                latestReviewWasForceApproved,
                PhaseExecutionReceiptStore.TryReadGitHeadSha(workspaceRoot),
                TryReadLatestReviewPhaseCommitSha(timelineEvents),
                ReviewCommitMatchesWorkspaceHead: null,
                CanExecute: false,
                BlockingReason: "release_approval_requires_passing_review_or_force_approval");
        }

        if (!string.Equals(latestReviewVerdict, "pass", StringComparison.OrdinalIgnoreCase))
        {
            return new ReleaseApprovalExecutionGuardResult(
                latestReviewVerdict,
                latestReviewWasForceApproved,
                PhaseExecutionReceiptStore.TryReadGitHeadSha(workspaceRoot),
                TryReadLatestReviewPhaseCommitSha(timelineEvents),
                ReviewCommitMatchesWorkspaceHead: null,
                CanExecute: true,
                BlockingReason: null);
        }

        var currentHead = PhaseExecutionReceiptStore.TryReadGitHeadSha(workspaceRoot);
        var reviewCommitSha = TryReadLatestReviewPhaseCommitSha(timelineEvents);
        if (string.IsNullOrWhiteSpace(reviewCommitSha))
        {
            return new ReleaseApprovalExecutionGuardResult(
                latestReviewVerdict,
                latestReviewWasForceApproved,
                currentHead,
                reviewCommitSha,
                ReviewCommitMatchesWorkspaceHead: false,
                CanExecute: false,
                BlockingReason: "release_approval_review_commit_missing");
        }

        if (string.IsNullOrWhiteSpace(currentHead))
        {
            return new ReleaseApprovalExecutionGuardResult(
                latestReviewVerdict,
                latestReviewWasForceApproved,
                currentHead,
                reviewCommitSha,
                ReviewCommitMatchesWorkspaceHead: null,
                CanExecute: true,
                BlockingReason: null);
        }

        var headMatches = string.Equals(currentHead, reviewCommitSha, StringComparison.Ordinal);
        return new ReleaseApprovalExecutionGuardResult(
            latestReviewVerdict,
            latestReviewWasForceApproved,
            currentHead,
            reviewCommitSha,
            ReviewCommitMatchesWorkspaceHead: headMatches,
            CanExecute: headMatches,
            BlockingReason: headMatches ? null : "release_approval_review_commit_not_current");
    }

    public static async Task EnsureApprovableAsync(
        string workspaceRoot,
        UserStoryFilePaths paths,
        CancellationToken cancellationToken)
    {
        var releaseApprovalPath = paths.GetLatestExistingPhaseArtifactPath(PhaseId.ReleaseApproval);
        if (string.IsNullOrWhiteSpace(releaseApprovalPath) || !File.Exists(releaseApprovalPath))
        {
            throw new WorkflowDomainException("Release approval cannot be approved because `05-release-approval.md` does not exist.");
        }

        var receipt = await LoadLatestReleaseApprovalReceiptAsync(paths, cancellationToken);
        if (receipt?.ReleaseApprovalEvidencePack is null)
        {
            throw new WorkflowDomainException("Release approval cannot be approved because the latest release-approval receipt does not contain a structured release evidence pack.");
        }

        var pack = receipt.ReleaseApprovalEvidencePack;
        if (!pack.SupportingArtifacts.Any(static item => item.Kind == "branch-context"))
        {
            throw new WorkflowDomainException("Release approval cannot be approved because branch context is missing from the structured release evidence pack.");
        }

        if (!pack.SupportingArtifacts.Any(static item => item.Kind == "timeline-context"))
        {
            throw new WorkflowDomainException("Release approval cannot be approved because timeline context is missing from the structured release evidence pack.");
        }

        if (!pack.SupportingArtifacts.Any(static item => item.Kind is "implementation-evidence-markdown" or "implementation-evidence-json"))
        {
            throw new WorkflowDomainException("Release approval cannot be approved because implementation evidence is missing from the structured release evidence pack.");
        }

        if (string.IsNullOrWhiteSpace(pack.ReviewVerdict))
        {
            throw new WorkflowDomainException("Release approval cannot be approved because the structured release evidence pack does not record the upstream review verdict.");
        }
    }

    private static IReadOnlyCollection<ReleaseApprovalEvidenceRule> BuildEvidenceRules(
        bool hasReleaseEvidencePack,
        bool hasImplementationEvidence,
        bool hasReviewGateResult,
        bool hasBranchContext,
        bool hasTimelineContext)
    {
        return
        [
            new ReleaseApprovalEvidenceRule(
                "release-evidence-pack",
                true,
                hasReleaseEvidencePack
                    ? "The latest release-approval receipt contains a structured release evidence pack."
                    : "The latest release-approval receipt is missing its structured release evidence pack."),
            new ReleaseApprovalEvidenceRule(
                "implementation-evidence",
                true,
                hasImplementationEvidence
                    ? "Implementation evidence links were propagated into the release evidence pack."
                    : "Implementation evidence links are missing from the release evidence pack."),
            new ReleaseApprovalEvidenceRule(
                "review-gate-result",
                true,
                hasReviewGateResult
                    ? "The release evidence pack records the upstream review verdict."
                    : "The release evidence pack does not record the upstream review verdict."),
            new ReleaseApprovalEvidenceRule(
                "branch-context",
                true,
                hasBranchContext
                    ? "Branch metadata is present for release-approval inspection."
                    : "Branch metadata is missing from release-approval supporting artifacts."),
            new ReleaseApprovalEvidenceRule(
                "timeline-context",
                true,
                hasTimelineContext
                    ? "Timeline history is present for release-approval inspection."
                    : "Timeline history is missing from release-approval supporting artifacts.")
        ];
    }

    private static IReadOnlyCollection<ReleaseApprovalPolicyCondition> BuildExecutionConditions(
        ReleaseApprovalExecutionGuardResult executionGuard)
    {
        var reviewOutcomeSatisfied =
            string.Equals(executionGuard.LatestReviewVerdict, "pass", StringComparison.OrdinalIgnoreCase) ||
            executionGuard.LatestReviewWasForceApproved;

        var reviewOutcomeMessage = executionGuard.LatestReviewWasForceApproved
            ? "Release approval may run because a human force-approval decision moved the workflow out of review."
            : string.Equals(executionGuard.LatestReviewVerdict, "pass", StringComparison.OrdinalIgnoreCase)
                ? "Release approval may run because the latest review verdict is `pass`."
                : "Release approval cannot run until review passes or a human explicitly force-approves review.";

        var reviewHeadStatus = executionGuard.BlockingReason is "release_approval_review_commit_missing" or "release_approval_review_commit_not_current"
            ? "blocked"
            : executionGuard.ReviewCommitMatchesWorkspaceHead == true
                ? "satisfied"
                : executionGuard.ReviewCommitMatchesWorkspaceHead is null
                    ? "attention"
                    : "blocked";

        var reviewHeadMessage = executionGuard.LatestReviewWasForceApproved &&
            !string.Equals(executionGuard.LatestReviewVerdict, "pass", StringComparison.OrdinalIgnoreCase)
            ? "Commit-consistency checks are not enforced when release approval was entered via explicit review force-approval."
            : executionGuard.ReviewCommitMatchesWorkspaceHead == true
                ? $"Workspace HEAD `{executionGuard.CurrentWorkspaceHeadSha}` matches approved review commit `{executionGuard.ApprovedReviewCommitSha}`."
                : string.IsNullOrWhiteSpace(executionGuard.ApprovedReviewCommitSha)
                    ? "The latest approved review commit is missing from the workflow timeline."
                    : string.IsNullOrWhiteSpace(executionGuard.CurrentWorkspaceHeadSha)
                        ? "Workspace HEAD could not be probed, so commit consistency is not verifiable right now."
                        : $"Workspace HEAD `{executionGuard.CurrentWorkspaceHeadSha}` differs from approved review commit `{executionGuard.ApprovedReviewCommitSha}`.";

        return
        [
            new ReleaseApprovalPolicyCondition(
                "release_approval_requires_review_outcome",
                "Release approval can only start after review passes or a human explicitly force-approves review.",
                reviewOutcomeSatisfied ? "satisfied" : "blocked",
                reviewOutcomeSatisfied,
                reviewOutcomeSatisfied ? null : "release_approval_requires_passing_review_or_force_approval",
                reviewOutcomeMessage),
            new ReleaseApprovalPolicyCondition(
                "release_approval_review_commit_current",
                "When release approval follows a passing review, the approved review commit must still match workspace HEAD.",
                reviewHeadStatus,
                executionGuard.BlockingReason is not "release_approval_review_commit_missing" and not "release_approval_review_commit_not_current",
                executionGuard.BlockingReason is "release_approval_review_commit_missing" or "release_approval_review_commit_not_current"
                    ? executionGuard.BlockingReason
                    : null,
                reviewHeadMessage)
        ];
    }

    private static IReadOnlyCollection<ReleaseApprovalPolicyCondition> BuildApprovalConditions(
        bool isCurrentReleaseApprovalPhase,
        bool hasReleaseArtifact,
        bool hasReleaseEvidencePack,
        bool hasImplementationEvidence,
        bool hasReviewGateResult,
        bool hasBranchContext,
        bool hasTimelineContext,
        string? approvalBlockingReason)
    {
        return
        [
            new ReleaseApprovalPolicyCondition(
                "release_approval_must_be_current_phase",
                "Approval is only available while the workflow is actively paused in release-approval.",
                isCurrentReleaseApprovalPhase ? "satisfied" : "blocked",
                isCurrentReleaseApprovalPhase,
                isCurrentReleaseApprovalPhase ? null : "release_approval_requires_current_phase",
                isCurrentReleaseApprovalPhase
                    ? "Release approval is the active workflow phase, so approval may be evaluated now."
                    : "Release approval is not the current workflow phase."),
            new ReleaseApprovalPolicyCondition(
                "release_approval_artifact_present",
                "The release-approval artifact must exist before the phase can be approved.",
                hasReleaseArtifact ? "satisfied" : "blocked",
                hasReleaseArtifact,
                hasReleaseArtifact ? null : "release_approval_artifact_missing",
                hasReleaseArtifact
                    ? "The current release-approval artifact is present."
                    : "The current release-approval artifact is missing."),
            new ReleaseApprovalPolicyCondition(
                "release_approval_evidence_pack_present",
                "The latest release-approval receipt must persist a structured release evidence pack.",
                hasReleaseEvidencePack ? "satisfied" : "blocked",
                hasReleaseEvidencePack,
                hasReleaseEvidencePack ? null : "release_approval_evidence_pack_missing",
                hasReleaseEvidencePack
                    ? "The structured release evidence pack is available for operator inspection."
                    : "No structured release evidence pack is available in the latest release-approval receipt."),
            new ReleaseApprovalPolicyCondition(
                "release_approval_branch_and_timeline_context_present",
                "Release approval must retain both branch metadata and timeline history in its supporting artifacts.",
                hasBranchContext && hasTimelineContext ? "satisfied" : "blocked",
                hasBranchContext && hasTimelineContext,
                hasBranchContext
                    ? hasTimelineContext ? null : "release_approval_timeline_context_missing"
                    : "release_approval_branch_context_missing",
                hasBranchContext && hasTimelineContext
                    ? "Branch and timeline context are both available in the structured release evidence pack."
                    : !hasBranchContext
                        ? "Branch context is missing from the structured release evidence pack."
                        : "Timeline context is missing from the structured release evidence pack."),
            new ReleaseApprovalPolicyCondition(
                "release_approval_upstream_evidence_present",
                "Release approval must expose both implementation evidence and the upstream review verdict before approval.",
                hasImplementationEvidence && hasReviewGateResult ? "satisfied" : "blocked",
                hasImplementationEvidence && hasReviewGateResult,
                hasImplementationEvidence
                    ? hasReviewGateResult ? null : "release_approval_review_gate_result_missing"
                    : "release_approval_implementation_evidence_missing",
                hasImplementationEvidence && hasReviewGateResult
                    ? "Implementation evidence and review verdict are both available in the structured release evidence pack."
                    : !hasImplementationEvidence
                        ? "Implementation evidence links are missing from the structured release evidence pack."
                        : "The upstream review verdict is missing from the structured release evidence pack.")
        ];
    }

    private static string? ResolveApprovalBlockingReason(
        bool isCurrentReleaseApprovalPhase,
        bool hasReleaseArtifact,
        bool hasReleaseEvidencePack,
        bool hasImplementationEvidence,
        bool hasReviewGateResult,
        bool hasBranchContext,
        bool hasTimelineContext)
    {
        if (!isCurrentReleaseApprovalPhase)
        {
            return "release_approval_requires_current_phase";
        }

        if (!hasReleaseArtifact)
        {
            return "release_approval_artifact_missing";
        }

        if (!hasReleaseEvidencePack)
        {
            return "release_approval_evidence_pack_missing";
        }

        if (!hasBranchContext)
        {
            return "release_approval_branch_context_missing";
        }

        if (!hasTimelineContext)
        {
            return "release_approval_timeline_context_missing";
        }

        if (!hasImplementationEvidence)
        {
            return "release_approval_implementation_evidence_missing";
        }

        if (!hasReviewGateResult)
        {
            return "release_approval_review_gate_result_missing";
        }

        return null;
    }

    private static ReviewForceApprovalDecision? TryReadLastForceApprovalDecision(
        IReadOnlyCollection<TimelineEventDetails> timelineEvents)
    {
        var latest = timelineEvents
            .Where(static item => item.Code == "review_force_approved")
            .OrderByDescending(static item => item.TimestampUtc, StringComparer.Ordinal)
            .FirstOrDefault();

        if (latest is null)
        {
            return null;
        }

        var reason = latest.Summary ?? string.Empty;
        const string marker = "Reason:";
        var markerIndex = reason.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex >= 0)
        {
            reason = reason[(markerIndex + marker.Length)..].Trim();
        }

        if (reason.EndsWith(".", StringComparison.Ordinal))
        {
            reason = reason[..^1];
        }

        return new ReviewForceApprovalDecision(
            latest.Actor ?? "unknown",
            latest.TimestampUtc,
            "release-approval",
            reason);
    }

    private static string? TryReadLatestReviewPhaseCommitSha(IReadOnlyCollection<TimelineEventDetails> timelineEvents)
    {
        var reviewSlug = WorkflowPresentation.ToPhaseSlug(PhaseId.Review);
        var commitEvent = timelineEvents
            .LastOrDefault(timelineEvent =>
                timelineEvent.Code == "phase_committed" &&
                string.Equals(timelineEvent.Phase, reviewSlug, StringComparison.Ordinal));
        if (commitEvent?.Summary is null)
        {
            return null;
        }

        var match = Regex.Match(commitEvent.Summary, "commit `(?<sha>[0-9a-fA-F]{7,40})`", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["sha"].Value : null;
    }

    private static async Task<PhaseExecutionReceipt?> LoadLatestReleaseApprovalReceiptAsync(
        UserStoryFilePaths paths,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(paths.ExecutionReceiptsDirectoryPath))
        {
            return null;
        }

        var receiptPath = Directory
            .GetFiles(paths.ExecutionReceiptsDirectoryPath, "*-release-approval.json")
            .OrderBy(static path => path, StringComparer.Ordinal)
            .LastOrDefault();
        return await PhaseExecutionReceiptStore.TryLoadAsync(receiptPath, cancellationToken);
    }
}
