using SpecForge.Domain.Persistence;
using SpecForge.Domain.Workflow;
using System.Text.RegularExpressions;

namespace SpecForge.Domain.Application;

internal sealed class UserStoryDependencyService
{
    private const string MissingDependencyReason = "missing";

    private static readonly Regex UserStoryIdRegex = new(
        pattern: "\\bUS-[A-Z0-9]+(?:-[A-Z0-9]+)*\\b",
        options: RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly UserStoryFileStore fileStore;

    public UserStoryDependencyService(UserStoryFileStore fileStore)
    {
        this.fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
    }

    public async Task<IReadOnlyCollection<UserStoryDependencySummary>> GetDependencySummariesAsync(
        string workspaceRoot,
        string mainArtifactPath,
        string currentUsId,
        CancellationToken cancellationToken)
    {
        var dependencyIds = await ReadDependencyIdsAsync(mainArtifactPath, currentUsId, cancellationToken);
        var dependencies = new List<UserStoryDependencySummary>(dependencyIds.Count);

        foreach (var dependencyId in dependencyIds)
        {
            dependencies.Add(await ResolveDependencySummaryAsync(workspaceRoot, dependencyId, cancellationToken));
        }

        return dependencies;
    }

    public static string ResolveOperationalStatus(
        UserStoryStatus workflowStatus,
        IReadOnlyCollection<UserStoryDependencySummary> dependencies) =>
        HasBlockingDependencies(workflowStatus, dependencies)
            ? "blocked"
            : WorkflowPresentation.ToStatusSlug(workflowStatus);

    public static bool HasBlockingDependencies(
        UserStoryStatus workflowStatus,
        IReadOnlyCollection<UserStoryDependencySummary> dependencies) =>
        workflowStatus != UserStoryStatus.Completed
        && dependencies.Any(static dependency => !dependency.IsSatisfied);

    private async Task<UserStoryDependencySummary> ResolveDependencySummaryAsync(
        string workspaceRoot,
        string dependencyId,
        CancellationToken cancellationToken)
    {
        UserStoryFilePaths paths;
        try
        {
            paths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, dependencyId);
        }
        catch (DirectoryNotFoundException)
        {
            return new UserStoryDependencySummary(
                dependencyId,
                Title: null,
                CurrentPhase: null,
                Status: null,
                IsSatisfied: false,
                MissingReason: MissingDependencyReason);
        }

        if (File.Exists(paths.DroppedMarkerFilePath))
        {
            return new UserStoryDependencySummary(
                dependencyId,
                Title: null,
                CurrentPhase: null,
                Status: null,
                IsSatisfied: false,
                MissingReason: "dropped");
        }

        var workflowRun = await fileStore.LoadAsync(paths.RootDirectory, cancellationToken);
        var mainArtifact = await File.ReadAllTextAsync(paths.MainArtifactPath, cancellationToken);
        var status = WorkflowPresentation.ToStatusSlug(workflowRun.Status);

        return new UserStoryDependencySummary(
            workflowRun.UsId,
            UserStoryMarkdown.ReadTitle(mainArtifact, dependencyId),
            WorkflowPresentation.ToPhaseSlug(workflowRun.CurrentPhase),
            status,
            IsSatisfied: workflowRun.Status == UserStoryStatus.Completed,
            MissingReason: null);
    }

    private static async Task<IReadOnlyList<string>> ReadDependencyIdsAsync(
        string mainArtifactPath,
        string currentUsId,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(mainArtifactPath))
        {
            return [];
        }

        var content = await File.ReadAllTextAsync(mainArtifactPath, cancellationToken);
        return ParseDependencyIds(content, currentUsId);
    }

    private static IReadOnlyList<string> ParseDependencyIds(string content, string currentUsId)
    {
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var dependencies = new List<string>();
        var insideDependencies = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Equals("## Dependencies", StringComparison.OrdinalIgnoreCase))
            {
                insideDependencies = true;
                continue;
            }

            if (insideDependencies && line.StartsWith("## ", StringComparison.Ordinal))
            {
                break;
            }

            if (!insideDependencies || !line.StartsWith("- ", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Match match in UserStoryIdRegex.Matches(line))
            {
                var dependencyId = match.Value.ToUpperInvariant();
                if (!dependencyId.Equals(currentUsId, StringComparison.OrdinalIgnoreCase))
                {
                    dependencies.Add(dependencyId);
                }
            }
        }

        return dependencies
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
    }
}
