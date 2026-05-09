using System.Text.Json;
using SpecForge.Domain.Application;

namespace SpecForge.OpenAICompatible;

public static class OpenAiCompatiblePhaseExecutionProviderFactory
{
    public const string ModelProfilesJsonEnvVar = "SPECFORGE_OPENAI_MODEL_PROFILES_JSON";
    public const string AgentProfilesJsonEnvVar = "SPECFORGE_OPENAI_AGENT_PROFILES_JSON";
    public const string PhaseAgentAssignmentsJsonEnvVar = "SPECFORGE_OPENAI_PHASE_AGENT_ASSIGNMENTS_JSON";
    public const string TechnicalDesignSubagentsEnabledEnvVar = "SPECFORGE_TECHNICAL_DESIGN_SUBAGENTS_ENABLED";
    public const string ReviewSubagentsEnabledEnvVar = "SPECFORGE_REVIEW_SUBAGENTS_ENABLED";
    public const string RefinementToleranceEnvVar = "SPECFORGE_REFINEMENT_TOLERANCE";
    public const string LegacyRefinementToleranceEnvVar = "SPECFORGE_CAPTURE_TOLERANCE";
    public const string MvpRigorEnvVar = "SPECFORGE_MVP_RIGOR";
    public const string ReviewToleranceEnvVar = "SPECFORGE_REVIEW_TOLERANCE";
    public const string ReviewEvidencePolicyEnvVar = "SPECFORGE_REVIEW_EVIDENCE_POLICY";
    public const string AutoRefinementAnswersEnabledEnvVar = "SPECFORGE_AUTO_REFINEMENT_ANSWERS_ENABLED";
    public const string LegacyAutoRefinementAnswersEnabledEnvVar = "SPECFORGE_AUTO_CLARIFICATION_ANSWERS_ENABLED";
    public const string AutoRefinementAnswersProfileEnvVar = "SPECFORGE_AUTO_REFINEMENT_ANSWERS_PROFILE";
    public const string LegacyAutoRefinementAnswersProfileEnvVar = "SPECFORGE_AUTO_CLARIFICATION_ANSWERS_PROFILE";
    public const string ReviewLearningEnabledEnvVar = "SPECFORGE_REVIEW_LEARNING_ENABLED";
    public const string ReviewLearningSkillPathEnvVar = "SPECFORGE_REVIEW_LEARNING_SKILL_PATH";
    public const string SystemPromptEnvVar = "SPECFORGE_OPENAI_SYSTEM_PROMPT";
    public const string TimeoutSecondsEnvVar = "SPECFORGE_OPENAI_TIMEOUT_SECONDS";

    private static readonly IReadOnlySet<string> BridgeableProviderKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        "openai-compatible",
        "codex",
        "copilot",
        "claude"
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly TimeSpan DefaultOpenAiTimeout = TimeSpan.FromMinutes(10);
    private const string OpenAiCompatibleKind = "openai-compatible";

