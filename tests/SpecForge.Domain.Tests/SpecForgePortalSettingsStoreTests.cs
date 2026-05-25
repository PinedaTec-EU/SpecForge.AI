using SpecForge.OpenAICompatible;

namespace SpecForge.Domain.Tests;

public sealed class SpecForgePortalSettingsStoreTests : IDisposable
{
    private readonly string workspaceRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public void LoadOrDefault_ReturnsExpectedWorkflowDefaults()
    {
        var settings = SpecForgePortalSettingsStore.LoadOrDefault(workspaceRoot);

        Assert.Empty(settings.ModelProfiles);
        Assert.Equal(
            ["planner", "implementer", "reviewer", "release-preparer"],
            settings.AgentProfiles.Select(static agent => agent.Name));
        Assert.Equal("planner", settings.PhaseAgentAssignments?.DefaultAgent);
        Assert.Equal("implementer", settings.PhaseAgentAssignments?.ImplementationAgent);
        Assert.True(File.Exists(Path.Combine(workspaceRoot, ".specs", "configuration", "settings.json")));
        Assert.Equal("balanced", settings.RefinementTolerance);
        Assert.Equal("medium", settings.MvpRigor);
        Assert.True(settings.ReviewSubagentsEnabled);
        Assert.False(settings.AutoRefinementAnswersEnabled);
        Assert.True(settings.AutoPlayEnabled);
        Assert.True(settings.AutoReviewEnabled);
        Assert.Equal(5, settings.MaxRefinementCycles);
        Assert.Equal(5, settings.MaxImplementationReviewCycles);
        Assert.True(settings.PauseOnFailedReview);
        Assert.True(settings.UseSemanticGraphWhenAvailable);
        Assert.False(settings.AllowGraphBuildRefreshForTouchedUserStoryScope);
        Assert.Equal("balanced", settings.DefaultHarnessProfile);
        Assert.Equal("workspace", settings.HarnessProfileAuthority);
        Assert.Equal("none", settings.HarnessProfileLockMode);
        Assert.Empty(settings.LockedHarnessPhaseIds);
        Assert.True(settings.AllowPerUserStoryHarnessProfileOverrides);
        Assert.True(settings.ReviewLearningEnabled);
        Assert.False(settings.CompletedUsLockOnCompleted);
        Assert.Equal(string.Empty, settings.DefaultUser);
    }

    [Fact]
    public void LoadOrDefault_BootstrapsDefaultUserFromGitWhenAvailable()
    {
        InitializeGitWorkspace();

        var settings = SpecForgePortalSettingsStore.LoadOrDefault(workspaceRoot);

        Assert.Equal("specforge-tests", settings.DefaultUser);
    }

    [Fact]
    public void Deserialize_AppliesMigrationDefaultsForLegacyPayloads()
    {
        var payload = """
            {
              "modelProfiles": [],
              "agentProfiles": [],
              "phaseAgentAssignments": {},
              "refinementTolerance": "balanced",
              "mvpRigor": "",
              "reviewTolerance": "balanced",
              "reviewEvidencePolicy": "balanced",
              "technicalDesignSubagentsEnabled": false,
              "autoRefinementAnswersEnabled": false,
              "autoRefinementAnswersProfile": null,
              "destructiveRewindEnabled": false,
              "maxRefinementCycles": 5,
              "maxImplementationReviewCycles": 3,
              "reviewLearningSkillPath": ".codex/skills/sdd-phase-agents/SKILL.md",
              "completedUsLockOnCompleted": false
            }
            """;

        var settings = SpecForgePortalSettingsStore.Deserialize(payload);

        Assert.Equal("medium", settings.MvpRigor);
        Assert.True(settings.ReviewSubagentsEnabled);
        Assert.True(settings.AutoPlayEnabled);
        Assert.True(settings.AutoReviewEnabled);
        Assert.True(settings.PauseOnFailedReview);
        Assert.True(settings.UseSemanticGraphWhenAvailable);
        Assert.False(settings.AllowGraphBuildRefreshForTouchedUserStoryScope);
        Assert.Equal("balanced", settings.DefaultHarnessProfile);
        Assert.Equal("workspace", settings.HarnessProfileAuthority);
        Assert.Equal("none", settings.HarnessProfileLockMode);
        Assert.Empty(settings.LockedHarnessPhaseIds);
        Assert.True(settings.AllowPerUserStoryHarnessProfileOverrides);
        Assert.True(settings.ReviewLearningEnabled);
        Assert.Equal(string.Empty, settings.DefaultUser);
    }

