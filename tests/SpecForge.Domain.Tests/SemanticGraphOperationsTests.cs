using System.Text.Json;
using SpecForge.Domain.Application;
using SpecForge.Domain.Persistence;

namespace SpecForge.Domain.Tests;

public sealed class SemanticGraphOperationsTests : IDisposable
{
    private readonly string workspaceRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RunGlobalOperationAsync_BuildsBaselineGlobalGraphArtifacts()
    {
        CreateWorkspaceSkeleton();

        var result = await SemanticGraphOperations.RunGlobalOperationAsync(
            workspaceRoot,
            new SemanticGraphGlobalOperationRequest("build", "tester", "seed baseline graph"));

        Assert.True(result.Executed);
        Assert.Contains(result.ArtifactsWritten, path => path.EndsWith("global-graph.json", StringComparison.Ordinal));

        var status = SemanticGraphOperations.DescribeStatus(workspaceRoot);
        Assert.True(status.GlobalGraph.Exists);
        Assert.Equal("fresh", status.GlobalGraph.State);
        Assert.Contains("neighbors:file", status.AvailableQueryKinds);

        var auditEvents = ReadAuditEvents();
        var buildEvent = Assert.Single(auditEvents, static entry => entry.EventFamily == "graph.build.completed");
        Assert.Equal("tester", buildEvent.Actor);
        Assert.Equal("build", buildEvent.RequestedMode);
        Assert.Contains(buildEvent.ArtifactsWritten, path => path.EndsWith("graph-cost-ledger.json", StringComparison.Ordinal));

        var ledger = ReadCostLedger();
        Assert.Equal(1, ledger.Builds.Count);
        Assert.NotNull(ledger.LastSuccessfulGlobalGraphBuild);
        Assert.Equal("graph.build.completed", ledger.LastSuccessfulGlobalGraphBuild!.EventFamily);
    }

    [Fact]
    public async Task RunGlobalOperationAsync_RebuildRequiresConfirmationWhenGraphAlreadyExists()
    {
        CreateWorkspaceSkeleton();
        await SemanticGraphOperations.RunGlobalOperationAsync(
            workspaceRoot,
            new SemanticGraphGlobalOperationRequest("build", "tester", "seed baseline graph"));

        var result = await SemanticGraphOperations.RunGlobalOperationAsync(
            workspaceRoot,
            new SemanticGraphGlobalOperationRequest("rebuild", "tester", "replace baseline graph"));

        Assert.False(result.Executed);
        Assert.True(result.RequiresOverwriteConfirmation);
        Assert.Contains(result.BlockedReasons, reason => reason.Contains("Overwrite confirmation", StringComparison.Ordinal));

        var ledger = ReadCostLedger();
        Assert.NotNull(ledger.LastFailedGraphMutation);
        Assert.Equal("graph.rebuild.failed", ledger.LastFailedGraphMutation!.EventFamily);
    }

    [Fact]
    public async Task MaterializeImpactGraphAsync_WritesImpactArtifactsAndWhyIncludedQuery()
    {
        CreateWorkspaceSkeleton();
        await SemanticGraphOperations.RunGlobalOperationAsync(
            workspaceRoot,
            new SemanticGraphGlobalOperationRequest("build", "tester", "seed baseline graph"));
        CreateGraphScopeRequest("US-0001", "src/App/Service.cs");

        var result = await SemanticGraphOperations.MaterializeImpactGraphAsync(
            workspaceRoot,
            new SemanticGraphImpactOperationRequest("US-0001", "tester", "prepare technical design scope"));

        Assert.True(result.Executed);

        var query = SemanticGraphOperations.ExecuteQuery(
            workspaceRoot,
            new SemanticGraphQueryRequest(
                QueryKind: "why-included:file",
                Actor: "tester",
                UsId: "US-0001",
                FilePath: "src/App/Service.cs"));

        Assert.Equal("impact-graph", query.SourceGraphUsed);
        Assert.Contains("src/App/Service.cs", query.IncludedFiles);
        Assert.Contains(query.InclusionReasons, reason => reason.Contains("Seed file", StringComparison.Ordinal));

        var ledger = ReadCostLedger();
        Assert.Equal(1, ledger.ImpactDerivations.Count);
    }

