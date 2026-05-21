using System.Text.Json;
using SpecForge.Domain.Persistence;
using SpecForge.Domain.Workflow;

namespace SpecForge.Domain.Application;

public sealed record PrPreparationPolicyDetails(
    string Status,
    bool PublicationReadyNow,
    string? PublicationBlockingReason,
    string PublicationMode,
    bool HasPrPreparationArtifact,
    bool HasBranchMetadata,
    bool HasReleaseApprovalArtifact,
    bool HasReleaseApprovalEvidencePack,
    bool HasValidationSummary,
    bool HasReviewerChecklist,
    bool HasPrBody,
    bool ExistingPullRequestReusable,
    string? ExistingPullRequestStatus,
    string? ExistingPullRequestUrl,
    string? BaseBranch,
    string? WorkBranch,
    IReadOnlyCollection<PrPreparationRequirementRule> RequirementRules,
    IReadOnlyCollection<PrPreparationPublicationCondition> PublicationConditions);

public sealed record PrPreparationRequirementRule(
    string Id,
    string Description,
    bool IsRequired,
    string CurrentStatusMessage);

public sealed record PrPreparationPublicationCondition(
    string Id,
    string Description,
    string Status,
    bool IsCurrentlySatisfied,
    string? BlockingReason = null,
    string? CurrentStatusMessage = null);

public static class PrPreparationPolicyDetailsBuilder
{
    public static PrPreparationPolicyDetails Build(
        WorkflowRun workflowRun,
        UserStoryFilePaths paths,
        PrPreparationStructuredEvidence? structuredEvidence,
        PullRequestDetails? pullRequest)
    {
        var artifactPath = paths.GetLatestExistingPhaseArtifactPath(PhaseId.PrPreparation);
        var hasArtifact = !string.IsNullOrWhiteSpace(artifactPath) && File.Exists(artifactPath);
        var validation = ValidatePrPreparationArtifact(artifactPath);
        var hasBranchMetadata = workflowRun.Branch is not null && File.Exists(paths.BranchFilePath);
        var hasReleaseArtifact = structuredEvidence?.ReleaseApprovalArtifactAvailable
            ?? !string.IsNullOrWhiteSpace(paths.GetLatestExistingPhaseArtifactPath(PhaseId.ReleaseApproval));
        var hasReleaseEvidencePack = structuredEvidence?.ReleaseApprovalEvidencePackAvailable
            ?? TryReadLatestReleaseApprovalEvidencePack(paths) is not null;
        var existingPullRequestReusable =
            workflowRun.Branch?.PullRequest is { Number: > 0, Url: not null } existing &&
            IsReusablePullRequest(existing);

        var publicationBlockingReason = ResolvePublicationBlockingReason(
            hasArtifact,
            validation,
            hasBranchMetadata,
            hasReleaseArtifact,
            hasReleaseEvidencePack);
        var publicationReadyNow = publicationBlockingReason is null;
        var status = publicationReadyNow
            ? "ready"
            : hasArtifact
                ? "blocked"
                : "attention";
        var publicationMode = existingPullRequestReusable
            ? "reuse-existing-pull-request"
            : publicationReadyNow
                ? "publish-draft-pull-request"
                : "blocked";

        return new PrPreparationPolicyDetails(
            Status: status,
            PublicationReadyNow: publicationReadyNow,
            PublicationBlockingReason: publicationBlockingReason,
            PublicationMode: publicationMode,
            HasPrPreparationArtifact: hasArtifact,
            HasBranchMetadata: hasBranchMetadata,
            HasReleaseApprovalArtifact: hasReleaseArtifact,
            HasReleaseApprovalEvidencePack: hasReleaseEvidencePack,
            HasValidationSummary: validation.HasValidationSummary,
            HasReviewerChecklist: validation.HasReviewerChecklist,
            HasPrBody: validation.HasPrBody,
            ExistingPullRequestReusable: existingPullRequestReusable,
            ExistingPullRequestStatus: workflowRun.Branch?.PullRequest?.Status ?? pullRequest?.Status,
            ExistingPullRequestUrl: workflowRun.Branch?.PullRequest?.Url ?? pullRequest?.Url,
            BaseBranch: workflowRun.Branch?.BaseBranch,
            WorkBranch: workflowRun.Branch?.WorkBranchName,
            RequirementRules: BuildRequirementRules(hasArtifact, validation, hasBranchMetadata, hasReleaseArtifact, hasReleaseEvidencePack),
            PublicationConditions: BuildPublicationConditions(
                publicationReadyNow,
                publicationBlockingReason,
                validation,
                hasBranchMetadata,
                hasReleaseArtifact,
                hasReleaseEvidencePack,
                existingPullRequestReusable,
                workflowRun.Branch?.PullRequest?.Url ?? pullRequest?.Url));
    }

