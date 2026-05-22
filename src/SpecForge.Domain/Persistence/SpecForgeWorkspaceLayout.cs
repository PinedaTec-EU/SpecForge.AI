using System.Diagnostics;

namespace SpecForge.Domain.Persistence;

internal static class SpecForgeWorkspaceLayout
{
    private const string ControlWorkspaceMarkerFileName = "specforge-control-workspace.txt";

    public static string ResolveControlWorkspaceRoot(string workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("Workspace root is required.", nameof(workspaceRoot));
        }

        var fullWorkspaceRoot = Path.GetFullPath(workspaceRoot);
        var markerPath = TryGetControlWorkspaceMarkerPath(fullWorkspaceRoot);
        if (markerPath is null || !File.Exists(markerPath))
        {
            return fullWorkspaceRoot;
        }

        var recordedRoot = File.ReadAllText(markerPath).Trim();
        if (string.IsNullOrWhiteSpace(recordedRoot))
        {
            return fullWorkspaceRoot;
        }

        var controlWorkspaceRoot = Path.GetFullPath(recordedRoot);
        return Directory.Exists(controlWorkspaceRoot)
            ? controlWorkspaceRoot
            : fullWorkspaceRoot;
    }

    public static void EnsureControlWorkspaceRegistered(string workspaceRoot)
    {
        var fullWorkspaceRoot = Path.GetFullPath(workspaceRoot);
        var markerPath = TryGetControlWorkspaceMarkerPath(fullWorkspaceRoot);
        if (markerPath is null || File.Exists(markerPath))
        {
            return;
        }

        var markerDirectory = Path.GetDirectoryName(markerPath);
        if (!string.IsNullOrWhiteSpace(markerDirectory))
        {
            Directory.CreateDirectory(markerDirectory);
        }

        File.WriteAllText(markerPath, fullWorkspaceRoot + Environment.NewLine);
    }

    public static string GetUserStoryWorktreeRoot(string workspaceRoot, string usId)
    {
        if (string.IsNullOrWhiteSpace(usId))
        {
            throw new ArgumentException("US id is required.", nameof(usId));
        }

        var controlWorkspaceRoot = ResolveControlWorkspaceRoot(workspaceRoot);
        var controlDirectory = new DirectoryInfo(controlWorkspaceRoot);
        var parentDirectory = controlDirectory.Parent
            ?? throw new InvalidOperationException("Control workspace must have a parent directory.");

        return Path.Combine(
            parentDirectory.FullName,
            ".specforge-worktrees",
            controlDirectory.Name,
            usId.Trim().ToUpperInvariant());
    }

    public static void MirrorUserStoryDirectory(string sourceWorkspaceRoot, string targetWorkspaceRoot, string usId)
    {
        var sourcePaths = UserStoryFilePaths.ResolveFromWorkspaceRoot(sourceWorkspaceRoot, usId);
        if (!Directory.Exists(sourcePaths.RootDirectory))
        {
            return;
        }

        var targetPaths = UserStoryFilePaths.FromWorkspaceRoot(targetWorkspaceRoot, category: "workflow", usId);
        CopyDirectory(sourcePaths.RootDirectory, targetPaths.RootDirectory);
    }

    private static string? TryGetControlWorkspaceMarkerPath(string workspaceRoot)
    {
        var commonGitDirectory = TryResolveGitCommonDirectory(workspaceRoot);
        if (commonGitDirectory is null)
        {
            return null;
        }

        return Path.Combine(commonGitDirectory, "specforge", ControlWorkspaceMarkerFileName);
    }

    private static string? TryResolveGitCommonDirectory(string workspaceRoot)
    {
        try
        {
            var result = RunGit(workspaceRoot, "rev-parse", "--git-common-dir");
            if (result.ExitCode != 0)
            {
                return null;
            }

            var commonDir = result.StdOut.Trim();
            if (string.IsNullOrWhiteSpace(commonDir))
            {
                return null;
            }

            return Path.GetFullPath(Path.Combine(workspaceRoot, commonDir));
        }
        catch
        {
            return null;
        }
    }

    private static GitCommandResult RunGit(string workspaceRoot, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workspaceRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        process.WaitForExit();
        return new GitCommandResult(
            process.ExitCode,
            process.StandardOutput.ReadToEnd(),
            process.StandardError.ReadToEnd());
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var filePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, filePath);
            var targetFilePath = Path.Combine(targetDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath)!);
            File.Copy(filePath, targetFilePath, overwrite: true);
        }
    }

    private sealed record GitCommandResult(int ExitCode, string StdOut, string StdErr);
}
