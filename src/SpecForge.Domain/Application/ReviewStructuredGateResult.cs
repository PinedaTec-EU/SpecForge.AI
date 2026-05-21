using SpecForge.Domain.Persistence;
using SpecForge.Domain.Workflow;

namespace SpecForge.Domain.Application;

public sealed record ReviewStructuredGateResult(
    string Verdict,
    string PrimaryReason,
    int QualityScore,
    int ConfidenceScore,
    bool HasBlockingFindings,
    int CriticalFindingCount,
    int IssueCount,
    int PassedValidationItemCount,
    int FailedValidationItemCount,
    int DeferredValidationItemCount,
    IReadOnlyCollection<PhaseArtifactIssue> FindingsSummary,
    IReadOnlyCollection<ReviewCorrectionTarget> CorrectionTargets,
    IReadOnlyCollection<ReviewEvidenceLink> LinkedEvidence);

public sealed record ReviewCorrectionTarget(
    string Item,
    string Status,
    string Severity,
    bool IsBlocking,
    string Evidence,
    string SuggestedAction);

public sealed record ReviewEvidenceLink(
    string Kind,
    string Path,
    string? Summary = null);

internal static class ReviewStructuredGateResultBuilder
{
    public static ReviewStructuredGateResult Build(
        UserStoryFilePaths paths,
        string reviewMarkdown,
        string reviewEvidencePolicy)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewMarkdown);

        var verdict = WorkflowRunner.TryReadReviewResult(reviewMarkdown) ?? "unknown";
        var primaryReason = WorkflowArtifactMarkdownReader.ParseReviewPrimaryReason(reviewMarkdown);
        var checklist = WorkflowArtifactMarkdownReader.ParseReviewValidationChecklist(reviewMarkdown);
        var findings = WorkflowArtifactMarkdownReader.ReadSeverityTaggedBulletSection(
            reviewMarkdown,
            "## Findings",
            verdict == "pass" ? "issue" : "critical");
        var recommendations = WorkflowArtifactMarkdownReader.ReadMarkdownBulletSection(reviewMarkdown, "## Recommendation");
        var policy = ReviewEvidencePolicy.Parse(reviewEvidencePolicy);
        var qualityScore = WorkflowArtifactMarkdownReader.ParseAssessmentPercentage(reviewMarkdown, "Quality score");
        var confidenceScore = WorkflowArtifactMarkdownReader.ParseAssessmentPercentage(reviewMarkdown, "Confidence score");

        var passedCount = checklist.Count(item => item.Status == "pass");
        var failedCount = checklist.Count(item => item.Status == "fail");
        var deferredCount = checklist.Count(item => item.Status == "deferred");

        var correctionTargets = checklist
            .Where(item => item.Status is "fail" or "deferred")
            .Select(item =>
            {
                var evidenceKind = ReviewEvidencePolicy.Classify(item.Item);
                var isBlocking = item.Status == "fail" &&
                    ReviewEvidencePolicy.IsBlocking(policy, evidenceKind);
                var suggestedAction = recommendations.Count > 0
                    ? recommendations[0]
                    : item.Status == "deferred"
                        ? "Track the deferred validation item and only proceed under the active review evidence policy."
                        : "Fix the failed validation item and rerun the review phase.";

                return new ReviewCorrectionTarget(
                    item.Item,
                    item.Status,
                    isBlocking ? "critical" : "issue",
                    isBlocking,
                    string.IsNullOrWhiteSpace(item.Evidence)
                        ? "No concrete review evidence was recorded for this item."
                        : item.Evidence,
                    suggestedAction);
            })
            .ToArray();

        if (findings.Count == 0)
        {
            findings =
            [
                new PhaseArtifactIssue(
                    correctionTargets.Any(static item => item.IsBlocking) ? "critical" : "issue",
                    "No blocking review findings beyond the validation checklist.")
            ];
        }

        var effectiveQualityScore = qualityScore >= 0
            ? qualityScore
            : DeriveQualityScore(passedCount, failedCount, deferredCount);
        var effectiveConfidenceScore = confidenceScore >= 0
            ? confidenceScore
            : DeriveConfidenceScore(checklist);
        var criticalFindingCount = findings.Count(item => item.Severity == "critical") +
            correctionTargets.Count(item => item.IsBlocking);
        var issueCount = findings.Count(item => item.Severity == "issue") +
            correctionTargets.Count(item => !item.IsBlocking);

        return new ReviewStructuredGateResult(
            verdict,
            string.IsNullOrWhiteSpace(primaryReason)
                ? verdict == "pass"
                    ? "Review passed because the validation checklist remained fully green."
                    : "Review failed because one or more validation checklist items did not pass."
                : primaryReason,
            effectiveQualityScore,
            effectiveConfidenceScore,
            correctionTargets.Any(static item => item.IsBlocking),
            criticalFindingCount,
            issueCount,
            passedCount,
            failedCount,
            deferredCount,
            findings,
            correctionTargets,
            BuildEvidenceLinks(paths));
    }

    private static int DeriveQualityScore(int passedCount, int failedCount, int deferredCount)
    {
        var total = passedCount + failedCount + deferredCount;
        if (total == 0)
        {
            return 0;
        }

        var weightedPasses = passedCount + (deferredCount * 0.5);
        return Math.Clamp((int)Math.Round((weightedPasses / total) * 100, MidpointRounding.AwayFromZero), 0, 100);
    }

    private static int DeriveConfidenceScore(IReadOnlyCollection<ReviewValidationChecklistItem> checklist)
    {
        if (checklist.Count == 0)
        {
            return 20;
        }

        var itemsWithEvidence = checklist.Count(item => !string.IsNullOrWhiteSpace(item.Evidence));
        return Math.Clamp(
            (int)Math.Round((itemsWithEvidence / (double)checklist.Count) * 100, MidpointRounding.AwayFromZero),
            0,
            100);
    }

    private static IReadOnlyCollection<ReviewEvidenceLink> BuildEvidenceLinks(UserStoryFilePaths paths)
    {
        var links = new List<ReviewEvidenceLink>();

        AddIfExists(
            links,
            "review-artifact",
            paths.GetPhaseArtifactPath(PhaseId.Review),
            "Canonical review artifact used for downstream gating.");
        AddIfExists(
            links,
            "review-raw-artifact",
            Path.Combine(paths.PhasesDirectoryPath, "04-review.raw.md"),
            "Raw provider output captured before the review guard enforced the checklist contract.");
        AddIfExists(
            links,
            "implementation-evidence-markdown",
            paths.GetPhaseEvidenceMarkdownPath(PhaseId.Implementation),
            "Human-readable implementation evidence consumed by review.");
        AddIfExists(
            links,
            "implementation-evidence-json",
            paths.GetPhaseEvidenceJsonPath(PhaseId.Implementation),
            "Machine-readable implementation evidence consumed by review.");
        AddIfExists(
            links,
            "graph-scope-request",
            paths.GraphScopeRequestPath,
            "User-story graph scope request available to implementation and review.");
        AddIfExists(
            links,
            "impact-graph",
            paths.ImpactGraphPath,
            "Impact graph slice linked to the reviewed user-story scope.");
        AddIfExists(
            links,
            "impact-graph-metadata",
            paths.ImpactGraphMetadataPath,
            "Impact graph freshness and lineage metadata.");
        AddIfExists(
            links,
            "impact-summary",
            paths.ImpactGraphSummaryPath,
            "Human-readable impact graph summary when graph-backed scope was materialized.");

        return links;
    }

    private static void AddIfExists(
        ICollection<ReviewEvidenceLink> links,
        string kind,
        string path,
        string summary)
    {
        if (!File.Exists(path))
        {
            return;
        }

        links.Add(new ReviewEvidenceLink(
            kind,
            PhaseExecutionReceiptStore.NormalizePath(path),
            summary));
    }
}
