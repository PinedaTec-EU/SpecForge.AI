namespace SpecForge.Domain.Application;

public sealed record GoalUserStoryDraft(
    string? UsId,
    string Title,
    string? Kind,
    string? Category,
    string SourceText,
    IReadOnlyList<string>? AcceptanceCriteria = null,
    IReadOnlyList<string>? Dependencies = null,
    IReadOnlyList<string>? ClarifiedAnswers = null,
    IReadOnlyList<string>? NonGoals = null,
    string? MvpOutcome = null,
    string? SliceRationale = null);

public sealed record GoalUserStoryCreationResult(
    string UsId,
    string Title,
    string Kind,
    string Category,
    int Sequence,
    string RootDirectory,
    string MainArtifactPath);

public sealed record GoalIntakeResult(
    string GoalId,
    string GoalText,
    string Strategy,
    string RecommendedFirstUserStory,
    IReadOnlyList<GoalUserStoryCreationResult> CreatedStories);