    [Fact]
    public async Task ExecuteQuery_NeighborsFile_ReturnsOwningProjectAndSiblingFiles()
    {
        CreateWorkspaceSkeleton();
        await SemanticGraphOperations.RunGlobalOperationAsync(
            workspaceRoot,
            new SemanticGraphGlobalOperationRequest("build", "tester", "seed baseline graph"));

        var query = SemanticGraphOperations.ExecuteQuery(
            workspaceRoot,
            new SemanticGraphQueryRequest(
                QueryKind: "neighbors:file",
                Actor: "tester",
                FilePath: "src/App/Service.cs",
                MaxDepth: 2));

        Assert.Equal("global-graph", query.SourceGraphUsed);
        Assert.Contains("src/App/App.csproj", query.IncludedFiles);
        Assert.Contains("src/App/Other.cs", query.IncludedFiles);

        var ledger = ReadCostLedger();
        Assert.Equal(1, ledger.Queries.Count);
        var auditEvents = ReadAuditEvents();
        Assert.Contains(auditEvents, static entry => entry.EventFamily == "graph.query.executed" && entry.ActualMode == "global-graph");
    }

    private IReadOnlyCollection<SemanticGraphAuditEvent> ReadAuditEvents()
    {
        var globalPaths = SemanticGraphFilePaths.FromWorkspaceRoot(workspaceRoot);
        return File.ReadAllLines(globalPaths.GraphBuildLogPath)
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<SemanticGraphAuditEvent>(line, new JsonSerializerOptions(JsonSerializerDefaults.Web)))
            .OfType<SemanticGraphAuditEvent>()
            .ToArray();
    }

    private SemanticGraphCostLedger ReadCostLedger()
    {
        var globalPaths = SemanticGraphFilePaths.FromWorkspaceRoot(workspaceRoot);
        return JsonSerializer.Deserialize<SemanticGraphCostLedger>(
                   File.ReadAllText(globalPaths.GraphCostLedgerPath),
                   new JsonSerializerOptions(JsonSerializerDefaults.Web))
               ?? throw new InvalidOperationException("Semantic graph cost ledger could not be parsed.");
    }

    private void CreateWorkspaceSkeleton()
    {
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "src", "App"));
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "tests", "App.Tests"));
        File.WriteAllText(Path.Combine(workspaceRoot, "SpecForge.AI.sln"), string.Empty);
        File.WriteAllText(
            Path.Combine(workspaceRoot, "src", "App", "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net9.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(workspaceRoot, "src", "App", "Service.cs"), "namespace App; public sealed class Service { }");
        File.WriteAllText(Path.Combine(workspaceRoot, "src", "App", "Other.cs"), "namespace App; public sealed class Other { }");
        File.WriteAllText(
            Path.Combine(workspaceRoot, "tests", "App.Tests", "App.Tests.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net9.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../../src/App/App.csproj" />
                <PackageReference Include="xunit" Version="2.9.0" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(workspaceRoot, "tests", "App.Tests", "ServiceTests.cs"), "namespace App.Tests; public sealed class ServiceTests { }");
    }

    private void CreateGraphScopeRequest(string usId, string seedFilePath)
    {
        var userStoryPaths = UserStoryFilePaths.FromWorkspaceRoot(workspaceRoot, "workflow", usId);
        Directory.CreateDirectory(userStoryPaths.ContextDirectoryPath);
        var request = new RefinementGraphScopeRequest(
            Depth: 2,
            SeedNodes: [new RefinementGraphSeedNode("service", "Service", "Primary design scope root.")],
            SeedFiles: [new PhaseExecutionArtifactInput(seedFilePath, PhaseExecutionReceiptStore.TryComputeFileSha256(Path.Combine(workspaceRoot, seedFilePath.Replace('/', Path.DirectorySeparatorChar))), "refinement")],
            UnresolvedScopeQuestions: []);
        File.WriteAllText(userStoryPaths.GraphScopeRequestPath, JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    public void Dispose()
    {
        if (Directory.Exists(workspaceRoot))
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }
}