    public static IPhaseExecutionProvider Create(Func<string, string?>? fallback = null)
    {
        fallback ??= static _ => null;
        var modelProfiles = ReadModelProfiles(ReadSetting(ModelProfilesJsonEnvVar, fallback), ModelProfilesJsonEnvVar);
        if (modelProfiles.Count == 0)
        {
            return new DeterministicPhaseExecutionProvider();
        }

        var providerKinds = modelProfiles
            .Select(static profile => NormalizeProviderKind(profile.Provider))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (!providerKinds.All(static providerKind => BridgeableProviderKinds.Contains(providerKind)))
        {
            throw new InvalidOperationException(
                $"Unsupported model profile provider set '{string.Join(", ", providerKinds)}'. Valid values: '{OpenAiCompatibleKind}', 'codex', 'copilot', 'claude'.");
        }

        return new OpenAiCompatiblePhaseExecutionProvider(
            new HttpClient { Timeout = ReadOpenAiTimeout(ReadSetting(TimeoutSecondsEnvVar, fallback)) },
            new OpenAiCompatibleProviderOptions(
                SystemPrompt: ReadSetting(SystemPromptEnvVar, fallback)
                    ?? "You generate SpecForge workflow artifacts. Follow the phase-specific Markdown output contract exactly and do not return JSON.",
                RefinementTolerance: ReadSetting(RefinementToleranceEnvVar, fallback)
                    ?? ReadSetting(LegacyRefinementToleranceEnvVar, fallback)
                    ?? "balanced",
                MvpRigor: ReadSetting(MvpRigorEnvVar, fallback) ?? "medium",
                ReviewTolerance: ReadSetting(ReviewToleranceEnvVar, fallback) ?? "balanced",
                ReviewEvidencePolicy: ReadSetting(ReviewEvidencePolicyEnvVar, fallback) ?? "balanced",
                AutoRefinementAnswersEnabled: IsEnabled(
                    ReadSetting(AutoRefinementAnswersEnabledEnvVar, fallback)
                        ?? ReadSetting(LegacyAutoRefinementAnswersEnabledEnvVar, fallback)),
                AutoRefinementAnswersProfile: NormalizeOptional(
                    ReadSetting(AutoRefinementAnswersProfileEnvVar, fallback)
                        ?? ReadSetting(LegacyAutoRefinementAnswersProfileEnvVar, fallback)),
                ReviewLearningEnabled: !string.Equals(
                    ReadSetting(ReviewLearningEnabledEnvVar, fallback),
                    "false",
                    StringComparison.OrdinalIgnoreCase),
                ReviewLearningSkillPath: NormalizeOptional(ReadSetting(ReviewLearningSkillPathEnvVar, fallback))
                    ?? ".codex/skills/sdd-phase-agents/SKILL.md",
                ModelProfiles: modelProfiles,
                AgentProfiles: ReadJsonList<OpenAiCompatibleAgentProfile>(ReadSetting(AgentProfilesJsonEnvVar, fallback), AgentProfilesJsonEnvVar),
                PhaseAgentAssignments: ReadJson<OpenAiCompatiblePhaseAgentAssignments>(
                    ReadSetting(PhaseAgentAssignmentsJsonEnvVar, fallback),
                    PhaseAgentAssignmentsJsonEnvVar),
                PhaseSubagents: new OpenAiCompatiblePhaseSubagentOptions(
                    TechnicalDesignEnabled: IsEnabled(ReadSetting(TechnicalDesignSubagentsEnabledEnvVar, fallback)),
                    ReviewEnabled: IsEnabled(ReadSetting(ReviewSubagentsEnabledEnvVar, fallback)))));
    }

    private static string? ReadSetting(string key, Func<string, string?> fallback)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? NormalizeOptional(fallback(key)) : value;
    }

    private static IReadOnlyList<OpenAiCompatibleModelProfile> ReadModelProfiles(string? payload, string settingName) =>
        ReadJsonList<OpenAiCompatibleModelProfile>(payload, settingName)
            .Select(static profile => profile with { Provider = NormalizeProviderKind(profile.Provider) })
            .ToList();

    private static IReadOnlyList<T> ReadJsonList<T>(string? payload, string settingName)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<T>>(payload, JsonOptions)
               ?? throw new InvalidOperationException($"Setting '{settingName}' could not be parsed.");
    }

    private static T? ReadJson<T>(string? payload, string settingName)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(payload, JsonOptions)
               ?? throw new InvalidOperationException($"Setting '{settingName}' could not be parsed.");
    }

    private static TimeSpan ReadOpenAiTimeout(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return DefaultOpenAiTimeout;
        }

        if (int.TryParse(configured, out var seconds) && seconds > 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        throw new InvalidOperationException(
            $"Environment variable '{TimeoutSecondsEnvVar}' must be a positive integer number of seconds.");
    }

    private static bool IsEnabled(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeProviderKind(string? providerKind) =>
        string.IsNullOrWhiteSpace(providerKind)
            ? OpenAiCompatibleKind
            : providerKind.Trim().ToLowerInvariant();
}
