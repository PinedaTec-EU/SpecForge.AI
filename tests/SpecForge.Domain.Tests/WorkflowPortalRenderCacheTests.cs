using SpecForge.Domain.Application;

namespace SpecForge.Domain.Tests;

public sealed class WorkflowPortalRenderCacheTests
{
    [Fact]
    public void TryGet_ReturnsFalseForMissingEntry()
    {
        var cache = new WorkflowPortalRenderCache();

        var found = cache.TryGet("signature", "phase", selectedPhase: null, out var html);

        Assert.False(found);
        Assert.Equal(string.Empty, html);
    }

    [Fact]
    public void Store_OverwritesExistingEntryWithoutIncreasingCount()
    {
        var cache = new WorkflowPortalRenderCache(maxEntries: 2);

        cache.Store("signature", "phase", selectedPhase: null, "first");
        cache.Store("signature", "phase", selectedPhase: null, "second");

        Assert.Equal(1, cache.Count);
        Assert.True(cache.TryGet("signature", "phase", selectedPhase: null, out var html));
        Assert.Equal("second", html);
    }

    [Fact]
    public void Store_EvictsLeastRecentlyUsedEntry()
    {
        var cache = new WorkflowPortalRenderCache(maxEntries: 2);

        cache.Store("signature-a", "phase-a", selectedPhase: null, "a");
        cache.Store("signature-b", "phase-b", selectedPhase: null, "b");
        Assert.True(cache.TryGet("signature-a", "phase-a", selectedPhase: null, out _));

        cache.Store("signature-c", "phase-c", selectedPhase: null, "c");

        Assert.True(cache.TryGet("signature-a", "phase-a", selectedPhase: null, out var htmlA));
        Assert.False(cache.TryGet("signature-b", "phase-b", selectedPhase: null, out _));
        Assert.True(cache.TryGet("signature-c", "phase-c", selectedPhase: null, out var htmlC));
        Assert.Equal("a", htmlA);
        Assert.Equal("c", htmlC);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void TryGet_MissesWhenSignatureChanges()
    {
        var cache = new WorkflowPortalRenderCache();

        cache.Store("signature-before", "refinement", selectedPhase: null, "html");

        Assert.False(cache.TryGet("signature-after", "refinement", selectedPhase: null, out _));
    }

    [Fact]
    public void TryGet_MissesWhenSelectedPhaseChanges()
    {
        var cache = new WorkflowPortalRenderCache();

        cache.Store("signature", "refinement", selectedPhase: null, "html");

        Assert.False(cache.TryGet("signature", "spec", selectedPhase: null, out _));
    }

    [Fact]
    public void TryGet_MissesWhenArtifactTimestampChanges()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var phase = BuildPhase(artifactPath: tempFile);
            var cache = new WorkflowPortalRenderCache();

            File.SetLastWriteTimeUtc(tempFile, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            cache.Store("signature", "refinement", phase, "before");

            File.SetLastWriteTimeUtc(tempFile, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
            var found = cache.TryGet("signature", "refinement", phase, out _);

            Assert.False(found);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void TryGet_MissesWhenOperationLogTimestampChanges()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var phase = BuildPhase(operationLogPath: tempFile);
            var cache = new WorkflowPortalRenderCache();

            File.SetLastWriteTimeUtc(tempFile, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
            cache.Store("signature", "refinement", phase, "before");

            File.SetLastWriteTimeUtc(tempFile, new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc));
            var found = cache.TryGet("signature", "refinement", phase, out _);

            Assert.False(found);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void TryGet_MissesWhenTrackedFileIsDeleted()
    {
        var tempFile = Path.GetTempFileName();
        var phase = BuildPhase(artifactPath: tempFile);
        var cache = new WorkflowPortalRenderCache();

        cache.Store("signature", "refinement", phase, "before");
        File.Delete(tempFile);

        Assert.False(cache.TryGet("signature", "refinement", phase, out _));
    }

    [Fact]
    public void Store_TrimsToOneEntryWhenConfiguredWithSingleSlot()
    {
        var cache = new WorkflowPortalRenderCache(maxEntries: 1);

        cache.Store("signature-a", "phase-a", selectedPhase: null, "a");
        cache.Store("signature-b", "phase-b", selectedPhase: null, "b");

        Assert.False(cache.TryGet("signature-a", "phase-a", selectedPhase: null, out _));
        Assert.True(cache.TryGet("signature-b", "phase-b", selectedPhase: null, out var html));
        Assert.Equal("b", html);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowPortalRenderCache(maxEntries: 0));
    }

    private static WorkflowPhaseDetails BuildPhase(string? artifactPath = null, string? operationLogPath = null) =>
        new(
            "refinement",
            "Refinement",
            Order: 1,
            RequiresApproval: true,
            ExpectsHumanIntervention: true,
            IsApproved: false,
            IsCurrent: true,
            State: "active",
            ArtifactPath: artifactPath,
            OperationLogPath: operationLogPath,
            ExecutePromptPath: null,
            ApprovePromptPath: null);
}
