using SpecForge.Domain.Workflow;

namespace SpecForge.Domain.Application;

public interface IPhaseExecutionProvider
{
    PhaseExecutionReadiness GetPhaseExecutionReadiness(PhaseId phaseId);

    RefinementAutoAnswerCapability DescribeRefinementAutoAnswerCapability() =>
        new(
            IsEnabled: false,
            Mode: "disabled",
            Summary: "Automatic refinement answering is disabled for this provider.");

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

    Task<UserStoryDecompositionEvaluationResult> EvaluateSpecDecompositionAsync(
        PhaseExecutionContext context,
        string specMarkdown,
        UserStoryDecompositionOptions options,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new UserStoryDecompositionEvaluationResult(
            ComplexityScore: 0,
            Decision: UserStoryDecomposition.DecisionNone,
            Rationale: "Decomposition evaluation is not implemented by this provider.",
            ProposedChildren: []));
}
