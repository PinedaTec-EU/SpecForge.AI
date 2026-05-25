using System.Diagnostics;

namespace SpecForge.Domain.Application;

public static class WorkspaceActorResolver
{
    public static string ResolveForWorkspace(string workspaceRoot)
    {
        var userName = TryRunGitConfig(workspaceRoot, "user.name");
        var normalizedUserName = NormalizeIdentity(userName);
        if (!string.IsNullOrWhiteSpace(normalizedUserName))
        {
            return normalizedUserName;
        }

        var email = TryRunGitConfig(workspaceRoot, "user.email");
        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalizedEmailActor = NormalizeIdentity(email.Split('@', 2)[0]);
            if (!string.IsNullOrWhiteSpace(normalizedEmailActor))
            {
                return normalizedEmailActor;
            }
        }

        return "cli-user";
    }

    private static string? TryRunGitConfig(string workspaceRoot, string key)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workspaceRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            process.StartInfo.ArgumentList.Add("config");
            process.StartInfo.ArgumentList.Add("--get");
            process.StartInfo.ArgumentList.Add(key);
            process.Start();
            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? stdout.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeIdentity(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return normalized
            .ToLowerInvariant()
            .Replace(" ", "-", StringComparison.Ordinal)
            .Replace("_", "-", StringComparison.Ordinal);
    }
}
