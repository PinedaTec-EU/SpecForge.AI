namespace SpecForge.Domain.Application;

public sealed record UserStoryDecompositionApprovalResult(
    string UsId,
    string Status,
    string CurrentPhase,
    IReadOnlyCollection<string> ChildUsIds);
