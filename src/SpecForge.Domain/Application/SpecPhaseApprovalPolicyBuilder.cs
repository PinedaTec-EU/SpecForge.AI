using SpecForge.Domain.Persistence;
using SpecForge.Domain.Workflow;

namespace SpecForge.Domain.Application;

internal static class SpecPhaseApprovalPolicyBuilder
{
    public static async Task<SpecPhaseApprovalPolicyDetails> BuildAsync(
        WorkflowRun workflowRun,
        UserStoryFilePaths paths,
        CancellationToken cancellationToken)
    {
        var isCurrentSpecPhase = workflowRun.CurrentPhase == PhaseId.Spec && workflowRun.Status != UserStoryStatus.Completed;
        var isApproved = workflowRun.IsPhaseApproved(PhaseId.Spec);
        var decompositionPendingApproval = await IsDecompositionPendingApprovalAsync(paths, cancellationToken);
        var specPath = paths.GetLatestExistingPhaseArtifactPath(PhaseId.Spec);
        var hasSpecArtifact = !string.IsNullOrWhiteSpace(specPath) && File.Exists(specPath);

        SpecBaselineValidationResult? validation = null;
        if (hasSpecArtifact)
        {
            validation = SpecBaselineSchemaValidator.Validate(await File.ReadAllTextAsync(specPath!, cancellationToken));
        }

        var unresolvedApprovalQuestionCount = validation?.UnresolvedApprovalQuestions.Count ?? 0;
        var hasUnresolvedApprovalQuestions = unresolvedApprovalQuestionCount > 0;
        var schemaIsValid = validation?.IsValid ?? false;

        string status;
        string? approvalBlockingReason;

        if (isApproved)
        {
            status = "already-approved";
            approvalBlockingReason = null;
        }
        else if (!isCurrentSpecPhase)
        {
            status = "not-current";
            approvalBlockingReason = null;
        }
        else if (decompositionPendingApproval)
        {
            status = "blocked";
            approvalBlockingReason = "decomposition_pending_user_approval";
        }
        else if (!hasSpecArtifact)
        {
            status = "blocked";
            approvalBlockingReason = "spec_missing_artifact";
        }
        else if (validation is not null && validation.MissingSections.Count > 0)
        {
            status = "blocked";
            approvalBlockingReason = "spec_schema_missing_sections";
        }
        else if (validation is not null && validation.PlaceholderSections.Count > 0)
        {
            status = "blocked";
            approvalBlockingReason = "spec_schema_placeholder_sections";
        }
        else if (hasUnresolvedApprovalQuestions)
        {
            status = "blocked";
            approvalBlockingReason = "spec_approval_questions_unresolved";
        }
        else
        {
            status = "ready";
            approvalBlockingReason = null;
        }

        var approvalAvailableNow = status == "ready";
        var approvalRules = new List<SpecPhaseApprovalRule>
        {
            new(
                "spec_is_current_and_pending_approval",
                "Spec approval only proceeds while `spec` is the active phase and the baseline has not already been approved.",
                isApproved ? "already-approved" : isCurrentSpecPhase ? "ready" : "not-current",
                isCurrentSpecPhase && !isApproved,
                CurrentStatusMessage: isApproved
                    ? "The spec baseline is already approved."
                    : isCurrentSpecPhase
                        ? "The workflow is currently waiting on spec approval."
                        : "The workflow is no longer at the spec approval checkpoint."),
            new(
                "decomposition_not_pending_approval",
                "Any pending decomposition proposal must be resolved before approving the spec baseline.",
                decompositionPendingApproval ? "blocked" : "ready",
                !decompositionPendingApproval,
                BlockingReason: decompositionPendingApproval ? "decomposition_pending_user_approval" : null,
                CurrentStatusMessage: decompositionPendingApproval
                    ? "A decomposition proposal is still waiting for human approval."
                    : "No pending decomposition approval blocks the baseline."),
            new(
                "spec_artifact_exists",
                "A generated spec artifact must exist before approval can proceed.",
                hasSpecArtifact ? "ready" : "blocked",
                hasSpecArtifact,
                BlockingReason: hasSpecArtifact ? null : "spec_missing_artifact",
                CurrentStatusMessage: hasSpecArtifact
                    ? $"Active spec artifact: `{specPath}`."
                    : "No current spec artifact was found for this user story."),
            new(
                "spec_schema_is_valid",
                "The spec artifact must satisfy the required schema with no missing or placeholder-only sections.",
                schemaIsValid || hasUnresolvedApprovalQuestions ? "attention" : "blocked",
                validation is not null && validation.MissingSections.Count == 0 && validation.PlaceholderSections.Count == 0,
                BlockingReason: validation is null
                    ? "spec_missing_artifact"
                    : validation.MissingSections.Count > 0
                        ? "spec_schema_missing_sections"
                        : validation.PlaceholderSections.Count > 0
                            ? "spec_schema_placeholder_sections"
                            : null,
                CurrentStatusMessage: validation is null
                    ? "Schema validation is unavailable because the spec artifact does not exist."
                    : validation.MissingSections.Count > 0
                        ? $"Missing sections: {string.Join(", ", validation.MissingSections)}."
                        : validation.PlaceholderSections.Count > 0
                            ? $"Placeholder-only sections: {string.Join(", ", validation.PlaceholderSections)}."
                            : "The required schema sections are present and materially filled."),
            new(
                "human_approval_questions_resolved",
                "All human approval questions must be answered before the spec baseline can be approved.",
                hasUnresolvedApprovalQuestions ? "blocked" : "ready",
                !hasUnresolvedApprovalQuestions,
                BlockingReason: hasUnresolvedApprovalQuestions ? "spec_approval_questions_unresolved" : null,
                CurrentStatusMessage: hasUnresolvedApprovalQuestions
                    ? $"{unresolvedApprovalQuestionCount} unresolved approval question(s) remain."
                    : "All human approval questions are resolved.")
        };

        return new SpecPhaseApprovalPolicyDetails(
            Status: status,
            ApprovalAvailableNow: approvalAvailableNow,
            ApprovalBlockingReason: approvalBlockingReason,
            HasSpecArtifact: hasSpecArtifact,
            SchemaIsValid: schemaIsValid,
            HasUnresolvedApprovalQuestions: hasUnresolvedApprovalQuestions,
            UnresolvedApprovalQuestionCount: unresolvedApprovalQuestionCount,
            DecompositionApprovalPending: decompositionPendingApproval,
            ApprovalRules: approvalRules);
    }

    private static async Task<bool> IsDecompositionPendingApprovalAsync(
        UserStoryFilePaths paths,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.DecompositionJsonPath))
        {
            return false;
        }

        var document = UserStoryDecomposition.Deserialize(
            await File.ReadAllTextAsync(paths.DecompositionJsonPath, cancellationToken));
        return document.State == UserStoryDecomposition.StatePendingApproval;
    }
}
