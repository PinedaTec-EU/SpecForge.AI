using SpecForge.Domain.Workflow;

namespace SpecForge.Domain.Application;

public sealed record PhaseExecutionEvidenceRecord(
    PhaseExecutionEvidenceActor Actor,
    IReadOnlyCollection<PhaseExecutionEvidenceReference> Inputs,
    IReadOnlyCollection<PhaseExecutionEvidenceReference> Outputs,
    IReadOnlyCollection<PhaseExecutionEvidenceSetting> Settings,
    IReadOnlyCollection<PhaseExecutionEvidenceTool> ToolsUsed,
    string? BlockingReason,
    PhaseExecutionValidationSummary ValidationSummary,
    IReadOnlyCollection<PhaseExecutionEvidenceLink> EvidenceLinks);

public sealed record PhaseExecutionEvidenceActor(
    string Kind,
    string? ProviderKind = null,
    string? Model = null,
    string? ProfileName = null,
    string? AgentName = null,
    string? AgentRole = null);

public sealed record PhaseExecutionEvidenceReference(
    string Kind,
    string Path,
    string? Sha256 = null,
    string? PhaseId = null);

public sealed record PhaseExecutionEvidenceSetting(
    string Name,
    string Value);

public sealed record PhaseExecutionEvidenceTool(
    string Name,
    string Access,
    string Source);

public sealed record PhaseExecutionEvidenceLink(
    string Label,
    string Path,
    string Kind);

public sealed record PhaseExecutionValidationSummary(
    string Status,
    string Summary,
    IReadOnlyCollection<string> Checks);

public static class PhaseExecutionEvidenceBuilder
{
    public static PhaseExecutionEvidenceRecord Build(
        PhaseId phaseId,
        PhaseExecutionInputManifest inputManifest,
        PhaseExecutionOutputManifest outputManifest,
        PhaseExecutionMetadata? executionMetadata,
        PhaseExecutionPolicy executionPolicy,
        string receiptPath,
        string? blockingReason = null)
    {
        var phaseSlug = WorkflowPresentation.ToPhaseSlug(phaseId);
        var inputs = BuildInputs(inputManifest);
        var outputs = BuildOutputs(outputManifest);
        var settings = BuildSettings(executionMetadata, executionPolicy);
        var toolsUsed = BuildToolsUsed(executionMetadata, executionPolicy);
        var validationSummary = BuildValidationSummary(phaseId, inputManifest, outputManifest);
        var evidenceLinks = BuildEvidenceLinks(inputManifest, outputManifest, receiptPath, phaseSlug);

        return new PhaseExecutionEvidenceRecord(
            Actor: BuildActor(executionMetadata),
            Inputs: inputs,
            Outputs: outputs,
            Settings: settings,
            ToolsUsed: toolsUsed,
            BlockingReason: string.IsNullOrWhiteSpace(blockingReason) ? null : blockingReason.Trim(),
            ValidationSummary: validationSummary,
            EvidenceLinks: evidenceLinks);
    }

    private static PhaseExecutionEvidenceActor BuildActor(PhaseExecutionMetadata? executionMetadata)
    {
        if (executionMetadata is null)
        {
            return new PhaseExecutionEvidenceActor("specforge-runtime");
        }

        return new PhaseExecutionEvidenceActor(
            Kind: string.IsNullOrWhiteSpace(executionMetadata.AgentName) ? "model-execution" : "phase-agent",
            ProviderKind: executionMetadata.ProviderKind,
            Model: executionMetadata.Model,
            ProfileName: executionMetadata.ProfileName,
            AgentName: executionMetadata.AgentName,
            AgentRole: executionMetadata.AgentRole);
    }

