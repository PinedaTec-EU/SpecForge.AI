using System.Diagnostics;
using System.Text.Json;

namespace SpecForge.Domain.Application;

public static class WorkspaceActorResolver
{
    private const string SettingsPath = ".specs/configuration/settings.json";

    public static string ResolveForWorkspace(string workspaceRoot) =>
        TryResolveConfiguredOrDetectedUser(workspaceRoot) ?? string.Empty;

    public static string ResolveRequiredUserForWorkspace(string workspaceRoot) =>
        TryResolveConfiguredOrDetectedUser(workspaceRoot)
        ?? throw new InvalidOperationException(
            "SpecForge could not resolve the workspace user. Configure 'User by default' in .specs/configuration/settings.json or set a git user for this workspace.");

    public static string? TryResolveConfiguredOrDetectedUser(string workspaceRoot)
    {
        var configuredUser = TryReadConfiguredUser(workspaceRoot);
        if (!string.IsNullOrWhiteSpace(configuredUser))
        {
            return configuredUser;
        }

        return TryDetectGitUser(workspaceRoot);
    }

    public static string? TryDetectGitUser(string workspaceRoot)
    {
        var userName = TryRunGitConfig(workspaceRoot, "user.name");
        var normalizedUserName = NormalizeGitIdentity(userName);
        if (!string.IsNullOrWhiteSpace(normalizedUserName))
        {
            return normalizedUserName;
        }

        var email = TryRunGitConfig(workspaceRoot, "user.email");
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalizedEmailActor = NormalizeGitIdentity(email.Split('@', 2)[0]);
        return string.IsNullOrWhiteSpace(normalizedEmailActor) ? null : normalizedEmailActor;
    }

    public static string NormalizeConfiguredUser(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    public static string NormalizeIdentity(string? value) =>
        NormalizeConfiguredUser(value);

    private static string NormalizeGitIdentity(string? value)
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

    private static string? TryReadConfiguredUser(string workspaceRoot)
    {
        try
        {
            var settingsPath = Path.Combine(workspaceRoot, SettingsPath);
            if (!File.Exists(settingsPath))
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            if (!document.RootElement.TryGetProperty("defaultUser", out var defaultUserElement))
            {
                return null;
            }

            return NormalizeConfiguredUser(defaultUserElement.GetString());
        }
        catch
        {
            return null;
        }
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

}
