using SpecForge.Domain.Workflow;

namespace SpecForge.Domain.Application;

public interface IPhaseExecutionProvider
{
    PhaseExecutionReadiness GetPhaseExecutionReadiness(PhaseId phaseId);

    Task<PhaseExecutionResult> ExecuteAsync(
        PhaseExecutionContext context,
        CancellationToken cancellationToken = default);

    Task<AutoRefinementAnswersResult?> TryAutoAnswerRefinementAsync(
        PhaseExecutionContext context,
        RefinementSession session,
        CancellationToken cancellationToken = default);

    Task<ApprovalAnswerSuggestionProviderResult> SuggestApprovalAnswerAsync(
        PhaseExecutionContext context,
        string specMarkdown,
        string question,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ApprovalAnswerSuggestionProviderResult(null));
}
