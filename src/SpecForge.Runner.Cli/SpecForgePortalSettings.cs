using System.Text.Json;
using SpecForge.Domain.Application;
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
    int MaxRefinementCycles,
    int MaxImplementationReviewCycles,
    bool DestructiveRewindEnabled,
    bool PauseOnFailedReview,
    bool UseSemanticGraphWhenAvailable,
    bool AllowGraphBuildRefreshForTouchedUserStoryScope,
    string DefaultHarnessProfile,
    HarnessPhaseProfileAssignments? PhaseHarnessProfiles,
    string HarnessProfileAuthority,
    string HarnessProfileLockMode,
    IReadOnlyCollection<string> LockedHarnessPhaseIds,
    bool AllowPerUserStoryHarnessProfileOverrides,
    bool ReviewLearningEnabled,
    string ReviewLearningSkillPath,
    bool CompletedUsLockOnCompleted,
    bool DecompositionEnabled,
    double DecompositionThreshold,
    double DecompositionTolerance,
    int DecompositionMaxChildren)
{
    public static readonly IReadOnlyList<OpenAiCompatibleAgentProfile> RecommendedBootstrapAgentProfiles =
    [
        new(
            Name: "planner",
            Role: "planner",
            ModelProfile: string.Empty,
            Instructions: "Focus on requirements, workflow consistency, and repository-aware planning.",
            RepositoryAccess: "read"),
        new(
            Name: "implementer",
            Role: "implementer",
            ModelProfile: string.Empty,
            Instructions: "Implement approved technical designs with focused code changes and matching tests.",
            RepositoryAccess: "read-write"),
        new(
            Name: "reviewer",
            Role: "reviewer",
            ModelProfile: string.Empty,
            Instructions: "Review implementation changes for correctness, regressions, missing tests, and release risk.",
            RepositoryAccess: "read"),
        new(
            Name: "release-preparer",
            Role: "release-preparer",
            ModelProfile: string.Empty,
            Instructions: "Prepare release and pull request artifacts from repository evidence.",
            RepositoryAccess: "read")
    ];

    public static readonly OpenAiCompatiblePhaseAgentAssignments RecommendedBootstrapPhaseAgentAssignments = new(
        DefaultAgent: "planner",
        RefinementAgent: "planner",
        SpecAgent: "planner",
        TechnicalDesignAgent: "planner",
        ImplementationAgent: "implementer",
        ReviewAgent: "reviewer",
        ReleaseApprovalAgent: "release-preparer",
        PrPreparationAgent: "release-preparer");

    public IReadOnlyList<OpenAiCompatibleAgentProfile> ResolveAgentProfiles() =>
        AgentProfiles.Count > 0
            ? AgentProfiles
            : ModelProfiles.Count > 0
                ? ModelProfiles
                .Select(static profile => new OpenAiCompatibleAgentProfile(
                    Name: profile.Name,
                    Role: profile.Name,
                    ModelProfile: profile.Name,
                    Instructions: string.Empty,
                    RepositoryAccess: profile.RepositoryAccess,
                    ReasoningEffort: profile.ReasoningEffort))
                .ToList()
                : RecommendedBootstrapAgentProfiles;
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
        Load(workspaceRoot) ?? SaveDefault(workspaceRoot);

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

        if (!document.RootElement.TryGetProperty("useSemanticGraphWhenAvailable", out _))
        {
            settings = settings with { UseSemanticGraphWhenAvailable = true };
        }

        if (!document.RootElement.TryGetProperty("allowGraphBuildRefreshForTouchedUserStoryScope", out _))
        {
            settings = settings with { AllowGraphBuildRefreshForTouchedUserStoryScope = false };
        }

        if (!document.RootElement.TryGetProperty("reviewSubagentsEnabled", out _))
        {
            settings = settings with { ReviewSubagentsEnabled = true };
        }

        if (!document.RootElement.TryGetProperty("defaultHarnessProfile", out _))
        {
            settings = settings with { DefaultHarnessProfile = HarnessProfileCatalog.BalancedProfileKey };
        }

        if (!document.RootElement.TryGetProperty("harnessProfileAuthority", out _))
        {
            settings = settings with { HarnessProfileAuthority = "workspace" };
        }

        if (!document.RootElement.TryGetProperty("harnessProfileLockMode", out _))
        {
            settings = settings with { HarnessProfileLockMode = "none" };
        }

        if (!document.RootElement.TryGetProperty("lockedHarnessPhaseIds", out _))
        {
            settings = settings with { LockedHarnessPhaseIds = [] };
        }

        if (!document.RootElement.TryGetProperty("allowPerUserStoryHarnessProfileOverrides", out _))
        {
            settings = settings with { AllowPerUserStoryHarnessProfileOverrides = true };
        }

        if (!document.RootElement.TryGetProperty("autoPlayEnabled", out _))
        {
            settings = settings with { AutoPlayEnabled = true };
        }

        if (!document.RootElement.TryGetProperty("autoReviewEnabled", out _))
        {
            settings = settings with { AutoReviewEnabled = true };
        }

        if (!document.RootElement.TryGetProperty("maxRefinementCycles", out _) || settings.MaxRefinementCycles <= 0)
        {
            settings = settings with { MaxRefinementCycles = 3 };
        }

        if (!document.RootElement.TryGetProperty("maxImplementationReviewCycles", out _) || settings.MaxImplementationReviewCycles <= 0)
        {
            settings = settings with { MaxImplementationReviewCycles = 5 };
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

        if (settings.AgentProfiles.Count == 0 && settings.ModelProfiles.Count == 0)
        {
            settings = settings with { AgentProfiles = SpecForgePortalSettings.RecommendedBootstrapAgentProfiles };

            if (!HasAnyPhaseAgentAssignment(settings.PhaseAgentAssignments))
            {
                settings = settings with
                {
                    PhaseAgentAssignments = SpecForgePortalSettings.RecommendedBootstrapPhaseAgentAssignments
                };
            }
        }

        settings = settings with
        {
            DefaultHarnessProfile = HarnessProfileCatalog.NormalizeProfileKey(settings.DefaultHarnessProfile),
            PhaseHarnessProfiles = (settings.PhaseHarnessProfiles ?? HarnessProfileRuntimeSettings.Default.PhaseProfiles) with
            {
                DefaultProfile = HarnessProfileCatalog.NormalizeOptionalProfileKey(settings.PhaseHarnessProfiles?.DefaultProfile),
                CaptureProfile = HarnessProfileCatalog.NormalizeOptionalProfileKey(settings.PhaseHarnessProfiles?.CaptureProfile),
                RefinementProfile = HarnessProfileCatalog.NormalizeOptionalProfileKey(settings.PhaseHarnessProfiles?.RefinementProfile),
                SpecProfile = HarnessProfileCatalog.NormalizeOptionalProfileKey(settings.PhaseHarnessProfiles?.SpecProfile),
                TechnicalDesignProfile = HarnessProfileCatalog.NormalizeOptionalProfileKey(settings.PhaseHarnessProfiles?.TechnicalDesignProfile),
                ImplementationProfile = HarnessProfileCatalog.NormalizeOptionalProfileKey(settings.PhaseHarnessProfiles?.ImplementationProfile),
                ReviewProfile = HarnessProfileCatalog.NormalizeOptionalProfileKey(settings.PhaseHarnessProfiles?.ReviewProfile),
                ReleaseApprovalProfile = HarnessProfileCatalog.NormalizeOptionalProfileKey(settings.PhaseHarnessProfiles?.ReleaseApprovalProfile),
                PrPreparationProfile = HarnessProfileCatalog.NormalizeOptionalProfileKey(settings.PhaseHarnessProfiles?.PrPreparationProfile)
            },
            HarnessProfileAuthority = HarnessProfileCatalog.NormalizeAuthority(settings.HarnessProfileAuthority),
            HarnessProfileLockMode = HarnessProfileCatalog.NormalizeLockMode(settings.HarnessProfileLockMode),
            LockedHarnessPhaseIds = settings.LockedHarnessPhaseIds
                .Select(HarnessProfileCatalog.NormalizePhaseSlug)
                .Where(static phaseId => phaseId is not null)
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToArray()
        };

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
            AgentProfiles: SpecForgePortalSettings.RecommendedBootstrapAgentProfiles,
            PhaseAgentAssignments: SpecForgePortalSettings.RecommendedBootstrapPhaseAgentAssignments,
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
            MaxRefinementCycles: 3,
            MaxImplementationReviewCycles: 5,
            DestructiveRewindEnabled: false,
            PauseOnFailedReview: true,
            UseSemanticGraphWhenAvailable: true,
            AllowGraphBuildRefreshForTouchedUserStoryScope: false,
            DefaultHarnessProfile: HarnessProfileCatalog.BalancedProfileKey,
            PhaseHarnessProfiles: HarnessProfileRuntimeSettings.Default.PhaseProfiles,
            HarnessProfileAuthority: "workspace",
            HarnessProfileLockMode: "none",
            LockedHarnessPhaseIds: [],
            AllowPerUserStoryHarnessProfileOverrides: true,
            ReviewLearningEnabled: true,
            ReviewLearningSkillPath: ".codex/skills/sdd-phase-agents/SKILL.md",
            CompletedUsLockOnCompleted: false,
            DecompositionEnabled: true,
            DecompositionThreshold: 0.60,
            DecompositionTolerance: 0.10,
            DecompositionMaxChildren: 5);

    private static SpecForgePortalSettings SaveDefault(string workspaceRoot)
    {
        var settings = CreateDefault();
        Save(workspaceRoot, settings);

        return settings;
    }

    private static string GetSettingsPath(string workspaceRoot) =>
        Path.Combine(workspaceRoot, SettingsPath);

    private static bool HasAnyPhaseAgentAssignment(OpenAiCompatiblePhaseAgentAssignments? assignments) =>
        !string.IsNullOrWhiteSpace(assignments?.DefaultAgent)
        || !string.IsNullOrWhiteSpace(assignments?.RefinementAgent)
        || !string.IsNullOrWhiteSpace(assignments?.SpecAgent)
        || !string.IsNullOrWhiteSpace(assignments?.TechnicalDesignAgent)
        || !string.IsNullOrWhiteSpace(assignments?.ImplementationAgent)
        || !string.IsNullOrWhiteSpace(assignments?.ReviewAgent)
        || !string.IsNullOrWhiteSpace(assignments?.ReleaseApprovalAgent)
        || !string.IsNullOrWhiteSpace(assignments?.PrPreparationAgent);
}
