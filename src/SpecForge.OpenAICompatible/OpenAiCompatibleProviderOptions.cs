namespace SpecForge.OpenAICompatible;

public sealed record OpenAiCompatibleModelProfile(
    string Name,
    string Provider,
    string BaseUrl,
    string ApiKey,
    string Model,
    string? ReasoningEffort = null,
    string RepositoryAccess = "none");

public sealed record OpenAiCompatibleAgentProfile(
    string Name,
    string Role,
    string ModelProfile,
    string Instructions,
    string RepositoryAccess,
    string? ReasoningEffort = null);

public sealed record OpenAiCompatiblePhaseAgentAssignments(
    string? DefaultAgent = null,
    string? RefinementAgent = null,
    string? SpecAgent = null,
    string? TechnicalDesignAgent = null,
    string? ImplementationAgent = null,
    string? ReviewAgent = null,
    string? ReleaseApprovalAgent = null,
    string? PrPreparationAgent = null);

public sealed record OpenAiCompatiblePhaseSubagentOptions(
    bool TechnicalDesignEnabled = false,
    bool ReviewEnabled = false);

public sealed record OpenAiCompatibleProviderOptions(
    string? SystemPrompt = null,
    string RefinementTolerance = "balanced",
    string MvpRigor = "medium",
    string ReviewTolerance = "balanced",
    string ReviewEvidencePolicy = "balanced",
    bool AutoRefinementAnswersEnabled = false,
    string? AutoRefinementAnswersProfile = null,
    bool PhaseSkillUsageReportingEnabled = true,
    bool ReviewLearningEnabled = true,
    string ReviewLearningSkillPath = ".codex/skills/sdd-phase-agents/SKILL.md",
    IReadOnlyList<OpenAiCompatibleModelProfile>? ModelProfiles = null,
    IReadOnlyList<OpenAiCompatibleAgentProfile>? AgentProfiles = null,
    OpenAiCompatiblePhaseAgentAssignments? PhaseAgentAssignments = null,
    OpenAiCompatiblePhaseSubagentOptions? PhaseSubagents = null);
