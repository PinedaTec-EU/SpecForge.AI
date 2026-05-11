using SpecForge.Domain.Workflow;

namespace SpecForge.Domain.Persistence;

public sealed class UserStoryFilePaths
{
    public static string SpecsDirectoryName => ".specs";

    public static string UserStoriesDirectoryName => "us";

    public UserStoryFilePaths(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("Root directory is required.", nameof(rootDirectory));
        }

        RootDirectory = rootDirectory;
        MainArtifactPath = Path.Combine(rootDirectory, "us.md");
        RefinementFilePath = Path.Combine(rootDirectory, "refinement.md");
        StateFilePath = Path.Combine(rootDirectory, "state.yaml");
        RuntimeFilePath = Path.Combine(rootDirectory, "runtime.yaml");
        TimelineFilePath = Path.Combine(rootDirectory, "timeline.md");
        DroppedMarkerFilePath = Path.Combine(rootDirectory, ".dropped");
        PhasesDirectoryPath = Path.Combine(rootDirectory, "phases");
        BranchFilePath = Path.Combine(rootDirectory, "branch.yaml");
        RestartsDirectoryPath = Path.Combine(rootDirectory, "restarts");
        ExecutionReceiptsDirectoryPath = Path.Combine(rootDirectory, "execution-receipts");
        ContextDirectoryPath = Path.Combine(rootDirectory, "context");
        AttachmentsDirectoryPath = Path.Combine(rootDirectory, "attachments");
    }

    public static UserStoryFilePaths FromWorkspaceRoot(string workspaceRoot, string category, string usId)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("Workspace root is required.", nameof(workspaceRoot));
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Category is required.", nameof(category));
        }

        if (string.IsNullOrWhiteSpace(usId))
        {
            throw new ArgumentException("US id is required.", nameof(usId));
        }

        var userStoryDirectory = Path.Combine(
            workspaceRoot,
            SpecsDirectoryName,
            UserStoriesDirectoryName,
            usId.Trim().ToUpperInvariant());

        return new UserStoryFilePaths(userStoryDirectory);
    }

    public static UserStoryFilePaths ResolveFromWorkspaceRoot(string workspaceRoot, string usId)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("Workspace root is required.", nameof(workspaceRoot));
        }

        if (string.IsNullOrWhiteSpace(usId))
        {
            throw new ArgumentException("US id is required.", nameof(usId));
        }

        var specsRoot = Path.Combine(workspaceRoot, SpecsDirectoryName, UserStoriesDirectoryName);
        var normalizedUsId = usId.Trim().ToUpperInvariant();
        EnsureFlatUserStoryLayout(workspaceRoot);
        if (!Directory.Exists(specsRoot))
        {
            throw new DirectoryNotFoundException($"User stories root '{specsRoot}' was not found.");
        }

        var flatCandidate = Path.Combine(specsRoot, normalizedUsId);
        if (Directory.Exists(flatCandidate))
        {
            return new UserStoryFilePaths(flatCandidate);
        }

        throw new DirectoryNotFoundException($"User story '{normalizedUsId}' was not found under '{specsRoot}'.");
    }

    public static void EnsureFlatUserStoryLayout(string workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("Workspace root is required.", nameof(workspaceRoot));
        }

        var specsRoot = Path.Combine(workspaceRoot, SpecsDirectoryName, UserStoriesDirectoryName);
        if (!Directory.Exists(specsRoot))
        {
            return;
        }

        foreach (var categoryDirectory in Directory.GetDirectories(specsRoot, "*", SearchOption.TopDirectoryOnly))
        {
            var directoryName = Path.GetFileName(categoryDirectory);
            if (directoryName.StartsWith("US-", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var legacyUserStoryDirectory in Directory.GetDirectories(categoryDirectory, "US-*", SearchOption.TopDirectoryOnly))
            {
                var userStoryDirectoryName = Path.GetFileName(legacyUserStoryDirectory);
                var flatUserStoryDirectory = Path.Combine(specsRoot, userStoryDirectoryName);
                if (Directory.Exists(flatUserStoryDirectory))
                {
                    throw new InvalidOperationException(
                        $"Cannot migrate legacy user story directory '{legacyUserStoryDirectory}' because '{flatUserStoryDirectory}' already exists.");
                }

                Directory.Move(legacyUserStoryDirectory, flatUserStoryDirectory);
                RewriteMovedUserStoryPathReferences(
                    flatUserStoryDirectory,
                    legacyUserStoryDirectory,
                    flatUserStoryDirectory,
                    Path.Combine(SpecsDirectoryName, UserStoriesDirectoryName, directoryName, userStoryDirectoryName),
                    Path.Combine(SpecsDirectoryName, UserStoriesDirectoryName, userStoryDirectoryName));
            }

            if (!Directory.EnumerateFileSystemEntries(categoryDirectory).Any())
            {
                Directory.Delete(categoryDirectory);
            }
        }
    }

    private static void RewriteMovedUserStoryPathReferences(
        string userStoryDirectory,
        string oldAbsoluteDirectory,
        string newAbsoluteDirectory,
        string oldRelativeDirectory,
        string newRelativeDirectory)
    {
        foreach (var filePath in Directory.EnumerateFiles(userStoryDirectory, "*", SearchOption.AllDirectories))
        {
            if (!ShouldRewritePathReferences(filePath))
            {
                continue;
            }

            var content = File.ReadAllText(filePath);
            var updated = content
                .Replace(oldAbsoluteDirectory, newAbsoluteDirectory, StringComparison.Ordinal)
                .Replace(oldRelativeDirectory.Replace('\\', '/'), newRelativeDirectory.Replace('\\', '/'), StringComparison.Ordinal)
                .Replace(oldRelativeDirectory.Replace('/', Path.DirectorySeparatorChar), newRelativeDirectory.Replace('/', Path.DirectorySeparatorChar), StringComparison.Ordinal);

            if (!string.Equals(content, updated, StringComparison.Ordinal))
            {
                File.WriteAllText(filePath, updated);
            }
        }
    }

    private static bool ShouldRewritePathReferences(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".md" or ".yaml" or ".yml" or ".json" or ".txt" => true,
            _ => false
        };
    }

    public string RootDirectory { get; }

    public string MainArtifactPath { get; }

    public string RefinementFilePath { get; }

    public string StateFilePath { get; }

    public string RuntimeFilePath { get; }

    public string TimelineFilePath { get; }

    public string DroppedMarkerFilePath { get; }

    public string PhasesDirectoryPath { get; }

    public string BranchFilePath { get; }

    public string RestartsDirectoryPath { get; }

    public string ExecutionReceiptsDirectoryPath { get; }

    public string ContextDirectoryPath { get; }

    public string AttachmentsDirectoryPath { get; }

    public string GetPhaseArtifactPath(PhaseId phaseId, int version = 1)
    {
        var fileName = GetPhaseArtifactFileStem(phaseId);
        var versionSuffix = version <= 1 ? string.Empty : $".v{version:00}";
        return Path.Combine(PhasesDirectoryPath, $"{fileName}{versionSuffix}.md");
    }

    public string GetPhaseArtifactJsonPath(PhaseId phaseId, int version = 1)
    {
        var fileName = GetPhaseArtifactFileStem(phaseId);
        var versionSuffix = version <= 1 ? string.Empty : $".v{version:00}";
        return Path.Combine(PhasesDirectoryPath, $"{fileName}{versionSuffix}.json");
    }

    public string? GetLatestExistingPhaseArtifactPath(PhaseId phaseId)
    {
        foreach (var fileStem in GetPhaseArtifactFileStems(phaseId))
        {
            string? latestPath = null;
            for (var version = 1; version < 100; version++)
            {
                var versionSuffix = version <= 1 ? string.Empty : $".v{version:00}";
                var candidate = Path.Combine(PhasesDirectoryPath, $"{fileStem}{versionSuffix}.md");
                if (!File.Exists(candidate))
                {
                    break;
                }

                latestPath = candidate;
            }

            if (latestPath is not null)
            {
                return latestPath;
            }
        }

        return null;
    }

    public string? GetLatestExistingPhaseArtifactJsonPath(PhaseId phaseId)
    {
        foreach (var fileStem in GetPhaseArtifactFileStems(phaseId))
        {
            string? latestPath = null;
            for (var version = 1; version < 100; version++)
            {
                var versionSuffix = version <= 1 ? string.Empty : $".v{version:00}";
                var candidate = Path.Combine(PhasesDirectoryPath, $"{fileStem}{versionSuffix}.json");
                if (!File.Exists(candidate))
                {
                    break;
                }

                latestPath = candidate;
            }

            if (latestPath is not null)
            {
                return latestPath;
            }
        }

        return null;
    }

    public string GetRestartArchiveDirectoryPath(DateTimeOffset timestampUtc)
    {
        var directoryName = timestampUtc.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'");
        return Path.Combine(RestartsDirectoryPath, directoryName);
    }

    public string GetPhaseOperationLogPath(PhaseId phaseId)
    {
        var fileStem = GetPhaseArtifactFileStem(phaseId);
        return Path.Combine(PhasesDirectoryPath, $"{fileStem}.ops.md");
    }

    public string GetPhaseEvidenceMarkdownPath(PhaseId phaseId)
    {
        var fileStem = GetPhaseArtifactFileStem(phaseId);
        return Path.Combine(PhasesDirectoryPath, $"{fileStem}.evidence.md");
    }

    public string GetPhaseEvidenceJsonPath(PhaseId phaseId)
    {
        var fileStem = GetPhaseArtifactFileStem(phaseId);
        return Path.Combine(PhasesDirectoryPath, $"{fileStem}.evidence.json");
    }

    public string? GetLatestExistingPhaseOperationLogPath(PhaseId phaseId)
    {
        var candidate = GetPhaseOperationLogPath(phaseId);
        return File.Exists(candidate) ? candidate : null;
    }

    public string? GetLatestExistingPhaseEvidenceMarkdownPath(PhaseId phaseId)
    {
        var candidate = GetPhaseEvidenceMarkdownPath(phaseId);
        return File.Exists(candidate) ? candidate : null;
    }

    public string? GetLatestExistingPhaseEvidenceJsonPath(PhaseId phaseId)
    {
        var candidate = GetPhaseEvidenceJsonPath(phaseId);
        return File.Exists(candidate) ? candidate : null;
    }

    private static string GetPhaseArtifactFileStem(PhaseId phaseId) => phaseId switch
    {
        PhaseId.Refinement => "00-refinement",
        PhaseId.Spec => "01-spec",
        PhaseId.TechnicalDesign => "02-technical-design",
        PhaseId.Implementation => "03-implementation",
        PhaseId.Review => "04-review",
        PhaseId.ReleaseApproval => "05-release-approval",
        PhaseId.PrPreparation => "06-pr-preparation",
        _ => throw new ArgumentOutOfRangeException(nameof(phaseId), phaseId, "No artifact path is defined for this phase.")
    };

    private static IReadOnlyList<string> GetPhaseArtifactFileStems(PhaseId phaseId) => phaseId switch
    {
        PhaseId.Refinement => ["00-refinement", "00-clarification"],
        PhaseId.Spec => ["01-spec", "01-refinement"],
        _ => [GetPhaseArtifactFileStem(phaseId)]
    };
}
