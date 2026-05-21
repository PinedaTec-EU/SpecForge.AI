namespace SpecForge.Domain.Application;

public sealed record AutoRefinementAnswerAttemptRecord(
    string Status,
    string Summary,
    string? Reason,
    int ResolvedAnswerCount);

public sealed record AutoRefinementAnswerInspectionDetails(
    string Status,
    string Summary,
    string? Reason,
    int ResolvedAnswerCount,
    string? TimestampUtc,
    string? ReceiptPath,
    PhaseExecutionEffectivePrompt? EffectivePrompt,
    PhaseExecutionEffectiveContext? EffectiveContext);
