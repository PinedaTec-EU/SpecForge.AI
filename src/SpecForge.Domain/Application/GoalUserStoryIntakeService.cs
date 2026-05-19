using System.Text.RegularExpressions;

namespace SpecForge.Domain.Application;

internal sealed class GoalUserStoryIntakeService
{
    private readonly RepositoryCategoryCatalog repositoryCategoryCatalog;

    public GoalUserStoryIntakeService(RepositoryCategoryCatalog repositoryCategoryCatalog)
    {
        this.repositoryCategoryCatalog = repositoryCategoryCatalog ?? throw new ArgumentNullException(nameof(repositoryCategoryCatalog));
    }

    public async Task<GoalIntakeResult> CreateUserStoriesFromGoalAsync(
        WorkflowRunner workflowRunner,
        Func<string, CancellationToken, Task<IReadOnlyCollection<UserStorySummary>>> listUserStoriesAsync,
        string workspaceRoot,
        string goalText,
        IReadOnlyList<GoalUserStoryDraft> stories,
        string? goalId,
        string? strategy,
        string actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workflowRunner);
        ArgumentNullException.ThrowIfNull(listUserStoriesAsync);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(goalText);
        if (stories.Count == 0)
        {
            throw new ArgumentException("At least one user story draft is required.", nameof(stories));
        }

        var normalizedGoalId = NormalizeGoalId(goalId);
        var normalizedStrategy = string.IsNullOrWhiteSpace(strategy)
            ? "small-user-stories"
            : strategy.Trim();
        var existingIds = (await listUserStoriesAsync(workspaceRoot, cancellationToken))
            .Select(static story => story.UsId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nextNumber = NextUserStoryNumber(existingIds);
        var normalizedStories = new List<NormalizedGoalUserStoryDraft>(stories.Count);

        for (var index = 0; index < stories.Count; index++)
        {
            var draft = stories[index];
            var usId = string.IsNullOrWhiteSpace(draft.UsId)
                ? NextAvailableUserStoryId(existingIds, ref nextNumber)
                : draft.UsId.Trim().ToUpperInvariant();
            if (!Regex.IsMatch(usId, "^US-[0-9]{4,}$", RegexOptions.CultureInvariant))
            {
                throw new ArgumentException($"User story id '{usId}' must use the US-0001 format.", nameof(stories));
            }

            if (!existingIds.Add(usId))
            {
                throw new ArgumentException($"User story id '{usId}' already exists or is duplicated in the goal intake.", nameof(stories));
            }

            var title = UserStoryMarkdown.RequireTrimmed(draft.Title, "User story title is required.");
            var kind = string.IsNullOrWhiteSpace(draft.Kind) ? "feature" : draft.Kind.Trim();
            var category = string.IsNullOrWhiteSpace(draft.Category) ? "workflow" : draft.Category.Trim();
            repositoryCategoryCatalog.EnsureCategoryIsAllowed(workspaceRoot, category);
            var tags = WorkflowRunner.NormalizeUserStoryTags(draft.Tags);
            _ = UserStoryMarkdown.RequireTrimmed(draft.SourceText, "User story source text is required.");
            normalizedStories.Add(new NormalizedGoalUserStoryDraft(usId, title, kind, category, tags, index + 1, draft));
        }

        var created = new List<GoalUserStoryCreationResult>(stories.Count);
        foreach (var story in normalizedStories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceText = BuildGoalUserStorySource(
                normalizedGoalId,
                goalText.Trim(),
                normalizedStrategy,
                story.Sequence,
                stories.Count,
                story.Draft);
            var rootDirectory = await workflowRunner.CreateUserStoryAsync(
                workspaceRoot,
                story.UsId,
                story.Title,
                story.Kind,
                story.Category,
                sourceText,
                actor,
                story.Tags,
                cancellationToken);

            created.Add(new GoalUserStoryCreationResult(
                story.UsId,
                story.Title,
                story.Kind,
                story.Category,
                story.Tags,
                story.Sequence,
                rootDirectory,
                Path.Combine(rootDirectory, "us.md")));
        }

        return new GoalIntakeResult(
            normalizedGoalId,
            goalText.Trim(),
            normalizedStrategy,
            created[0].UsId,
            created);
    }

