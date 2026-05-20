namespace SpecForge.Domain.Application;

public sealed record SemanticGraphTokenUsage(
    int InputTokens,
    int OutputTokens,
    int TotalTokens);

public sealed record SemanticGraphAuditEvent(
    string EventId,
    string Timestamp,
    string EventFamily,
    string Actor,
    string TriggerSurface,
    string WorkspaceRoot,
    string? UsId,
    string? Phase,
    string? Reason,
    string RequestedMode,
    string ActualMode,
    string? SourcePreference,
    string GraphStateBefore,
    string GraphStateAfter,
    bool OverwriteRequested,
    bool OverwriteConfirmed,
    bool ReusedExistingGraph,
    bool ReplacedExistingGraph,
    bool FallbackUsed,
    string? FallbackReason,
    string? BuilderStrategy,
    string? ModelProfile,
    string? EmbeddingProfile,
    int LatencyMs,
    int? FilesProcessed,
    SemanticGraphTokenUsage? TokenUsage,
    IReadOnlyCollection<string> ArtifactsRead,
    IReadOnlyCollection<string> ArtifactsWritten,
    IReadOnlyCollection<string> Warnings,
    string? ErrorCode,
    string? ErrorSummary);

public sealed record SemanticGraphCostLedger(
    string ContractKey,
    string GeneratedAtUtc,
    SemanticGraphOperationLedger Builds,
    SemanticGraphOperationLedger Refreshes,
    SemanticGraphOperationLedger Rebuilds,
    SemanticGraphOperationLedger ImpactDerivations,
    SemanticGraphOperationLedger Queries,
    SemanticGraphTokenUsage TotalTokenUsage,
    SemanticGraphAuditLedgerPointer? LastSuccessfulGlobalGraphBuild,
    SemanticGraphAuditLedgerPointer? LastFailedGraphMutation);

public sealed record SemanticGraphOperationLedger(
    int Count,
    long TotalLatencyMs,
    double AverageLatencyMs,
    int TotalFilesProcessed);

public sealed record SemanticGraphAuditLedgerPointer(
    string EventId,
    string Timestamp,
    string EventFamily,
    string Actor,
    string RequestedMode,
    string ActualMode,
    string GraphStateAfter,
    string? ErrorCode);
