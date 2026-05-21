using SpecForge.Domain.Persistence;
using SpecForge.Domain.Workflow;

namespace SpecForge.Domain.Application;

public sealed record ReleaseApprovalEvidencePack(
    string GeneratedAtUtc,
    string ReleaseApprovalArtifactPath,
    string? ReviewVerdict,
    string? ReviewPrimaryReason,
    IReadOnlyCollection<ReleaseApprovalChangedFile> ChangedFiles,
    IReadOnlyCollection<ReleaseApprovalValidationResult> ValidationResults,
    IReadOnlyCollection<string> ReleaseRiskSummary,
    IReadOnlyCollection<ReleaseApprovalArtifactLink> SupportingArtifacts);

public sealed record ReleaseApprovalChangedFile(
    string Path,
    string ChangeKind,
    string CurrentStatusCode,
    string? BaselineStatusCode = null);

public sealed record ReleaseApprovalValidationResult(
    string Status,
    string Item,
    string Evidence);

public sealed record ReleaseApprovalArtifactLink(
    string Kind,
    string Path,
    string? Summary = null);

internal static class ReleaseApprovalEvidencePackBuilder
{
    public static ReleaseApprovalEvidencePack Build(
        UserStoryFilePaths paths,
        string releaseApprovalArtifactPath)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentException.ThrowIfNullOrWhiteSpace(releaseApprovalArtifactPath);

        var releaseApprovalMarkdown = File.ReadAllText(releaseApprovalArtifactPath);
        var reviewArtifactPath = paths.GetLatestExistingPhaseArtifactPath(PhaseId.Review);
        var reviewMarkdown = !string.IsNullOrWhiteSpace(reviewArtifactPath) && File.Exists(reviewArtifactPath)
            ? File.ReadAllText(reviewArtifactPath)
            : string.Empty;
        var reviewVerdict = string.IsNullOrWhiteSpace(reviewMarkdown)
            ? null
            : WorkflowRunner.TryReadReviewResult(reviewMarkdown);
        var reviewPrimaryReason = string.IsNullOrWhiteSpace(reviewMarkdown)
            ? null
            : WorkflowArtifactMarkdownReader.ParseReviewPrimaryReason(reviewMarkdown);
        var validationResults = string.IsNullOrWhiteSpace(reviewMarkdown)
            ? Array.Empty<ReleaseApprovalValidationResult>()
            : WorkflowArtifactMarkdownReader.ParseReviewValidationChecklist(reviewMarkdown)
                .Select(static item => new ReleaseApprovalValidationResult(item.Status, item.Item, item.Evidence))
                .ToArray();
        var releaseRiskSummary = WorkflowArtifactMarkdownReader.ReadMarkdownBulletSection(releaseApprovalMarkdown, "## Residual Risks");
        var implementationStructuredEvidence = TryLoadLatestImplementationStructuredEvidence(paths);

        return new ReleaseApprovalEvidencePack(
            DateTimeOffset.UtcNow.ToString("O"),
            PhaseExecutionReceiptStore.NormalizePath(releaseApprovalArtifactPath),
            reviewVerdict,
            string.IsNullOrWhiteSpace(reviewPrimaryReason) ? null : reviewPrimaryReason,
            implementationStructuredEvidence?.TouchedFiles
                .Select(static item => new ReleaseApprovalChangedFile(
                    item.Path,
                    item.ChangeKind,
                    item.CurrentStatusCode,
                    item.BaselineStatusCode))
                .ToArray()
                ?? [],
            validationResults,
            releaseRiskSummary.Count > 0 ? releaseRiskSummary : ["No residual risks were recorded in the release approval artifact."],
            BuildSupportingArtifacts(paths, implementationStructuredEvidence));
    }

    private static ImplementationStructuredEvidence? TryLoadLatestImplementationStructuredEvidence(UserStoryFilePaths paths)
    {
        var receiptPath = Directory
            .GetFiles(paths.ExecutionReceiptsDirectoryPath, "*-implementation.json")
            .OrderBy(static path => path, StringComparer.Ordinal)
            .LastOrDefault();

        if (string.IsNullOrWhiteSpace(receiptPath))
        {
            return null;
        }

        return PhaseExecutionReceiptStore.TryLoadAsync(receiptPath).GetAwaiter().GetResult()?.ImplementationStructuredEvidence;
    }

    private static IReadOnlyCollection<ReleaseApprovalArtifactLink> BuildSupportingArtifacts(
        UserStoryFilePaths paths,
        ImplementationStructuredEvidence? implementationStructuredEvidence)
    {
        var links = new List<ReleaseApprovalArtifactLink>();

        AddIfExists(links, "spec-artifact", paths.GetLatestExistingPhaseArtifactPath(PhaseId.Spec), "Approved spec artifact.");
        AddIfExists(links, "technical-design-artifact", paths.GetLatestExistingPhaseArtifactPath(PhaseId.TechnicalDesign), "Active technical design artifact.");
        AddIfExists(links, "implementation-artifact", paths.GetLatestExistingPhaseArtifactPath(PhaseId.Implementation), "Latest implementation artifact.");
        AddIfExists(links, "review-artifact", paths.GetLatestExistingPhaseArtifactPath(PhaseId.Review), "Latest review artifact.");
        AddIfExists(links, "implementation-evidence-markdown", paths.GetPhaseEvidenceMarkdownPath(PhaseId.Implementation), "Human-readable implementation evidence.");
        AddIfExists(links, "implementation-evidence-json", paths.GetPhaseEvidenceJsonPath(PhaseId.Implementation), "Machine-readable implementation evidence.");
        AddIfExists(links, "branch-context", paths.BranchFilePath, "Branch metadata injected into release approval.");
        AddIfExists(links, "timeline-context", paths.TimelineFilePath, "Workflow timeline injected into release approval.");

        if (implementationStructuredEvidence?.GraphEvidence is not null)
        {
            if (!string.IsNullOrWhiteSpace(implementationStructuredEvidence.GraphEvidence.ImpactGraphPath))
            {
                links.Add(new ReleaseApprovalArtifactLink(
                    "impact-graph",
                    implementationStructuredEvidence.GraphEvidence.ImpactGraphPath!,
                    "Impact graph linked from implementation evidence."));
            }

            if (!string.IsNullOrWhiteSpace(implementationStructuredEvidence.GraphEvidence.ImpactSummaryPath))
            {
                links.Add(new ReleaseApprovalArtifactLink(
                    "impact-summary",
                    implementationStructuredEvidence.GraphEvidence.ImpactSummaryPath!,
                    "Impact summary linked from implementation evidence."));
            }
        }

        return links;
    }

    private static void AddIfExists(
        ICollection<ReleaseApprovalArtifactLink> links,
        string kind,
        string? path,
        string summary)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        links.Add(new ReleaseApprovalArtifactLink(
            kind,
            PhaseExecutionReceiptStore.NormalizePath(path),
            summary));
    }
}
