namespace SpecForge.Domain.Application;

using System.Text.RegularExpressions;

internal static class UserStoryMarkdown
{
    public const string MetadataHeading = "## Metadata";
    private static readonly Regex MarkdownLinkRegex = new(@"^\[(?<label>[^\]]+)\]\((?<url>[^)]+)\)$", RegexOptions.Compiled);
    private static readonly Regex LegacyIssueUrlRegex = new(@"^Issue URL:\s*(?<url>https?://\S+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
        if (!UserStoryKinds.IsSupported(kind))
        {
            throw new Workflow.WorkflowDomainException($"Unsupported user story kind '{kind}'.");
        }
    }

    public static string RewriteUserStoryInfo(
        string markdown,
        string usId,
        string title,
        string kind,
        string createdBy,
        string owner,
        string category,
        IReadOnlyCollection<string> tags,
        IReadOnlyCollection<UserStoryExternalReference> externalReferences)
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

        var metadataLines = BuildMetadataLines(kind, createdBy, owner, category, tags, externalReferences);
        lines.RemoveRange(metadataIndex, endIndex - metadataIndex);
        lines.InsertRange(metadataIndex, metadataLines);
        RewriteLegacyIssueUrlLine(lines, externalReferences);

        return string.Join('\n', lines).TrimEnd() + Environment.NewLine;
    }

    public static List<string> BuildMetadataLines(
        string kind,
        string createdBy,
        string owner,
        string category,
        IReadOnlyCollection<string> tags,
        IReadOnlyCollection<UserStoryExternalReference> externalReferences)
    {
        var metadataLines = new List<string>
        {
            MetadataHeading,
            $"- Kind: `{kind}`",
            $"- Created By: `{createdBy}`",
            $"- Owner: `{owner}`",
            $"- Category: `{category}`"
        };

        if (tags.Count > 0)
        {
            metadataLines.Add($"- Tags: {string.Join(", ", tags.Select(static tag => $"`{tag}`"))}");
        }

        if (externalReferences.Count > 0)
        {
            metadataLines.Add("- External References:");
            metadataLines.AddRange(externalReferences.Select(static reference => $"  - [{reference.Label}]({reference.Url})"));
        }

        metadataLines.Add(string.Empty);
        return metadataLines;
    }

    public static IReadOnlyList<UserStoryExternalReference> NormalizeExternalReferences(
        IReadOnlyCollection<UserStoryExternalReference>? externalReferences)
    {
        if (externalReferences is null || externalReferences.Count == 0)
        {
            return [];
        }

        var normalized = new List<UserStoryExternalReference>(externalReferences.Count);
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in externalReferences)
        {
            var normalizedUrl = NormalizeOptionalScalar(reference.Url);
            if (string.IsNullOrWhiteSpace(normalizedUrl))
            {
                continue;
            }

            if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new Workflow.WorkflowDomainException($"External reference URL '{reference.Url}' is not a valid absolute HTTP(S) URL.");
            }

            var provider = NormalizeOptionalScalar(reference.Provider) ?? InferExternalReferenceProvider(uri);
            var label = NormalizeOptionalScalar(reference.Label) ?? InferExternalReferenceLabel(provider);
            var normalizedReference = new UserStoryExternalReference(uri.AbsoluteUri, label, provider);
            if (seenUrls.Add(normalizedReference.Url))
            {
                normalized.Add(normalizedReference);
            }
        }

        return normalized;
    }

    public static IReadOnlyList<UserStoryExternalReference> ReadExternalReferences(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var references = new List<UserStoryExternalReference>();
        var insideExternalReferences = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            var trimmed = line.Trim();

            if (trimmed.StartsWith("## ", StringComparison.Ordinal) && insideExternalReferences)
            {
                break;
            }

            if (trimmed.StartsWith("- External References:", StringComparison.OrdinalIgnoreCase))
            {
                insideExternalReferences = true;
                continue;
            }

            if (!insideExternalReferences)
            {
                continue;
            }

            if (!line.StartsWith("  - ", StringComparison.Ordinal) && !line.StartsWith("\t- ", StringComparison.Ordinal))
            {
                break;
            }

            var value = trimmed[2..].Trim();
            var reference = ParseExternalReference(value);
            if (reference is not null)
            {
                references.Add(reference);
            }
        }

        var normalizedReferences = NormalizeExternalReferences(references);
        if (normalizedReferences.Count > 0)
        {
            return normalizedReferences;
        }

        var legacyReference = ReadLegacyIssueUrlReference(lines);
        return legacyReference is null
            ? normalizedReferences
            : NormalizeExternalReferences([legacyReference]);
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

    private static UserStoryExternalReference? ParseExternalReference(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var markdownLinkMatch = MarkdownLinkRegex.Match(value);
        if (markdownLinkMatch.Success)
        {
            var url = markdownLinkMatch.Groups["url"].Value.Trim();
            var label = markdownLinkMatch.Groups["label"].Value.Trim();
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return new UserStoryExternalReference(url, label, InferExternalReferenceProvider(uri));
            }
        }

        if (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var rawUri))
        {
            var provider = InferExternalReferenceProvider(rawUri);
            return new UserStoryExternalReference(rawUri.AbsoluteUri, InferExternalReferenceLabel(provider), provider);
        }

        return null;
    }

    private static void RewriteLegacyIssueUrlLine(
        IList<string> lines,
        IReadOnlyCollection<UserStoryExternalReference> externalReferences)
    {
        var replacementUrl = externalReferences.FirstOrDefault()?.Url;
        if (string.IsNullOrWhiteSpace(replacementUrl))
        {
            return;
        }

        for (var index = 0; index < lines.Count; index++)
        {
            if (!LegacyIssueUrlRegex.IsMatch(lines[index].Trim()))
            {
                continue;
            }

            lines[index] = $"Issue URL: {replacementUrl}";
            return;
        }
    }

    private static UserStoryExternalReference? ReadLegacyIssueUrlReference(IEnumerable<string> lines)
    {
        foreach (var rawLine in lines)
        {
            var match = LegacyIssueUrlRegex.Match(rawLine.Trim());
            if (!match.Success)
            {
                continue;
            }

            var url = match.Groups["url"].Value.Trim();
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return null;
            }

            var provider = InferExternalReferenceProvider(uri);
            return new UserStoryExternalReference(uri.AbsoluteUri, InferExternalReferenceLabel(provider), provider);
        }

        return null;
    }

    private static string InferExternalReferenceProvider(Uri uri)
    {
        var host = uri.Host.Trim().ToLowerInvariant();
        if (host.Contains("github.", StringComparison.Ordinal))
        {
            return "github";
        }

        if (host.Contains("atlassian.", StringComparison.Ordinal) || host.Contains("jira.", StringComparison.Ordinal))
        {
            return "jira";
        }

        return "external";
    }

    private static string InferExternalReferenceLabel(string provider) =>
        provider switch
        {
            "github" => "GitHub issue",
            "jira" => "Jira issue",
            _ => "External issue"
        };
}
