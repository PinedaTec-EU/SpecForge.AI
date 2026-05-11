using System.Text.Json;
using SpecForge.OpenAICompatible;

internal sealed record SpecForgePortalSettings(
    IReadOnlyList<OpenAiCompatibleModelProfile> ModelProfiles,
    IReadOnlyList<OpenAiCompatibleAgentProfile> AgentProfiles,
    OpenAiCompatiblePhaseAgentAssignments? PhaseAgentAssignments,
    string RefinementTolerance,
    string MvpRigor,
    string ReviewTolerance,
    string ReviewEvidencePolicy,
    bool TechnicalDesignSubagentsEnabled,
    bool ReviewSubagentsEnabled,
    bool AutoRefinementAnswersEnabled,
    string? AutoRefinementAnswersProfile,
    bool AutoPlayEnabled,
    bool AutoReviewEnabled,
    int MaxImplementationReviewCycles,
    bool DestructiveRewindEnabled,
    bool PauseOnFailedReview,
    bool ReviewLearningEnabled,
    string ReviewLearningSkillPath,
    bool CompletedUsLockOnCompleted,
    bool DecompositionEnabled,
    double DecompositionThreshold,
    double DecompositionTolerance,
    int DecompositionMaxChildren)
{
    public IReadOnlyList<OpenAiCompatibleAgentProfile> ResolveAgentProfiles() =>
        AgentProfiles.Count > 0
            ? AgentProfiles
            : ModelProfiles
                .Select(static profile => new OpenAiCompatibleAgentProfile(
                    Name: profile.Name,
                    Role: profile.Name,
                    ModelProfile: profile.Name,
                    Instructions: string.Empty,
                    RepositoryAccess: profile.RepositoryAccess,
                    ReasoningEffort: profile.ReasoningEffort))
                .ToList();
}

internal static class SpecForgePortalSettingsStore
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private const string SettingsPath = ".specs/configuration/settings.json";

    public static SpecForgePortalSettings? Load(string workspaceRoot)
    {
        var path = GetSettingsPath(workspaceRoot);
        if (!File.Exists(path))
        {
            return null;
        }

        return Deserialize(File.ReadAllText(path));
    }

    public static SpecForgePortalSettings LoadOrDefault(string workspaceRoot) =>
        Load(workspaceRoot) ?? CreateDefault();

    public static SpecForgePortalSettings Deserialize(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var settings = JsonSerializer.Deserialize<SpecForgePortalSettings>(payload, JsonOptions)
            ?? throw new InvalidOperationException("Configuration payload could not be parsed.");

        if (!document.RootElement.TryGetProperty("reviewLearningEnabled", out _))
        {
            settings = settings with { ReviewLearningEnabled = true };
        }

        if (!document.RootElement.TryGetProperty("mvpRigor", out _) || string.IsNullOrWhiteSpace(settings.MvpRigor))
        {
            settings = settings with { MvpRigor = "medium" };
        }

        if (!document.RootElement.TryGetProperty("pauseOnFailedReview", out _))
        {
            settings = settings with { PauseOnFailedReview = true };
        }

        if (!document.RootElement.TryGetProperty("reviewSubagentsEnabled", out _))
        {
            settings = settings with { ReviewSubagentsEnabled = true };
        }

        if (!document.RootElement.TryGetProperty("autoPlayEnabled", out _))
        {
            settings = settings with { AutoPlayEnabled = true };
        }

        if (!document.RootElement.TryGetProperty("autoReviewEnabled", out _))
        {
            settings = settings with { AutoReviewEnabled = true };
        }

        if (!document.RootElement.TryGetProperty("decompositionEnabled", out _))
        {
            settings = settings with { DecompositionEnabled = true };
        }

        if (!document.RootElement.TryGetProperty("decompositionThreshold", out _) || settings.DecompositionThreshold <= 0)
        {
            settings = settings with { DecompositionThreshold = 0.60 };
        }

        if (!document.RootElement.TryGetProperty("decompositionTolerance", out _) || settings.DecompositionTolerance < 0)
        {
            settings = settings with { DecompositionTolerance = 0.10 };
        }

        if (!document.RootElement.TryGetProperty("decompositionMaxChildren", out _) || settings.DecompositionMaxChildren <= 0)
        {
            settings = settings with { DecompositionMaxChildren = 5 };
        }

        return settings;
    }

    public static void Save(string workspaceRoot, SpecForgePortalSettings settings)
    {
        var path = GetSettingsPath(workspaceRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, JsonOptions));
    }

    private static SpecForgePortalSettings CreateDefault() =>
        new(
            ModelProfiles: [],
            AgentProfiles: [],
            PhaseAgentAssignments: new OpenAiCompatiblePhaseAgentAssignments(),
            RefinementTolerance: "balanced",
            MvpRigor: "medium",
            ReviewTolerance: "balanced",
            ReviewEvidencePolicy: "balanced",
            TechnicalDesignSubagentsEnabled: false,
            ReviewSubagentsEnabled: true,
            AutoRefinementAnswersEnabled: false,
            AutoRefinementAnswersProfile: null,
            AutoPlayEnabled: true,
            AutoReviewEnabled: true,
            MaxImplementationReviewCycles: 5,
            DestructiveRewindEnabled: false,
            PauseOnFailedReview: true,
            ReviewLearningEnabled: true,
            ReviewLearningSkillPath: ".codex/skills/sdd-phase-agents/SKILL.md",
            CompletedUsLockOnCompleted: false,
            DecompositionEnabled: true,
            DecompositionThreshold: 0.60,
            DecompositionTolerance: 0.10,
            DecompositionMaxChildren: 5);

    private static string GetSettingsPath(string workspaceRoot) =>
        Path.Combine(workspaceRoot, SettingsPath);
}
