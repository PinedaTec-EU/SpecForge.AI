namespace SpecForge.Domain.Application;

public sealed record UpdateUserStoryInfoResult(
    string UsId,
    string MainArtifactPath,
    UserStorySummary Summary);
