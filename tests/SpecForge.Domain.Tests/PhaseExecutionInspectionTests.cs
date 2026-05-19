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

    [Fact]
    public void Receipt_SerializesEffectivePromptAndEffectiveContext_WhenPresent()
    {
        var receipt = new PhaseExecutionReceipt(
            ExecutionId: "execution-1",
            UsId: "US-0001",
            PhaseId: "spec",
            StartedAtUtc: "2026-05-19T10:00:00.0000000+00:00",
            CompletedAtUtc: "2026-05-19T10:00:01.0000000+00:00",
            InputManifest: new PhaseExecutionInputManifest(
                "manifest-hash",
                "/repo",
                "/repo/.specs/us/US-0001/us.md",
                "us-hash",
                "git-head",
                [],
                [],
                null,
                null),
            OutputManifest: new PhaseExecutionOutputManifest(
                "/repo/.specs/us/US-0001/phases/01-spec.md",
                "artifact-hash",
                []),
            Usage: new TokenUsage(10, 20, 30),
            Execution: new PhaseExecutionMetadata("openai-compatible", "test-model"),
            EvidenceRecord: new PhaseExecutionEvidenceRecord(
                new PhaseExecutionEvidenceActor("phase-agent", ProviderKind: "openai-compatible", Model: "test-model"),
                [new PhaseExecutionEvidenceReference("user-story", "/repo/.specs/us/US-0001/us.md", "us-hash")],
                [new PhaseExecutionEvidenceReference("result-artifact", "/repo/.specs/us/US-0001/phases/01-spec.md", "artifact-hash")],
                [new PhaseExecutionEvidenceSetting("policy-key", "shared-phase-policy/v1")],
                [new PhaseExecutionEvidenceTool("model:openai-compatible", "execute", "execution-metadata")],
                null,
                new PhaseExecutionValidationSummary("captured", "Execution persisted evidence links.", ["result-artifact-generated"]),
                [new PhaseExecutionEvidenceLink("receipt", "/repo/.specs/us/US-0001/execution-receipts/execution-1.json", "receipt")]),
            EffectivePrompt: new PhaseExecutionEffectivePrompt("system", "user"),
            EffectiveContext: new PhaseExecutionEffectiveContext(
                "/repo",
                "/repo/.specs/us/US-0001/us.md",
                "git-head",
                [],
                [],
                null,
                null));

        var json = JsonSerializer.Serialize(receipt, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"effectivePrompt\":{", json);
        Assert.Contains("\"effectiveContext\":{", json);
        Assert.Contains("\"evidenceRecord\":{", json);
        Assert.Contains("\"systemPrompt\":\"system\"", json);
        Assert.Contains("\"userPrompt\":\"user\"", json);
        Assert.Contains("\"workspaceRoot\":\"/repo\"", json);
        Assert.Contains("\"validationSummary\":{", json);
    }

    [Fact]
    public void PhaseExecutionPolicyCatalog_DescribesImplementationAndReviewPolicies()
    {
        var implementationPolicy = PhaseExecutionPolicyCatalog.Describe(
            PhaseId.Implementation,
            new PhaseExecutionReadiness(
                PhaseId.Implementation,
                CanExecute: true,
                RequiredPermissions: PhaseExecutionPermissionCatalog.Describe(PhaseId.Implementation)));
        var reviewPolicy = PhaseExecutionPolicyCatalog.Describe(
            PhaseId.Review,
            new PhaseExecutionReadiness(
                PhaseId.Review,
                CanExecute: true,
                RequiredPermissions: PhaseExecutionPermissionCatalog.Describe(PhaseId.Review)),
            reviewEvidencePolicy: "release");

        Assert.Equal("implementation", implementationPolicy.PhaseId);
        Assert.Equal("shared-phase-policy/v1", implementationPolicy.PolicyKey);
        Assert.Contains(implementationPolicy.AllowedTools, tool => tool.Tool == "workspace-write" && tool.Enforcement == "enforced");
        Assert.Contains(implementationPolicy.WritablePaths, path => path.Path == "<workspace-root>/**" && path.Actor == "phase-agent");
        Assert.Contains(implementationPolicy.ForbiddenPaths, path => path.Path == "<workspace-root>/.git/**");
        Assert.Contains(implementationPolicy.EvidenceRequirements, item => item.Id == "implementation_evidence_record");

        Assert.Equal("review", reviewPolicy.PhaseId);
        Assert.Contains("`release`", reviewPolicy.Summary);
        Assert.Contains(reviewPolicy.EvidenceRequirements, item => item.Id == "validation_strategy_evidence" && item.PolicyInput == "release");
        Assert.Contains(reviewPolicy.EligibilityRules, rule => rule.Id == "review_evidence_policy_selected");
    }

    [Fact]
    public void PhaseExecutionEvidenceBuilder_CapturesSharedEvidenceShape()
    {
        var inputManifest = new PhaseExecutionInputManifest(
            "manifest-hash",
            "/repo",
            "/repo/.specs/us/US-0001/us.md",
            "us-hash",
            "git-head",
            [new PhaseExecutionArtifactInput("/repo/.specs/us/US-0001/phases/01-spec.md", "spec-hash", "spec")],
            [new PhaseExecutionArtifactInput("/repo/context/architecture.md", "ctx-hash")],
            new PhaseExecutionArtifactInput("/repo/.specs/us/US-0001/phases/02-technical-design.md", "td-hash", "technical-design"),
            "op-hash");
        var outputManifest = new PhaseExecutionOutputManifest(
            "/repo/.specs/us/US-0001/phases/03-implementation.md",
            "impl-hash",
            [
                new PhaseExecutionArtifactInput("/repo/.specs/us/US-0001/phases/03-implementation.evidence.md", "emd-hash"),
                new PhaseExecutionArtifactInput("/repo/.specs/us/US-0001/phases/03-implementation.evidence.json", "ejson-hash")
            ]);
        var policy = PhaseExecutionPolicyCatalog.Describe(
            PhaseId.Implementation,
            new PhaseExecutionReadiness(
                PhaseId.Implementation,
                CanExecute: true,
                RequiredPermissions: PhaseExecutionPermissionCatalog.Describe(PhaseId.Implementation)));
        var execution = new PhaseExecutionMetadata(
            "openai-compatible",
            "test-model",
            "impl-profile",
            RuntimeVersion: "0.1.5.554",
            AgentName: "implementer",
            AgentRole: "implementation",
            UsedSkills: [".codex/skills/sdd-phase-agents/SKILL.md"]);

        var record = PhaseExecutionEvidenceBuilder.Build(
            PhaseId.Implementation,
            inputManifest,
            outputManifest,
            execution,
            policy,
            "/repo/.specs/us/US-0001/execution-receipts/20260520-implementation.json");

        Assert.Equal("phase-agent", record.Actor.Kind);
        Assert.Contains(record.Inputs, item => item.Kind == "current-artifact");
        Assert.Contains(record.Outputs, item => item.Kind == "phase-evidence");
        Assert.Contains(record.Settings, item => item.Name == "policy-key" && item.Value == "shared-phase-policy/v1");
        Assert.Contains(record.ToolsUsed, item => item.Name == "workspace-write");
        Assert.Equal("captured", record.ValidationSummary.Status);
        Assert.Contains("phase-evidence-generated", record.ValidationSummary.Checks);
        Assert.Contains(record.EvidenceLinks, item => item.Kind == "receipt");
    }

    public void Dispose()
    {
        if (Directory.Exists(workspaceRoot))
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }
}
