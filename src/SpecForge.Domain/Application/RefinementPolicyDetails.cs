namespace SpecForge.Domain.Application;

public sealed record RefinementPolicyDetails(
    string Tolerance,
    int PendingQuestionCount,
    int UnansweredQuestionCount,
    IReadOnlyCollection<RefinementBlockingCondition> BlockingConditions,
    RefinementAutoAnswerPolicy AutoAnswer);

public sealed record RefinementBlockingCondition(
    string Id,
    string Description,
    string Status,
    bool IsCurrentlyBlocking,
    string? BlockingReason = null);

public sealed record RefinementAutoAnswerPolicy(
    bool IsEnabled,
    string Mode,
    string Summary,
    string? ProfileName,
    string? AgentName,
    string? AgentRole,
    bool IsCurrentlyEligible,
    string EligibilityStatus,
    string? EligibilityReason = null);

public sealed record RefinementAutoAnswerCapability(
    bool IsEnabled,
    string Mode,
    string Summary,
    string? ProfileName = null,
    string? AgentName = null,
    string? AgentRole = null);

public static class RefinementPolicyDetailsBuilder
{
    public static RefinementPolicyDetails Build(
        string tolerance,
        RefinementSession session,
        PhaseExecutionReadiness readiness,
        RefinementAutoAnswerCapability autoAnswerCapability)
    {
        var normalizedTolerance = string.IsNullOrWhiteSpace(tolerance) ? "balanced" : tolerance.Trim();
        var orderedItems = session.Items
            .OrderBy(static item => item.Index)
            .ToArray();
        var pendingQuestionCount = orderedItems.Length;
        var unansweredQuestionCount = orderedItems.Count(static item => string.IsNullOrWhiteSpace(item.Answer));
        var hasPendingQuestions = pendingQuestionCount > 0;
        var hasUnansweredQuestions = unansweredQuestionCount > 0;
        var readinessBlocking = !readiness.CanExecute;
        var autoAnswerEligibility = ResolveAutoAnswerEligibility(
            session,
            autoAnswerCapability,
            readiness,
            hasPendingQuestions,
            unansweredQuestionCount);

        return new RefinementPolicyDetails(
            normalizedTolerance,
            pendingQuestionCount,
            unansweredQuestionCount,
            [
                new RefinementBlockingCondition(
                    "repository_read_access_required",
                    "Refinement execution requires an assigned agent with repository read access.",
                    readinessBlocking ? "blocking" : "ready",
                    readinessBlocking,
                    readinessBlocking ? readiness.BlockingReason : null),
                new RefinementBlockingCondition(
                    "unanswered_questions_require_resolution",
                    "Spec cannot continue while refinement still has unanswered questions.",
                    hasUnansweredQuestions ? "blocking" : "clear",
                    hasUnansweredQuestions,
                    hasUnansweredQuestions ? "refinement_pending_answers" : null)
            ],
            new RefinementAutoAnswerPolicy(
                autoAnswerCapability.IsEnabled,
                autoAnswerCapability.Mode,
                autoAnswerCapability.Summary,
                autoAnswerCapability.ProfileName,
                autoAnswerCapability.AgentName,
                autoAnswerCapability.AgentRole,
                autoAnswerEligibility.IsEligible,
                autoAnswerEligibility.Status,
                autoAnswerEligibility.Reason));
    }

    private static (bool IsEligible, string Status, string? Reason) ResolveAutoAnswerEligibility(
        RefinementSession session,
        RefinementAutoAnswerCapability autoAnswerCapability,
        PhaseExecutionReadiness readiness,
        bool hasPendingQuestions,
        int unansweredQuestionCount)
    {
        if (!autoAnswerCapability.IsEnabled)
        {
            return (false, "disabled", "Automatic refinement answering is disabled for the active provider configuration.");
        }

        if (!readiness.CanExecute)
        {
            return (false, "blocked", readiness.BlockingReason ?? "Refinement execution prerequisites are not currently satisfied.");
        }

        if (!string.Equals(session.Status, "needs_refinement", StringComparison.Ordinal))
        {
            return (false, "not-needed", "The latest persisted refinement session is already ready for spec.");
        }

        if (!hasPendingQuestions)
        {
            return (false, "not-needed", "No pending refinement questions remain.");
        }

        if (unansweredQuestionCount == 0)
        {
            return (false, "waiting-manual-submit", "All pending refinement questions already have answers; submit them to retry refinement.");
        }

        return (true, "eligible", "Automatic refinement answering can attempt one grounded retry from the current repository context.");
    }
}