    private static IReadOnlyCollection<PhaseExecutionEvidenceReference> BuildInputs(PhaseExecutionInputManifest inputManifest)
    {
        var inputs = new List<PhaseExecutionEvidenceReference>
        {
            new(
                "user-story",
                inputManifest.UserStoryPath,
                inputManifest.UserStorySha256)
        };

        if (inputManifest.CurrentArtifact is not null)
        {
            inputs.Add(new PhaseExecutionEvidenceReference(
                "current-artifact",
                inputManifest.CurrentArtifact.Path,
                inputManifest.CurrentArtifact.Sha256,
                inputManifest.CurrentArtifact.PhaseId));
        }

        inputs.AddRange(inputManifest.PreviousArtifacts.Select(item =>
            new PhaseExecutionEvidenceReference(
                "previous-artifact",
                item.Path,
                item.Sha256,
                item.PhaseId)));

        inputs.AddRange(inputManifest.ContextFiles.Select(item =>
            new PhaseExecutionEvidenceReference(
                "context-file",
                item.Path,
                item.Sha256,
                item.PhaseId)));

        return inputs;
    }

    private static IReadOnlyCollection<PhaseExecutionEvidenceReference> BuildOutputs(PhaseExecutionOutputManifest outputManifest)
    {
        var outputs = new List<PhaseExecutionEvidenceReference>
        {
            new(
                "result-artifact",
                outputManifest.ResultArtifactPath,
                outputManifest.ResultArtifactSha256)
        };

        outputs.AddRange(outputManifest.GeneratedFiles.Select(item =>
            new PhaseExecutionEvidenceReference(
                ClassifyGeneratedFileKind(item.Path),
                item.Path,
                item.Sha256,
                item.PhaseId)));

        return outputs;
    }

    private static IReadOnlyCollection<PhaseExecutionEvidenceSetting> BuildSettings(
        PhaseExecutionMetadata? executionMetadata,
        PhaseExecutionPolicy executionPolicy)
    {
        var settings = new List<PhaseExecutionEvidenceSetting>
        {
            new("policy-key", executionPolicy.PolicyKey),
            new("phase-id", executionPolicy.PhaseId),
            new("repository-access", executionPolicy.Permissions.RepositoryAccess),
            new("workspace-write-access", executionPolicy.Permissions.WorkspaceWriteAccess ? "true" : "false")
        };

        if (!string.IsNullOrWhiteSpace(executionMetadata?.ProviderKind))
        {
            settings.Add(new PhaseExecutionEvidenceSetting("provider-kind", executionMetadata.ProviderKind));
        }

        if (!string.IsNullOrWhiteSpace(executionMetadata?.Model))
        {
            settings.Add(new PhaseExecutionEvidenceSetting("model", executionMetadata.Model));
        }

        if (!string.IsNullOrWhiteSpace(executionMetadata?.ProfileName))
        {
            settings.Add(new PhaseExecutionEvidenceSetting("profile-name", executionMetadata.ProfileName!));
        }

        if (!string.IsNullOrWhiteSpace(executionMetadata?.RuntimeVersion))
        {
            settings.Add(new PhaseExecutionEvidenceSetting("runtime-version", executionMetadata.RuntimeVersion!));
        }

        return settings;
    }

    private static IReadOnlyCollection<PhaseExecutionEvidenceTool> BuildToolsUsed(
        PhaseExecutionMetadata? executionMetadata,
        PhaseExecutionPolicy executionPolicy)
    {
        var tools = new List<PhaseExecutionEvidenceTool>();

        if (!string.IsNullOrWhiteSpace(executionMetadata?.ProviderKind))
        {
            tools.Add(new PhaseExecutionEvidenceTool(
                $"model:{executionMetadata.ProviderKind}",
                "execute",
                "execution-metadata"));
        }

        if (executionPolicy.Permissions.RepositoryAccess is "read" or "read-write")
        {
            tools.Add(new PhaseExecutionEvidenceTool(
                "workspace",
                executionPolicy.Permissions.RepositoryAccess,
                "phase-policy"));
        }

        if (executionPolicy.Permissions.WorkspaceWriteAccess)
        {
            tools.Add(new PhaseExecutionEvidenceTool(
                "workspace-write",
                "write",
                "phase-policy"));
        }

        if (executionMetadata?.UsedSkills is not null)
        {
            tools.AddRange(executionMetadata.UsedSkills
                .Where(static skill => !string.IsNullOrWhiteSpace(skill))
                .Distinct(StringComparer.Ordinal)
                .Select(skill => new PhaseExecutionEvidenceTool(
                    "skill",
                    skill,
                    "execution-metadata")));
        }

        return tools;
    }

