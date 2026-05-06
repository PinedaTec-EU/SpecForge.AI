namespace SpecForge.Domain.Application;

public sealed record ApprovalAnswerSuggestionResult(
    string UsId,
    string CurrentPhase,
    string Status,
    string Question,
    string? Answer,
    TokenUsage? Usage,
    long DurationMs,
    PhaseExecutionMetadata? Execution);

public sealed record ApprovalAnswerSuggestionProviderResult(
    string? Answer,
    TokenUsage? Usage = null,
    PhaseExecutionMetadata? Execution = null);
