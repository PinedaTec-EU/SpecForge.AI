namespace SpecForge.Domain.Application;

internal static class UserStoryMarkdown
{
    public const string MetadataHeading = "## Metadata";

    public static string RequireTrimmed(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message);
        }

        return value.Trim();
    }

    public static string? NormalizeOptionalScalar(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    public static void ValidateUserStoryKind(string kind)
    {
        if (kind is not ("feature" or "bug" or "hotfix"))
        {
            throw new Workflow.WorkflowDomainException($"Unsupported user story kind '{kind}'.");
        }
    }

    public static string RewriteUserStoryInfo(
        string markdown,
        string usId,
        string title,
        string kind,
        string category,
        IReadOnlyCollection<string> tags)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        if (lines.Count == 0 || !lines[0].StartsWith("# ", StringComparison.Ordinal))
        {
            throw new Workflow.WorkflowDomainException("User story heading was not found.");
        }

        lines[0] = $"# {usId} · {title}";
        var metadataIndex = lines.FindIndex(static line => line.Equals(MetadataHeading, StringComparison.OrdinalIgnoreCase));
        if (metadataIndex < 0)
        {
            throw new Workflow.WorkflowDomainException("User story metadata section was not found.");
        }

        var endIndex = lines.Count;
        for (var index = metadataIndex + 1; index < lines.Count; index++)
        {
            if (lines[index].StartsWith("## ", StringComparison.Ordinal))
            {
                endIndex = index;
                break;
            }
        }

        var metadataLines = new List<string>
        {
            MetadataHeading,
            $"- Kind: `{kind}`",
            $"- Category: `{category}`"
        };

        if (tags.Count > 0)
        {
            metadataLines.Add($"- Tags: {string.Join(", ", tags.Select(static tag => $"`{tag}`"))}");
        }

        metadataLines.Add(string.Empty);
        lines.RemoveRange(metadataIndex, endIndex - metadataIndex);
        lines.InsertRange(metadataIndex, metadataLines);

        return string.Join('\n', lines).TrimEnd() + Environment.NewLine;
    }

    public static string ReadTitle(string content, string fallback)
    {
        var titleLine = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .FirstOrDefault(static line => line.StartsWith("# ", StringComparison.Ordinal));

        return titleLine?.Replace("# ", string.Empty, StringComparison.Ordinal).Trim()
            ?? fallback;
    }

    public static string ReadObjectiveSummary(string content)
    {
        var lines = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n');
        var objectiveLines = new List<string>();
        var insideObjective = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Equals("## Objective", StringComparison.OrdinalIgnoreCase))
            {
                insideObjective = true;
                continue;
            }

            if (insideObjective && line.StartsWith("## ", StringComparison.Ordinal))
            {
                break;
            }

            if (insideObjective && line.Length > 0)
            {
                objectiveLines.Add(line);
            }
        }

        var summary = string.Join(" ", objectiveLines).Trim();
        return summary.Length <= 280
            ? summary
            : string.Concat(summary.AsSpan(0, 277), "...");
    }
}
