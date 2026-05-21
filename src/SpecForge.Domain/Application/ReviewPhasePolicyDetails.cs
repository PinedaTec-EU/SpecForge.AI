namespace SpecForge.Domain.Application;

public sealed record ReviewPhasePolicyDetails(
    string ActiveEvidencePolicy,
    string? LatestGateVerdict,
    bool? LatestHasBlockingFindings,
    bool ForceApprovalAvailableNow,
    bool ForceApprovalRequiresReason,
    string? ForceApprovalBlockingReason,
    IReadOnlyCollection<ReviewEvidencePolicyRule> EvidenceRules,
    IReadOnlyCollection<ReviewPhaseOverrideCondition> OverrideConditions,
    ReviewForceApprovalDecision? LastForceApprovalDecision = null);

public sealed record ReviewEvidencePolicyRule(
    string EvidenceKind,
    bool IsBlocking,
    string CurrentStatusMessage);

public sealed record ReviewPhaseOverrideCondition(
    string Id,
    string Description,
    string Status,
    bool IsCurrentlySatisfied,
    string? BlockingReason = null,
    string? CurrentStatusMessage = null);

public sealed record ReviewForceApprovalDecision(
    string Actor,
    string TimestampUtc,
    string TargetPhase,
    string Reason);

public static class ReviewPhasePolicyDetailsBuilder
{
    public static ReviewPhasePolicyDetails Build(
        string reviewEvidencePolicy,
        bool isCurrentReviewPhase,
        ReviewStructuredGateResult? structuredGateResult,
        IReadOnlyCollection<TimelineEventDetails> timelineEvents)
    {
        var normalizedPolicy = ReviewEvidencePolicy.Normalize(reviewEvidencePolicy);
        var forceApprovalAvailableNow = isCurrentReviewPhase;
        var forceApprovalBlockingReason = forceApprovalAvailableNow
            ? null
            : "review_force_approval_requires_current_review_phase";

        return new ReviewPhasePolicyDetails(
            ActiveEvidencePolicy: normalizedPolicy,
            LatestGateVerdict: structuredGateResult?.Verdict,
            LatestHasBlockingFindings: structuredGateResult?.HasBlockingFindings,
            ForceApprovalAvailableNow: forceApprovalAvailableNow,
            ForceApprovalRequiresReason: true,
            ForceApprovalBlockingReason: forceApprovalBlockingReason,
            EvidenceRules: BuildEvidenceRules(normalizedPolicy),
            OverrideConditions: BuildOverrideConditions(forceApprovalAvailableNow, forceApprovalBlockingReason, structuredGateResult),
            LastForceApprovalDecision: TryReadLastForceApprovalDecision(timelineEvents));
    }

    private static IReadOnlyCollection<ReviewEvidencePolicyRule> BuildEvidenceRules(string reviewEvidencePolicy)
    {
        var policy = ReviewEvidencePolicy.Parse(reviewEvidencePolicy);

        return
        [
            BuildEvidenceRule(policy, "automated", "Automated validation items are treated as blocking when they fail under the active policy."),
            BuildEvidenceRule(policy, "static", "Static analysis and non-runtime validation items are treated as blocking when they fail under the active policy."),
            BuildEvidenceRule(policy, "operational", "Operational evidence can stay non-blocking when the active policy permits deferred operational gaps."),
            BuildEvidenceRule(policy, "deferred", "Deferred evidence remains non-blocking unless the active review policy escalates every gap to blocking.")
        ];
    }

    private static ReviewEvidencePolicyRule BuildEvidenceRule(
        ReviewEvidencePolicyMode policy,
        string evidenceKind,
        string message)
    {
        var kind = evidenceKind switch
        {
            "static" => ReviewValidationEvidenceKind.Static,
            "operational" => ReviewValidationEvidenceKind.Operational,
            "deferred" => ReviewValidationEvidenceKind.Deferred,
            _ => ReviewValidationEvidenceKind.Automated
        };

        return new ReviewEvidencePolicyRule(
            evidenceKind,
            ReviewEvidencePolicy.IsBlocking(policy, kind),
            message);
    }

    private static IReadOnlyCollection<ReviewPhaseOverrideCondition> BuildOverrideConditions(
        bool forceApprovalAvailableNow,
        string? forceApprovalBlockingReason,
        ReviewStructuredGateResult? structuredGateResult)
    {
        return
        [
            new ReviewPhaseOverrideCondition(
                "review_must_be_current_phase",
                "Force approval is only available while the workflow is actively paused in review.",
                forceApprovalAvailableNow ? "satisfied" : "blocked",
                forceApprovalAvailableNow,
                forceApprovalAvailableNow ? null : forceApprovalBlockingReason,
                forceApprovalAvailableNow
                    ? "Review is the active phase, so an operator may choose to override it."
                    : "Review is no longer the active phase, so force approval is unavailable."),
            new ReviewPhaseOverrideCondition(
                "force_approval_reason_required",
                "Operators must provide an explicit rationale before overriding review.",
                "required",
                true,
                null,
                "Approve Anyway always requires a human reason that is recorded in the workflow audit trail."),
            new ReviewPhaseOverrideCondition(
                "override_acknowledges_gate_state",
                "Operators should only override review after inspecting the latest review gate result and linked evidence.",
                structuredGateResult?.HasBlockingFindings == true ? "attention" : "satisfied",
                structuredGateResult is not null,
                structuredGateResult is null ? "review_gate_result_missing" : null,
                structuredGateResult is null
                    ? "No structured review gate result is currently available for operator inspection."
                    : $"Latest review verdict is `{structuredGateResult.Verdict}` with {(structuredGateResult.HasBlockingFindings ? "blocking" : "non-blocking")} findings.")
        ];
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
}
