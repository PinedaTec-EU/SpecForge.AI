using System.Diagnostics;
using SpecForge.Domain.Workflow;

namespace SpecForge.Domain.Application;

internal interface IWorkBranchManager
{
    Task<WorkBranchCreationResult> CreateBranchAsync(
        string workspaceRoot,
        string baseBranch,
        string workBranch,
        CancellationToken cancellationToken = default);

    Task<WorkBranchActivationResult> EnsureActiveWorkBranchAsync(
        string workspaceRoot,
        string usId,
        string workBranch,
        string protectedUserStoryDirectory,
        CancellationToken cancellationToken = default);
}

internal sealed record WorkBranchCreationResult(
    bool IsGitWorkspace,
    bool BranchCreated,
    string? CurrentBranch,
    string? UpstreamBranch);

internal sealed record WorkBranchActivationResult(
    bool IsGitWorkspace,
    bool BranchSwitched,
    bool StashCreated,
    string? PreviousBranch,
    string? CurrentBranch,
    string? StashRef,
    string? StashMessage);

internal sealed class GitWorkBranchManager : IWorkBranchManager
{
    public async Task<WorkBranchCreationResult> CreateBranchAsync(
        string workspaceRoot,
        string baseBranch,
        string workBranch,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseBranch);
        ArgumentException.ThrowIfNullOrWhiteSpace(workBranch);

        if (!await IsGitWorkspaceAsync(workspaceRoot, cancellationToken))
        {
            return new WorkBranchCreationResult(
                IsGitWorkspace: false,
                BranchCreated: false,
                CurrentBranch: null,
                UpstreamBranch: null);
        }

        var normalizedBaseBranch = baseBranch.Trim();
        var normalizedWorkBranch = workBranch.Trim();
        var currentBranch = await GetCurrentBranchAsync(workspaceRoot, cancellationToken);
        if (string.Equals(currentBranch, normalizedWorkBranch, StringComparison.Ordinal))
        {
            return new WorkBranchCreationResult(
                IsGitWorkspace: true,
                BranchCreated: false,
                CurrentBranch: currentBranch,
                UpstreamBranch: null);
        }

        await EnsureLocalBranchExistsAsync(workspaceRoot, normalizedBaseBranch, cancellationToken);
        var upstreamBranch = await GetUpstreamBranchAsync(workspaceRoot, normalizedBaseBranch, cancellationToken);
        var localSha = await RevParseAsync(workspaceRoot, $"refs/heads/{normalizedBaseBranch}", cancellationToken);
        var upstreamSha = await RevParseAsync(workspaceRoot, $"refs/remotes/{upstreamBranch}", cancellationToken);

        if (!string.Equals(localSha, upstreamSha, StringComparison.Ordinal))
        {
            throw new WorkflowDomainException(
                $"Base branch '{normalizedBaseBranch}' is not up to date with upstream '{upstreamBranch}'. Update the local branch before creating '{normalizedWorkBranch}'.");
        }

        if (await LocalBranchExistsAsync(workspaceRoot, normalizedWorkBranch, cancellationToken))
        {
            throw new WorkflowDomainException(
                $"Work branch '{normalizedWorkBranch}' already exists locally. Switch to it or choose a different branch name.");
        }

