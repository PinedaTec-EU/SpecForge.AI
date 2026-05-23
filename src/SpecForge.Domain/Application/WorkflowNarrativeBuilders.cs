using System.Text;
using SpecForge.Domain.Workflow;

namespace SpecForge.Domain.Application;

internal static class WorkflowNarrativeBuilders
{
    public static string BuildChildUserStorySource(string parentUsId, UserStoryDecompositionChildDraft child)
    {
        var builder = new StringBuilder()
            .AppendLine($"Child user story split from `{parentUsId}`.")
            .AppendLine()
            .AppendLine("## Objective")
            .AppendLine()
            .AppendLine(child.Objective.Trim())
            .AppendLine()
            .AppendLine("## Acceptance Criteria");

        foreach (var criterion in child.AcceptanceCriteria.DefaultIfEmpty("The child scope is implemented and reviewable against the parent spec."))
        {
            builder.AppendLine($"- {criterion}");
        }

        builder
            .AppendLine()
            .AppendLine("## Parent Link")
            .AppendLine()
            .AppendLine($"- Parent US: `{parentUsId}`")
            .AppendLine("- The parent spec is the decision record for this split.");

        if (child.Dependencies.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Dependencies");

            foreach (var dependency in child.Dependencies)
            {
                builder.AppendLine($"- {dependency}");
            }
        }

        return builder.ToString();
    }

    public static string BuildArtifactOperationLogEntry(
        PhaseId phaseId,
        string timestamp,
        string actor,
        string sourceArtifactPath,
        string generatedArtifactPath,
        string? runtimeVersion,
        IReadOnlyCollection<string> contextArtifactPaths,
        string normalizedPrompt)
    {
        var builder = new StringBuilder()
            .AppendLine()
            .AppendLine($"## {timestamp} · `{actor}`")
            .AppendLine()
            .AppendLine($"- Source Artifact: `{sourceArtifactPath.Replace('\\', '/')}`")
            .AppendLine($"- Result Artifact: `{generatedArtifactPath.Replace('\\', '/')}`");

        if (!string.IsNullOrWhiteSpace(runtimeVersion))
        {
            builder.AppendLine($"- Runtime Version: `{runtimeVersion.Trim()}`");
        }

        if (contextArtifactPaths.Count > 0)
        {
            builder.AppendLine("- Context Artifacts:");
            foreach (var contextArtifactPath in contextArtifactPaths)
            {
                builder.AppendLine($"  - `{contextArtifactPath.Replace('\\', '/')}`");
            }
        }

        builder.AppendLine("- Prompt:")
            .AppendLine("```text")
            .AppendLine(normalizedPrompt)
            .AppendLine("```");

        return builder.ToString();
    }
}
