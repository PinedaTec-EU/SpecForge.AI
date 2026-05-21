using System.Text.Json;
using SpecForge.Domain.Application;
using SpecForge.Domain.Persistence;
using SpecForge.Domain.Workflow;

namespace SpecForge.Domain.Tests;

public sealed class SpecForgeApplicationServiceTests : IDisposable
{
    private readonly string workspaceRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ListUserStoriesAsync_ReturnsSummariesFromSpecsDirectory()
    {
        var runner = new WorkflowRunner();
        var applicationService = new SpecForgeApplicationService();
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");

        var items = await applicationService.ListUserStoriesAsync(workspaceRoot);

        var summary = Assert.Single(items);
        Assert.Equal("US-0001", summary.UsId);
        Assert.Equal("workflow", summary.Category);
        Assert.Equal("capture", summary.CurrentPhase);
        Assert.Equal("active", summary.Status);
    }

    [Fact]
    public async Task CreateUserStoryAsync_PersistsCustomTagsInSummaryAndWorkflow()
    {
        var applicationService = new SpecForgeApplicationService();

        await applicationService.CreateUserStoryAsync(
            workspaceRoot,
            "US-0001",
            "Tagged story",
            "feature",
            "workflow",
            "Initial source",
            tags: ["UX", "mcp", "ux"]);

        var summary = await applicationService.GetUserStorySummaryAsync(workspaceRoot, "US-0001");
        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        var usMarkdown = await File.ReadAllTextAsync(summary.MainArtifactPath);

        Assert.Equal(["mcp", "ux"], summary.Tags);
        Assert.Equal(["mcp", "ux"], workflow.Tags);
        Assert.Contains("- Tags: `mcp`, `ux`", usMarkdown);
    }

    [Fact]
    public async Task UpdateUserStoryInfoAsync_RewritesMetadataAndPreservesBody()
    {
        var applicationService = new SpecForgeApplicationService();
        await applicationService.CreateUserStoryAsync(
            workspaceRoot,
            "US-0001",
            "Original story",
            "feature",
            "workflow",
            "Initial source",
            tags: ["workflow"]);

        var result = await applicationService.UpdateUserStoryInfoAsync(
            workspaceRoot,
            "US-0001",
            title: "Updated story",
            kind: "bug",
            category: "configuration",
            tags: ["#sf-central", "configuration"]);

        var usMarkdown = await File.ReadAllTextAsync(result.MainArtifactPath);

        Assert.Equal("US-0001", result.UsId);
        Assert.Equal("Updated story", result.Summary.Title.Replace("US-0001 · ", string.Empty, StringComparison.Ordinal));
        Assert.Equal("configuration", result.Summary.Category);
        Assert.Equal(["configuration", "sf-central"], result.Summary.Tags);
        Assert.Contains("# US-0001 · Updated story", usMarkdown);
        Assert.Contains("- Kind: `bug`", usMarkdown);
        Assert.Contains("- Category: `configuration`", usMarkdown);
        Assert.Contains("- Tags: `configuration`, `sf-central`", usMarkdown);
        Assert.Contains("Initial source", usMarkdown);
    }

    [Fact]
    public async Task GetCurrentPhaseAsync_WithIncompleteDependency_BlocksWorkflowStart()
    {
        var applicationService = new SpecForgeApplicationService();
        await applicationService.CreateUserStoriesFromGoalAsync(
            workspaceRoot,
            "/goals Build Central sync.",
            [
                new GoalUserStoryDraft(
                    UsId: "US-0001",
                    Title: "Configure Central connection",
                    Kind: "feature",
                    Category: "workflow",
                    SourceText: "As an admin, I want to configure Central."),
                new GoalUserStoryDraft(
                    UsId: "US-0002",
                    Title: "Sync Central configuration",
                    Kind: "feature",
                    Category: "workflow",
                    SourceText: "As a developer, I want to sync Central configuration.",
                    Dependencies: ["US-0001"])
            ]);

        var summary = await applicationService.GetUserStorySummaryAsync(workspaceRoot, "US-0002");
        var currentPhase = await applicationService.GetCurrentPhaseAsync(workspaceRoot, "US-0002");
        var exception = await Assert.ThrowsAsync<WorkflowDomainException>(
            () => applicationService.GenerateNextPhaseAsync(workspaceRoot, "US-0002"));

        var dependency = Assert.Single(summary.Dependencies);
        Assert.Equal("US-0001", dependency.UsId);
        Assert.False(dependency.IsSatisfied);
        Assert.Equal("blocked", summary.Status);
        Assert.Equal("blocked", currentPhase.Status);
        Assert.False(currentPhase.CanAdvance);
        Assert.Equal("dependency_not_completed", currentPhase.BlockingReason);
        Assert.Contains("dependency_not_completed", exception.Message);
    }

