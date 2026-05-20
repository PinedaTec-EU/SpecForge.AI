namespace SpecForge.Domain.Application;

public sealed record RefinementSkillPreselection(
    IReadOnlyCollection<RefinementSkillSelectionItem> RequiredSkills,
    IReadOnlyCollection<RefinementSkillSelectionItem> CandidateSkills,
    IReadOnlyCollection<RefinementSkillSelectionItem> RejectedSkills,
    IReadOnlyCollection<string> ContextGaps);

public sealed record RefinementSkillSelectionItem(
    string SkillPath,
    string Rationale);

public static class RefinementSkillPreselectionBuilder
{
    private static readonly string[] IgnoredDirectoryNames =
    [
        ".git",
        ".specs",
        "node_modules",
        "bin",
        "obj",
        "dist",
        "dist-tests"
    ];

    private const string LocalSddWorkflowSkill = ".codex/skills/sdd-phase-agents/SKILL.md";
    private const string FunctionalCommitSkill = ".codex/skills/functional-commit-version-bump/SKILL.md";
    private const string DotNetSkill = "../ai-skills-shared/.shared-skills/skills/dotnet/SKILL.md";
    private const string HexagonalSkill = "../ai-skills-shared/.shared-skills/skills/hexagonal/SKILL.md";
    private const string DddSkill = "../ai-skills-shared/.shared-skills/skills/ddd/SKILL.md";
    private const string DomainEventsSkill = "../ai-skills-shared/.shared-skills/skills/domain-events/SKILL.md";
    private const string TerraformSkill = "../ai-skills-shared/.shared-skills/skills/terraform/SKILL.md";

    public static RefinementSkillPreselection Build(
        string workspaceRoot,
        PhaseExecutionContext context,
        IReadOnlyCollection<string> pendingQuestions)
    {
        var required = new List<RefinementSkillSelectionItem>
        {
            new(
                LocalSddWorkflowSkill,
                "Refinement in this repository must follow the local SDD workflow guardrails before any downstream phase handoff.")
        };
        var candidates = new List<RefinementSkillSelectionItem>();
        var rejected = new List<RefinementSkillSelectionItem>
        {
            new(
                FunctionalCommitSkill,
                "Functional commit and version-bump rules are not part of refinement analysis because this phase should not mutate deliverable code yet.")
        };

        var userStoryText = File.Exists(context.UserStoryPath)
            ? File.ReadAllText(context.UserStoryPath)
            : string.Empty;
        var signalText = string.Join(
            Environment.NewLine,
            [userStoryText, .. pendingQuestions]);
        var normalizedSignalText = signalText.ToLowerInvariant();

        var isDotNetRepo = WorkspaceContainsAny(workspaceRoot, ".csproj", ".sln", ".slnx");
        var hasTerraformFiles = WorkspaceContainsAny(workspaceRoot, ".tf", ".tfvars");

        if (isDotNetRepo)
        {
            required.Add(new RefinementSkillSelectionItem(
                DotNetSkill,
                "The repository contains .NET solution or project files, so refinement should keep downstream scope aligned with shared .NET engineering rules."));
        }
        else
        {
            rejected.Add(new RefinementSkillSelectionItem(
                DotNetSkill,
                "No .NET solution or project files were detected in the workspace, so the shared .NET skill is not a first-pass refinement dependency."));
        }

        AddConditionalSkill(
            candidates,
            rejected,
            HexagonalSkill,
            MatchesAny(normalizedSignalText, "adapter", "controller", "provider", "store", "port", "api", "mcp", "server", "endpoint"),
            "The user-story wording or refinement questions reference adapters, APIs, or provider boundaries that may need hexagonal guardrails.",
            "No adapter, API, or port-boundary signal was detected in the current refinement context.");

        AddConditionalSkill(
            candidates,
            rejected,
            DddSkill,
            MatchesAny(normalizedSignalText, "domain", "aggregate", "entity", "value object", "policy", "workflow", "invariant"),
            "The refinement context references domain concepts that may need DDD tactical rules before spec proceeds.",
            "No strong domain-modeling signal was detected in the current refinement context.");

        AddConditionalSkill(
            candidates,
            rejected,
            DomainEventsSkill,
            MatchesAny(normalizedSignalText, "event", "emit", "trigger", "notification", "integration"),
            "The refinement context references events or triggers that may need shared domain-event guidance.",
            "No event-driven signal was detected in the current refinement context.");

        AddConditionalSkill(
            candidates,
            rejected,
            TerraformSkill,
            hasTerraformFiles || MatchesAny(normalizedSignalText, "terraform", "infrastructure", "kubernetes", "k8s", "helm"),
            hasTerraformFiles
                ? "Terraform files exist in the workspace, so infra rules may be relevant if the user story touches deployment or environment scope."
                : "The refinement wording references infrastructure concerns that may need Terraform guardrails.",
            "No Terraform files or infrastructure-oriented signal was detected in the current refinement context.");

        var contextGaps = pendingQuestions
            .Where(static question => !string.IsNullOrWhiteSpace(question))
            .Select(static question => question.Trim())
            .ToList();

        if (contextGaps.Count > 0 && context.ContextFilePaths.Count == 0)
        {
            contextGaps.Add("No repository context files are attached yet for this refinement run.");
        }

        return new RefinementSkillPreselection(
            required,
            candidates,
            rejected,
            contextGaps);
    }

    private static void AddConditionalSkill(
        ICollection<RefinementSkillSelectionItem> candidates,
        ICollection<RefinementSkillSelectionItem> rejected,
        string skillPath,
        bool includeAsCandidate,
        string candidateRationale,
        string rejectedRationale)
    {
        if (includeAsCandidate)
        {
            candidates.Add(new RefinementSkillSelectionItem(skillPath, candidateRationale));
            return;
        }

        rejected.Add(new RefinementSkillSelectionItem(skillPath, rejectedRationale));
    }

    private static bool MatchesAny(string value, params string[] signals) =>
        signals.Any(signal => value.Contains(signal, StringComparison.Ordinal));

    private static bool WorkspaceContainsAny(string workspaceRoot, params string[] fileExtensions)
    {
        var pending = new Queue<string>();
        pending.Enqueue(workspaceRoot);
        var normalizedExtensions = fileExtensions
            .Where(static extension => !string.IsNullOrWhiteSpace(extension))
            .Select(static extension => extension.Trim().ToLowerInvariant())
            .ToArray();

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            IEnumerable<string> directories;
            IEnumerable<string> files;

            try
            {
                directories = Directory.EnumerateDirectories(current)
                    .Where(directory => !IgnoredDirectoryNames.Contains(Path.GetFileName(directory), StringComparer.OrdinalIgnoreCase));
                files = Directory.EnumerateFiles(current);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var filePath in files)
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                if (normalizedExtensions.Contains(extension, StringComparer.Ordinal))
                {
                    return true;
                }

                var fileName = Path.GetFileName(filePath).ToLowerInvariant();
                if (normalizedExtensions.Contains(fileName, StringComparer.Ordinal))
                {
                    return true;
                }
            }

            foreach (var directory in directories)
            {
                pending.Enqueue(directory);
            }
        }

        return false;
    }
}