        await RunGitAsync(
            workspaceRoot,
            ["switch", "--create", normalizedWorkBranch, normalizedBaseBranch],
            cancellationToken);
        var createdBranch = await GetCurrentBranchAsync(workspaceRoot, cancellationToken);
        return new WorkBranchCreationResult(
            IsGitWorkspace: true,
            BranchCreated: true,
            CurrentBranch: createdBranch,
            UpstreamBranch: upstreamBranch);
    }

    public async Task<WorkBranchActivationResult> EnsureActiveWorkBranchAsync(
        string workspaceRoot,
        string usId,
        string workBranch,
        string protectedUserStoryDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(usId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workBranch);
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedUserStoryDirectory);

        if (!await IsGitWorkspaceAsync(workspaceRoot, cancellationToken))
        {
            return new WorkBranchActivationResult(
                IsGitWorkspace: false,
                BranchSwitched: false,
                StashCreated: false,
                PreviousBranch: null,
                CurrentBranch: null,
                StashRef: null,
                StashMessage: null);
        }

        var normalizedWorkBranch = workBranch.Trim();
        var currentBranch = await GetCurrentBranchAsync(workspaceRoot, cancellationToken);
        if (string.Equals(currentBranch, normalizedWorkBranch, StringComparison.Ordinal))
        {
            return new WorkBranchActivationResult(
                IsGitWorkspace: true,
                BranchSwitched: false,
                StashCreated: false,
                PreviousBranch: currentBranch,
                CurrentBranch: currentBranch,
                StashRef: null,
                StashMessage: null);
        }

        if (!await LocalBranchExistsAsync(workspaceRoot, normalizedWorkBranch, cancellationToken))
        {
            throw new WorkflowDomainException(
                $"Work branch '{normalizedWorkBranch}' recorded for user story '{usId}' does not exist locally. Restore or fetch the branch before running the next workflow phase.");
        }

        string? stashMessage = null;
        string? stashRef = null;
        var protectedPathspec = BuildProtectedUserStoryPathspec(workspaceRoot, protectedUserStoryDirectory);
        if (await HasWorkspaceChangesOutsideUserStoryAsync(workspaceRoot, protectedPathspec, cancellationToken))
        {
            stashMessage = $"SpecForge branch guard for {usId}: user must review and accept this stash before recovery. Stashed changes before switching from '{DescribeBranch(currentBranch)}' to '{normalizedWorkBranch}'.";
            await RunGitAsync(
                workspaceRoot,
                ["stash", "push", "--include-untracked", "-m", stashMessage, "--", ".", protectedPathspec],
                cancellationToken);
            stashRef = await GetLatestStashRefAsync(workspaceRoot, cancellationToken);
        }

        await RunGitAsync(workspaceRoot, ["switch", normalizedWorkBranch], cancellationToken);
        var activatedBranch = await GetCurrentBranchAsync(workspaceRoot, cancellationToken);
        return new WorkBranchActivationResult(
            IsGitWorkspace: true,
            BranchSwitched: true,
            StashCreated: stashRef is not null,
            PreviousBranch: currentBranch,
            CurrentBranch: activatedBranch,
            StashRef: stashRef,
            StashMessage: stashMessage);
    }

    private static async Task<bool> IsGitWorkspaceAsync(string workspaceRoot, CancellationToken cancellationToken)
    {
        try
        {
            await RunGitAsync(workspaceRoot, ["rev-parse", "--show-toplevel"], cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task EnsureLocalBranchExistsAsync(
        string workspaceRoot,
        string branchName,
        CancellationToken cancellationToken)
    {
        if (!await LocalBranchExistsAsync(workspaceRoot, branchName, cancellationToken))
        {
            throw new WorkflowDomainException(
                $"Base branch '{branchName}' does not exist locally. Create or fetch it before approving spec.");
        }
    }

    private static async Task<bool> LocalBranchExistsAsync(
        string workspaceRoot,
        string branchName,
        CancellationToken cancellationToken)
    {
        try
        {
            await RevParseAsync(workspaceRoot, $"refs/heads/{branchName}", cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string> GetUpstreamBranchAsync(
        string workspaceRoot,
        string branchName,
        CancellationToken cancellationToken)
    {
        var result = await RunGitAsync(
            workspaceRoot,
            ["for-each-ref", "--format=%(upstream:short)", $"refs/heads/{branchName}"],
            cancellationToken);
        var upstreamBranch = result.StdOut.Trim();
        if (string.IsNullOrWhiteSpace(upstreamBranch))
        {
            throw new WorkflowDomainException(
                $"Base branch '{branchName}' does not have an upstream tracking branch. Update local tracking before creating a work branch.");
        }

        return upstreamBranch;
    }

    private static async Task<string> GetCurrentBranchAsync(string workspaceRoot, CancellationToken cancellationToken)
    {
        var result = await RunGitAsync(workspaceRoot, ["branch", "--show-current"], cancellationToken);
        return result.StdOut.Trim();
    }

    private static async Task<bool> HasWorkspaceChangesOutsideUserStoryAsync(
        string workspaceRoot,
        string protectedPathspec,
        CancellationToken cancellationToken)
    {
        var result = await RunGitAsync(
            workspaceRoot,
            ["status", "--porcelain", "--untracked-files=all", "--", ".", protectedPathspec],
            cancellationToken);
        return !string.IsNullOrWhiteSpace(result.StdOut);
    }

    private static async Task<string?> GetLatestStashRefAsync(string workspaceRoot, CancellationToken cancellationToken)
    {
        var result = await RunGitAsync(workspaceRoot, ["stash", "list", "--format=%gd", "-n", "1"], cancellationToken);
        var stashRef = result.StdOut.Trim();
        return string.IsNullOrWhiteSpace(stashRef) ? null : stashRef;
    }

    private static string BuildProtectedUserStoryPathspec(string workspaceRoot, string protectedUserStoryDirectory)
    {
        var relativePath = Path.GetRelativePath(workspaceRoot, protectedUserStoryDirectory)
            .Replace('\\', '/')
            .TrimEnd('/');
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath.StartsWith("..", StringComparison.Ordinal))
        {
            throw new WorkflowDomainException("User story directory must be inside the workspace before activating the recorded work branch.");
        }

        return $":(exclude){relativePath}/**";
    }

    private static string DescribeBranch(string? branchName) =>
        string.IsNullOrWhiteSpace(branchName) ? "detached HEAD" : branchName;

    private static async Task<string> RevParseAsync(
        string workspaceRoot,
        string revision,
        CancellationToken cancellationToken)
    {
        var result = await RunGitAsync(workspaceRoot, ["rev-parse", "--verify", revision], cancellationToken);
        return result.StdOut.Trim();
    }

    private static async Task<GitCommandResult> RunGitAsync(
        string workspaceRoot,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
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
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new WorkflowDomainException(
                $"Git command failed: git {string.Join(' ', arguments)}. stderr: {stderr.Trim()} stdout: {stdout.Trim()}");
        }

        return new GitCommandResult(stdout, stderr);
    }

    private sealed record GitCommandResult(string StdOut, string StdErr);
}
