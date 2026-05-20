using System.Text.Json;
using SpecForge.Domain.Application;
using SpecForge.Domain.Persistence;
using SpecForge.Domain.Workflow;

namespace SpecForge.Domain.Tests;

public sealed class TechnicalDesignContextPackBuilderTests : IDisposable
{
    private readonly string workspaceRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly string? originalUseGraph = Environment.GetEnvironmentVariable("SPECFORGE_USE_SEMANTIC_GRAPH_WHEN_AVAILABLE");
    private readonly string? originalAllowGraphMutation = Environment.GetEnvironmentVariable("SPECFORGE_ALLOW_GRAPH_BUILD_REFRESH_FOR_TOUCHED_US_SCOPE");

    [Fact]
    public async Task BuildAsync_WhenGraphDisabled_ReturnsSkillAndScopeContextWithoutGraphExpansions()
    {
        CreateWorkspaceSkeleton();
        CreateGraphScopeRequest("US-0001");
        Environment.SetEnvironmentVariable("SPECFORGE_USE_SEMANTIC_GRAPH_WHEN_AVAILABLE", "false");
        Environment.SetEnvironmentVariable("SPECFORGE_ALLOW_GRAPH_BUILD_REFRESH_FOR_TOUCHED_US_SCOPE", "false");

        var paths = UserStoryFilePaths.FromWorkspaceRoot(workspaceRoot, "workflow", "US-0001");
        var context = BuildTechnicalDesignContext();

        var pack = await TechnicalDesignContextPackBuilder.BuildAsync(workspaceRoot, "US-0001", paths, context);

        Assert.False(pack.GraphEnabled);
        Assert.False(pack.GraphAvailable);
        Assert.Equal("disabled", pack.ImpactGraphState);
        Assert.NotEmpty(pack.SelectedSkills);
        Assert.NotNull(pack.GraphScopeRequest);
        Assert.Empty(pack.GraphBackedExpansions);
    }

    [Fact]
    public async Task BuildAsync_WhenGraphMutationAllowed_MaterializesFallbackImpactGraph()
    {
        CreateWorkspaceSkeleton();
        CreateGraphScopeRequest("US-0001");
        Environment.SetEnvironmentVariable("SPECFORGE_USE_SEMANTIC_GRAPH_WHEN_AVAILABLE", "true");
        Environment.SetEnvironmentVariable("SPECFORGE_ALLOW_GRAPH_BUILD_REFRESH_FOR_TOUCHED_US_SCOPE", "true");

        var paths = UserStoryFilePaths.FromWorkspaceRoot(workspaceRoot, "workflow", "US-0001");
        var context = BuildTechnicalDesignContext();

        var pack = await TechnicalDesignContextPackBuilder.BuildAsync(workspaceRoot, "US-0001", paths, context);

        Assert.True(pack.GraphEnabled);
        Assert.True(pack.GraphAvailable);
        Assert.True(pack.FallbackUsed);
        Assert.Equal("fresh", pack.ImpactGraphState);
        Assert.NotNull(pack.ImpactSummaryPath);
        Assert.Contains(pack.SelectedSkills, item => item.SkillPath.Contains("/dotnet/", StringComparison.Ordinal));
        Assert.Contains(pack.GraphBackedExpansions, item => item.Path == "src/App/Service.cs");
        Assert.True(File.Exists(paths.ImpactGraphPath));
        Assert.True(File.Exists(paths.ImpactGraphSummaryPath));
    }

    private PhaseExecutionContext BuildTechnicalDesignContext()
    {
        var userStoryPath = Path.Combine(workspaceRoot, ".specs", "us", "US-0001", "us.md");
        var specPath = Path.Combine(workspaceRoot, ".specs", "us", "US-0001", "phases", "01-spec.md");
        var contextFilePath = Path.Combine(workspaceRoot, "context", "architecture.md");

        return new PhaseExecutionContext(
            WorkspaceRoot: workspaceRoot,
            UsId: "US-0001",
            PhaseId: PhaseId.TechnicalDesign,
            UserStoryPath: userStoryPath,
            PreviousArtifactPaths: new Dictionary<PhaseId, string>
            {
                [PhaseId.Spec] = specPath
            },
            ContextFilePaths: [contextFilePath]);
    }

    private void CreateWorkspaceSkeleton()
    {
        Directory.CreateDirectory(Path.Combine(workspaceRoot, ".specs", "us", "US-0001", "phases"));
        Directory.CreateDirectory(Path.Combine(workspaceRoot, ".specs", "us", "US-0001", "context"));
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "src", "App"));
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "context"));

        File.WriteAllText(Path.Combine(workspaceRoot, ".specs", "us", "US-0001", "us.md"), "# User Story");
        File.WriteAllText(Path.Combine(workspaceRoot, ".specs", "us", "US-0001", "phases", "01-spec.md"), "# Spec");
        File.WriteAllText(Path.Combine(workspaceRoot, "context", "architecture.md"), "# Architecture");
        File.WriteAllText(
            Path.Combine(workspaceRoot, "src", "App", "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(workspaceRoot, "src", "App", "Service.cs"), "namespace App; public sealed class Service { }");
        File.WriteAllText(Path.Combine(workspaceRoot, "src", "App", "Other.cs"), "namespace App; public sealed class Other { }");
    }

    private void CreateGraphScopeRequest(string usId)
    {
        var paths = UserStoryFilePaths.FromWorkspaceRoot(workspaceRoot, "workflow", usId);
        Directory.CreateDirectory(paths.ContextDirectoryPath);
        var request = new RefinementGraphScopeRequest(
            2,
            [new RefinementGraphSeedNode("service", "Service", "Primary scope root.")],
            [new PhaseExecutionArtifactInput("src/App/Service.cs", PhaseExecutionReceiptStore.TryComputeFileSha256(Path.Combine(workspaceRoot, "src", "App", "Service.cs")), "refinement")],
            ["Clarify validation strategy boundaries."]);
        File.WriteAllText(paths.GraphScopeRequestPath, JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SPECFORGE_USE_SEMANTIC_GRAPH_WHEN_AVAILABLE", originalUseGraph);
        Environment.SetEnvironmentVariable("SPECFORGE_ALLOW_GRAPH_BUILD_REFRESH_FOR_TOUCHED_US_SCOPE", originalAllowGraphMutation);

        if (Directory.Exists(workspaceRoot))
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }
}
