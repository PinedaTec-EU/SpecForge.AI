using SpecForge.Domain.Workflow;

namespace SpecForge.Domain.Application;

public sealed record PhaseExecutionEffectivePrompt(
    string SystemPrompt,
    string UserPrompt,
    IReadOnlyCollection<string>? Warnings = null,
    IReadOnlyCollection<PhaseExecutionPromptSource>? SourcePrompts = null);

public sealed record PhaseExecutionPromptSource(
    string Role,
    string Path,
    bool IsOverride,
    string? ContentSha256 = null,
    string? EmbeddedContentSha256 = null);

public sealed record PhaseExecutionEffectiveContext(
    string WorkspaceRoot,
    string UserStoryPath,
    string? WorkspaceGitHeadSha,
    IReadOnlyCollection<PhaseExecutionArtifactInput> PreviousArtifacts,
    IReadOnlyCollection<PhaseExecutionArtifactInput> ContextFiles,
    PhaseExecutionArtifactInput? CurrentArtifact,
    string? OperationPromptSha256,
    TechnicalDesignContextPack? TechnicalDesignContextPack = null);

public static class PhaseExecutionInspectionBuilder
{
    public static PhaseExecutionEffectiveContext BuildEffectiveContext(
        string workspaceRoot,
        PhaseExecutionContext context)
    {
        var previousArtifacts = context.PreviousArtifactPaths
            .OrderBy(static item => item.Key)
            .Select(static item => new PhaseExecutionArtifactInput(
                PhaseExecutionReceiptStore.NormalizePath(item.Value),
                PhaseExecutionReceiptStore.TryComputeFileSha256(item.Value),
                WorkflowPresentation.ToPhaseSlug(item.Key)))
            .ToArray();
        var contextFiles = context.ContextFilePaths
            .OrderBy(static path => path, StringComparer.Ordinal)
            .Select(static path => new PhaseExecutionArtifactInput(
                PhaseExecutionReceiptStore.NormalizePath(path),
                PhaseExecutionReceiptStore.TryComputeFileSha256(path)))
            .ToArray();
        var currentArtifact = string.IsNullOrWhiteSpace(context.CurrentArtifactPath)
            ? null
            : new PhaseExecutionArtifactInput(
                PhaseExecutionReceiptStore.NormalizePath(context.CurrentArtifactPath),
                PhaseExecutionReceiptStore.TryComputeFileSha256(context.CurrentArtifactPath),
                WorkflowPresentation.ToPhaseSlug(context.PhaseId));

        return new PhaseExecutionEffectiveContext(
            WorkspaceRoot: PhaseExecutionReceiptStore.NormalizePath(workspaceRoot),
            UserStoryPath: PhaseExecutionReceiptStore.NormalizePath(context.UserStoryPath),
            WorkspaceGitHeadSha: PhaseExecutionReceiptStore.TryReadGitHeadSha(workspaceRoot),
            PreviousArtifacts: previousArtifacts,
            ContextFiles: contextFiles,
            CurrentArtifact: currentArtifact,
            OperationPromptSha256: PhaseExecutionReceiptStore.ComputeSha256(context.OperationPrompt),
            TechnicalDesignContextPack: context.TechnicalDesignContextPack);
    }
}
