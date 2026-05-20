using SpecForge.Domain.Persistence;

namespace SpecForge.Domain.Tests;

public sealed class UserStoryFilePathsTests
{
    [Fact]
    public void FromWorkspaceRoot_BuildsSpecsPathAtWorkspaceRoot()
    {
        var paths = UserStoryFilePaths.FromWorkspaceRoot("/repo", "workflow", "US-0001");

        Assert.Equal("/repo/.specs/us/US-0001", paths.RootDirectory);
        Assert.Equal("/repo/.specs/us/US-0001/state.yaml", paths.StateFilePath);
        Assert.Equal("/repo/.specs/us/US-0001/branch.yaml", paths.BranchFilePath);
        Assert.Equal("/repo/.specs/us/US-0001/restarts", paths.RestartsDirectoryPath);
        Assert.Equal("/repo/.specs/us/US-0001/context/graph-scope-request.json", paths.GraphScopeRequestPath);
        Assert.Equal("/repo/.specs/us/US-0001/context/impact-graph.json", paths.ImpactGraphPath);
        Assert.Equal("/repo/.specs/us/US-0001/context/impact-graph.meta.json", paths.ImpactGraphMetadataPath);
        Assert.Equal("/repo/.specs/us/US-0001/context/impact-summary.md", paths.ImpactGraphSummaryPath);
    }

    [Fact]
    public void GetRestartArchiveDirectoryPath_UsesTimestampedArchiveDirectory()
    {
        var paths = UserStoryFilePaths.FromWorkspaceRoot("/repo", "workflow", "US-0001");

        var archiveDirectory = paths.GetRestartArchiveDirectoryPath(new DateTimeOffset(2026, 4, 18, 10, 30, 0, TimeSpan.Zero));

        Assert.Equal("/repo/.specs/us/US-0001/restarts/20260418T103000Z", archiveDirectory);
    }

    [Fact]
    public void GetPhaseArtifactPath_ForSpec_UsesSpecArtifactName()
    {
        var paths = UserStoryFilePaths.FromWorkspaceRoot("/repo", "workflow", "US-0001");

        var artifactPath = paths.GetPhaseArtifactPath(SpecForge.Domain.Workflow.PhaseId.Spec);

        Assert.Equal("/repo/.specs/us/US-0001/phases/01-spec.md", artifactPath);
    }

    [Fact]
    public async Task ResolveFromWorkspaceRoot_MigratesLegacyCategoryDirectoriesToFlatLayout()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var legacyRoot = Path.Combine(workspaceRoot, ".specs", "us", "workflow", "US-0001");
        var oldRelativePath = ".specs/us/workflow/US-0001/phases/01-spec.md";
        try
        {
            Directory.CreateDirectory(legacyRoot);
            await File.WriteAllTextAsync(
                Path.Combine(legacyRoot, "us.md"),
                $"""
                # US-0001 · Test

                ## Metadata
                - Kind: `feature`
                - Category: `workflow`

                See {oldRelativePath}.
                """);

            var paths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, "US-0001");

            Assert.Equal(Path.Combine(workspaceRoot, ".specs", "us", "US-0001"), paths.RootDirectory);
            Assert.False(Directory.Exists(legacyRoot));
            Assert.Contains(
                ".specs/us/US-0001/phases/01-spec.md",
                await File.ReadAllTextAsync(paths.MainArtifactPath));
        }
        finally
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }
    }
}
