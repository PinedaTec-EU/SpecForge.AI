namespace SpecForge.Domain.Application;

public sealed record UserStorySummary(
    string UsId,
    string Title,
    string Description,
    string CreatedBy,
    string Owner,
    string Category,
    IReadOnlyCollection<string> Tags,
    IReadOnlyCollection<UserStoryExternalReference> ExternalReferences,
    string DirectoryPath,
    string MainArtifactPath,
    string CurrentPhase,
    string Status,
    string? WorkBranch,
    IReadOnlyCollection<UserStoryDependencySummary> Dependencies,
    string WorkflowKind = "normal",
    string? ParentUsId = null,
    IReadOnlyCollection<string>? ChildUsIds = null);

public sealed record UserStoryDependencySummary(
    string UsId,
    string? Title,
    string? CurrentPhase,
    string? Status,
    bool IsSatisfied,
    string? MissingReason);