    internal static PrPreparationArtifactValidation ValidatePrPreparationArtifact(string? artifactPath)
    {
        if (string.IsNullOrWhiteSpace(artifactPath) || !File.Exists(artifactPath))
        {
            return new PrPreparationArtifactValidation(
                Exists: false,
                IsPublishable: false,
                MissingFields: ["artifact"],
                HasValidationSummary: false,
                HasReviewerChecklist: false,
                HasPrBody: false);
        }

        var document = PrPreparationArtifactJson.ParseMarkdown(File.ReadAllText(artifactPath));
        var missingFields = WorkflowRunner.ValidatePrPreparationArtifact(document);
        return new PrPreparationArtifactValidation(
            Exists: true,
            IsPublishable: missingFields.Count == 0,
            MissingFields: missingFields,
            HasValidationSummary: document.ValidationSummary.Count > 0,
            HasReviewerChecklist: document.ReviewerChecklist.Count > 0,
            HasPrBody: document.PrBody.Count > 0 && document.PrBody.Any(static line => !string.IsNullOrWhiteSpace(line) && line.Trim() != "..."));
    }

    private static IReadOnlyCollection<PrPreparationRequirementRule> BuildRequirementRules(
        bool hasArtifact,
        PrPreparationArtifactValidation validation,
        bool hasBranchMetadata,
        bool hasReleaseArtifact,
        bool hasReleaseEvidencePack)
    {
        return
        [
            new PrPreparationRequirementRule(
                "pr_preparation_artifact_present",
                "A materialized PR preparation artifact must exist before publication can be evaluated.",
                true,
                hasArtifact ? "The PR preparation artifact exists." : "The PR preparation artifact is missing."),
            new PrPreparationRequirementRule(
                "pr_preparation_artifact_publishable",
                "The PR preparation artifact must contain title, summary, change narrative, validation summary, reviewer checklist, and PR body.",
                true,
                validation.IsPublishable
                    ? "The PR preparation artifact satisfies the publication contract."
                    : $"The PR preparation artifact is incomplete: {string.Join(", ", validation.MissingFields)}."),
            new PrPreparationRequirementRule(
                "pr_preparation_branch_metadata_present",
                "Branch metadata must exist so publication knows the base and work branches.",
                true,
                hasBranchMetadata ? "Branch metadata is available." : "Branch metadata is missing."),
            new PrPreparationRequirementRule(
                "pr_preparation_release_context_present",
                "PR preparation must carry forward the approved release artifact and its structured evidence pack.",
                true,
                hasReleaseArtifact && hasReleaseEvidencePack
                    ? "Approved release artifact and structured release evidence pack are both available."
                    : !hasReleaseArtifact
                        ? "Approved release artifact is missing."
                        : "Structured release evidence pack is missing.")
        ];
    }

