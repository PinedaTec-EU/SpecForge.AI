using System.Text.Json;
using SpecForge.Domain.Application;
using SpecForge.OpenAICompatible;

namespace SpecForge.Domain.Tests;

public sealed class OpenAiCompatiblePhaseExecutionProviderFactoryTests : IDisposable
{
    private readonly Dictionary<string, string?> previousValues = new();

    [Fact]
    public void Create_WithoutConfiguredProfiles_ReturnsDeterministicProvider()
    {
        ClearFactoryEnvironment();

        var provider = OpenAiCompatiblePhaseExecutionProviderFactory.Create();

        Assert.IsType<DeterministicPhaseExecutionProvider>(provider);
    }

    [Fact]
    public void Create_RejectsUnsupportedProviderKindFromFallback()
    {
        ClearFactoryEnvironment();

        var profiles = JsonSerializer.Serialize(new[]
        {
            new OpenAiCompatibleModelProfile(
                Name: "bad",
                Provider: "unsupported",
                BaseUrl: "",
                ApiKey: "",
                Model: "",
                ReasoningEffort: null,
                RepositoryAccess: "read")
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            OpenAiCompatiblePhaseExecutionProviderFactory.Create(key =>
                key == OpenAiCompatiblePhaseExecutionProviderFactory.ModelProfilesJsonEnvVar ? profiles : null));

        Assert.Contains("Unsupported model profile provider set 'unsupported'", exception.Message);
    }

    [Fact]
    public void Create_RejectsInvalidTimeoutFromEnvironment()
    {
        ClearFactoryEnvironment();
        SetEnvironment(OpenAiCompatiblePhaseExecutionProviderFactory.TimeoutSecondsEnvVar, "0");
        SetEnvironment(
            OpenAiCompatiblePhaseExecutionProviderFactory.ModelProfilesJsonEnvVar,
            JsonSerializer.Serialize(new[]
            {
                new OpenAiCompatibleModelProfile(
                    Name: "local",
                    Provider: "codex",
                    BaseUrl: "",
                    ApiKey: "",
                    Model: "",
                    ReasoningEffort: null,
                    RepositoryAccess: "read")
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            OpenAiCompatiblePhaseExecutionProviderFactory.Create());

        Assert.Contains("SPECFORGE_OPENAI_TIMEOUT_SECONDS", exception.Message);
    }

    public void Dispose()
    {
        foreach (var (key, value) in previousValues)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private void ClearFactoryEnvironment()
    {
        foreach (var key in FactoryEnvironmentKeys)
        {
            SetEnvironment(key, null);
        }
    }

    private void SetEnvironment(string key, string? value)
    {
        if (!previousValues.ContainsKey(key))
        {
            previousValues[key] = Environment.GetEnvironmentVariable(key);
        }

        Environment.SetEnvironmentVariable(key, value);
    }

    private static readonly string[] FactoryEnvironmentKeys =
    [
        OpenAiCompatiblePhaseExecutionProviderFactory.ModelProfilesJsonEnvVar,
        OpenAiCompatiblePhaseExecutionProviderFactory.AgentProfilesJsonEnvVar,
        OpenAiCompatiblePhaseExecutionProviderFactory.PhaseAgentAssignmentsJsonEnvVar,
        OpenAiCompatiblePhaseExecutionProviderFactory.TechnicalDesignSubagentsEnabledEnvVar,
        OpenAiCompatiblePhaseExecutionProviderFactory.ReviewSubagentsEnabledEnvVar,
        OpenAiCompatiblePhaseExecutionProviderFactory.RefinementToleranceEnvVar,
        OpenAiCompatiblePhaseExecutionProviderFactory.LegacyRefinementToleranceEnvVar,
        OpenAiCompatiblePhaseExecutionProviderFactory.MvpRigorEnvVar,
        OpenAiCompatiblePhaseExecutionProviderFactory.ReviewToleranceEnvVar,
        OpenAiCompatiblePhaseExecutionProviderFactory.ReviewEvidencePolicyEnvVar,
        OpenAiCompatiblePhaseExecutionProviderFactory.AutoRefinementAnswersEnabledEnvVar,
        OpenAiCompatiblePhaseExecutionProviderFactory.LegacyAutoRefinementAnswersEnabledEnvVar,
        OpenAiCompatiblePhaseExecutionProviderFactory.AutoRefinementAnswersProfileEnvVar,
        OpenAiCompatiblePhaseExecutionProviderFactory.LegacyAutoRefinementAnswersProfileEnvVar,
        OpenAiCompatiblePhaseExecutionProviderFactory.ReviewLearningEnabledEnvVar,
        OpenAiCompatiblePhaseExecutionProviderFactory.ReviewLearningSkillPathEnvVar,
        OpenAiCompatiblePhaseExecutionProviderFactory.SystemPromptEnvVar,
        OpenAiCompatiblePhaseExecutionProviderFactory.TimeoutSecondsEnvVar
    ];
}
