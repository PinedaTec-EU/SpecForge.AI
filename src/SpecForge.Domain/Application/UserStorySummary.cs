namespace SpecForge.Domain.Application;

public sealed record UserStorySummary(
    string UsId,
    string Title,
    string Description,
    string Category,
    IReadOnlyCollection<string> Tags,
    string DirectoryPath,
    string MainArtifactPath,
    string CurrentPhase,
    string Status,
    string? WorkBranch,
    IReadOnlyCollection<UserStoryDependencySummary> Dependencies);

public sealed record UserStoryDependencySummary(
    string UsId,
    string? Title,
    string? CurrentPhase,
    string? Status,
    bool IsSatisfied,
    string? MissingReason);
