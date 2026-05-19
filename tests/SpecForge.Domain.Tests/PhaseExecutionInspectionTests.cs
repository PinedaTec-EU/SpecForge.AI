using System.Text.Json;
using SpecForge.Domain.Application;
using SpecForge.Domain.Workflow;

namespace SpecForge.Domain.Tests;

public sealed class PhaseExecutionInspectionTests : IDisposable
{
    private readonly string workspaceRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task BuildEffectiveContext_CapturesNormalizedPathsAndHashes()
    {
        Directory.CreateDirectory(workspaceRoot);
        var userStoryPath = Path.Combine(workspaceRoot, ".specs", "us", "US-0001", "us.md");
        var previousArtifactPath = Path.Combine(workspaceRoot, ".specs", "us", "US-0001", "phases", "01-spec.md");
        var contextFilePath = Path.Combine(workspaceRoot, "context", "architecture.md");
        var currentArtifactPath = Path.Combine(workspaceRoot, ".specs", "us", "US-0001", "phases", "02-technical-design.md");

        Directory.CreateDirectory(Path.GetDirectoryName(userStoryPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(previousArtifactPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(contextFilePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(currentArtifactPath)!);
        await File.WriteAllTextAsync(userStoryPath, "# US");
        await File.WriteAllTextAsync(previousArtifactPath, "# Spec");
        await File.WriteAllTextAsync(contextFilePath, "# Context");
        await File.WriteAllTextAsync(currentArtifactPath, "# Technical Design");

        var context = new PhaseExecutionContext(
            WorkspaceRoot: workspaceRoot,
            UsId: "US-0001",
            PhaseId: PhaseId.TechnicalDesign,
            UserStoryPath: userStoryPath,
            PreviousArtifactPaths: new Dictionary<PhaseId, string>
            {
                [PhaseId.Spec] = previousArtifactPath
            },
            ContextFilePaths: [contextFilePath],
            CurrentArtifactPath: currentArtifactPath,
            OperationPrompt: "Tighten the design constraints.");

        var effectiveContext = PhaseExecutionInspectionBuilder.BuildEffectiveContext(workspaceRoot, context);

        Assert.Equal(PhaseExecutionReceiptStore.NormalizePath(workspaceRoot), effectiveContext.WorkspaceRoot);
        Assert.Equal(PhaseExecutionReceiptStore.NormalizePath(userStoryPath), effectiveContext.UserStoryPath);
        Assert.Equal(
            PhaseExecutionReceiptStore.ComputeSha256("Tighten the design constraints."),
            effectiveContext.OperationPromptSha256);

        var previousArtifact = Assert.Single(effectiveContext.PreviousArtifacts);
        Assert.Equal("spec", previousArtifact.PhaseId);
        Assert.Equal(PhaseExecutionReceiptStore.NormalizePath(previousArtifactPath), previousArtifact.Path);
        Assert.NotNull(previousArtifact.Sha256);

        var contextFile = Assert.Single(effectiveContext.ContextFiles);
        Assert.Equal(PhaseExecutionReceiptStore.NormalizePath(contextFilePath), contextFile.Path);
        Assert.NotNull(contextFile.Sha256);

        Assert.NotNull(effectiveContext.CurrentArtifact);
        Assert.Equal("technical-design", effectiveContext.CurrentArtifact!.PhaseId);
        Assert.Equal(PhaseExecutionReceiptStore.NormalizePath(currentArtifactPath), effectiveContext.CurrentArtifact.Path);
    }

    [Fact]
    public void EffectivePrompt_SerializesSourcePromptsAsPartOfSharedContract()
    {
        var prompt = new PhaseExecutionEffectivePrompt(
            SystemPrompt: "system",
            UserPrompt: "user",
            Warnings: ["override drift"],
            SourcePrompts:
            [
                new PhaseExecutionPromptSource(
                    "phase-task",
                    "/repo/.specs/prompts/phases/spec.execute.md",
                    IsOverride: true,
                    ContentSha256: "abc",
                    EmbeddedContentSha256: "def")
            ]);

        var json = JsonSerializer.Serialize(prompt, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"systemPrompt\":\"system\"", json);
        Assert.Contains("\"userPrompt\":\"user\"", json);
        Assert.Contains("\"warnings\":[\"override drift\"]", json);
        Assert.Contains("\"sourcePrompts\":[", json);
        Assert.Contains("\"role\":\"phase-task\"", json);
        Assert.Contains("\"isOverride\":true", json);
        Assert.Contains("\"contentSha256\":\"abc\"", json);
        Assert.Contains("\"embeddedContentSha256\":\"def\"", json);
    }

    public void Dispose()
    {
        if (Directory.Exists(workspaceRoot))
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }
}