    private static PhaseExecutionValidationSummary BuildValidationSummary(
        PhaseId phaseId,
        PhaseExecutionInputManifest inputManifest,
        PhaseExecutionOutputManifest outputManifest)
    {
        var checks = new List<string>
        {
            "result-artifact-generated"
        };

        if (outputManifest.GeneratedFiles.Any(item => item.Path.EndsWith(".evidence.md", StringComparison.OrdinalIgnoreCase) ||
                                                      item.Path.EndsWith(".evidence.json", StringComparison.OrdinalIgnoreCase)))
        {
            checks.Add("phase-evidence-generated");
        }

        if (phaseId == PhaseId.Review &&
            inputManifest.ContextFiles.Any(item => item.Path.EndsWith(".evidence.md", StringComparison.OrdinalIgnoreCase) ||
                                                   item.Path.EndsWith(".evidence.json", StringComparison.OrdinalIgnoreCase)))
        {
            checks.Add("implementation-evidence-consumed");
        }

        var status = checks.Count > 1 ? "captured" : "declared";
        var summary = phaseId switch
        {
            PhaseId.Implementation when checks.Contains("phase-evidence-generated", StringComparer.Ordinal) =>
                "Implementation execution persisted repository evidence artifacts alongside the phase output.",
            PhaseId.Review when checks.Contains("implementation-evidence-consumed", StringComparer.Ordinal) =>
                "Review execution consumed implementation evidence inputs and persisted the review artifact chain.",
            _ => "Execution receipt captured reusable evidence references for this phase."
        };

        return new PhaseExecutionValidationSummary(status, summary, checks);
    }

    private static IReadOnlyCollection<PhaseExecutionEvidenceLink> BuildEvidenceLinks(
        PhaseExecutionInputManifest inputManifest,
        PhaseExecutionOutputManifest outputManifest,
        string receiptPath,
        string phaseSlug)
    {
        var links = new List<PhaseExecutionEvidenceLink>
        {
            new("receipt", PhaseExecutionReceiptStore.NormalizePath(receiptPath), "receipt"),
            new("user-story", inputManifest.UserStoryPath, "user-story"),
            new($"{phaseSlug}-artifact", outputManifest.ResultArtifactPath, "phase-artifact")
        };

        if (inputManifest.CurrentArtifact is not null)
        {
            links.Add(new PhaseExecutionEvidenceLink(
                $"current-{inputManifest.CurrentArtifact.PhaseId ?? phaseSlug}",
                inputManifest.CurrentArtifact.Path,
                "current-artifact"));
        }

        links.AddRange(outputManifest.GeneratedFiles
            .Where(item => item.Path.EndsWith(".evidence.md", StringComparison.OrdinalIgnoreCase) ||
                           item.Path.EndsWith(".evidence.json", StringComparison.OrdinalIgnoreCase) ||
                           item.Path.EndsWith(".raw.md", StringComparison.OrdinalIgnoreCase) ||
                           item.Path.EndsWith(".ops.md", StringComparison.OrdinalIgnoreCase))
            .Select(item => new PhaseExecutionEvidenceLink(
                Path.GetFileName(item.Path),
                item.Path,
                ClassifyGeneratedFileKind(item.Path))));

        return links
            .GroupBy(static link => $"{link.Kind}|{link.Path}", StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();
    }

    private static string ClassifyGeneratedFileKind(string path)
    {
        if (path.EndsWith(".evidence.md", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".evidence.json", StringComparison.OrdinalIgnoreCase))
        {
            return "phase-evidence";
        }

        if (path.EndsWith(".raw.md", StringComparison.OrdinalIgnoreCase))
        {
            return "raw-artifact";
        }

        if (path.EndsWith(".ops.md", StringComparison.OrdinalIgnoreCase))
        {
            return "operation-log";
        }

        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return "artifact-json";
        }

        return "generated-file";
    }
}
