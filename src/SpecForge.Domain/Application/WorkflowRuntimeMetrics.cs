using SpecForge.Domain.Persistence;
using SpecForge.Domain.Workflow;

namespace SpecForge.Domain.Application;

public sealed record WorkflowRuntimeMetrics(
    int AttemptCount,
    int RetryCount,
    long? LeadTimeMs,
    long WaitingUserDurationMs,
    long BlockedDurationMs,
    string? FirstEventAtUtc,
    string? LastEventAtUtc);

public sealed record PhaseRuntimeMetrics(
    string PhaseId,
    int AttemptCount,
    int RetryCount,
    long? LeadTimeMs,
    long ExecutionDurationMs,
    long WaitingUserDurationMs,
    long BlockedDurationMs,
    string? FirstEventAtUtc,
    string? LastEventAtUtc);

public sealed record WorkflowRuntimeMetricsSnapshot(
    WorkflowRuntimeMetrics Workflow,
    IReadOnlyDictionary<string, PhaseRuntimeMetrics> ByPhase);

public static class WorkflowRuntimeMetricsBuilder
{
    public static async Task<WorkflowRuntimeMetricsSnapshot> BuildAsync(
        WorkflowRun workflowRun,
        UserStoryFilePaths paths,
        IReadOnlyCollection<TimelineEventDetails> timelineEvents,
        IReadOnlyCollection<PhaseIterationDetails> phaseIterations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflowRun);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(timelineEvents);
        ArgumentNullException.ThrowIfNull(phaseIterations);

        var orderedEvents = timelineEvents
            .Select(eventDetails => new EventPoint(eventDetails, TryParseTimestamp(eventDetails.TimestampUtc)))
            .Where(static item => item.Timestamp is not null)
            .OrderBy(static item => item.Timestamp)
            .ToArray();
        var now = DateTimeOffset.UtcNow;
        var phaseMetrics = new Dictionary<string, PhaseRuntimeMetrics>(StringComparer.Ordinal);
        var phaseIds = new[]
        {
            WorkflowPresentation.ToPhaseSlug(PhaseId.Capture),
            WorkflowPresentation.ToPhaseSlug(PhaseId.Refinement),
            WorkflowPresentation.ToPhaseSlug(PhaseId.Spec),
            WorkflowPresentation.ToPhaseSlug(PhaseId.TechnicalDesign),
            WorkflowPresentation.ToPhaseSlug(PhaseId.Implementation),
            WorkflowPresentation.ToPhaseSlug(PhaseId.Review),
            WorkflowPresentation.ToPhaseSlug(PhaseId.ReleaseApproval),
            WorkflowPresentation.ToPhaseSlug(PhaseId.PrPreparation)
        };

        foreach (var phaseId in phaseIds)
        {
            var phaseEvents = orderedEvents
                .Where(item => string.Equals(item.Event.Phase, phaseId, StringComparison.Ordinal))
                .ToArray();
            var attempts = phaseIterations.Count(item => string.Equals(item.PhaseId, phaseId, StringComparison.Ordinal));
            var retries = Math.Max(0, attempts - 1);
            var executionDurationMs = phaseIterations
                .Where(item => string.Equals(item.PhaseId, phaseId, StringComparison.Ordinal))
                .Sum(static item => item.DurationMs ?? 0);
            var waitingUserDurationMs = SumWaitingUserDurationMs(phaseId, phaseEvents, now);
            var blockedDurationMs = await SumBlockedDurationMsAsync(phaseId, phaseEvents, paths, now, cancellationToken);
            var firstEventAtUtc = phaseEvents.FirstOrDefault()?.Event.TimestampUtc;
            var lastEventAtUtc = phaseEvents.LastOrDefault()?.Event.TimestampUtc;
            var firstPhaseEvent = phaseEvents.FirstOrDefault();
            var lastPhaseEvent = phaseEvents.LastOrDefault();
            var leadTimeMs = ComputeLeadTimeMs(firstPhaseEvent?.Timestamp, ResolveLeadTimeEnd(
                lastPhaseEvent?.Timestamp,
                lastPhaseEvent?.Event.Phase,
                workflowRun.CurrentPhase,
                workflowRun.Status,
                phaseId,
                now));
            phaseMetrics[phaseId] = new PhaseRuntimeMetrics(
                phaseId,
                attempts,
                retries,
                leadTimeMs,
                executionDurationMs,
                waitingUserDurationMs,
                blockedDurationMs,
                firstEventAtUtc,
                lastEventAtUtc);
        }

