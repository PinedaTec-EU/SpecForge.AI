using SpecForge.Domain.Application;
using SpecForge.Domain.Persistence;
using SpecForge.Domain.Workflow;

namespace SpecForge.Domain.Tests;

public sealed class WorkflowRuntimeMetricsBuilderTests : IDisposable
{
    private readonly string workspaceRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task BuildAsync_DerivesAttemptsLeadTimeWaitingAndBlockedDurations()
    {
        var runner = new WorkflowRunner();
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Metrics story", "feature", "workflow", "Initial source");
        var paths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, "US-0001");
        var workflowRun = await new UserStoryFileStore().LoadAsync(paths.RootDirectory);

        var reviewArtifactPath = paths.GetPhaseArtifactPath(PhaseId.Review, version: 1);
        Directory.CreateDirectory(Path.GetDirectoryName(reviewArtifactPath)!);
        await File.WriteAllTextAsync(
            reviewArtifactPath,
            """
            # Review · US-0001 · v01

            ## State
            - Result: `fail`
            """);

        var timelineEvents = new[]
        {
            new TimelineEventDetails("2026-05-21T10:00:00.0000000+00:00", "refinement_requested", "system", "refinement", "Questions remain.", [], null, null, null),
            new TimelineEventDetails("2026-05-21T10:10:00.0000000+00:00", "refinement_answered", "user", "refinement", "Answers submitted.", [], null, null, null),
            new TimelineEventDetails("2026-05-21T10:20:00.0000000+00:00", "phase_completed", "user", "spec", "Spec generated.", [paths.GetPhaseArtifactPath(PhaseId.Spec, version: 1)], new TokenUsage(10, 5, 15), 1200, null),
            new TimelineEventDetails("2026-05-21T10:35:00.0000000+00:00", "phase_approved", "user", "spec", "Spec approved.", [], null, null, null),
            new TimelineEventDetails("2026-05-21T10:40:00.0000000+00:00", "phase_completed", "system", "review", "Review failed.", [reviewArtifactPath], new TokenUsage(20, 10, 30), 2200, null),
            new TimelineEventDetails("2026-05-21T10:55:00.0000000+00:00", "review_force_approved", "user", "review", "Override recorded.", [], null, null, null)
        };
        var phaseIterations = new[]
        {
            new PhaseIterationDetails("spec-1", 1, "spec", "2026-05-21T10:20:00.0000000+00:00", "phase_completed", "user", "Spec generated.", paths.GetPhaseArtifactPath(PhaseId.Spec, version: 1), null, [], null, null, new TokenUsage(10, 5, 15), 1200, null),
            new PhaseIterationDetails("spec-2", 2, "spec", "2026-05-21T10:32:00.0000000+00:00", "artifact_operated", "user", "Spec regenerated.", paths.GetPhaseArtifactPath(PhaseId.Spec, version: 2), null, [], null, null, new TokenUsage(12, 8, 20), 1600, null),
            new PhaseIterationDetails("review-1", 1, "review", "2026-05-21T10:40:00.0000000+00:00", "phase_completed", "system", "Review failed.", reviewArtifactPath, null, [], null, null, new TokenUsage(20, 10, 30), 2200, null)
        };

        var metrics = await WorkflowRuntimeMetricsBuilder.BuildAsync(workflowRun, paths, timelineEvents, phaseIterations);

        Assert.Equal(3, metrics.Workflow.AttemptCount);
        Assert.Equal(1, metrics.Workflow.RetryCount);
        Assert.Equal(Convert.ToInt64(TimeSpan.FromMinutes(55).TotalMilliseconds), metrics.Workflow.LeadTimeMs);
        Assert.Equal(Convert.ToInt64(TimeSpan.FromMinutes(25).TotalMilliseconds), metrics.Workflow.WaitingUserDurationMs);
        Assert.Equal(Convert.ToInt64(TimeSpan.FromMinutes(15).TotalMilliseconds), metrics.Workflow.BlockedDurationMs);

        var specMetrics = Assert.Contains("spec", metrics.ByPhase);
        Assert.Equal(2, specMetrics.AttemptCount);
        Assert.Equal(1, specMetrics.RetryCount);
        Assert.Equal(Convert.ToInt64(TimeSpan.FromMinutes(15).TotalMilliseconds), specMetrics.WaitingUserDurationMs);

        var reviewMetrics = Assert.Contains("review", metrics.ByPhase);
        Assert.Equal(Convert.ToInt64(TimeSpan.FromMinutes(15).TotalMilliseconds), reviewMetrics.BlockedDurationMs);
        Assert.Equal(2200, reviewMetrics.ExecutionDurationMs);
    }

    public void Dispose()
    {
        if (Directory.Exists(workspaceRoot))
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }
}
