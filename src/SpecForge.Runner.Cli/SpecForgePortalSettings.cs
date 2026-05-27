using System.Text.Json;
using SpecForge.Domain.Application;
using SpecForge.OpenAICompatible;

internal sealed record SpecForgePortalSettings(
    IReadOnlyList<OpenAiCompatibleModelProfile> ModelProfiles,
    IReadOnlyList<OpenAiCompatibleAgentProfile> AgentProfiles,
    OpenAiCompatiblePhaseAgentAssignments? PhaseAgentAssignments,
    string DefaultUser,
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
    int PhaseQualityGateThresholdPercent,
    int RefinementQualityGateMaxRetries,
    int ReviewQualityGateMaxRetries,
    bool KeepBestPhaseArtifactOnQualityRegression,
    bool DestructiveRewindEnabled,
    bool PauseOnFailedReview,
    bool UseSemanticGraphWhenAvailable,
    bool AllowGraphBuildRefreshForTouchedUserStoryScope,
    string WorkflowGraphLayoutMode,
    string WorkflowGraphInitialZoomMode,
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

    public PortalExecutionConfigurationValidation ValidateLinkedExecutionConfiguration()
    {
        if (ModelProfiles.Count == 0 && AgentProfiles.Count == 0)
        {
            return PortalExecutionConfigurationValidation.Valid();
        }

        var normalizedModelProfiles = ModelProfiles
            .Select(static profile => profile with
            {
                Name = profile.Name.Trim(),
                Provider = profile.Provider.Trim(),
                BaseUrl = profile.BaseUrl.Trim(),
                ApiKey = profile.ApiKey.Trim(),
                Model = profile.Model.Trim()
            })
            .ToList();
        var normalizedAgentProfiles = ResolveAgentProfiles()
            .Select(static agent => agent with
            {
                Name = agent.Name.Trim(),
                ModelProfile = agent.ModelProfile.Trim(),
                RepositoryAccess = agent.RepositoryAccess.Trim()
            })
            .ToList();
        var modelProfilesByName = normalizedModelProfiles
            .GroupBy(static profile => profile.Name, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.ToList(), StringComparer.Ordinal);
        var agentProfilesByName = normalizedAgentProfiles
            .GroupBy(static agent => agent.Name, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.ToList(), StringComparer.Ordinal);

        var defaultAgentName = NormalizeOptionalAssignment(PhaseAgentAssignments?.DefaultAgent)
            ?? (normalizedAgentProfiles.Count == 1 ? NormalizeOptionalAssignment(normalizedAgentProfiles[0].Name) : null);
        if (normalizedAgentProfiles.Count > 1
            && defaultAgentName is null
            && !HasExplicitAgentsForAllModelDrivenPhases(PhaseAgentAssignments))
        {
            return PortalExecutionConfigurationValidation.Invalid(
                "A default fallback agent is required when multiple linked agents are configured.");
        }

        var referencedAgentNames = new HashSet<string>(StringComparer.Ordinal);
        AddReferencedAgent(referencedAgentNames, defaultAgentName);
        AddReferencedAgent(referencedAgentNames, ResolveAssignedAgent(PhaseAgentAssignments?.RefinementAgent, defaultAgentName));
        AddReferencedAgent(referencedAgentNames, ResolveAssignedAgent(PhaseAgentAssignments?.SpecAgent, defaultAgentName));
        AddReferencedAgent(referencedAgentNames, ResolveAssignedAgent(PhaseAgentAssignments?.TechnicalDesignAgent, defaultAgentName));
        AddReferencedAgent(referencedAgentNames, ResolveAssignedAgent(PhaseAgentAssignments?.ImplementationAgent, defaultAgentName));
        AddReferencedAgent(referencedAgentNames, ResolveAssignedAgent(PhaseAgentAssignments?.ReviewAgent, defaultAgentName));
        AddReferencedAgent(referencedAgentNames, ResolveAssignedAgent(PhaseAgentAssignments?.ReleaseApprovalAgent, defaultAgentName));
        AddReferencedAgent(referencedAgentNames, ResolveAssignedAgent(PhaseAgentAssignments?.PrPreparationAgent, defaultAgentName));

        if (AutoRefinementAnswersEnabled)
        {
            var autoRefinementAgent = NormalizeOptionalAssignment(AutoRefinementAnswersProfile);
            if (autoRefinementAgent is null)
            {
                return PortalExecutionConfigurationValidation.Invalid(
                    "Model-driven refinement answers require a configured linked agent.");
            }

            AddReferencedAgent(referencedAgentNames, autoRefinementAgent);
        }

        foreach (var agentName in referencedAgentNames)
        {
            if (!agentProfilesByName.TryGetValue(agentName, out var agentMatches) || agentMatches.Count == 0)
            {
                return PortalExecutionConfigurationValidation.Invalid(
                    $"Linked agent '{agentName}' was not configured.");
            }

            if (agentMatches.Count > 1)
            {
                return PortalExecutionConfigurationValidation.Invalid(
                    $"Linked agent '{agentName}' is ambiguous because it is configured more than once.");
            }

            var agent = agentMatches[0];
            if (string.IsNullOrWhiteSpace(agent.ModelProfile))
            {
                return PortalExecutionConfigurationValidation.Invalid(
                    $"Linked agent '{agent.Name}' is missing its model profile.");
            }

            if (!modelProfilesByName.TryGetValue(agent.ModelProfile, out var modelMatches) || modelMatches.Count == 0)
            {
                return PortalExecutionConfigurationValidation.Invalid(
                    $"Linked model profile '{agent.ModelProfile}' for agent '{agent.Name}' was not configured.");
            }

            if (modelMatches.Count > 1)
            {
                return PortalExecutionConfigurationValidation.Invalid(
                    $"Linked model profile '{agent.ModelProfile}' is ambiguous because it is configured more than once.");
            }

            var model = modelMatches[0];
            var provider = NormalizeProviderKind(model.Provider);
            if (!IsSupportedProviderKind(provider))
            {
                return PortalExecutionConfigurationValidation.Invalid(
                    $"Linked model profile '{model.Name}' uses unsupported provider '{model.Provider}'.");
            }

            if (!IsNativeCliProvider(provider) && string.IsNullOrWhiteSpace(model.BaseUrl))
            {
                return PortalExecutionConfigurationValidation.Invalid(
                    $"Linked model profile '{model.Name}' is missing its base URL.");
            }

            if (!IsNativeCliProvider(provider) && string.IsNullOrWhiteSpace(model.Model))
            {
                return PortalExecutionConfigurationValidation.Invalid(
                    $"Linked model profile '{model.Name}' is missing its model.");
            }

            if (!IsNativeCliProvider(provider)
                && RequiresApiKey(model.BaseUrl)
                && string.IsNullOrWhiteSpace(model.ApiKey))
            {
                return PortalExecutionConfigurationValidation.Invalid(
                    $"Linked model profile '{model.Name}' needs an API key for its remote base URL.");
            }
        }

        return PortalExecutionConfigurationValidation.Valid();
    }

    private static bool HasExplicitAgentsForAllModelDrivenPhases(OpenAiCompatiblePhaseAgentAssignments? assignments) =>
        !string.IsNullOrWhiteSpace(assignments?.RefinementAgent)
        && !string.IsNullOrWhiteSpace(assignments?.SpecAgent)
        && !string.IsNullOrWhiteSpace(assignments?.TechnicalDesignAgent)
        && !string.IsNullOrWhiteSpace(assignments?.ImplementationAgent)
        && !string.IsNullOrWhiteSpace(assignments?.ReviewAgent)
        && !string.IsNullOrWhiteSpace(assignments?.ReleaseApprovalAgent)
        && !string.IsNullOrWhiteSpace(assignments?.PrPreparationAgent);

    private static string? NormalizeOptionalAssignment(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? ResolveAssignedAgent(string? explicitAssignment, string? defaultAgentName) =>
        NormalizeOptionalAssignment(explicitAssignment) ?? defaultAgentName;

    private static void AddReferencedAgent(ISet<string> referencedAgentNames, string? agentName)
    {
        if (!string.IsNullOrWhiteSpace(agentName))
        {
            referencedAgentNames.Add(agentName);
        }
    }

    private static string NormalizeProviderKind(string? provider) =>
        string.IsNullOrWhiteSpace(provider) ? "openai-compatible" : provider.Trim().ToLowerInvariant();

    private static bool IsSupportedProviderKind(string provider) =>
        provider is "openai-compatible" or "codex" or "copilot" or "claude";

    private static bool IsNativeCliProvider(string provider) =>
        provider is "codex" or "copilot" or "claude";

    private static bool RequiresApiKey(string? baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.IsLoopback)
        {
            return false;
        }

        var host = uri.Host.Trim();
        return !host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
               && !host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
               && !host.Equals("::1", StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed record PortalExecutionConfigurationValidation(
    bool IsValid,
    string? Message)
{
    public static PortalExecutionConfigurationValidation Valid() => new(true, null);

    public static PortalExecutionConfigurationValidation Invalid(string message) => new(false, message);
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

        return Deserialize(File.ReadAllText(path), workspaceRoot);
    }

    public static SpecForgePortalSettings LoadOrDefault(string workspaceRoot) =>
        Load(workspaceRoot) ?? SaveDefault(workspaceRoot);

    public static SpecForgePortalSettings Deserialize(string payload, string? workspaceRoot = null)
    {
        using var document = JsonDocument.Parse(payload);
        var settings = JsonSerializer.Deserialize<SpecForgePortalSettings>(payload, JsonOptions)
            ?? throw new InvalidOperationException("Configuration payload could not be parsed.");

        if (!document.RootElement.TryGetProperty("defaultUser", out _))
        {
            settings = settings with
            {
                DefaultUser = string.IsNullOrWhiteSpace(workspaceRoot)
                    ? string.Empty
                    : WorkspaceActorResolver.TryDetectGitUser(workspaceRoot) ?? string.Empty
            };
        }
        else
        {
            settings = settings with { DefaultUser = WorkspaceActorResolver.NormalizeConfiguredUser(settings.DefaultUser) };
        }

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

        if (!document.RootElement.TryGetProperty("workflowGraphLayoutMode", out _)
            || !string.Equals(settings.WorkflowGraphLayoutMode, "horizontal", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(settings.WorkflowGraphLayoutMode, "vertical", StringComparison.OrdinalIgnoreCase))
        {
            settings = settings with { WorkflowGraphLayoutMode = "vertical" };
        }
        else
        {
            settings = settings with
            {
                WorkflowGraphLayoutMode = string.Equals(settings.WorkflowGraphLayoutMode, "horizontal", StringComparison.OrdinalIgnoreCase)
                    ? "horizontal"
                    : "vertical"
            };
        }

        if (!document.RootElement.TryGetProperty("workflowGraphInitialZoomMode", out _)
            || !string.Equals(settings.WorkflowGraphInitialZoomMode, "actual-size", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(settings.WorkflowGraphInitialZoomMode, "fit-width", StringComparison.OrdinalIgnoreCase))
        {
            settings = settings with { WorkflowGraphInitialZoomMode = "fit-width" };
        }
        else
        {
            settings = settings with
            {
                WorkflowGraphInitialZoomMode = string.Equals(settings.WorkflowGraphInitialZoomMode, "fit-width", StringComparison.OrdinalIgnoreCase)
                    ? "fit-width"
                    : "actual-size"
            };
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
            settings = settings with { MaxRefinementCycles = 5 };
        }

        if (!document.RootElement.TryGetProperty("maxImplementationReviewCycles", out _) || settings.MaxImplementationReviewCycles <= 0)
        {
            settings = settings with { MaxImplementationReviewCycles = 5 };
        }

        if (!document.RootElement.TryGetProperty("phaseQualityGateThresholdPercent", out _))
        {
            settings = settings with { PhaseQualityGateThresholdPercent = 85 };
        }
        else
        {
            settings = settings with { PhaseQualityGateThresholdPercent = Math.Clamp(settings.PhaseQualityGateThresholdPercent, 0, 100) };
        }

        if (!document.RootElement.TryGetProperty("refinementQualityGateMaxRetries", out _) || settings.RefinementQualityGateMaxRetries <= 0)
        {
            settings = settings with { RefinementQualityGateMaxRetries = 5 };
        }

        if (!document.RootElement.TryGetProperty("reviewQualityGateMaxRetries", out _) || settings.ReviewQualityGateMaxRetries <= 0)
        {
            settings = settings with { ReviewQualityGateMaxRetries = 3 };
        }

        if (!document.RootElement.TryGetProperty("keepBestPhaseArtifactOnQualityRegression", out _))
        {
            settings = settings with { KeepBestPhaseArtifactOnQualityRegression = true };
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
            DefaultUser: string.Empty,
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
            MaxRefinementCycles: 5,
            MaxImplementationReviewCycles: 5,
            PhaseQualityGateThresholdPercent: 85,
            RefinementQualityGateMaxRetries: 5,
            ReviewQualityGateMaxRetries: 3,
            KeepBestPhaseArtifactOnQualityRegression: true,
            DestructiveRewindEnabled: false,
            PauseOnFailedReview: true,
            UseSemanticGraphWhenAvailable: true,
            AllowGraphBuildRefreshForTouchedUserStoryScope: false,
            WorkflowGraphLayoutMode: "vertical",
            WorkflowGraphInitialZoomMode: "fit-width",
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
        var settings = CreateDefault() with
        {
            DefaultUser = WorkspaceActorResolver.TryDetectGitUser(workspaceRoot) ?? string.Empty
        };
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