    private static string BuildGoalUserStorySource(
        string goalId,
        string goalText,
        string strategy,
        int sequence,
        int totalStories,
        GoalUserStoryDraft draft)
    {
        var acceptanceCriteria = NormalizeList(draft.AcceptanceCriteria);
        var dependencies = NormalizeList(draft.Dependencies);
        var clarifiedAnswers = NormalizeList(draft.ClarifiedAnswers);
        var nonGoals = NormalizeList(draft.NonGoals);
        var lines = new List<string>
        {
            "## SpecForge Goal Intake",
            "",
            $"- Goal: `{goalId}`",
            $"- Strategy: `{strategy}`",
            $"- Sequence: `{sequence}` of `{totalStories}`",
            "- Coding policy: do not implement directly from the broad goal; drive this story through SpecForge SDD phases before code changes.",
            "",
            "## Original Goal",
            "",
            goalText,
            "",
            "## User Story Slice",
            "",
            UserStoryMarkdown.RequireTrimmed(draft.SourceText, "User story source text is required.")
        };

        if (!string.IsNullOrWhiteSpace(draft.MvpOutcome) || !string.IsNullOrWhiteSpace(draft.SliceRationale))
        {
            lines.Add("");
            lines.Add("## MVP Slice");
            lines.Add("");
            if (!string.IsNullOrWhiteSpace(draft.MvpOutcome))
            {
                lines.Add($"- Outcome: {draft.MvpOutcome.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(draft.SliceRationale))
            {
                lines.Add($"- Slice rationale: {draft.SliceRationale.Trim()}");
            }
        }

        if (acceptanceCriteria.Count > 0)
        {
            lines.Add("");
            lines.Add("## Acceptance Intent");
            lines.Add("");
            lines.AddRange(acceptanceCriteria.Select(static item => $"- {item}"));
        }

        if (nonGoals.Count > 0)
        {
            lines.Add("");
            lines.Add("## Non Goals");
            lines.Add("");
            lines.AddRange(nonGoals.Select(static item => $"- {item}"));
        }

        if (clarifiedAnswers.Count > 0)
        {
            lines.Add("");
            lines.Add("## Clarified Intake Answers");
            lines.Add("");
            lines.AddRange(clarifiedAnswers.Select(static item => $"- {item}"));
        }

        if (dependencies.Count > 0)
        {
            lines.Add("");
            lines.Add("## Dependencies");
            lines.Add("");
            lines.AddRange(dependencies.Select(static item => $"- {item}"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static IReadOnlyList<string> NormalizeList(IReadOnlyList<string>? values) =>
        values?
            .Select(static value => value?.Trim())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .ToArray()
        ?? [];

    private static string NormalizeGoalId(string? goalId) =>
        string.IsNullOrWhiteSpace(goalId)
            ? $"GOAL-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}"
            : goalId.Trim().ToUpperInvariant();

    private static int NextUserStoryNumber(IReadOnlySet<string> existingIds)
    {
        var max = existingIds
            .Select(static id => Regex.Match(id, "^US-([0-9]+)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
            .Where(static match => match.Success)
            .Select(static match => int.Parse(match.Groups[1].Value))
            .DefaultIfEmpty(0)
            .Max();
        return max + 1;
    }

    private static string NextAvailableUserStoryId(ISet<string> reservedIds, ref int nextNumber)
    {
        while (true)
        {
            var candidate = $"US-{nextNumber:0000}";
            nextNumber++;
            if (!reservedIds.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private sealed record NormalizedGoalUserStoryDraft(
        string UsId,
        string Title,
        string Kind,
        string Category,
        IReadOnlyList<string> Tags,
        int Sequence,
        GoalUserStoryDraft Draft);
}