        var firstWorkflowEvent = orderedEvents.FirstOrDefault();
        var lastWorkflowEvent = orderedEvents.LastOrDefault();
        var workflowLeadTimeMs = ComputeLeadTimeMs(
            firstWorkflowEvent?.Timestamp,
            ResolveLeadTimeEnd(
                lastWorkflowEvent?.Timestamp,
                lastWorkflowEvent?.Event.Phase,
                workflowRun.CurrentPhase,
                workflowRun.Status,
                WorkflowPresentation.ToPhaseSlug(workflowRun.CurrentPhase),
                now));
        var workflowMetrics = new WorkflowRuntimeMetrics(
            AttemptCount: phaseIterations.Count,
            RetryCount: phaseMetrics.Values.Sum(static metrics => metrics.RetryCount),
            LeadTimeMs: workflowLeadTimeMs,
            WaitingUserDurationMs: phaseMetrics.Values.Sum(static metrics => metrics.WaitingUserDurationMs),
            BlockedDurationMs: phaseMetrics.Values.Sum(static metrics => metrics.BlockedDurationMs),
            FirstEventAtUtc: orderedEvents.FirstOrDefault()?.Event.TimestampUtc,
            LastEventAtUtc: orderedEvents.LastOrDefault()?.Event.TimestampUtc);
        return new WorkflowRuntimeMetricsSnapshot(workflowMetrics, phaseMetrics);
    }

    private static long SumWaitingUserDurationMs(
        string phaseId,
        IReadOnlyList<EventPoint> phaseEvents,
        DateTimeOffset now)
    {
        long total = 0;
        for (var index = 0; index < phaseEvents.Count; index++)
        {
            var current = phaseEvents[index];
            if (!IsWaitingUserStart(phaseId, current.Event))
            {
                continue;
            }

            var next = index + 1 < phaseEvents.Count
                ? phaseEvents[index + 1].Timestamp
                : now;
            total += ComputeLeadTimeMs(current.Timestamp, next) ?? 0;
        }

        return total;
    }

    private static async Task<long> SumBlockedDurationMsAsync(
        string phaseId,
        IReadOnlyList<EventPoint> phaseEvents,
        UserStoryFilePaths paths,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(phaseId, WorkflowPresentation.ToPhaseSlug(PhaseId.Review), StringComparison.Ordinal))
        {
            return 0;
        }

        long total = 0;
        for (var index = 0; index < phaseEvents.Count; index++)
        {
            var current = phaseEvents[index];
            if (!await IsReviewFailureStartAsync(current.Event, paths, cancellationToken))
            {
                continue;
            }

            var next = index + 1 < phaseEvents.Count
                ? phaseEvents[index + 1].Timestamp
                : now;
            total += ComputeLeadTimeMs(current.Timestamp, next) ?? 0;
        }

        return total;
    }

    private static bool IsWaitingUserStart(string phaseId, TimelineEventDetails timelineEvent) =>
        string.Equals(timelineEvent.Code, "refinement_requested", StringComparison.Ordinal)
        || string.Equals(timelineEvent.Code, "decomposition_proposed", StringComparison.Ordinal)
        || (string.Equals(timelineEvent.Code, "phase_completed", StringComparison.Ordinal)
            && (string.Equals(phaseId, WorkflowPresentation.ToPhaseSlug(PhaseId.Spec), StringComparison.Ordinal)
                || string.Equals(phaseId, WorkflowPresentation.ToPhaseSlug(PhaseId.ReleaseApproval), StringComparison.Ordinal)));

    private static async Task<bool> IsReviewFailureStartAsync(
        TimelineEventDetails timelineEvent,
        UserStoryFilePaths paths,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(timelineEvent.Code, "phase_completed", StringComparison.Ordinal)
            || !string.Equals(timelineEvent.Phase, WorkflowPresentation.ToPhaseSlug(PhaseId.Review), StringComparison.Ordinal))
        {
            return false;
        }

        var reviewArtifactPath = timelineEvent.Artifacts
            .LastOrDefault(path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(path).StartsWith("04-review", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(reviewArtifactPath))
        {
            reviewArtifactPath = paths.GetLatestExistingPhaseArtifactPath(PhaseId.Review);
        }

        if (string.IsNullOrWhiteSpace(reviewArtifactPath) || !File.Exists(reviewArtifactPath))
        {
            return false;
        }

        var markdown = await File.ReadAllTextAsync(reviewArtifactPath, cancellationToken);
        return !string.Equals(WorkflowRunner.TryReadReviewResult(markdown), "pass", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTimeOffset? TryParseTimestamp(string? value) =>
        DateTimeOffset.TryParse(value, out var timestamp) ? timestamp : null;

    private static long? ComputeLeadTimeMs(DateTimeOffset? start, DateTimeOffset? end)
    {
        if (start is null || end is null || end < start)
        {
            return null;
        }

        return Convert.ToInt64((end.Value - start.Value).TotalMilliseconds);
    }

    private static DateTimeOffset? ResolveLeadTimeEnd(
        DateTimeOffset? lastRecordedTimestamp,
        string? lastRecordedPhaseId,
        PhaseId currentPhase,
        UserStoryStatus status,
        string phaseId,
        DateTimeOffset now)
    {
        if (lastRecordedTimestamp is null)
        {
            return null;
        }

        var currentPhaseSlug = WorkflowPresentation.ToPhaseSlug(currentPhase);
        if (!string.Equals(currentPhaseSlug, phaseId, StringComparison.Ordinal)
            || !string.Equals(lastRecordedPhaseId, currentPhaseSlug, StringComparison.Ordinal))
        {
            return lastRecordedTimestamp;
        }

        return status is UserStoryStatus.Active or UserStoryStatus.WaitingUser or UserStoryStatus.Blocked
            ? now
            : lastRecordedTimestamp;
    }

    private sealed record EventPoint(
        TimelineEventDetails Event,
        DateTimeOffset? Timestamp);
}
