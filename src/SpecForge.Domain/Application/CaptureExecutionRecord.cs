namespace SpecForge.Domain.Application;

public sealed record CaptureExecutionRecord(
    string Actor,
    string CreatedAtUtc,
    string SourceKind,
    string? SourceReference,
    IReadOnlyCollection<string> MaterializedArtifacts);