    [Fact]
    public void Deserialize_BootstrapsRecommendedAgentsWhenNoProfilesAreConfigured()
    {
        var settings = SpecForgePortalSettingsStore.Deserialize(
            """
            {
              "modelProfiles": [],
              "agentProfiles": [],
              "phaseAgentAssignments": {},
              "refinementTolerance": "balanced",
              "mvpRigor": "medium",
              "reviewTolerance": "balanced",
              "reviewEvidencePolicy": "balanced",
              "technicalDesignSubagentsEnabled": false,
              "reviewSubagentsEnabled": true,
              "autoRefinementAnswersEnabled": false,
              "autoRefinementAnswersProfile": null,
              "autoPlayEnabled": true,
              "autoReviewEnabled": true,
              "maxRefinementCycles": 5,
              "maxImplementationReviewCycles": 5,
              "destructiveRewindEnabled": false,
              "pauseOnFailedReview": true,
              "useSemanticGraphWhenAvailable": true,
              "allowGraphBuildRefreshForTouchedUserStoryScope": false,
              "reviewLearningEnabled": true,
              "reviewLearningSkillPath": ".codex/skills/sdd-phase-agents/SKILL.md",
              "completedUsLockOnCompleted": false
            }
            """);

        Assert.Equal(
            ["planner", "implementer", "reviewer", "release-preparer"],
            settings.AgentProfiles.Select(static agent => agent.Name));
        Assert.Equal("planner", settings.PhaseAgentAssignments?.DefaultAgent);
        Assert.Equal("planner", settings.PhaseAgentAssignments?.TechnicalDesignAgent);
        Assert.Equal("implementer", settings.PhaseAgentAssignments?.ImplementationAgent);
        Assert.Equal("reviewer", settings.PhaseAgentAssignments?.ReviewAgent);
        Assert.Equal("release-preparer", settings.PhaseAgentAssignments?.PrPreparationAgent);
    }

    [Fact]
    public void ResolveAgentProfiles_DerivesAgentsFromModelProfilesWhenNoAgentsConfigured()
    {
        var settings = SpecForgePortalSettingsStore.Deserialize(
            """
            {
              "modelProfiles": [
                {
                  "name": "planner",
                  "provider": "codex",
                  "baseUrl": "",
                  "apiKey": "",
                  "model": "",
                  "reasoningEffort": "high",
                  "repositoryAccess": "read"
                }
              ],
              "agentProfiles": [],
              "phaseAgentAssignments": {},
              "refinementTolerance": "balanced",
              "mvpRigor": "medium",
              "reviewTolerance": "balanced",
              "reviewEvidencePolicy": "balanced",
              "technicalDesignSubagentsEnabled": false,
              "reviewSubagentsEnabled": true,
              "autoRefinementAnswersEnabled": false,
              "autoRefinementAnswersProfile": null,
              "autoPlayEnabled": true,
              "autoReviewEnabled": true,
              "maxRefinementCycles": 5,
              "maxImplementationReviewCycles": 5,
              "destructiveRewindEnabled": false,
              "pauseOnFailedReview": true,
              "useSemanticGraphWhenAvailable": true,
              "allowGraphBuildRefreshForTouchedUserStoryScope": false,
              "reviewLearningEnabled": true,
              "reviewLearningSkillPath": ".codex/skills/sdd-phase-agents/SKILL.md",
              "completedUsLockOnCompleted": false
            }
            """);

        var agent = Assert.Single(settings.ResolveAgentProfiles());
        Assert.Equal("planner", agent.Name);
        Assert.Equal("planner", agent.Role);
        Assert.Equal("planner", agent.ModelProfile);
        Assert.Equal("read", agent.RepositoryAccess);
        Assert.Equal("high", agent.ReasoningEffort);
    }

    [Fact]
    public void Save_AndLoad_RoundTripSettings()
    {
        var settings = SpecForgePortalSettingsStore.LoadOrDefault(workspaceRoot) with
        {
            ModelProfiles =
            [
                new OpenAiCompatibleModelProfile(
                    Name: "local",
                    Provider: "codex",
                    BaseUrl: "",
                    ApiKey: "",
                    Model: "",
                    ReasoningEffort: "medium",
                    RepositoryAccess: "read")
            ],
            MvpRigor = "high",
            AutoPlayEnabled = false,
            UseSemanticGraphWhenAvailable = false,
            AllowGraphBuildRefreshForTouchedUserStoryScope = true,
            DefaultHarnessProfile = "regulated",
            HarnessProfileAuthority = "central",
            HarnessProfileLockMode = "phase",
            LockedHarnessPhaseIds = ["review", "release-approval"],
            AllowPerUserStoryHarnessProfileOverrides = false
        };

        SpecForgePortalSettingsStore.Save(workspaceRoot, settings);

        var loaded = SpecForgePortalSettingsStore.Load(workspaceRoot);
        Assert.NotNull(loaded);
        Assert.Equal("high", loaded.MvpRigor);
        Assert.False(loaded.AutoPlayEnabled);
        Assert.False(loaded.UseSemanticGraphWhenAvailable);
        Assert.True(loaded.AllowGraphBuildRefreshForTouchedUserStoryScope);
        Assert.Equal("regulated", loaded.DefaultHarnessProfile);
        Assert.Equal("central", loaded.HarnessProfileAuthority);
        Assert.Equal("phase", loaded.HarnessProfileLockMode);
        Assert.Equal(["review", "release-approval"], loaded.LockedHarnessPhaseIds);
        Assert.False(loaded.AllowPerUserStoryHarnessProfileOverrides);
        Assert.Equal("local", Assert.Single(loaded.ModelProfiles).Name);
        Assert.Equal(string.Empty, loaded.DefaultUser);
    }

    public void Dispose()
    {
        if (Directory.Exists(workspaceRoot))
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    private void InitializeGitWorkspace()
    {
        Directory.CreateDirectory(workspaceRoot);
        RunGit("init");
        RunGit("config", "user.email", "specforge-tests@example.com");
        RunGit("config", "user.name", "SpecForge Tests");
    }

    private void RunGit(params string[] arguments)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workspaceRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new Xunit.Sdk.XunitException(
                $"Git command failed in '{workspaceRoot}': git {string.Join(' ', arguments)}{Environment.NewLine}{stderr}");
        }
    }
}
