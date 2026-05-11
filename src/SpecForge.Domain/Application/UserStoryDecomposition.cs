using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpecForge.Domain.Application;

public sealed record UserStoryDecompositionOptions(
    bool Enabled = false,
    double Threshold = 0.60,
    double Tolerance = 0.10,
    int MaxChildren = 5,
    bool AllowRequiredOverride = false)
{
    public static UserStoryDecompositionOptions Default { get; } = new();

    public double SuggestedFloor => Math.Max(0, Threshold - Tolerance);

    public UserStoryDecompositionOptions Normalize() =>
        this with
        {
            Threshold = Math.Clamp(Threshold, 0, 1),
            Tolerance = Math.Clamp(Tolerance, 0, 1),
            MaxChildren = Math.Max(1, MaxChildren)
        };
}

public sealed record UserStoryDecompositionEvaluationResult(
    double ComplexityScore,
    string Decision,
    string Rationale,
    IReadOnlyList<UserStoryDecompositionChildDraft> ProposedChildren,
    TokenUsage? Usage = null,
    PhaseExecutionMetadata? Execution = null);

public sealed record UserStoryDecompositionChildDraft(
    string Title,
    string Objective,
    IReadOnlyList<string> AcceptanceCriteria,
    IReadOnlyList<string> Dependencies);

public sealed record UserStoryDecompositionDocument(
    string State,
    double ComplexityScore,
    double Threshold,
    double Tolerance,
    string Decision,
    string Rationale,
    IReadOnlyList<UserStoryDecompositionChildDraft> ProposedChildren,
    IReadOnlyList<string> CreatedChildUsIds);

public static class UserStoryDecomposition
{
    public const string DecisionNone = "none";
    public const string DecisionSuggested = "suggested";
    public const string DecisionRequired = "required";
    public const string StatePendingApproval = "pending_approval";
    public const string StateApproved = "approved";
    public const string StateRejected = "rejected";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static string ResolveDecision(double complexityScore, UserStoryDecompositionOptions options)
    {
        var normalized = options.Normalize();
        if (complexityScore >= normalized.Threshold)
        {
            return DecisionRequired;
        }

        return complexityScore >= normalized.SuggestedFloor
            ? DecisionSuggested
            : DecisionNone;
    }

    public static UserStoryDecompositionDocument Normalize(
        UserStoryDecompositionEvaluationResult result,
        UserStoryDecompositionOptions options)
    {
        var normalizedOptions = options.Normalize();
        var score = Math.Clamp(result.ComplexityScore, 0, 1);
        var decision = ResolveDecision(score, normalizedOptions);
        var children = result.ProposedChildren
            .Where(static child => !string.IsNullOrWhiteSpace(child.Title) && !string.IsNullOrWhiteSpace(child.Objective))
            .Take(normalizedOptions.MaxChildren)
            .Select(static child => new UserStoryDecompositionChildDraft(
                child.Title.Trim(),
                child.Objective.Trim(),
                NormalizeLines(child.AcceptanceCriteria),
                NormalizeLines(child.Dependencies)))
            .ToArray();

        if (decision == DecisionNone || children.Length == 0)
        {
            decision = DecisionNone;
            children = [];
        }

        return new UserStoryDecompositionDocument(
            State: decision == DecisionNone ? StateApproved : StatePendingApproval,
            ComplexityScore: score,
            Threshold: normalizedOptions.Threshold,
            Tolerance: normalizedOptions.Tolerance,
            Decision: decision,
            Rationale: string.IsNullOrWhiteSpace(result.Rationale)
                ? "The generated spec does not require decomposition under the configured complexity threshold."
                : result.Rationale.Trim(),
            ProposedChildren: children,
            CreatedChildUsIds: []);
    }

    public static string Serialize(UserStoryDecompositionDocument document) =>
        JsonSerializer.Serialize(document, JsonOptions) + Environment.NewLine;

    public static UserStoryDecompositionDocument Deserialize(string json) =>
        JsonSerializer.Deserialize<UserStoryDecompositionDocument>(json, JsonOptions)
        ?? throw new InvalidOperationException("Decomposition document could not be parsed.");

    public static string RenderMarkdown(string usId, UserStoryDecompositionDocument document)
    {
        var lines = new List<string>
        {
            $"# Decomposition · {usId}",
            string.Empty,
            "## State",
            $"- State: `{document.State}`",
            $"- Decision: `{document.Decision}`",
            $"- Complexity score: `{document.ComplexityScore:0.00}`",
            $"- Threshold: `{document.Threshold:0.00}`",
            $"- Tolerance: `{document.Tolerance:0.00}`",
            string.Empty,
            "## Rationale",
            document.Rationale,
            string.Empty,
            "## Proposed Child User Stories"
        };

        if (document.ProposedChildren.Count == 0)
        {
            lines.Add("- No child user stories proposed.");
        }
        else
        {
            for (var index = 0; index < document.ProposedChildren.Count; index++)
            {
                var child = document.ProposedChildren[index];
                lines.Add($"{index + 1}. {child.Title}");
                lines.Add($"   - Objective: {child.Objective}");
                lines.Add($"   - Acceptance criteria: {FormatInlineList(child.AcceptanceCriteria)}");
                lines.Add($"   - Dependencies: {FormatInlineList(child.Dependencies)}");
            }
        }

        if (document.CreatedChildUsIds.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("## Created Child User Stories");
            lines.AddRange(document.CreatedChildUsIds.Select(static usId => $"- `{usId}`"));
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static IReadOnlyList<string> NormalizeLines(IReadOnlyList<string>? items) =>
        (items ?? Array.Empty<string>())
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item.Trim())
            .ToArray();

    private static string FormatInlineList(IReadOnlyList<string> items) =>
        items.Count == 0 ? "n/a" : string.Join("; ", items);
}
