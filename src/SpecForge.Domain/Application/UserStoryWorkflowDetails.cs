namespace SpecForge.Domain.Application;

public sealed record UserStoryWorkflowDetails(
    string UsId,
    string Title,
    string Kind,
    string Category,
    IReadOnlyCollection<string> Tags,
    string Status,
    string CurrentPhase,
    string DirectoryPath,
    string? WorkBranch,
    string MainArtifactPath,
    string TimelinePath,
    string RawTimeline,
    string? CreatedWithRuntimeVersion,
    string? LastRuntimeVersion,
    IReadOnlyCollection<UserStoryDependencySummary> Dependencies,
    string WorkflowKind,
    string? ParentUsId,
    IReadOnlyCollection<UserStorySummary> ChildUserStories,
    DecompositionDetails? Decomposition,
    PullRequestDetails? PullRequest,
    HarnessProfileGovernance? HarnessProfileGovernance,
    IReadOnlyCollection<WorkflowPhaseDetails> Phases,
    CurrentPhaseControls Controls,
    WorkflowRuntimeMetrics? Metrics,
    RefinementSessionDetails? Refinement,
    IReadOnlyCollection<ApprovalQuestionDetails> ApprovalQuestions,
    IReadOnlyCollection<TimelineEventDetails> Events,
    IReadOnlyCollection<PhaseIterationDetails> PhaseIterations,
    string ContextFilesDirectoryPath,
    IReadOnlyCollection<UserStoryFileDetails> ContextFiles,
    string AttachmentsDirectoryPath,
    IReadOnlyCollection<UserStoryFileDetails> Attachments);

public sealed record WorkflowPhaseDetails(
    string PhaseId,
    string Title,
    int Order,
    bool RequiresApproval,
    bool ExpectsHumanIntervention,
    bool IsApproved,
    bool IsCurrent,
    string State,
    string? ArtifactPath,
    string? OperationLogPath,
    string? ExecutePromptPath,
    string? ApprovePromptPath,
    string? ExecuteSystemPromptPath = null,
    string? ApproveSystemPromptPath = null,
    PhaseExecutionBoundarySummary? ExecutionBoundary = null,
    CaptureExecutionRecord? CaptureRecord = null,
    PhaseExecutionReadiness? ExecutionReadiness = null,
    PhaseExecutionPolicy? ExecutionPolicy = null,
    PhaseExecutionEnvelope? ExecutionEnvelope = null,
    ResolvedHarnessPhaseProfile? HarnessProfile = null,
    SpecPhaseApprovalPolicyDetails? SpecApprovalPolicy = null,
    ReviewPhasePolicyDetails? ReviewPolicy = null,
    ReleaseApprovalPolicyDetails? ReleaseApprovalPolicy = null,
    PrPreparationPolicyDetails? PrPreparationPolicy = null,
    PhaseExecutionInspectionDetails? LatestExecutionInspection = null,
    PhaseRuntimeMetrics? RuntimeMetrics = null);

public sealed record PhaseExecutionBoundarySummary(
    string BoundaryKind,
    bool IsModelBacked,
    string Summary);

public sealed record PhaseExecutionInspectionDetails(
    string? ReceiptPath,
    AutoRefinementAnswerInspectionDetails? AutoRefinementAnswerInspection,
    PhaseExecutionEvidenceRecord? EvidenceRecord,
    RefinementPolicyDetails? RefinementPolicySnapshot,
    RefinementSkillPreselection? RefinementSkillPreselection,
    RefinementGraphScopeRequest? RefinementGraphScopeRequest,
    SpecPhaseApprovalPolicyDetails? SpecApprovalPolicySnapshot,
    ImplementationPhasePolicySnapshot? ImplementationPolicySnapshot,
    ReviewPhasePolicySnapshot? ReviewPolicySnapshot,
    ReleaseApprovalPhasePolicySnapshot? ReleaseApprovalPolicySnapshot,
    ImplementationStructuredEvidence? ImplementationStructuredEvidence,
    ReviewStructuredGateResult? ReviewStructuredGateResult,
    ReleaseApprovalEvidencePack? ReleaseApprovalEvidencePack,
    PrPreparationStructuredEvidence? PrPreparationStructuredEvidence,
    TechnicalDesignContextPack? TechnicalDesignContextPack,
    PhaseExecutionEffectivePrompt? EffectivePrompt,
    PhaseExecutionEffectiveContext? EffectiveContext);

public sealed record SpecPhaseApprovalPolicyDetails(
    string Status,
    bool ApprovalAvailableNow,
    string? ApprovalBlockingReason,
    bool HasSpecArtifact,
    bool SchemaIsValid,
    bool HasUnresolvedApprovalQuestions,
    int UnresolvedApprovalQuestionCount,
    bool DecompositionApprovalPending,
    IReadOnlyCollection<SpecPhaseApprovalRule> ApprovalRules);

public sealed record SpecPhaseApprovalRule(
    string Id,
    string Description,
    string Status,
    bool IsCurrentlySatisfied,
    string? BlockingReason = null,
    string? CurrentStatusMessage = null);

public sealed record CurrentPhaseControls(
    bool CanContinue,
    bool CanApprove,
    bool RequiresApproval,
    string? BlockingReason,
    bool CanRestartFromSource,
    IReadOnlyCollection<string> RegressionTargets,
    IReadOnlyCollection<string> RewindTargets,
    string? ExecutionPhase = null,
    PhaseExecutionReadiness? ExecutionReadiness = null);

public sealed record DecompositionDetails(
    string State,
    string Decision,
    double ComplexityScore,
    double Threshold,
    double Tolerance,
    string Rationale,
    string? ArtifactPath,
    IReadOnlyCollection<UserStoryDecompositionChildDraft> ProposedChildren,
    IReadOnlyCollection<string> CreatedChildUsIds);

public sealed record TimelineEventDetails(
    string TimestampUtc,
    string Code,
    string? Actor,
    string? Phase,
    string? Summary,
    IReadOnlyCollection<string> Artifacts,
    TokenUsage? Usage,
    long? DurationMs,
    PhaseExecutionMetadata? Execution);

public sealed record PhaseIterationDetails(
    string IterationKey,
    int Attempt,
    string PhaseId,
    string TimestampUtc,
    string Code,
    string? Actor,
    string? Summary,
    string OutputArtifactPath,
    string? InputArtifactPath,
    IReadOnlyCollection<string> ContextArtifactPaths,
    string? OperationLogPath,
    string? OperationPrompt,
    TokenUsage? Usage,
    long? DurationMs,
    PhaseExecutionMetadata? Execution);

public sealed record RefinementSessionDetails(
    string Status,
    string Tolerance,
    string? Reason,
    IReadOnlyCollection<RefinementQuestionAnswerDetails> Items,
    RefinementPolicyDetails? Policy = null);

public sealed record RefinementQuestionAnswerDetails(
    int Index,
    string Question,
    string? Answer);

public sealed record ApprovalQuestionDetails(
    int Index,
    string Question,
    string Status,
    bool IsResolved,
    string? Answer,
    string? AnsweredBy,
    string? AnsweredAtUtc);

public sealed record UserStoryFileDetails(
    string Name,
    string Path);

public sealed record PullRequestDetails(
    string Status,
    string Title,
    bool IsDraft,
    int? Number,
    string? Url,
    string? RemoteBranch,
    string? PublishedAtUtc);

public sealed record UserStoryFilesResult(
    string UsId,
    IReadOnlyCollection<UserStoryFileDetails> ContextFiles,
    IReadOnlyCollection<UserStoryFileDetails> Attachments);