    [Fact]
    public async Task ListUserStoriesAsync_IgnoresIncompleteUserStoryDirectories()
    {
        var runner = new WorkflowRunner();
        var applicationService = new SpecForgeApplicationService();
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, ".specs", "us", "workflow", "US-PROOF-001"));

        var items = await applicationService.ListUserStoriesAsync(workspaceRoot);

        var summary = Assert.Single(items);
        Assert.Equal("US-0001", summary.UsId);
    }

    [Fact]
    public async Task ListUserStoriesAsync_IgnoresDroppedUserStories()
    {
        var runner = new WorkflowRunner();
        var applicationService = new SpecForgeApplicationService();
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0002", "Story two", "feature", "workflow", "Initial source");
        var droppedPaths = UserStoryFilePaths.FromWorkspaceRoot(workspaceRoot, "workflow", "US-0001");
        await File.WriteAllTextAsync(droppedPaths.DroppedMarkerFilePath, "Dropped by user.");

        var items = await applicationService.ListUserStoriesAsync(workspaceRoot);

        var summary = Assert.Single(items);
        Assert.Equal("US-0002", summary.UsId);
    }

    [Fact]
    public async Task ListUserStoriesAsync_DroppedVisibilityReturnsOnlyDroppedUserStories()
    {
        var runner = new WorkflowRunner();
        var applicationService = new SpecForgeApplicationService();
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0002", "Story two", "feature", "workflow", "Initial source");
        var droppedPaths = UserStoryFilePaths.FromWorkspaceRoot(workspaceRoot, "workflow", "US-0001");
        await File.WriteAllTextAsync(droppedPaths.DroppedMarkerFilePath, "Dropped by user.");

        var items = await applicationService.ListUserStoriesAsync(workspaceRoot, "dropped");

        var summary = Assert.Single(items);
        Assert.Equal("US-0001", summary.UsId);
    }

    [Fact]
    public async Task GetUserStorySummaryAsync_ReturnsBranchNameWhenAvailable()
    {
        var runner = new WorkflowRunner();
        var applicationService = new SpecForgeApplicationService();
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await ResolvePendingApprovalQuestionsAsync(runner, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001", "main");

        var summary = await applicationService.GetUserStorySummaryAsync(workspaceRoot, "US-0001");

        Assert.Equal("feature/us-0001-story-one", summary.WorkBranch);
        Assert.Equal("workflow", summary.Category);
        Assert.Equal("active", summary.Status);
    }

    [Fact]
    public async Task RequestRegressionAsync_UsesPhaseSlugAndReturnsUpdatedState()
    {
        var runner = new WorkflowRunner();
        var applicationService = new SpecForgeApplicationService();
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await ResolvePendingApprovalQuestionsAsync(runner, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001", "main");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

        var result = await applicationService.RequestRegressionAsync(
            workspaceRoot,
            "US-0001",
            "technical-design",
            "Review requested regression");

        Assert.Equal("US-0001", result.UsId);
        Assert.Equal("technical-design", result.CurrentPhase);
        Assert.Equal("active", result.Status);
    }

    [Fact]
    public async Task RequestRegressionAsync_ToApprovedSpec_NonDestructivePreservesContinuationControls()
    {
        var runner = new WorkflowRunner();
        var applicationService = new SpecForgeApplicationService();
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await ResolvePendingApprovalQuestionsAsync(runner, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001", "main");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

        var result = await applicationService.RequestRegressionAsync(
            workspaceRoot,
            "US-0001",
            "spec",
            "Return to approved spec");

        Assert.Equal("spec", result.CurrentPhase);
        Assert.Equal("active", result.Status);

        var currentPhase = await applicationService.GetCurrentPhaseAsync(workspaceRoot, "US-0001");
        Assert.True(currentPhase.CanAdvance);
        Assert.False(currentPhase.CanApprove);
        Assert.True(currentPhase.RequiresApproval);
        Assert.Null(currentPhase.BlockingReason);
    }

    [Fact]
    public async Task GetCurrentPhaseAsync_RefinementWithNoQuestions_CanAdvance()
    {
        var fileStore = new UserStoryFileStore();
        var runner = new WorkflowRunner(
            fileStore,
            new DeterministicPhaseExecutionProvider(),
            new RepositoryCategoryCatalog(),
            new NoOpWorkBranchManager());
        var applicationService = new SpecForgeApplicationService(fileStore, runner);
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");

        var paths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, "US-0001");
        var workflowRun = await fileStore.LoadAsync(paths.RootDirectory);
        workflowRun.RestoreState(PhaseId.Refinement, UserStoryStatus.WaitingUser);
        await fileStore.SaveAsync(workflowRun, paths.RootDirectory);
        await File.WriteAllTextAsync(
            paths.RefinementFilePath,
            """
            ## Refinement Log

            - Status: `needs_refinement`
            - Tolerance: `balanced`
            - Reason: No critical business facts are missing.

            ### Questions

            ### Answers
            """);

        var currentPhase = await applicationService.GetCurrentPhaseAsync(workspaceRoot, "US-0001");

        Assert.True(currentPhase.CanAdvance);
        Assert.Null(currentPhase.BlockingReason);
    }

    [Fact]
    public async Task GetUserStoryWorkflowAsync_ExposesVisibleRefinementPolicyInputs()
    {
        var fileStore = new UserStoryFileStore();
        var runner = new WorkflowRunner(
            fileStore,
            new DeterministicPhaseExecutionProvider(),
            new RepositoryCategoryCatalog(),
            new NoOpWorkBranchManager(),
            runtimeVersion: null,
            refinementTolerance: "strict");
        var applicationService = new SpecForgeApplicationService(fileStore, runner);
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");

        var paths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, "US-0001");
        var workflowRun = await fileStore.LoadAsync(paths.RootDirectory);
        workflowRun.RestoreState(PhaseId.Refinement, UserStoryStatus.WaitingUser);
        await fileStore.SaveAsync(workflowRun, paths.RootDirectory);
        await File.WriteAllTextAsync(
            paths.RefinementFilePath,
            """
            ## Refinement Log

            - Status: `needs_refinement`
            - Tolerance: `strict`
            - Reason: Missing business details.

            ### Questions

            1. Which actor executes the workflow?
            2. What visible outcome should the workflow produce?

            ### Answers
            """);

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");

        Assert.NotNull(workflow.Refinement);
        var policy = workflow.Refinement!.Policy;
        Assert.NotNull(policy);
        Assert.Equal("strict", policy!.Tolerance);
        Assert.Equal(2, policy.PendingQuestionCount);
        Assert.Equal(2, policy.UnansweredQuestionCount);
        Assert.Contains(policy.BlockingConditions, condition =>
            condition.Id == "unanswered_questions_require_resolution" &&
            condition.IsCurrentlyBlocking &&
            condition.BlockingReason == "refinement_pending_answers");
        Assert.True(policy.AutoAnswer.IsEnabled);
        Assert.Equal("deterministic", policy.AutoAnswer.Mode);
        Assert.True(policy.AutoAnswer.IsCurrentlyEligible);
        Assert.Equal("eligible", policy.AutoAnswer.EligibilityStatus);
    }

    [Fact]
    public async Task RewindWorkflowAsync_ToApprovedSpec_NonDestructivePreservesContinuationControls()
    {
        var runner = new WorkflowRunner();
        var applicationService = new SpecForgeApplicationService();
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await ResolvePendingApprovalQuestionsAsync(runner, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001", "main");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

        var result = await applicationService.RewindWorkflowAsync(
            workspaceRoot,
            "US-0001",
            "spec");

        Assert.Equal("spec", result.CurrentPhase);
        Assert.Equal("active", result.Status);

        var currentPhase = await applicationService.GetCurrentPhaseAsync(workspaceRoot, "US-0001");
        Assert.True(currentPhase.CanAdvance);
        Assert.False(currentPhase.CanApprove);
        Assert.True(currentPhase.RequiresApproval);
        Assert.Null(currentPhase.BlockingReason);
    }

    [Fact]
    public async Task GenerateNextPhaseAsync_AfterNonDestructiveReviewRewind_ReplaysReviewBeforeReleaseApproval()
    {
        var runner = new WorkflowRunner(new PassingReviewPhaseExecutionProvider());
        var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), runner);
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await ResolvePendingApprovalQuestionsAsync(runner, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001", "main");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await applicationService.GenerateNextPhaseAsync(workspaceRoot, "US-0001");

        var rewind = await applicationService.RewindWorkflowAsync(
            workspaceRoot,
            "US-0001",
            "review");

        Assert.Equal("review", rewind.CurrentPhase);
        Assert.Equal("active", rewind.Status);
        var currentPhase = await applicationService.GetCurrentPhaseAsync(workspaceRoot, "US-0001");
        Assert.True(currentPhase.CanAdvance);
        Assert.Null(currentPhase.BlockingReason);
        Assert.Equal("review", currentPhase.ExecutionPhase);

        var rewoundWorkflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        Assert.Equal("review", rewoundWorkflow.Controls.ExecutionPhase);

        var replay = await applicationService.GenerateNextPhaseAsync(workspaceRoot, "US-0001");

        Assert.Equal("review", replay.CurrentPhase);
        Assert.Equal("active", replay.Status);
        Assert.NotNull(replay.GeneratedArtifactPath);
        Assert.EndsWith("04-review.v02.md", replay.GeneratedArtifactPath, StringComparison.Ordinal);

        var paths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, "US-0001");
        Assert.True(File.Exists(paths.GetPhaseArtifactPath(PhaseId.Review, version: 2)));
        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        Assert.Equal("review", workflow.CurrentPhase);
        Assert.Contains(workflow.Events, timelineEvent =>
            timelineEvent.Code == "phase_completed" &&
            timelineEvent.Phase == "review" &&
            timelineEvent.Artifacts.Any(artifact => artifact.Contains("04-review.v02.md", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task GenerateNextPhaseAsync_AfterFailedReview_ReplaysCurrentReview()
    {
        var fileStore = new UserStoryFileStore();
        var runner = new WorkflowRunner(
            fileStore,
            new RetryPassingReviewPhaseExecutionProvider(),
            new RepositoryCategoryCatalog(),
            new NoOpWorkBranchManager());
        var applicationService = new SpecForgeApplicationService(fileStore, runner);
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Review rerun", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await ResolvePendingApprovalQuestionsAsync(runner, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001", "main");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

        var currentPhase = await applicationService.GetCurrentPhaseAsync(workspaceRoot, "US-0001");
        Assert.False(currentPhase.CanAdvance);
        Assert.Equal("review_failed", currentPhase.BlockingReason);
        Assert.Equal("review", currentPhase.ExecutionPhase);
        Assert.NotNull(currentPhase.ExecutionReadiness);
        Assert.True(currentPhase.ExecutionReadiness!.CanExecute);

        var replay = await applicationService.GenerateNextPhaseAsync(workspaceRoot, "US-0001");

        Assert.Equal("review", replay.CurrentPhase);
        Assert.NotNull(replay.GeneratedArtifactPath);
        Assert.EndsWith("04-review.v02.md", replay.GeneratedArtifactPath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RestartUserStoryFromSourceAsync_ReturnsRegeneratedSpecState()
    {
        var runner = new WorkflowRunner();
        var applicationService = new SpecForgeApplicationService();
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

        var paths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, "US-0001");
        await File.WriteAllTextAsync(paths.MainArtifactPath, "# US-0001 · Story one\n\n## Objective\nUpdated source");

        var result = await applicationService.RestartUserStoryFromSourceAsync(
            workspaceRoot,
            "US-0001",
            "Source changed after spec");

        Assert.Equal("US-0001", result.UsId);
        Assert.Equal("spec", result.CurrentPhase);
        Assert.Equal("waiting-user", result.Status);
        Assert.NotNull(result.GeneratedArtifactPath);
    }

    [Fact]
    public async Task GetUserStoryWorkflowAsync_ReturnsPhaseDetailsControlsAndTimelineEvents()
    {
        var runner = new WorkflowRunner();
        var applicationService = new SpecForgeApplicationService();
        var promptInitializer = new RepositoryPromptInitializer();
        await promptInitializer.InitializeAsync(workspaceRoot);
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.OperateCurrentPhaseArtifactAsync(workspaceRoot, "US-0001", "Keep the spec implementation-only.", actor: "alice");
        var paths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, "US-0001");
        Directory.CreateDirectory(paths.ContextDirectoryPath);
        await File.WriteAllTextAsync(Path.Combine(paths.ContextDirectoryPath, "service.cs"), "Context");
        Directory.CreateDirectory(paths.AttachmentsDirectoryPath);
        await File.WriteAllTextAsync(Path.Combine(paths.AttachmentsDirectoryPath, "notes.md"), "Attachment");

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");

        Assert.Equal("US-0001", workflow.UsId);
        Assert.Equal("spec", workflow.CurrentPhase);
        Assert.Equal("workflow", workflow.Category);
        Assert.Equal("waiting-user", workflow.Status);
        Assert.Equal(8, workflow.Phases.Count);
        Assert.NotNull(workflow.Refinement);
        Assert.Equal("ready_for_spec", workflow.Refinement!.Status);
        Assert.Contains(workflow.Phases, phase => phase.PhaseId == "refinement" && phase.ExpectsHumanIntervention);
        Assert.Contains(workflow.Phases, phase => phase.PhaseId == "technical-design" && !phase.ExpectsHumanIntervention);
        Assert.Contains(workflow.Phases, phase => phase.PhaseId == "refinement" && phase.Title == "Refinement" && phase.ExecutePromptPath is not null);
        Assert.Contains(workflow.Phases, phase => phase.PhaseId == "spec" && phase.IsCurrent && phase.Title == "Spec" && phase.ArtifactPath is not null && phase.OperationLogPath is not null);
        Assert.Contains(workflow.Phases, phase => phase.PhaseId == "spec" && phase.ExecutePromptPath is not null && phase.ApprovePromptPath is not null);
        Assert.All(workflow.ApprovalQuestions, question => Assert.Equal(question.IsResolved, string.Equals(question.Status, "resolved", StringComparison.Ordinal)));
        Assert.False(workflow.Controls.CanApprove);
        Assert.False(workflow.Controls.CanContinue);
        Assert.Empty(workflow.Controls.RegressionTargets);
        Assert.Contains("refinement", workflow.Controls.RewindTargets);
        Assert.Single(workflow.ContextFiles);
        Assert.Equal(paths.ContextDirectoryPath, workflow.ContextFilesDirectoryPath);
        Assert.Single(workflow.Attachments);
        Assert.Equal(paths.AttachmentsDirectoryPath, workflow.AttachmentsDirectoryPath);
        Assert.True(File.Exists(paths.RefinementFilePath));
        var userStory = await File.ReadAllTextAsync(paths.MainArtifactPath);
        Assert.DoesNotContain("## Refinement Log", userStory);
        Assert.Contains("`phase_completed`", workflow.RawTimeline);
        Assert.Contains("`artifact_operated`", workflow.RawTimeline);
        Assert.Contains(workflow.Events, timelineEvent => timelineEvent.Code == "phase_completed");
        Assert.Contains(workflow.Events, timelineEvent => timelineEvent.Code == "artifact_operated" && timelineEvent.Actor == "alice");
        var specIterations = workflow.PhaseIterations
            .Where(iteration => iteration.PhaseId == "spec")
            .OrderBy(iteration => iteration.Attempt)
            .ToArray();
        Assert.Equal(2, specIterations.Length);
        Assert.EndsWith(".ops.md", specIterations[1].OperationLogPath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".md", specIterations[1].OutputArtifactPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetUserStoryWorkflowAsync_ExposesLatestExecutionInspectionPerPhase()
    {
        var runner = new WorkflowRunner(new InspectionAwarePhaseExecutionProvider());
        var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), runner);

        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        var specPhase = Assert.Single(workflow.Phases, phase => phase.PhaseId == "spec");

        Assert.NotNull(specPhase.LatestExecutionInspection);
        Assert.NotNull(specPhase.LatestExecutionInspection!.EffectivePrompt);
        Assert.NotNull(specPhase.LatestExecutionInspection.EffectiveContext);
        Assert.Equal("system instructions", specPhase.LatestExecutionInspection.EffectivePrompt!.SystemPrompt);
        Assert.Equal("user instructions", specPhase.LatestExecutionInspection.EffectivePrompt.UserPrompt);
        Assert.Equal(workflow.MainArtifactPath, specPhase.LatestExecutionInspection.EffectiveContext!.UserStoryPath);
        Assert.NotNull(specPhase.LatestExecutionInspection.ReceiptPath);
    }

    [Fact]
    public async Task GetUserStoryWorkflowAsync_ExposesReceiptLinkedRefinementPolicySnapshot()
    {
        var runner = new WorkflowRunner();
        var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), runner);

        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        var refinementPhase = Assert.Single(workflow.Phases, phase => phase.PhaseId == "refinement");

        Assert.NotNull(refinementPhase.LatestExecutionInspection);
        Assert.NotNull(refinementPhase.LatestExecutionInspection!.RefinementPolicySnapshot);
        Assert.Equal("balanced", refinementPhase.LatestExecutionInspection.RefinementPolicySnapshot!.Tolerance);
        Assert.Equal("not-needed", refinementPhase.LatestExecutionInspection.RefinementPolicySnapshot.AutoAnswer.EligibilityStatus);
        Assert.Contains(
            refinementPhase.LatestExecutionInspection.RefinementPolicySnapshot.BlockingConditions,
            condition => condition.Id == "unanswered_questions_require_resolution");
    }

    [Fact]
    public async Task GetUserStoryWorkflowAsync_ExposesReceiptLinkedRefinementSkillPreselection()
    {
        var runner = new WorkflowRunner();
        var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), runner);

        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "TODO");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        var refinementPhase = Assert.Single(workflow.Phases, phase => phase.PhaseId == "refinement");

        Assert.NotNull(refinementPhase.LatestExecutionInspection);
        Assert.NotNull(refinementPhase.LatestExecutionInspection!.RefinementSkillPreselection);
        Assert.Contains(
            refinementPhase.LatestExecutionInspection.RefinementSkillPreselection!.RequiredSkills,
            skill => skill.SkillPath == ".codex/skills/sdd-phase-agents/SKILL.md");
        Assert.NotEmpty(refinementPhase.LatestExecutionInspection.RefinementSkillPreselection.ContextGaps);
    }

    [Fact]
    public async Task GetUserStoryWorkflowAsync_ExposesReceiptLinkedRefinementGraphScopeRequest()
    {
        var runner = new WorkflowRunner();
        var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), runner);

        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "TODO");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        var refinementPhase = Assert.Single(workflow.Phases, phase => phase.PhaseId == "refinement");

        Assert.NotNull(refinementPhase.LatestExecutionInspection);
        Assert.NotNull(refinementPhase.LatestExecutionInspection!.RefinementGraphScopeRequest);
        Assert.True(refinementPhase.LatestExecutionInspection.RefinementGraphScopeRequest!.Depth >= 1);
        Assert.NotEmpty(refinementPhase.LatestExecutionInspection.RefinementGraphScopeRequest.SeedNodes);
        Assert.NotEmpty(refinementPhase.LatestExecutionInspection.RefinementGraphScopeRequest.SeedFiles);
        Assert.NotEmpty(refinementPhase.LatestExecutionInspection.RefinementGraphScopeRequest.UnresolvedScopeQuestions);
    }

    [Fact]
    public async Task GetUserStoryWorkflowAsync_ExposesReceiptLinkedSpecApprovalPolicySnapshot()
    {
        var runner = new WorkflowRunner();
        var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), runner);

        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        var specPhase = Assert.Single(workflow.Phases, phase => phase.PhaseId == "spec");

        Assert.NotNull(specPhase.LatestExecutionInspection);
        Assert.NotNull(specPhase.LatestExecutionInspection!.SpecApprovalPolicySnapshot);
        Assert.Equal("blocked", specPhase.LatestExecutionInspection.SpecApprovalPolicySnapshot!.Status);
        Assert.Equal("spec_approval_questions_unresolved", specPhase.LatestExecutionInspection.SpecApprovalPolicySnapshot.ApprovalBlockingReason);
        Assert.Contains(
            specPhase.LatestExecutionInspection.SpecApprovalPolicySnapshot.ApprovalRules,
            rule => rule.Id == "human_approval_questions_resolved");
    }

    [Fact]
    public async Task GetUserStoryWorkflowAsync_ExposesReceiptLinkedTechnicalDesignEvidenceRecord()
    {
        var runner = new WorkflowRunner(new InspectionAwarePhaseExecutionProvider());
        var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), runner);

        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await ResolvePendingApprovalQuestionsAsync(runner, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001", "main");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        var technicalDesignPhase = Assert.Single(workflow.Phases, phase => phase.PhaseId == "technical-design");

        Assert.NotNull(technicalDesignPhase.LatestExecutionInspection);
        Assert.NotNull(technicalDesignPhase.LatestExecutionInspection!.EvidenceRecord);
        Assert.Contains(
            technicalDesignPhase.LatestExecutionInspection.EvidenceRecord!.Inputs,
            item => item.Kind == "previous-artifact" && item.PhaseId == "spec");
        Assert.Contains(
            technicalDesignPhase.LatestExecutionInspection.EvidenceRecord.Outputs,
            item => item.Kind == "result-artifact");
        Assert.Contains(
            technicalDesignPhase.LatestExecutionInspection.EvidenceRecord.Settings,
            item => item.Name == "phase-id" && item.Value == "technical-design");
    }

    [Fact]
    public async Task GetUserStoryWorkflowAsync_ExposesTechnicalDesignContextPack()
    {
        var originalUseGraph = Environment.GetEnvironmentVariable("SPECFORGE_USE_SEMANTIC_GRAPH_WHEN_AVAILABLE");
        var originalAllowMutation = Environment.GetEnvironmentVariable("SPECFORGE_ALLOW_GRAPH_BUILD_REFRESH_FOR_TOUCHED_US_SCOPE");
        Environment.SetEnvironmentVariable("SPECFORGE_USE_SEMANTIC_GRAPH_WHEN_AVAILABLE", "true");
        Environment.SetEnvironmentVariable("SPECFORGE_ALLOW_GRAPH_BUILD_REFRESH_FOR_TOUCHED_US_SCOPE", "true");

        try
        {
            var runner = new WorkflowRunner(new InspectionAwarePhaseExecutionProvider());
            var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), runner);

            await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
            await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
            await ResolvePendingApprovalQuestionsAsync(runner, "US-0001");
            await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001", "main");
            var paths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, "US-0001");
            Directory.CreateDirectory(paths.ContextDirectoryPath);
            await File.WriteAllTextAsync(
                paths.GraphScopeRequestPath,
                JsonSerializer.Serialize(
                    new RefinementGraphScopeRequest(
                        2,
                        [new RefinementGraphSeedNode("user-story-intent", "User Story Intent", "Primary design scope root.")],
                        [new PhaseExecutionArtifactInput("src/App/Service.cs", "service-hash", "refinement")],
                        []),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

            var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
            var technicalDesignPhase = Assert.Single(workflow.Phases, phase => phase.PhaseId == "technical-design");

            Assert.NotNull(technicalDesignPhase.LatestExecutionInspection);
            Assert.NotNull(technicalDesignPhase.LatestExecutionInspection!.TechnicalDesignContextPack);
            Assert.True(technicalDesignPhase.LatestExecutionInspection.TechnicalDesignContextPack!.GraphEnabled);
            Assert.NotEmpty(technicalDesignPhase.LatestExecutionInspection.TechnicalDesignContextPack.SelectedSkills);
            Assert.NotNull(technicalDesignPhase.LatestExecutionInspection.TechnicalDesignContextPack.GraphScopeRequest);
            Assert.NotEmpty(technicalDesignPhase.LatestExecutionInspection.TechnicalDesignContextPack.GraphQueryEvidence);
            Assert.Contains(
                technicalDesignPhase.LatestExecutionInspection.TechnicalDesignContextPack.GraphQueryEvidence,
                item => item.QueryKind == "status");
        }
        finally
        {
            Environment.SetEnvironmentVariable("SPECFORGE_USE_SEMANTIC_GRAPH_WHEN_AVAILABLE", originalUseGraph);
            Environment.SetEnvironmentVariable("SPECFORGE_ALLOW_GRAPH_BUILD_REFRESH_FOR_TOUCHED_US_SCOPE", originalAllowMutation);
        }
    }

    [Fact]
    public async Task GetUserStoryWorkflowAsync_ExposesExecutionPolicyPerPhase()
    {
        var runner = new WorkflowRunner(
            new InspectionAwarePhaseExecutionProvider(),
            runtimeVersion: null,
            refinementTolerance: "balanced",
            completedUsLockOnCompleted: true,
            reviewEvidencePolicy: "release");
        var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), runner);

        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        var capturePhase = Assert.Single(workflow.Phases, phase => phase.PhaseId == "capture");
        var reviewPhase = Assert.Single(workflow.Phases, phase => phase.PhaseId == "review");

        Assert.NotNull(capturePhase.ExecutionPolicy);
        Assert.Equal("shared-phase-policy/v1", capturePhase.ExecutionPolicy!.PolicyKey);
        Assert.Contains(capturePhase.ExecutionPolicy.EligibilityRules, rule => rule.Id == "entry_phase_no_model_required");

        Assert.NotNull(reviewPhase.ExecutionPolicy);
        Assert.Equal("read-write", reviewPhase.ExecutionPolicy!.Permissions.RepositoryAccess);
        Assert.Contains(reviewPhase.ExecutionPolicy.EvidenceRequirements, item => item.Id == "validation_strategy_evidence" && item.PolicyInput == "release");
        Assert.Contains(reviewPhase.ExecutionPolicy.EligibilityRules, rule => rule.Id == "review_evidence_policy_selected");
    }

    [Fact]
    public async Task GetUserStoryWorkflowAsync_ExposesTechnicalDesignPolicyVisibility()
    {
        var readiness = new PhaseExecutionReadiness(
            PhaseId.TechnicalDesign,
            CanExecute: true,
            RequiredPermissions: PhaseExecutionPermissionCatalog.Describe(PhaseId.TechnicalDesign),
            AssignedModelSecurity: new PhaseExecutionModelSecurity(
                "openai-compatible",
                "gpt-5",
                "technical-design",
                "read",
                NativeCliRequired: false,
                NativeCliAvailable: false,
                AgentName: "designer",
                AgentRole: "technical-design"),
            ValidationMessage: "Phase permission precheck passed for the assigned agent profile.",
            PhaseSubagentsEnabled: true);
        var runner = new WorkflowRunner(new CapabilityAwarePhaseExecutionProvider(readiness));
        var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), runner);

        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        var technicalDesignPhase = Assert.Single(workflow.Phases, phase => phase.PhaseId == "technical-design");

        Assert.NotNull(technicalDesignPhase.ExecutionReadiness);
        Assert.True(technicalDesignPhase.ExecutionReadiness!.PhaseSubagentsEnabled);
        Assert.NotNull(technicalDesignPhase.ExecutionPolicy);
        Assert.Contains(technicalDesignPhase.ExecutionPolicy!.EvidenceRequirements, item => item.Id == "design_receipt_evidence");
        Assert.Contains(technicalDesignPhase.ExecutionPolicy.EligibilityRules, item => item.Id == "technical_design_subagent_mode_declared");
    }

    [Fact]
    public async Task GetUserStoryWorkflowAsync_ExposesImplementationPolicyVisibility()
    {
        var runner = new WorkflowRunner(
            new InspectionAwarePhaseExecutionProvider(),
            runtimeVersion: null,
            refinementTolerance: "balanced",
            completedUsLockOnCompleted: true,
            reviewEvidencePolicy: "release");
        var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), runner);

        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        var implementationPhase = Assert.Single(workflow.Phases, phase => phase.PhaseId == "implementation");

        Assert.NotNull(implementationPhase.ExecutionReadiness);
        Assert.True(implementationPhase.ExecutionReadiness!.CanExecute);
        Assert.NotNull(implementationPhase.ExecutionPolicy);
        Assert.Equal("read-write", implementationPhase.ExecutionPolicy!.Permissions.RepositoryAccess);
        Assert.True(implementationPhase.ExecutionPolicy.Permissions.WorkspaceWriteAccess);
        Assert.Contains(implementationPhase.ExecutionPolicy.EvidenceRequirements, item => item.Id == "implementation_evidence_record");
        Assert.Contains(implementationPhase.ExecutionPolicy.EvidenceRequirements, item => item.Id == "graph_guided_scope_evidence");
        Assert.Contains(implementationPhase.ExecutionPolicy.EligibilityRules, item => item.Id == "implementation_write_scope_declared");
        Assert.Contains(implementationPhase.ExecutionPolicy.EligibilityRules, item => item.Id == "implementation_review_loop_visible");
    }

    [Fact]
    public async Task GetUserStoryWorkflowAsync_ExposesReviewPolicyVisibility()
    {
        var runner = new WorkflowRunner(
            new InspectionAwarePhaseExecutionProvider(),
            runtimeVersion: null,
            refinementTolerance: "balanced",
            completedUsLockOnCompleted: true,
            reviewEvidencePolicy: "release");
        var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), runner);

        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await ResolvePendingApprovalQuestionsAsync(runner, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001", "main");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        var reviewPhase = Assert.Single(workflow.Phases, phase => phase.PhaseId == "review");

        Assert.NotNull(reviewPhase.ReviewPolicy);
        Assert.Equal("release", reviewPhase.ReviewPolicy!.ActiveEvidencePolicy);
        Assert.True(reviewPhase.ReviewPolicy.ForceApprovalAvailableNow);
        Assert.True(reviewPhase.ReviewPolicy.ForceApprovalRequiresReason);
        Assert.Contains(reviewPhase.ReviewPolicy.EvidenceRules, item => item.EvidenceKind == "automated" && item.IsBlocking);
        Assert.Contains(reviewPhase.ReviewPolicy.EvidenceRules, item => item.EvidenceKind == "operational" && !item.IsBlocking);
        Assert.Contains(reviewPhase.ReviewPolicy.OverrideConditions, item => item.Id == "review_must_be_current_phase" && item.IsCurrentlySatisfied);
    }

    [Fact]
    public async Task GetUserStoryWorkflowAsync_ExposesReceiptLinkedImplementationPolicySnapshot()
    {
        var runner = new WorkflowRunner(new InspectionAwarePhaseExecutionProvider());
        var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), runner);

        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await ResolvePendingApprovalQuestionsAsync(runner, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001", "main");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        var implementationPhase = Assert.Single(workflow.Phases, phase => phase.PhaseId == "implementation");

        Assert.NotNull(implementationPhase.LatestExecutionInspection);
        Assert.NotNull(implementationPhase.LatestExecutionInspection!.ImplementationPolicySnapshot);
        Assert.True(implementationPhase.LatestExecutionInspection.ImplementationPolicySnapshot!.ExecutionAllowed);
        Assert.Equal("read-write", implementationPhase.LatestExecutionInspection.ImplementationPolicySnapshot.Permissions.RepositoryAccess);
        Assert.Contains(
            implementationPhase.LatestExecutionInspection.ImplementationPolicySnapshot.EvidenceRequirements,
            item => item.Id == "implementation_evidence_record");
        Assert.Contains(
            implementationPhase.LatestExecutionInspection.ImplementationPolicySnapshot.EvidenceRequirements,
            item => item.Id == "graph_guided_scope_evidence");
    }

    [Fact]
    public async Task GetUserStoryWorkflowAsync_ExposesReceiptLinkedImplementationStructuredEvidence()
    {
        var runner = new WorkflowRunner(new InspectionAwarePhaseExecutionProvider());
        var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), runner);

        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await ResolvePendingApprovalQuestionsAsync(runner, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001", "main");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        var implementationPhase = Assert.Single(workflow.Phases, phase => phase.PhaseId == "implementation");

        Assert.NotNull(implementationPhase.LatestExecutionInspection);
        Assert.NotNull(implementationPhase.LatestExecutionInspection!.ImplementationStructuredEvidence);
        Assert.NotEmpty(implementationPhase.LatestExecutionInspection.ImplementationStructuredEvidence!.Summary);
        Assert.NotNull(implementationPhase.LatestExecutionInspection.ImplementationStructuredEvidence.EvidenceJsonPath);
        Assert.NotNull(implementationPhase.LatestExecutionInspection.ImplementationStructuredEvidence.EvidenceMarkdownPath);
        Assert.NotNull(implementationPhase.LatestExecutionInspection.ImplementationStructuredEvidence.TouchedFiles);
    }

    [Fact]
    public async Task GetUserStoryWorkflowAsync_ExposesImplementationExecutionInspection()
    {
        var runner = new WorkflowRunner(new InspectionAwarePhaseExecutionProvider());
        var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), runner);

        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await ResolvePendingApprovalQuestionsAsync(runner, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001", "main");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        var implementationPhase = Assert.Single(workflow.Phases, phase => phase.PhaseId == "implementation");

        Assert.NotNull(implementationPhase.LatestExecutionInspection);
        Assert.NotNull(implementationPhase.LatestExecutionInspection!.EffectivePrompt);
        Assert.NotNull(implementationPhase.LatestExecutionInspection.EffectiveContext);
        Assert.Equal("system instructions", implementationPhase.LatestExecutionInspection.EffectivePrompt!.SystemPrompt);
        Assert.Equal("user instructions", implementationPhase.LatestExecutionInspection.EffectivePrompt.UserPrompt);
        Assert.NotEmpty(implementationPhase.LatestExecutionInspection.EffectiveContext!.PreviousArtifacts);
        Assert.NotNull(implementationPhase.LatestExecutionInspection.ReceiptPath);
    }

    [Fact]
    public async Task GetUserStoryWorkflowAsync_ExposesReviewExecutionInspection()
    {
        var runner = new WorkflowRunner(new InspectionAwarePhaseExecutionProvider());
        var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), runner);

        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await ResolvePendingApprovalQuestionsAsync(runner, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001", "main");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        var reviewPhase = Assert.Single(workflow.Phases, phase => phase.PhaseId == "review");

        Assert.NotNull(reviewPhase.LatestExecutionInspection);
        Assert.NotNull(reviewPhase.LatestExecutionInspection!.EffectivePrompt);
        Assert.NotNull(reviewPhase.LatestExecutionInspection.EffectiveContext);
        Assert.Equal("system instructions", reviewPhase.LatestExecutionInspection.EffectivePrompt!.SystemPrompt);
        Assert.Equal("user instructions", reviewPhase.LatestExecutionInspection.EffectivePrompt.UserPrompt);
        Assert.NotEmpty(reviewPhase.LatestExecutionInspection.EffectiveContext!.PreviousArtifacts);
        Assert.NotNull(reviewPhase.LatestExecutionInspection.ReceiptPath);
    }

    [Fact]
    public async Task GetUserStoryWorkflowAsync_ExposesReceiptLinkedReviewStructuredGateResult()
    {
        var runner = new WorkflowRunner(new InspectionAwarePhaseExecutionProvider());
        var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), runner);

        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await ResolvePendingApprovalQuestionsAsync(runner, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001", "main");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        var reviewPhase = Assert.Single(workflow.Phases, phase => phase.PhaseId == "review");

        Assert.NotNull(reviewPhase.LatestExecutionInspection);
        Assert.NotNull(reviewPhase.LatestExecutionInspection!.ReviewStructuredGateResult);
        Assert.Equal("fail", reviewPhase.LatestExecutionInspection.ReviewStructuredGateResult!.Verdict);
        Assert.True(reviewPhase.LatestExecutionInspection.ReviewStructuredGateResult.HasBlockingFindings);
        Assert.NotEmpty(reviewPhase.LatestExecutionInspection.ReviewStructuredGateResult.FindingsSummary);
        Assert.Contains(
            reviewPhase.LatestExecutionInspection.ReviewStructuredGateResult.LinkedEvidence,
            item => item.Kind == "implementation-evidence-markdown");
    }

    [Fact]
    public async Task GetUserStoryWorkflowAsync_ExposesReviewForceApprovalDecision()
    {
        var runner = new WorkflowRunner(new PassingReviewPhaseExecutionProvider());
        var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), runner);

        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Review override", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await ResolvePendingApprovalQuestionsAsync(runner, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001", "main");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ApproveReviewAnywayAsync(workspaceRoot, "US-0001", "User accepts the remaining review risk for this release.");

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        var reviewPhase = Assert.Single(workflow.Phases, phase => phase.PhaseId == "review");

        Assert.NotNull(reviewPhase.ReviewPolicy);
        Assert.False(reviewPhase.ReviewPolicy!.ForceApprovalAvailableNow);
        Assert.Equal("review_force_approval_requires_current_review_phase", reviewPhase.ReviewPolicy.ForceApprovalBlockingReason);
        Assert.NotNull(reviewPhase.ReviewPolicy.LastForceApprovalDecision);
        Assert.Equal("release-approval", reviewPhase.ReviewPolicy.LastForceApprovalDecision!.TargetPhase);
        Assert.Contains("remaining review risk for this release", reviewPhase.ReviewPolicy.LastForceApprovalDecision.Reason);
    }

    [Fact]
    public async Task GetUserStoryWorkflowAsync_ExposesExecutionEnvelopePerPhase()
    {
        var runner = new WorkflowRunner(new InspectionAwarePhaseExecutionProvider());
        var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), runner);

        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        var capturePhase = Assert.Single(workflow.Phases, phase => phase.PhaseId == "capture");
        var implementationPhase = Assert.Single(workflow.Phases, phase => phase.PhaseId == "implementation");

        Assert.NotNull(capturePhase.ExecutionEnvelope);
        Assert.Equal("workflow-entry", capturePhase.ExecutionEnvelope!.ExecutionMode);
        Assert.Equal("runtime-managed", capturePhase.ExecutionEnvelope.SandboxMode);

        Assert.NotNull(implementationPhase.ExecutionEnvelope);
        Assert.Equal("managed-provider", implementationPhase.ExecutionEnvelope!.ExecutionMode);
        Assert.Equal("provider-managed", implementationPhase.ExecutionEnvelope.SandboxMode);
        Assert.Contains(implementationPhase.ExecutionEnvelope.ToolPermissions, item => item.Tool == "context-materialization");
        Assert.Equal("extended", implementationPhase.ExecutionEnvelope.Budget.ComputeTier);
        Assert.Equal("artifact-only", implementationPhase.ExecutionEnvelope.Budget.MutationBudget);
        Assert.Contains(implementationPhase.ExecutionEnvelope.RepositoryBoundaries, item => item.Kind == "forbidden-path" && item.Path == "<workspace-root>/.git/**");
    }

    [Fact]
    public async Task GetUserStoryWorkflowAsync_DescribesCaptureAsWorkflowEntryBoundary()
    {
        var runner = new WorkflowRunner(new InspectionAwarePhaseExecutionProvider());
        var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), runner);

        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        var capturePhase = Assert.Single(workflow.Phases, phase => phase.PhaseId == "capture");
        var specPhase = Assert.Single(workflow.Phases, phase => phase.PhaseId == "spec");

        Assert.NotNull(capturePhase.ExecutionBoundary);
        Assert.Equal("workflow-entry", capturePhase.ExecutionBoundary!.BoundaryKind);
        Assert.False(capturePhase.ExecutionBoundary.IsModelBacked);
        Assert.Contains("does not execute a phase model", capturePhase.ExecutionBoundary.Summary, StringComparison.Ordinal);

        Assert.NotNull(specPhase.ExecutionBoundary);
        Assert.Equal("model-phase", specPhase.ExecutionBoundary!.BoundaryKind);
        Assert.True(specPhase.ExecutionBoundary.IsModelBacked);
    }

    [Fact]
    public async Task GetUserStoryWorkflowAsync_ExposesCaptureExecutionRecord()
    {
        var applicationService = new SpecForgeApplicationService();

        await applicationService.CreateUserStoryAsync(
            workspaceRoot,
            "US-0001",
            "Story one",
            "feature",
            "workflow",
            "Initial source",
            actor: "alice");

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        var capturePhase = Assert.Single(workflow.Phases, phase => phase.PhaseId == "capture");

        Assert.NotNull(capturePhase.CaptureRecord);
        Assert.Equal("alice", capturePhase.CaptureRecord!.Actor);
        Assert.Equal("direct-text", capturePhase.CaptureRecord.SourceKind);
        Assert.Null(capturePhase.CaptureRecord.SourceReference);
        Assert.Contains(capturePhase.CaptureRecord.MaterializedArtifacts, path => path.EndsWith("/us.md", StringComparison.Ordinal));
        Assert.Contains(capturePhase.CaptureRecord.MaterializedArtifacts, path => path.EndsWith("/state.yaml", StringComparison.Ordinal));
        Assert.Contains(capturePhase.CaptureRecord.MaterializedArtifacts, path => path.EndsWith("/timeline.md", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportUserStoryAsync_ExposesImportedCaptureSource()
    {
        var applicationService = new SpecForgeApplicationService();
        var importPath = Path.Combine(workspaceRoot, "incoming.md");
        Directory.CreateDirectory(workspaceRoot);
        await File.WriteAllTextAsync(importPath, "# Imported\n\nSource");

        await applicationService.ImportUserStoryAsync(
            workspaceRoot,
            "US-0001",
            importPath,
            "Imported story",
            "feature",
            "workflow",
            actor: "importer");

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        var capturePhase = Assert.Single(workflow.Phases, phase => phase.PhaseId == "capture");

        Assert.NotNull(capturePhase.CaptureRecord);
        Assert.Equal("imported-markdown", capturePhase.CaptureRecord!.SourceKind);
        Assert.Equal(Path.GetFullPath(importPath).Replace('\\', '/'), capturePhase.CaptureRecord.SourceReference);
    }

    [Fact]
    public async Task GetCurrentPhaseAsync_CompletedWorkflow_CannotAdvance()
    {
        var runner = new WorkflowRunner(
            new UserStoryFileStore(),
            new PassingReviewPhaseExecutionProvider(),
            new RepositoryCategoryCatalog(),
            new NoOpWorkBranchManager(),
            new RecordingPullRequestPublisher());
        var applicationService = new SpecForgeApplicationService();
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await ResolvePendingApprovalQuestionsAsync(runner, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001", "main");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

        var currentPhase = await applicationService.GetCurrentPhaseAsync(workspaceRoot, "US-0001");

        Assert.Equal("pr-preparation", currentPhase.CurrentPhase);
        Assert.Equal("completed", currentPhase.Status);
        Assert.False(currentPhase.CanAdvance);
        Assert.False(currentPhase.CanApprove);
        Assert.False(currentPhase.RequiresApproval);
        Assert.Equal("workflow_completed", currentPhase.BlockingReason);
    }

    [Fact]
    public async Task GetUserStoryWorkflowAsync_CompletedWorkflow_AppendsCompletedPhaseAsCurrent()
    {
        var runner = new WorkflowRunner(
            new UserStoryFileStore(),
            new PassingReviewPhaseExecutionProvider(),
            new RepositoryCategoryCatalog(),
            new NoOpWorkBranchManager(),
            new RecordingPullRequestPublisher());
        var applicationService = new SpecForgeApplicationService();
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await ResolvePendingApprovalQuestionsAsync(runner, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001", "main");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");

        var completedPhase = Assert.Single(workflow.Phases, phase => phase.PhaseId == "completed");
        Assert.True(completedPhase.IsCurrent);
        Assert.Equal("current", completedPhase.State);
        Assert.Contains(workflow.Phases, phase => phase.PhaseId == "pr-preparation" && !phase.IsCurrent);
    }

    [Fact]
    public async Task ReopenCompletedWorkflowAsync_FunctionalIssue_ReturnsWorkflowToSpec()
    {
        var runner = new WorkflowRunner(
            new UserStoryFileStore(),
            new PassingReviewPhaseExecutionProvider(),
            new RepositoryCategoryCatalog(),
            new NoOpWorkBranchManager(),
            new RecordingPullRequestPublisher());
        var applicationService = new SpecForgeApplicationService();
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await ResolvePendingApprovalQuestionsAsync(runner, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001", "main");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

        var result = await applicationService.ReopenCompletedWorkflowAsync(
            workspaceRoot,
            "US-0001",
            "functional-issue",
            "Customer validation found a business rule gap.",
            actor: "alice");

        Assert.Equal("US-0001", result.UsId);
        Assert.Equal("spec", result.CurrentPhase);
        Assert.Equal("waiting-user", result.Status);

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        Assert.Equal("spec", workflow.CurrentPhase);
        Assert.Equal("waiting-user", workflow.Status);
        Assert.Contains(workflow.Events, timelineEvent =>
            timelineEvent.Code == "workflow_reopened"
            && timelineEvent.Actor == "alice"
            && timelineEvent.Summary is not null
            && timelineEvent.Summary.Contains("functional-issue", StringComparison.Ordinal)
            && timelineEvent.Summary.Contains("spec", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReopenCompletedWorkflowAsync_Defect_ReturnsWorkflowToImplementation()
    {
        var runner = new WorkflowRunner(
            new UserStoryFileStore(),
            new PassingReviewPhaseExecutionProvider(),
            new RepositoryCategoryCatalog(),
            new NoOpWorkBranchManager(),
            new RecordingPullRequestPublisher());
        var applicationService = new SpecForgeApplicationService();
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await ResolvePendingApprovalQuestionsAsync(runner, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001", "main");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

        var result = await applicationService.ReopenCompletedWorkflowAsync(
            workspaceRoot,
            "US-0001",
            "defect",
            "Production validation found a bug in the delivered behavior.",
            actor: "alice");

        Assert.Equal("US-0001", result.UsId);
        Assert.Equal("implementation", result.CurrentPhase);
        Assert.Equal("active", result.Status);

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        Assert.Equal("implementation", workflow.CurrentPhase);
        Assert.Equal("active", workflow.Status);
        Assert.Contains(workflow.Events, timelineEvent =>
            timelineEvent.Code == "workflow_reopened"
            && timelineEvent.Actor == "alice"
            && timelineEvent.Summary is not null
            && timelineEvent.Summary.Contains("defect", StringComparison.Ordinal)
            && timelineEvent.Summary.Contains("implementation", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReopenCompletedWorkflowAsync_TechnicalIssue_ReturnsWorkflowToTechnicalDesign()
    {
        var runner = new WorkflowRunner(
            new UserStoryFileStore(),
            new PassingReviewPhaseExecutionProvider(),
            new RepositoryCategoryCatalog(),
            new NoOpWorkBranchManager(),
            new RecordingPullRequestPublisher());
        var applicationService = new SpecForgeApplicationService();
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await ResolvePendingApprovalQuestionsAsync(runner, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001", "main");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

        var result = await applicationService.ReopenCompletedWorkflowAsync(
            workspaceRoot,
            "US-0001",
            "technical-issue",
            "APR found technical debt that requires design corrections.",
            actor: "alice");

        Assert.Equal("US-0001", result.UsId);
        Assert.Equal("technical-design", result.CurrentPhase);
        Assert.Equal("active", result.Status);

        var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, "US-0001");
        Assert.Equal("technical-design", workflow.CurrentPhase);
        Assert.Equal("active", workflow.Status);
        Assert.Contains(workflow.Events, timelineEvent =>
            timelineEvent.Code == "workflow_reopened"
            && timelineEvent.Actor == "alice"
            && timelineEvent.Summary is not null
            && timelineEvent.Summary.Contains("technical-issue", StringComparison.Ordinal)
            && timelineEvent.Summary.Contains("technical-design", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RewindWorkflowAsync_CompletedWorkflowWithLockEnabled_Throws()
    {
        var runner = new WorkflowRunner(
            new UserStoryFileStore(),
            new PassingReviewPhaseExecutionProvider(),
            new RepositoryCategoryCatalog(),
            new NoOpWorkBranchManager(),
            new RecordingPullRequestPublisher(),
            runtimeVersion: null,
            refinementTolerance: "balanced",
            completedUsLockOnCompleted: true);
        var applicationService = new SpecForgeApplicationService();
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await ResolvePendingApprovalQuestionsAsync(runner, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001", "main");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

        var error = await Assert.ThrowsAsync<WorkflowDomainException>(() =>
            applicationService.RewindWorkflowAsync(workspaceRoot, "US-0001", "review"));

        Assert.Contains("locked", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetCurrentPhaseAsync_WithUnresolvedSpecApprovalQuestions_CannotApprove()
    {
        var runner = new WorkflowRunner();
        var applicationService = new SpecForgeApplicationService();
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

        var currentPhase = await applicationService.GetCurrentPhaseAsync(workspaceRoot, "US-0001");

        Assert.Equal("spec", currentPhase.CurrentPhase);
        Assert.False(currentPhase.CanAdvance);
        Assert.False(currentPhase.CanApprove);
        Assert.True(currentPhase.RequiresApproval);
        Assert.Equal("spec_pending_user_approval", currentPhase.BlockingReason);
    }

    [Fact]
    public async Task AddUserStoryFilesAsync_CopiesFilesIntoRequestedKind()
    {
        var runner = new WorkflowRunner();
        var applicationService = new SpecForgeApplicationService();
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        var sourcePath = Path.Combine(workspaceRoot, "src", "service.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        await File.WriteAllTextAsync(sourcePath, "class Service {}");

        var result = await applicationService.AddUserStoryFilesAsync(
            workspaceRoot,
            "US-0001",
            [sourcePath],
            "context");

        var contextFile = Assert.Single(result.ContextFiles);
        Assert.Equal("service.cs", contextFile.Name);
        Assert.Empty(result.Attachments);
        Assert.True(File.Exists(contextFile.Path));
    }

    [Fact]
    public async Task SetUserStoryFileKindAsync_MovesFilesBetweenContextAndAttachments()
    {
        var runner = new WorkflowRunner();
        var applicationService = new SpecForgeApplicationService();
        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        var paths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, "US-0001");
        Directory.CreateDirectory(paths.AttachmentsDirectoryPath);
        var attachmentPath = Path.Combine(paths.AttachmentsDirectoryPath, "notes.md");
        await File.WriteAllTextAsync(attachmentPath, "Attachment");

        var result = await applicationService.SetUserStoryFileKindAsync(
            workspaceRoot,
            "US-0001",
            attachmentPath,
            "context");

        Assert.Empty(result.Attachments);
        var contextFile = Assert.Single(result.ContextFiles);
        Assert.Equal("notes.md", contextFile.Name);
        Assert.True(File.Exists(contextFile.Path));
        Assert.False(File.Exists(attachmentPath));
    }

    [Fact]
    public async Task GenerateNextPhaseAsync_PersistsRuntimeStatusAndRejectsDuplicateExecutionWhileRunning()
    {
        var provider = new BlockingPhaseExecutionProvider();
        var runner = new WorkflowRunner(provider);
        var applicationService = new SpecForgeApplicationService(
            new UserStoryFileStore(),
            runner,
            new RepositoryPromptInitializer(),
            new RepositoryCategoryCatalog(),
            new UserStoryRuntimeStatusStore());

        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");

        var runningTask = applicationService.GenerateNextPhaseAsync(workspaceRoot, "US-0001");
        await provider.WaitUntilStartedAsync();

        var runtimeWhileRunning = await applicationService.GetUserStoryRuntimeStatusAsync(workspaceRoot, "US-0001");
        Assert.Equal("running", runtimeWhileRunning.Status);
        Assert.Equal("generate-next-phase", runtimeWhileRunning.ActiveOperation);
        Assert.Equal("capture", runtimeWhileRunning.CurrentPhase);
        Assert.False(runtimeWhileRunning.IsStale);
        Assert.NotNull(runtimeWhileRunning.StartedAtUtc);
        Assert.NotNull(runtimeWhileRunning.LastHeartbeatUtc);

        var currentPhaseWhileRunning = await applicationService.GetCurrentPhaseAsync(workspaceRoot, "US-0001");
        Assert.False(currentPhaseWhileRunning.CanAdvance);
        Assert.False(currentPhaseWhileRunning.CanApprove);
        Assert.Equal("phase_execution_in_progress", currentPhaseWhileRunning.BlockingReason);

        var duplicateException = await Assert.ThrowsAsync<WorkflowDomainException>(
            () => applicationService.GenerateNextPhaseAsync(workspaceRoot, "US-0001"));
        Assert.Contains("phase_execution_in_progress", duplicateException.Message);

        provider.Release();
        var result = await runningTask;
        Assert.Equal("spec", result.CurrentPhase);

        var runtimeAfterCompletion = await applicationService.GetUserStoryRuntimeStatusAsync(workspaceRoot, "US-0001");
        Assert.Equal("idle", runtimeAfterCompletion.Status);
        Assert.Null(runtimeAfterCompletion.ActiveOperation);
        Assert.Equal("succeeded", runtimeAfterCompletion.LastOutcome);
        Assert.NotNull(runtimeAfterCompletion.LastCompletedAtUtc);
    }

    [Fact]
    public async Task GenerateNextPhaseAsync_IgnoresRuntimeLockFromDeadOwnerProcess()
    {
        var runner = new WorkflowRunner(new DeterministicPhaseExecutionProvider());
        var deadOwnerStore = new UserStoryRuntimeStatusStore(currentProcessId: int.MaxValue);
        var applicationService = new SpecForgeApplicationService(
            new UserStoryFileStore(),
            runner,
            new RepositoryPromptInitializer(),
            new RepositoryCategoryCatalog(),
            deadOwnerStore);

        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");

        var paths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, "US-0001");
        await using (var ignored = await deadOwnerStore.StartOperationAsync(
            paths.RootDirectory,
            "US-0001",
            "capture",
            "generate-next-phase"))
        {
        }

        var runtimeBeforeRecovery = await applicationService.GetUserStoryRuntimeStatusAsync(workspaceRoot, "US-0001");
        Assert.Equal("running", runtimeBeforeRecovery.Status);
        Assert.True(runtimeBeforeRecovery.IsStale);

        var currentPhaseBeforeRecovery = await applicationService.GetCurrentPhaseAsync(workspaceRoot, "US-0001");
        Assert.True(currentPhaseBeforeRecovery.CanAdvance);
        Assert.Null(currentPhaseBeforeRecovery.BlockingReason);

        var result = await applicationService.GenerateNextPhaseAsync(workspaceRoot, "US-0001");

        Assert.Equal("spec", result.CurrentPhase);
    }

    [Fact]
    public async Task GetCurrentPhaseAsync_BlocksAdvanceWhenImplementationProfileLacksRepositoryWriteAccess()
    {
        var runner = new WorkflowRunner(new CapabilityAwarePhaseExecutionProvider(
            new PhaseExecutionReadiness(PhaseId.Implementation, CanExecute: false, PhaseExecutionBlockingReasons.ImplementationRequiresRepositoryWriteAccess)));
        var applicationService = new SpecForgeApplicationService(
            new UserStoryFileStore(),
            runner,
            new RepositoryPromptInitializer(),
            new RepositoryCategoryCatalog(),
            new UserStoryRuntimeStatusStore());

        await runner.CreateUserStoryAsync(workspaceRoot, "US-0001", "Story one", "feature", "workflow", "Initial source");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");
        await ResolvePendingApprovalQuestionsAsync(runner, "US-0001");
        await runner.ApproveCurrentPhaseAsync(workspaceRoot, "US-0001", "main");
        await runner.ContinuePhaseAsync(workspaceRoot, "US-0001");

        var currentPhase = await applicationService.GetCurrentPhaseAsync(workspaceRoot, "US-0001");

        Assert.Equal("technical-design", currentPhase.CurrentPhase);
        Assert.False(currentPhase.CanAdvance);
        Assert.False(currentPhase.CanApprove);
        Assert.False(currentPhase.RequiresApproval);
        Assert.Equal(PhaseExecutionBlockingReasons.ImplementationRequiresRepositoryWriteAccess, currentPhase.BlockingReason);

        var error = await Assert.ThrowsAsync<WorkflowDomainException>(() =>
            applicationService.GenerateNextPhaseAsync(workspaceRoot, "US-0001"));
        Assert.Contains(PhaseExecutionBlockingReasons.ImplementationRequiresRepositoryWriteAccess, error.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(workspaceRoot))
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    private async Task ResolvePendingApprovalQuestionsAsync(WorkflowRunner runner, string usId)
    {
        var paths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, usId);
        var artifactPath = paths.GetLatestExistingPhaseArtifactPath(PhaseId.Spec)
            ?? throw new InvalidOperationException("Expected a spec artifact before resolving approval questions.");
        var markdown = await File.ReadAllTextAsync(artifactPath);
        var pendingQuestions = ApprovalQuestionMarkdown.ParseFromMarkdown(markdown)
            .Where(static item => !item.Resolved)
            .Select(static item => item.Question)
            .ToArray();

        foreach (var question in pendingQuestions)
        {
            await runner.SubmitApprovalAnswerAsync(
                workspaceRoot,
                usId,
                question,
                $"Resolved in test setup for: {question}",
                "test-user");
        }
    }

    private sealed class BlockingPhaseExecutionProvider : IPhaseExecutionProvider
    {
        private readonly TaskCompletionSource<bool> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly DeterministicPhaseExecutionProvider inner = new();

        public PhaseExecutionReadiness GetPhaseExecutionReadiness(PhaseId phaseId) =>
            inner.GetPhaseExecutionReadiness(phaseId);

        public async Task<PhaseExecutionResult> ExecuteAsync(PhaseExecutionContext context, CancellationToken cancellationToken = default)
        {
            started.TrySetResult(true);
            await release.Task.WaitAsync(cancellationToken);
            return await inner.ExecuteAsync(context, cancellationToken);
        }

        public Task<AutoRefinementAnswersResult?> TryAutoAnswerRefinementAsync(
            PhaseExecutionContext context,
            RefinementSession session,
            CancellationToken cancellationToken = default) =>
            inner.TryAutoAnswerRefinementAsync(context, session, cancellationToken);

        public Task WaitUntilStartedAsync() => started.Task;

        public void Release() => release.TrySetResult(true);
    }

    private sealed class CapabilityAwarePhaseExecutionProvider : IPhaseExecutionProvider
    {
        private readonly DeterministicPhaseExecutionProvider inner = new();
        private readonly IReadOnlyDictionary<PhaseId, PhaseExecutionReadiness> readinessByPhase;

        public CapabilityAwarePhaseExecutionProvider(params PhaseExecutionReadiness[] readiness)
        {
            readinessByPhase = readiness.ToDictionary(item => item.PhaseId);
        }

        public PhaseExecutionReadiness GetPhaseExecutionReadiness(PhaseId phaseId) =>
            readinessByPhase.TryGetValue(phaseId, out var readiness)
                ? readiness
                : inner.GetPhaseExecutionReadiness(phaseId);

        public Task<AutoRefinementAnswersResult?> TryAutoAnswerRefinementAsync(
            PhaseExecutionContext context,
            RefinementSession session,
            CancellationToken cancellationToken = default) =>
            inner.TryAutoAnswerRefinementAsync(context, session, cancellationToken);

        public Task<PhaseExecutionResult> ExecuteAsync(PhaseExecutionContext context, CancellationToken cancellationToken = default) =>
            inner.ExecuteAsync(context, cancellationToken);
    }

    private sealed class InspectionAwarePhaseExecutionProvider : IPhaseExecutionProvider
    {
        private readonly DeterministicPhaseExecutionProvider inner = new();

        public PhaseExecutionReadiness GetPhaseExecutionReadiness(PhaseId phaseId) =>
            inner.GetPhaseExecutionReadiness(phaseId);

        public Task<AutoRefinementAnswersResult?> TryAutoAnswerRefinementAsync(
            PhaseExecutionContext context,
            RefinementSession session,
            CancellationToken cancellationToken = default) =>
            inner.TryAutoAnswerRefinementAsync(context, session, cancellationToken);

        public async Task<PhaseExecutionResult> ExecuteAsync(
            PhaseExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            var result = await inner.ExecuteAsync(context, cancellationToken);
            return result with
            {
                EffectivePrompt = new PhaseExecutionEffectivePrompt(
                    "system instructions",
                    "user instructions",
                    ["prompt warning"],
                    [
                        new PhaseExecutionPromptSource(
                            "phase-task",
                            "/repo/.specs/prompts/phases/spec.execute.md",
                            IsOverride: true,
                            ContentSha256: "content-hash",
                            EmbeddedContentSha256: "embedded-hash")
                    ])
            };
        }
    }

    private sealed class PassingReviewPhaseExecutionProvider : IPhaseExecutionProvider
    {
        private readonly DeterministicPhaseExecutionProvider inner = new();

        public PhaseExecutionReadiness GetPhaseExecutionReadiness(PhaseId phaseId) =>
            inner.GetPhaseExecutionReadiness(phaseId);

        public Task<AutoRefinementAnswersResult?> TryAutoAnswerRefinementAsync(
            PhaseExecutionContext context,
            RefinementSession session,
            CancellationToken cancellationToken = default) =>
            inner.TryAutoAnswerRefinementAsync(context, session, cancellationToken);

        public async Task<PhaseExecutionResult> ExecuteAsync(
            PhaseExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            if (context.PhaseId == PhaseId.Implementation)
            {
                var featurePath = Path.Combine(context.WorkspaceRoot, "src", "Feature.cs");
                Directory.CreateDirectory(Path.GetDirectoryName(featurePath)!);
                await File.WriteAllTextAsync(
                    featurePath,
                    "namespace SpecForge;\npublic static class Feature { public const int Enabled = 1; }\n",
                    cancellationToken);
            }

            if (context.PhaseId == PhaseId.Review)
            {
                var paths = UserStoryFilePaths.ResolveFromWorkspaceRoot(context.WorkspaceRoot, context.UsId);
                var validationItems = WorkflowRunner.ReadTechnicalDesignValidationStrategy(paths);
                var checklist = validationItems.Select(item => $"- ✅ {item} Evidence: Validated by replay test.").ToArray();
                var content = string.Join(
                    Environment.NewLine,
                    [
                        $"# Review · {context.UsId} · v01",
                        string.Empty,
                        "## State",
                        "- Result: `pass`",
                        string.Empty,
                        "## Validation Checklist",
                        ..checklist,
                        string.Empty,
                        "## Findings",
                        "- No findings.",
                        string.Empty,
                        "## Verdict",
                        "- Final result: `pass`",
                        "- Primary reason: Replay test review passed.",
                        string.Empty,
                        "## Recommendation",
                        "- Advance."
                    ]) + Environment.NewLine;

                return new PhaseExecutionResult(
                    content,
                    ExecutionKind: "test-double");
            }

            return await inner.ExecuteAsync(context, cancellationToken);
        }
    }

    private sealed class RetryPassingReviewPhaseExecutionProvider : IPhaseExecutionProvider
    {
        private readonly DeterministicPhaseExecutionProvider inner = new();
        private int reviewAttemptCount;

        public PhaseExecutionReadiness GetPhaseExecutionReadiness(PhaseId phaseId) =>
            inner.GetPhaseExecutionReadiness(phaseId);

        public Task<AutoRefinementAnswersResult?> TryAutoAnswerRefinementAsync(
            PhaseExecutionContext context,
            RefinementSession session,
            CancellationToken cancellationToken = default) =>
            inner.TryAutoAnswerRefinementAsync(context, session, cancellationToken);

        public async Task<PhaseExecutionResult> ExecuteAsync(
            PhaseExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            if (context.PhaseId == PhaseId.Implementation)
            {
                var featurePath = Path.Combine(context.WorkspaceRoot, "src", "Feature.cs");
                Directory.CreateDirectory(Path.GetDirectoryName(featurePath)!);
                await File.WriteAllTextAsync(
                    featurePath,
                    "namespace SpecForge;\npublic static class Feature { public const int Enabled = 1; }\n",
                    cancellationToken);
            }

            if (context.PhaseId == PhaseId.Review)
            {
                reviewAttemptCount++;
                if (reviewAttemptCount == 1)
                {
                    var failingContent = string.Join(
                        Environment.NewLine,
                        [
                            $"# Review · {context.UsId} · v01",
                            string.Empty,
                            "## State",
                            "- Result: `pass`",
                            string.Empty,
                            "## Checks Performed",
                            "- [x] Schema conformance",
                            string.Empty,
                            "## Findings",
                            "- No findings.",
                            string.Empty,
                            "## Verdict",
                            "- Final result: `pass`",
                            "- Primary reason: Generic review claimed success.",
                            string.Empty,
                            "## Recommendation",
                            "- Advance."
                        ]) + Environment.NewLine;

                    return new PhaseExecutionResult(failingContent, ExecutionKind: "test-double");
                }

                var paths = UserStoryFilePaths.ResolveFromWorkspaceRoot(context.WorkspaceRoot, context.UsId);
                var validationItems = WorkflowRunner.ReadTechnicalDesignValidationStrategy(paths);
                var checklist = validationItems.Select(item => $"- ✅ {item} Evidence: Validated on retry.").ToArray();
                var content = string.Join(
                    Environment.NewLine,
                    [
                        $"# Review · {context.UsId} · v02",
                        string.Empty,
                        "## State",
                        "- Result: `pass`",
                        string.Empty,
                        "## Validation Checklist",
                        ..checklist,
                        string.Empty,
                        "## Findings",
                        "- No findings.",
                        string.Empty,
                        "## Verdict",
                        "- Final result: `pass`",
                        "- Primary reason: Retry validated every required item.",
                        string.Empty,
                        "## Recommendation",
                        "- Advance."
                    ]) + Environment.NewLine;

                return new PhaseExecutionResult(content, ExecutionKind: "test-double");
            }

            return await inner.ExecuteAsync(context, cancellationToken);
        }
    }

    private sealed class NoOpWorkBranchManager : IWorkBranchManager
    {
        public Task<WorkBranchCreationResult> CreateBranchAsync(
            string workspaceRoot,
            string baseBranch,
            string workBranch,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkBranchCreationResult(
                IsGitWorkspace: true,
                BranchCreated: false,
                CurrentBranch: baseBranch,
                UpstreamBranch: $"origin/{baseBranch}"));

        public Task<WorkBranchActivationResult> EnsureActiveWorkBranchAsync(
            string workspaceRoot,
            string usId,
            string workBranch,
            string protectedUserStoryDirectory,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new WorkBranchActivationResult(
                IsGitWorkspace: true,
                BranchSwitched: false,
                StashCreated: false,
                PreviousBranch: workBranch,
                CurrentBranch: workBranch,
                StashRef: null,
                StashMessage: null));
    }

    private sealed class RecordingPullRequestPublisher : IPullRequestPublisher
    {
        public Task<PullRequestPublicationResult> PublishAsync(
            string workspaceRoot,
            string usId,
            WorkBranch branch,
            PrPreparationArtifactDocument artifact,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PullRequestPublicationResult(
                CommitCreated: true,
                CommitSha: "abc123",
                RemoteBranch: branch.WorkBranchName,
                IsDraft: true,
                Number: 101,
                Url: "https://github.com/example/repo/pull/101"));
    }
}
