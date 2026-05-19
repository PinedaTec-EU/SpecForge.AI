using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SpecForge.Domain.Workflow;

namespace SpecForge.Domain.Application;

public sealed record PhaseExecutionReceipt(
    string ExecutionId,
    string UsId,
    string PhaseId,
    string StartedAtUtc,
    string CompletedAtUtc,
    PhaseExecutionInputManifest InputManifest,
    PhaseExecutionOutputManifest OutputManifest,
    TokenUsage? Usage,
    PhaseExecutionMetadata? Execution,
    PhaseExecutionEvidenceRecord? EvidenceRecord = null,
    PhaseExecutionEffectivePrompt? EffectivePrompt = null,
    PhaseExecutionEffectiveContext? EffectiveContext = null);

public sealed record PhaseExecutionInputManifest(
    string ManifestSha256,
    string WorkspaceRoot,
    string UserStoryPath,
    string? UserStorySha256,
    string? WorkspaceGitHeadSha,
    IReadOnlyCollection<PhaseExecutionArtifactInput> PreviousArtifacts,
    IReadOnlyCollection<PhaseExecutionArtifactInput> ContextFiles,
    PhaseExecutionArtifactInput? CurrentArtifact,
    string? OperationPromptSha256);

public sealed record PhaseExecutionOutputManifest(
    string ResultArtifactPath,
    string? ResultArtifactSha256,
    IReadOnlyCollection<PhaseExecutionArtifactInput> GeneratedFiles);

public sealed record PhaseExecutionArtifactInput(
    string Path,
    string? Sha256,
    string? PhaseId = null);

public static class PhaseExecutionReceiptStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static PhaseExecutionInputManifest BuildInputManifest(
        string workspaceRoot,
        PhaseExecutionContext context)
    {
        var effectiveContext = PhaseExecutionInspectionBuilder.BuildEffectiveContext(workspaceRoot, context);
        var manifestWithoutHash = new PhaseExecutionInputManifest(
            ManifestSha256: string.Empty,
            WorkspaceRoot: effectiveContext.WorkspaceRoot,
            UserStoryPath: effectiveContext.UserStoryPath,
            UserStorySha256: TryComputeFileSha256(context.UserStoryPath),
            WorkspaceGitHeadSha: effectiveContext.WorkspaceGitHeadSha,
            PreviousArtifacts: effectiveContext.PreviousArtifacts,
            ContextFiles: effectiveContext.ContextFiles,
            CurrentArtifact: effectiveContext.CurrentArtifact,
            OperationPromptSha256: effectiveContext.OperationPromptSha256);

        return manifestWithoutHash with
        {
            ManifestSha256 = ComputeSha256(JsonSerializer.Serialize(manifestWithoutHash, SerializerOptions)) ?? string.Empty
        };
    }

    public static async Task<string> PersistAsync(
        string receiptsDirectoryPath,
        PhaseExecutionReceipt receipt,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(receiptsDirectoryPath);
        var receiptPath = Path.Combine(receiptsDirectoryPath, $"{receipt.ExecutionId}.json");
        await File.WriteAllTextAsync(receiptPath, JsonSerializer.Serialize(receipt, SerializerOptions), cancellationToken);
        return receiptPath;
    }

    public static async Task<PhaseExecutionReceipt?> TryLoadAsync(
        string? receiptPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(receiptPath) || !File.Exists(receiptPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(receiptPath);
        return await JsonSerializer.DeserializeAsync<PhaseExecutionReceipt>(stream, SerializerOptions, cancellationToken);
    }

    public static string? ComputeSha256(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }

    public static string? TryComputeFileSha256(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static string NormalizePath(string path) => path.Replace('\\', '/');

    internal static string? TryReadGitHeadSha(string workspaceRoot)
    {
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    WorkingDirectory = workspaceRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };
            process.StartInfo.ArgumentList.Add("rev-parse");
            process.StartInfo.ArgumentList.Add("HEAD");
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
