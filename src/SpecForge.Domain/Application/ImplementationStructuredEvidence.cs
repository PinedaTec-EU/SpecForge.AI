using System.Text.Json;
using SpecForge.Domain.Persistence;
using SpecForge.Domain.Workflow;

namespace SpecForge.Domain.Application;

public sealed record ImplementationStructuredEvidence(
    string GeneratedAtUtc,
    string EvidenceJsonPath,
    string EvidenceMarkdownPath,
    IReadOnlyCollection<string> Summary,
    IReadOnlyCollection<ImplementationTouchedFileEvidence> TouchedFiles,
    ImplementationGraphEvidence? GraphEvidence);

public sealed record ImplementationTouchedFileEvidence(
    string Path,
    string ChangeKind,
    string? BaselineStatusCode,
    string CurrentStatusCode,
    string? BaselineFingerprint,
    string CurrentFingerprint);

public sealed record ImplementationGraphEvidence(
    bool GraphScopeRequestAvailable,
    string? GraphScopeRequestPath,
    string? ImpactGraphPath,
    string? ImpactGraphMetadataPath,
    string? ImpactSummaryPath,
    string? ImpactGraphState,
    IReadOnlyCollection<ImplementationGraphOperationReference> OperationReferences,
    IReadOnlyCollection<string> Warnings);

public sealed record ImplementationGraphOperationReference(
    string EventId,
    string Timestamp,
    string EventFamily,
    string RequestedMode,
    string ActualMode,
    string TriggerSurface,
    bool FallbackUsed,
    int LatencyMs,
    IReadOnlyCollection<string> ArtifactsRead,
    IReadOnlyCollection<string> ArtifactsWritten,
    IReadOnlyCollection<string> Warnings);

internal static class ImplementationStructuredEvidenceBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static ImplementationStructuredEvidence Build(
        string workspaceRoot,
        string usId,
        UserStoryFilePaths paths,
        ImplementationPhaseEvidenceDocument evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(usId);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(evidence);

        var graphEvidence = BuildGraphEvidence(workspaceRoot, usId, paths);

        return new ImplementationStructuredEvidence(
            evidence.GeneratedAtUtc,
            PhaseExecutionReceiptStore.NormalizePath(paths.GetPhaseEvidenceJsonPath(PhaseId.Implementation)),
            PhaseExecutionReceiptStore.NormalizePath(paths.GetPhaseEvidenceMarkdownPath(PhaseId.Implementation)),
            evidence.Summary,
            evidence.TouchedFiles
                .Select(static item => new ImplementationTouchedFileEvidence(
                    item.Path,
                    item.ChangeKind,
                    item.BaselineStatusCode,
                    item.CurrentStatusCode,
                    item.BaselineFingerprint,
                    item.CurrentFingerprint))
                .ToArray(),
            graphEvidence);
    }

    private static ImplementationGraphEvidence? BuildGraphEvidence(string workspaceRoot, string usId, UserStoryFilePaths paths)
    {
        var graphScopeRequestAvailable = File.Exists(paths.GraphScopeRequestPath);
        var impactGraphExists = File.Exists(paths.ImpactGraphPath);
        var impactGraphMetadataExists = File.Exists(paths.ImpactGraphMetadataPath);
        var impactSummaryExists = File.Exists(paths.ImpactGraphSummaryPath);
        var operationReferences = ReadGraphOperationReferences(workspaceRoot, usId);
        var warnings = new List<string>();

        string? impactGraphState = null;
        if (impactGraphMetadataExists)
        {
            try
            {
                using var stream = File.OpenRead(paths.ImpactGraphMetadataPath);
                var metadata = JsonSerializer.Deserialize<ImplementationImpactGraphMetadata>(stream, SerializerOptions);
                impactGraphState = metadata?.State;
            }
            catch
            {
                warnings.Add("Impact graph metadata could not be parsed for implementation evidence.");
            }
        }

        if (!graphScopeRequestAvailable &&
            !impactGraphExists &&
            !impactGraphMetadataExists &&
            !impactSummaryExists &&
            operationReferences.Count == 0)
        {
            return null;
        }

        if (!impactGraphExists && impactGraphMetadataExists)
        {
            warnings.Add("Impact graph metadata exists without the corresponding impact graph artifact.");
        }

        return new ImplementationGraphEvidence(
            graphScopeRequestAvailable,
            graphScopeRequestAvailable ? PhaseExecutionReceiptStore.NormalizePath(paths.GraphScopeRequestPath) : null,
            impactGraphExists ? PhaseExecutionReceiptStore.NormalizePath(paths.ImpactGraphPath) : null,
            impactGraphMetadataExists ? PhaseExecutionReceiptStore.NormalizePath(paths.ImpactGraphMetadataPath) : null,
            impactSummaryExists ? PhaseExecutionReceiptStore.NormalizePath(paths.ImpactGraphSummaryPath) : null,
            impactGraphState,
            operationReferences,
            warnings);
    }

    private static IReadOnlyCollection<ImplementationGraphOperationReference> ReadGraphOperationReferences(
        string workspaceRoot,
        string usId)
    {
        var graphPaths = SemanticGraphFilePaths.FromWorkspaceRoot(workspaceRoot);
        if (!File.Exists(graphPaths.GraphBuildLogPath))
        {
            return [];
        }

        var references = new List<ImplementationGraphOperationReference>();
        foreach (var line in File.ReadLines(graphPaths.GraphBuildLogPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            SemanticGraphAuditEvent? auditEvent;
            try
            {
                auditEvent = JsonSerializer.Deserialize<SemanticGraphAuditEvent>(line, SerializerOptions);
            }
            catch
            {
                continue;
            }

            if (auditEvent is null ||
                !string.Equals(auditEvent.UsId, usId, StringComparison.Ordinal) ||
                !auditEvent.EventFamily.StartsWith("graph.", StringComparison.Ordinal))
            {
                continue;
            }

            references.Add(new ImplementationGraphOperationReference(
                auditEvent.EventId,
                auditEvent.Timestamp,
                auditEvent.EventFamily,
                auditEvent.RequestedMode,
                auditEvent.ActualMode,
                auditEvent.TriggerSurface,
                auditEvent.FallbackUsed,
                auditEvent.LatencyMs,
                auditEvent.ArtifactsRead,
                auditEvent.ArtifactsWritten,
                auditEvent.Warnings));
        }

        return references
            .OrderByDescending(static item => item.Timestamp, StringComparer.Ordinal)
            .Take(5)
            .ToArray();
    }

    private sealed record ImplementationImpactGraphMetadata(
        string? State);
}
