using SpecForge.Domain.Application;

namespace SpecForge.Domain.Tests;

public sealed class UserStoryExternalReferenceTests : IDisposable
{
    private readonly string workspaceRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task GetUserStorySummaryAndWorkflowAsync_ExposePersistedExternalReferences()
    {
        var runner = new WorkflowRunner();
        var applicationService = new SpecForgeApplicationService();

        await runner.CreateUserStoryAsync(
            workspaceRoot,
            "US-0001",
            "Tagged story",
            "feature",
            "workflow",
            "Initial source",
            tags: ["UX", "mcp", "ux"],
            externalReferences:
            [
                new UserStoryExternalReference(
                    Url: "https://github.com/PinedaTec-EU/SpecForge.AI/issues/61",
                    Label: "",
                    Provider: "")
            ]);

        var summary = await applicationService.GetUserStorySummaryAsync(workspaceRoot, "US-0001");
        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        var usMarkdown = await File.ReadAllTextAsync(summary.MainArtifactPath);

        var summaryReference = Assert.Single(summary.ExternalReferences);
        Assert.Equal("https://github.com/PinedaTec-EU/SpecForge.AI/issues/61", summaryReference.Url);
        Assert.Equal("GitHub issue", summaryReference.Label);
        Assert.Equal("github", summaryReference.Provider);

        var workflowReference = Assert.Single(workflow.ExternalReferences ?? []);
        Assert.Equal(summaryReference.Url, workflowReference.Url);
        Assert.Contains("- External References:", usMarkdown);
        Assert.Contains("[GitHub issue](https://github.com/PinedaTec-EU/SpecForge.AI/issues/61)", usMarkdown);
    }

    [Fact]
    public async Task UpdateUserStoryInfoAsync_RewritesExternalReferences()
    {
        var runner = new WorkflowRunner();
        var applicationService = new SpecForgeApplicationService();
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Original story", "feature", "workflow", "Initial source");

        var result = await applicationService.UpdateUserStoryInfoAsync(
            workspaceRoot,
            "US-0001",
            externalReferences:
            [
                new UserStoryExternalReference(
                    Url: "https://jira.example.com/browse/SF-57",
                    Label: "",
                    Provider: "")
            ],
            actor: "bob");

        var usMarkdown = await File.ReadAllTextAsync(result.MainArtifactPath);
        var externalReference = Assert.Single(result.Summary.ExternalReferences);

        Assert.Equal("https://jira.example.com/browse/SF-57", externalReference.Url);
        Assert.Equal("Jira issue", externalReference.Label);
        Assert.Equal("jira", externalReference.Provider);
        Assert.Contains("[Jira issue](https://jira.example.com/browse/SF-57)", usMarkdown);
    }

    [Fact]
    public async Task GetUserStorySummaryAsync_FallsBackToLegacyIssueUrlLine()
    {
        var runner = new WorkflowRunner();
        var applicationService = new SpecForgeApplicationService();

        await runner.CreateUserStoryAsync(
            workspaceRoot,
            "US-0002",
            "Legacy linked story",
            "feature",
            "workflow",
            """
            Source GitHub issue: SF-88 (#88)
            Issue URL: https://github.com/PinedaTec-EU/SpecForge.AI/issues/88
            """);

        var summary = await applicationService.GetUserStorySummaryAsync(workspaceRoot, "US-0002");

        var reference = Assert.Single(summary.ExternalReferences);
        Assert.Equal("https://github.com/PinedaTec-EU/SpecForge.AI/issues/88", reference.Url);
        Assert.Equal("GitHub issue", reference.Label);
        Assert.Equal("github", reference.Provider);
    }

    [Fact]
    public async Task UpdateUserStoryInfoAsync_RewritesLegacyIssueUrlLine()
    {
        var runner = new WorkflowRunner();
        var applicationService = new SpecForgeApplicationService();

        await runner.CreateUserStoryAsync(
            workspaceRoot,
            "US-0003",
            "Legacy linked story",
            "feature",
            "workflow",
            """
            Source GitHub issue: SF-88 (#88)
            Issue URL: https://github.com/PinedaTec-EU/SpecForge.AI/issues/88
            """);

        await applicationService.UpdateUserStoryInfoAsync(
            workspaceRoot,
            "US-0003",
            externalReferences:
            [
                new UserStoryExternalReference(
                    Url: "https://github.com/PinedaTec-EU/SpecForge.AI/issues/99",
                    Label: "",
                    Provider: "")
            ],
            actor: "bob");

        var usMarkdown = await File.ReadAllTextAsync(Path.Combine(workspaceRoot, ".specs", "us", "US-0003", "us.md"));

        Assert.Contains("- External References:", usMarkdown);
        Assert.Contains("[GitHub issue](https://github.com/PinedaTec-EU/SpecForge.AI/issues/99)", usMarkdown);
        Assert.Contains("Issue URL: https://github.com/PinedaTec-EU/SpecForge.AI/issues/99", usMarkdown);
        Assert.DoesNotContain("Issue URL: https://github.com/PinedaTec-EU/SpecForge.AI/issues/88", usMarkdown);
    }

    public void Dispose()
    {
        if (Directory.Exists(workspaceRoot))
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }
}
