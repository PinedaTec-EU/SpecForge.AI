using SpecForge.Domain.Application;

namespace SpecForge.Domain.Tests;

public sealed class GoalIntakeTests : IDisposable
{
    private readonly string workspaceRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CreateUserStoriesFromGoalAsync_AssignsIdsAndPersistsTraceableStories()
    {
        var service = new SpecForgeApplicationService();

        var result = await service.CreateUserStoriesFromGoalAsync(
            workspaceRoot,
            "/goals Build GitHub authentication with sessions and admin roles.",
            [
                new GoalUserStoryDraft(
                    UsId: null,
                    Title: "Login with GitHub OAuth",
                    Kind: "feature",
                    Category: "integrations",
                    SourceText: "As a user, I want to sign in with GitHub so that I can access the product without a local password.",
                    AcceptanceCriteria: ["OAuth failures do not create a session."],
                    Dependencies: null,
                    ClarifiedAnswers: ["GitHub is the only identity provider for the MVP."],
                    NonGoals: ["Local password sign-up is out of scope."],
                    MvpOutcome: "A user can authenticate with GitHub and enter the product.",
                    SliceRationale: "Authentication is the first independent release slice."),
                new GoalUserStoryDraft(
                    UsId: null,
                    Title: "Persist authenticated sessions",
                    Kind: null,
                    Category: "workflow",
                    SourceText: "As an authenticated user, I want my session to persist securely so that I do not need to sign in on every page load.",
                    AcceptanceCriteria: null,
                    Dependencies: ["US-0001"])
            ],
            goalId: "goal-auth",
            strategy: "small-user-stories",
            actor: "model-on-behalf-of-user");

        Assert.Equal("GOAL-AUTH", result.GoalId);
        Assert.Equal("US-0001", result.RecommendedFirstUserStory);
        Assert.Collection(
            result.CreatedStories,
            first =>
            {
                Assert.Equal("US-0001", first.UsId);
                Assert.Equal("integrations", first.Category);
                Assert.True(File.Exists(first.MainArtifactPath));
            },
            second =>
            {
                Assert.Equal("US-0002", second.UsId);
                Assert.Equal("feature", second.Kind);
                Assert.True(File.Exists(second.MainArtifactPath));
            });

        var firstStory = await File.ReadAllTextAsync(result.CreatedStories[0].MainArtifactPath);
        Assert.Contains("- Goal: `GOAL-AUTH`", firstStory);
        Assert.Contains("- Coding policy: do not implement directly from the broad goal", firstStory);
        Assert.Contains("/goals Build GitHub authentication", firstStory);
        Assert.Contains("- OAuth failures do not create a session.", firstStory);
        Assert.Contains("## MVP Slice", firstStory);
        Assert.Contains("- Outcome: A user can authenticate with GitHub and enter the product.", firstStory);
        Assert.Contains("- Local password sign-up is out of scope.", firstStory);
        Assert.Contains("- GitHub is the only identity provider for the MVP.", firstStory);
    }

    [Fact]
    public async Task CreateUserStoriesFromGoalAsync_ContinuesAfterExistingUserStoryIds()
    {
        var service = new SpecForgeApplicationService();
        await service.CreateUserStoryAsync(workspaceRoot, "US-0007", "Existing story", "feature", "workflow", "Existing source");

        var result = await service.CreateUserStoriesFromGoalAsync(
            workspaceRoot,
            "/goals Add billing operations.",
            [
                new GoalUserStoryDraft(
                    UsId: null,
                    Title: "Create invoice view",
                    Kind: null,
                    Category: null,
                    SourceText: "As an operator, I want to inspect invoices so that I can answer billing questions.")
            ]);

        Assert.Equal("US-0008", result.CreatedStories.Single().UsId);
    }

    public void Dispose()
    {
        if (Directory.Exists(workspaceRoot))
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }
}
