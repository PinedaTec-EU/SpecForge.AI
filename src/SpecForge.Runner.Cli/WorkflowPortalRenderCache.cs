using System.Collections.Concurrent;
using SpecForge.Domain.Application;

internal sealed class WorkflowPortalRenderCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> entries = new(StringComparer.Ordinal);
    private readonly int maxEntries;

    public WorkflowPortalRenderCache(int maxEntries = 16)
    {
        if (maxEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEntries), "Render cache must allow at least one entry.");
        }

        this.maxEntries = maxEntries;
    }

    public int Count => entries.Count;

    public bool TryGet(string signature, string selectedPhaseId, WorkflowPhaseDetails? selectedPhase, out string html)
    {
        var key = BuildKey(signature, selectedPhaseId, selectedPhase);
        if (entries.TryGetValue(key, out var entry))
        {
            entries[key] = entry with { LastAccessedUtc = DateTimeOffset.UtcNow };
            html = entry.Html;
            return true;
        }

        html = string.Empty;
        return false;
    }

    public void Store(string signature, string selectedPhaseId, WorkflowPhaseDetails? selectedPhase, string html)
    {
        var key = BuildKey(signature, selectedPhaseId, selectedPhase);
        entries[key] = new CacheEntry(html, DateTimeOffset.UtcNow);
        Trim();
    }

    private void Trim()
    {
        if (entries.Count <= maxEntries)
        {
            return;
        }

        var keysToRemove = entries
            .OrderBy(static item => item.Value.LastAccessedUtc)
            .Take(entries.Count - maxEntries)
            .Select(static item => item.Key)
            .ToArray();
        foreach (var key in keysToRemove)
        {
            entries.TryRemove(key, out _);
        }
    }

    private static string BuildKey(string signature, string selectedPhaseId, WorkflowPhaseDetails? selectedPhase)
    {
        var artifactStamp = ReadLastWriteStamp(selectedPhase?.ArtifactPath);
        var operationStamp = ReadLastWriteStamp(selectedPhase?.OperationLogPath);
        return $"{signature}:{selectedPhaseId}:{artifactStamp}:{operationStamp}";
    }

    private static long ReadLastWriteStamp(string? path) =>
        path is not null && File.Exists(path)
            ? File.GetLastWriteTimeUtc(path).Ticks
            : 0;

    private sealed record CacheEntry(string Html, DateTimeOffset LastAccessedUtc);
}