    private static IReadOnlyCollection<PrPreparationPublicationCondition> BuildPublicationConditions(
        bool publicationReadyNow,
        string? publicationBlockingReason,
        PrPreparationArtifactValidation validation,
        bool hasBranchMetadata,
        bool hasReleaseArtifact,
        bool hasReleaseEvidencePack,
        bool existingPullRequestReusable,
        string? existingPullRequestUrl)
    {
        return
        [
            new PrPreparationPublicationCondition(
                "pr_preparation_artifact_publishable",
                "The PR preparation artifact must satisfy the publication contract before publish/reuse.",
                validation.IsPublishable ? "satisfied" : "blocked",
                validation.IsPublishable,
                validation.IsPublishable ? null : "pr_preparation_artifact_not_publishable",
                validation.IsPublishable
                    ? "The current PR preparation artifact is publishable."
                    : $"The current PR preparation artifact is incomplete: {string.Join(", ", validation.MissingFields)}."),
            new PrPreparationPublicationCondition(
                "pr_preparation_branch_metadata_required",
                "Branch metadata must be present so publication can target the correct base and work branches.",
                hasBranchMetadata ? "satisfied" : "blocked",
                hasBranchMetadata,
                hasBranchMetadata ? null : "pr_preparation_branch_metadata_missing",
                hasBranchMetadata
                    ? "Branch metadata is available for publication."
                    : "Branch metadata is missing, so publication cannot proceed."),
            new PrPreparationPublicationCondition(
                "pr_preparation_release_context_required",
                "Publication must be grounded in the approved release artifact and structured release evidence pack.",
                hasReleaseArtifact && hasReleaseEvidencePack ? "satisfied" : "blocked",
                hasReleaseArtifact && hasReleaseEvidencePack,
                hasReleaseArtifact
                    ? hasReleaseEvidencePack ? null : "pr_preparation_release_evidence_pack_missing"
                    : "pr_preparation_release_artifact_missing",
                hasReleaseArtifact && hasReleaseEvidencePack
                    ? "Approved release context is available for PR generation."
                    : !hasReleaseArtifact
                        ? "Approved release artifact is missing."
                        : "Structured release evidence pack is missing."),
            new PrPreparationPublicationCondition(
                "pr_preparation_publication_mode_declared",
                "The system must declare whether publication will create a draft PR or reuse an existing one.",
                publicationReadyNow || existingPullRequestReusable ? "satisfied" : "attention",
                publicationReadyNow || existingPullRequestReusable,
                publicationReadyNow || existingPullRequestReusable ? null : publicationBlockingReason,
                existingPullRequestReusable
                    ? $"The workflow can reuse the existing pull request: {existingPullRequestUrl}."
                    : publicationReadyNow
                        ? "The workflow is ready to publish a draft pull request."
                        : "Publication mode cannot be resolved until the blocking requirements are satisfied.")
        ];
    }

    private static string? ResolvePublicationBlockingReason(
        bool hasArtifact,
        PrPreparationArtifactValidation validation,
        bool hasBranchMetadata,
        bool hasReleaseArtifact,
        bool hasReleaseEvidencePack)
    {
        if (!hasArtifact)
        {
            return "pr_preparation_artifact_missing";
        }

        if (!validation.IsPublishable)
        {
            return "pr_preparation_artifact_not_publishable";
        }

        if (!hasBranchMetadata)
        {
            return "pr_preparation_branch_metadata_missing";
        }

        if (!hasReleaseArtifact)
        {
            return "pr_preparation_release_artifact_missing";
        }

        if (!hasReleaseEvidencePack)
        {
            return "pr_preparation_release_evidence_pack_missing";
        }

        return null;
    }

    private static bool IsReusablePullRequest(PullRequestRecord pullRequest) =>
        !string.IsNullOrWhiteSpace(pullRequest.Url) &&
        pullRequest.Number is > 0 &&
        string.Equals(pullRequest.Status, "draft", StringComparison.OrdinalIgnoreCase);

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

    internal sealed record PrPreparationArtifactValidation(
        bool Exists,
        bool IsPublishable,
        IReadOnlyCollection<string> MissingFields,
        bool HasValidationSummary,
        bool HasReviewerChecklist,
        bool HasPrBody);
}
