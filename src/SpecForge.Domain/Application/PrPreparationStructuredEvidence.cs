using SpecForge.Domain.Persistence;
using SpecForge.Domain.Workflow;
using System.Text.Json;

namespace SpecForge.Domain.Application;

public sealed record PrPreparationStructuredEvidence(
    string GeneratedAtUtc,
    string PrPreparationArtifactPath,
    string State,
    string PrTitle,
    string PrSummary,
    string BaseBranch,
    string WorkBranch,
    bool ReleaseApprovalArtifactAvailable,
    bool ReleaseApprovalEvidencePackAvailable,
    IReadOnlyCollection<string> BasedOn,
    IReadOnlyCollection<PrPreparationParticipant> Participants,
    IReadOnlyCollection<string> ValidationSummary,
    IReadOnlyCollection<string> ReviewerChecklist,
    IReadOnlyCollection<PrPreparationEvidenceLink> LinkedEvidence);

public sealed record PrPreparationEvidenceLink(
    string Kind,
    string Path,
    string? Summary = null);

internal static class PrPreparationStructuredEvidenceBuilder
{
    public static PrPreparationStructuredEvidence Build(
        WorkflowRun workflowRun,
        UserStoryFilePaths paths,
        string prPreparationArtifactPath)
    {
        var document = PrPreparationArtifactJson.ParseMarkdown(File.ReadAllText(prPreparationArtifactPath));
        var linkedEvidence = BuildLinkedEvidence(paths);

        return new PrPreparationStructuredEvidence(
            GeneratedAtUtc: DateTimeOffset.UtcNow.ToString("O"),
            PrPreparationArtifactPath: PhaseExecutionReceiptStore.NormalizePath(prPreparationArtifactPath),
            State: document.State,
            PrTitle: document.PrTitle,
            PrSummary: document.PrSummary,
            BaseBranch: workflowRun.Branch?.BaseBranch ?? "unknown",
            WorkBranch: workflowRun.Branch?.WorkBranchName ?? "unknown",
            ReleaseApprovalArtifactAvailable: !string.IsNullOrWhiteSpace(paths.GetLatestExistingPhaseArtifactPath(PhaseId.ReleaseApproval)),
            ReleaseApprovalEvidencePackAvailable: TryReadLatestReleaseApprovalEvidencePack(paths) is not null,
            BasedOn: document.BasedOn,
            Participants: document.Participants,
            ValidationSummary: document.ValidationSummary,
            ReviewerChecklist: document.ReviewerChecklist,
            LinkedEvidence: linkedEvidence);
    }

    private static IReadOnlyCollection<PrPreparationEvidenceLink> BuildLinkedEvidence(UserStoryFilePaths paths)
    {
        var links = new List<PrPreparationEvidenceLink>();

        AddIfExists(links, "release-approval-artifact", paths.GetLatestExistingPhaseArtifactPath(PhaseId.ReleaseApproval), "Approved release artifact feeding PR preparation.");
        AddIfExists(links, "review-artifact", paths.GetLatestExistingPhaseArtifactPath(PhaseId.Review), "Review artifact referenced by PR preparation.");
        AddIfExists(links, "implementation-artifact", paths.GetLatestExistingPhaseArtifactPath(PhaseId.Implementation), "Implementation artifact referenced by PR preparation.");
        AddIfExists(links, "implementation-evidence-markdown", paths.GetLatestExistingPhaseEvidenceMarkdownPath(PhaseId.Implementation), "Human-readable implementation evidence.");
        AddIfExists(links, "implementation-evidence-json", paths.GetLatestExistingPhaseEvidenceJsonPath(PhaseId.Implementation), "Machine-readable implementation evidence.");
        AddIfExists(links, "branch-context", paths.BranchFilePath, "Workflow branch metadata used for PR publication.");
        AddIfExists(links, "timeline-context", paths.TimelineFilePath, "Workflow timeline context used to identify participants and publication posture.");

        var releaseEvidencePack = TryReadLatestReleaseApprovalEvidencePack(paths);
        if (releaseEvidencePack is not null)
        {
            foreach (var item in releaseEvidencePack.SupportingArtifacts)
            {
                if (string.IsNullOrWhiteSpace(item.Path))
                {
                    continue;
                }

                links.Add(new PrPreparationEvidenceLink(item.Kind, item.Path, item.Summary));
            }
        }

        return links
            .GroupBy(static item => $"{item.Kind}|{item.Path}", StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();
    }

    private static ReleaseApprovalEvidencePack? TryReadLatestReleaseApprovalEvidencePack(UserStoryFilePaths paths)
    {
        if (!Directory.Exists(paths.ExecutionReceiptsDirectoryPath))
        {
            return null;
        }

        var receiptPath = Directory
            .GetFiles(paths.ExecutionReceiptsDirectoryPath, "*-release-approval.json")
            .OrderBy(static path => path, StringComparer.Ordinal)
            .LastOrDefault();
        if (string.IsNullOrWhiteSpace(receiptPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(receiptPath);
            return JsonSerializer.Deserialize<PhaseExecutionReceipt>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?.ReleaseApprovalEvidencePack;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void AddIfExists(
        ICollection<PrPreparationEvidenceLink> links,
        string kind,
        string? path,
        string summary)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        links.Add(new PrPreparationEvidenceLink(
            kind,
            PhaseExecutionReceiptStore.NormalizePath(path),
            summary));
    }
}
