using SpecForge.Domain.Persistence;
using SpecForge.Domain.Workflow;
using System.Text.Json;

namespace SpecForge.Domain.Application;

public sealed class SpecForgeApplicationService
{
    private const string DependencyBlockingReason = "dependency_not_completed";
    private readonly UserStoryFileStore fileStore;
    private readonly WorkflowRunner workflowRunner;
    private readonly RepositoryPromptInitializer repositoryPromptInitializer;
    private readonly RepositoryCategoryCatalog repositoryCategoryCatalog;
    private readonly UserStoryRuntimeStatusStore runtimeStatusStore;
    private readonly GoalUserStoryIntakeService goalUserStoryIntakeService;
    private readonly UserStoryFilesService userStoryFilesService;
    private readonly UserStoryDependencyService userStoryDependencyService;
    private readonly string? runtimeVersion;
    private readonly bool completedUsLockOnCompleted;

    public SpecForgeApplicationService()
        : this(new UserStoryFileStore(), new WorkflowRunner(), new RepositoryPromptInitializer(), new RepositoryCategoryCatalog(), new UserStoryRuntimeStatusStore(), null)
    {
    }

    public SpecForgeApplicationService(
        UserStoryFileStore fileStore,
        WorkflowRunner workflowRunner,
        RepositoryPromptInitializer? repositoryPromptInitializer = null,
        RepositoryCategoryCatalog? repositoryCategoryCatalog = null,
        UserStoryRuntimeStatusStore? runtimeStatusStore = null,
        string? runtimeVersion = null,
        bool completedUsLockOnCompleted = true)
    {
        this.fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        this.workflowRunner = workflowRunner ?? throw new ArgumentNullException(nameof(workflowRunner));
        this.repositoryPromptInitializer = repositoryPromptInitializer ?? new RepositoryPromptInitializer();
        this.repositoryCategoryCatalog = repositoryCategoryCatalog ?? new RepositoryCategoryCatalog();
        this.runtimeStatusStore = runtimeStatusStore ?? new UserStoryRuntimeStatusStore();
        goalUserStoryIntakeService = new GoalUserStoryIntakeService(this.repositoryCategoryCatalog);
        userStoryFilesService = new UserStoryFilesService();
        userStoryDependencyService = new UserStoryDependencyService(this.fileStore);
        this.runtimeVersion = string.IsNullOrWhiteSpace(runtimeVersion) ? null : runtimeVersion.Trim();
        this.completedUsLockOnCompleted = completedUsLockOnCompleted;
    }

    public Task<InitializeRepoPromptsResult> InitializeRepoPromptsAsync(
        string workspaceRoot,
        bool overwrite = false,
        CancellationToken cancellationToken = default) =>
        repositoryPromptInitializer.InitializeAsync(workspaceRoot, overwrite, cancellationToken);

    public Task<InitializeRepoPromptsResult> ExportPromptTemplateAsync(
        string workspaceRoot,
        string promptPath,
        bool overwrite = false,
        CancellationToken cancellationToken = default) =>
        repositoryPromptInitializer.ExportPromptTemplateAsync(workspaceRoot, promptPath, overwrite, cancellationToken);

    public async Task<CreateOrImportUserStoryResult> CreateUserStoryAsync(
        string workspaceRoot,
        string usId,
        string title,
        string kind,
        string category,
        string sourceText,
        string actor = "user",
        IReadOnlyCollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        repositoryCategoryCatalog.EnsureCategoryIsAllowed(workspaceRoot, category);
        var normalizedTags = WorkflowRunner.NormalizeUserStoryTags(tags);
        var rootDirectory = await workflowRunner.CreateUserStoryAsync(
            workspaceRoot,
            usId,
            title,
            kind,
            category,
            sourceText,
            actor,
            normalizedTags,
            captureSourceKind: "direct-text",
            captureSourceReference: null,
            cancellationToken);
        return new CreateOrImportUserStoryResult(usId, rootDirectory, Path.Combine(rootDirectory, "us.md"));
    }

    public Task<GoalIntakeResult> CreateUserStoriesFromGoalAsync(
        string workspaceRoot,
        string goalText,
        IReadOnlyList<GoalUserStoryDraft> stories,
        string? goalId = null,
        string? strategy = null,
        string actor = "model-on-behalf-of-user",
        CancellationToken cancellationToken = default) =>
        goalUserStoryIntakeService.CreateUserStoriesFromGoalAsync(
            workflowRunner,
            async (root, token) => await ListUserStoriesAsync(root, cancellationToken: token),
            workspaceRoot,
            goalText,
            stories,
            goalId,
            strategy,
            actor,
            cancellationToken);

    public async Task<CreateOrImportUserStoryResult> ImportUserStoryAsync(
        string workspaceRoot,
        string usId,
        string sourcePath,
        string title,
        string kind,
        string category,
        string actor = "user",
        IReadOnlyCollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var sourceText = await File.ReadAllTextAsync(sourcePath, cancellationToken);
        repositoryCategoryCatalog.EnsureCategoryIsAllowed(workspaceRoot, category);
        var normalizedTags = WorkflowRunner.NormalizeUserStoryTags(tags);
        var rootDirectory = await workflowRunner.CreateUserStoryAsync(
            workspaceRoot,
            usId,
            title,
            kind,
            category,
            sourceText,
            actor,
            normalizedTags,
            captureSourceKind: "imported-markdown",
            captureSourceReference: Path.GetFullPath(sourcePath).Replace('\\', '/'),
            cancellationToken);
        return new CreateOrImportUserStoryResult(usId, rootDirectory, Path.Combine(rootDirectory, "us.md"));
    }

    public async Task<UpdateUserStoryInfoResult> UpdateUserStoryInfoAsync(
        string workspaceRoot,
        string usId,
        string? title = null,
        string? kind = null,
        string? category = null,
        IReadOnlyCollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var paths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, usId);
        var workflowRun = await fileStore.LoadAsync(paths.RootDirectory, cancellationToken);
        var metadata = await WorkflowRunner.ReadUserStoryMetadataAsync(paths.MainArtifactPath, workflowRun.UsId, cancellationToken);
        var nextTitle = UserStoryMarkdown.NormalizeOptionalScalar(title) ?? metadata.Title;
        var nextKind = UserStoryMarkdown.NormalizeOptionalScalar(kind)?.ToLowerInvariant() ?? metadata.Kind;
        var nextCategory = UserStoryMarkdown.NormalizeOptionalScalar(category)?.ToLowerInvariant() ?? metadata.Category;
        var nextTags = tags is null
            ? metadata.Tags
            : WorkflowRunner.NormalizeUserStoryTags(tags);

        UserStoryMarkdown.ValidateUserStoryKind(nextKind);
        repositoryCategoryCatalog.EnsureCategoryIsAllowed(workspaceRoot, nextCategory);

        var content = await File.ReadAllTextAsync(paths.MainArtifactPath, cancellationToken);
        var updated = UserStoryMarkdown.RewriteUserStoryInfo(content, workflowRun.UsId, nextTitle, nextKind, nextCategory, nextTags);
        await File.WriteAllTextAsync(paths.MainArtifactPath, updated, cancellationToken);

        var summary = await GetUserStorySummaryAsync(workspaceRoot, workflowRun.UsId, cancellationToken);
        return new UpdateUserStoryInfoResult(workflowRun.UsId, paths.MainArtifactPath, summary);
    }

    public async Task<IReadOnlyCollection<UserStorySummary>> ListUserStoriesAsync(
        string workspaceRoot,
        string visibility = "active",
        CancellationToken cancellationToken = default)
    {
        var normalizedVisibility = visibility.Trim().ToLowerInvariant();
        if (normalizedVisibility is not "active" and not "dropped")
        {
            throw new ArgumentException("User story visibility must be active or dropped.", nameof(visibility));
        }

        var specsRoot = Path.Combine(
            workspaceRoot,
            UserStoryFilePaths.SpecsDirectoryName,
            UserStoryFilePaths.UserStoriesDirectoryName);
        UserStoryFilePaths.EnsureFlatUserStoryLayout(workspaceRoot);

        if (!Directory.Exists(specsRoot))
        {
            return [];
        }

        var directories = Directory.GetDirectories(specsRoot, "US-*", SearchOption.TopDirectoryOnly)
            .ToArray();
        var summaries = new List<UserStorySummary>(directories.Length);

        foreach (var directory in directories.OrderBy(static directory => directory, StringComparer.Ordinal))
        {
            var paths = new UserStoryFilePaths(directory);
            var isDropped = File.Exists(paths.DroppedMarkerFilePath);
            if (!File.Exists(paths.StateFilePath) || (normalizedVisibility == "active" && isDropped) || (normalizedVisibility == "dropped" && !isDropped))
            {
                continue;
            }

            summaries.Add(await GetUserStorySummaryFromDirectoryAsync(directory, cancellationToken));
        }

        return summaries;
    }

    public async Task<UserStorySummary> GetUserStorySummaryAsync(
        string workspaceRoot,
        string usId,
        CancellationToken cancellationToken = default)
    {
        var paths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, usId);
        return await GetUserStorySummaryFromDirectoryAsync(paths.RootDirectory, cancellationToken);
    }

    public async Task<UserStoryWorkflowDetails> GetUserStoryWorkflowAsync(
        string workspaceRoot,
        string usId,
        CancellationToken cancellationToken = default)
    {
        var paths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, usId);
        var workflowRun = await fileStore.LoadAsync(paths.RootDirectory, cancellationToken);
        var title = await ReadTitleAsync(paths.MainArtifactPath, cancellationToken);
        var metadata = await WorkflowRunner.ReadUserStoryMetadataAsync(paths.MainArtifactPath, workflowRun.UsId, cancellationToken);
        var rawTimeline = File.Exists(paths.TimelineFilePath)
            ? await File.ReadAllTextAsync(paths.TimelineFilePath, cancellationToken)
            : string.Empty;
        var refinement = await ReadRefinementSessionAsync(paths, cancellationToken);
        var approvalQuestions = await ReadApprovalQuestionsAsync(paths, cancellationToken);
        var dependencies = await userStoryDependencyService.GetDependencySummariesAsync(
            workspaceRoot,
            paths.MainArtifactPath,
            workflowRun.UsId,
            cancellationToken);
        var currentPhase = await GetCurrentPhaseAsync(workspaceRoot, usId, cancellationToken);

        var timelineEvents = TimelineMarkdownParser.ParseEvents(rawTimeline);
        return new UserStoryWorkflowDetails(
            workflowRun.UsId,
            title,
            metadata.Kind,
            metadata.Category,
            metadata.Tags,
            UserStoryDependencyService.ResolveOperationalStatus(workflowRun.Status, dependencies),
            WorkflowPresentation.ToPhaseSlug(workflowRun.CurrentPhase),
            paths.RootDirectory,
            workflowRun.Branch?.WorkBranchName,
            paths.MainArtifactPath,
            paths.TimelineFilePath,
            rawTimeline,
            workflowRun.CreatedWithRuntimeVersion,
            workflowRun.LastRuntimeVersion,
            dependencies,
            workflowRun.WorkflowKind,
            workflowRun.ParentUsId,
            await BuildChildStorySummariesAsync(workspaceRoot, workflowRun, cancellationToken),
            await ReadDecompositionDetailsAsync(paths, cancellationToken),
            workflowRun.Branch?.PullRequest is null || workflowRun.Branch.PullRequest.Status is "superseded" or "close_pending"
                ? null
                : new PullRequestDetails(
                    workflowRun.Branch.PullRequest.Status,
                    workflowRun.Branch.PullRequest.Title,
                    workflowRun.Branch.PullRequest.IsDraft,
                    workflowRun.Branch.PullRequest.Number,
                    workflowRun.Branch.PullRequest.Url,
                    workflowRun.Branch.PullRequest.RemoteBranch,
                    workflowRun.Branch.PullRequest.PublishedAtUtc?.ToString("O")),
            await BuildPhaseDetailsAsync(workflowRun, paths, timelineEvents, cancellationToken),
            new CurrentPhaseControls(
                currentPhase.CanAdvance,
                currentPhase.CanApprove,
                currentPhase.RequiresApproval,
                currentPhase.BlockingReason,
                workflowRun.CurrentPhase != Workflow.PhaseId.Capture,
                BuildRegressionTargets(workflowRun),
                BuildRewindTargets(workflowRun),
                currentPhase.ExecutionPhase,
                currentPhase.ExecutionReadiness),
            refinement is null
                ? null
                : new RefinementSessionDetails(
                    refinement.Status,
                    refinement.Tolerance,
                    refinement.Reason,
                    refinement.Items.Select(item => new RefinementQuestionAnswerDetails(item.Index, item.Question, item.Answer)).ToArray(),
                    workflowRunner.GetRefinementPolicyDetails(refinement)),
            approvalQuestions,
            timelineEvents,
            WorkflowIterationDetailsBuilder.Build(paths, timelineEvents),
            paths.ContextDirectoryPath,
            UserStoryFilesService.BuildFileDetails(paths.ContextDirectoryPath),
            paths.AttachmentsDirectoryPath,
            UserStoryFilesService.BuildFileDetails(paths.AttachmentsDirectoryPath));
    }

    public async Task<WorkflowLineageAnalysisResult> AnalyzeUserStoryLineageAsync(
        string workspaceRoot,
        string usId,
        CancellationToken cancellationToken = default)
    {
        var paths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, usId);
        var rawTimeline = File.Exists(paths.TimelineFilePath)
            ? await File.ReadAllTextAsync(paths.TimelineFilePath, cancellationToken)
            : string.Empty;
        return WorkflowLineageAnalyzer.Analyze(usId, paths, TimelineMarkdownParser.ParseEvents(rawTimeline));
    }

    public Task<WorkflowLineageRepairResult> RepairUserStoryLineageAsync(
        string workspaceRoot,
        string usId,
        string actor = "user",
        CancellationToken cancellationToken = default)
    {
        return workflowRunner.RepairUserStoryLineageAsync(workspaceRoot, usId, actor, cancellationToken);
    }

    private async Task<UserStorySummary> GetUserStorySummaryFromDirectoryAsync(
        string directory,
        CancellationToken cancellationToken)
    {
        var mainArtifactPath = Path.Combine(directory, "us.md");
        var workflowRun = await fileStore.LoadAsync(directory, cancellationToken);
        var mainArtifact = await File.ReadAllTextAsync(mainArtifactPath, cancellationToken);
        var title = UserStoryMarkdown.ReadTitle(mainArtifact, Path.GetFileName(Path.GetDirectoryName(mainArtifactPath) ?? mainArtifactPath));
        var description = UserStoryMarkdown.ReadObjectiveSummary(mainArtifact);
        var metadata = await WorkflowRunner.ReadUserStoryMetadataAsync(mainArtifactPath, workflowRun.UsId, cancellationToken);
        var dependencies = await userStoryDependencyService.GetDependencySummariesAsync(
            FindWorkspaceRoot(new UserStoryFilePaths(directory)),
            mainArtifactPath,
            workflowRun.UsId,
            cancellationToken);

        return new UserStorySummary(
            workflowRun.UsId,
            title,
            description,
            metadata.Category,
            metadata.Tags,
            directory,
            mainArtifactPath,
            WorkflowPresentation.ToPhaseSlug(workflowRun.CurrentPhase),
            UserStoryDependencyService.ResolveOperationalStatus(workflowRun.Status, dependencies),
            workflowRun.Branch?.WorkBranchName,
            dependencies,
            workflowRun.WorkflowKind,
            workflowRun.ParentUsId,
            workflowRun.ChildUsIds);
    }

    public async Task<CurrentPhaseSummary> GetCurrentPhaseAsync(
        string workspaceRoot,
        string usId,
        CancellationToken cancellationToken = default)
    {
        var paths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, usId);
        var workflowRun = await fileStore.LoadAsync(paths.RootDirectory, cancellationToken);
        var runtime = await runtimeStatusStore.GetAsync(
            paths.RootDirectory,
            usId,
            WorkflowPresentation.ToPhaseSlug(workflowRun.CurrentPhase),
            cancellationToken);

        if (runtime.Status == RuntimeStatus.Running && !runtime.IsStale)
        {
            return new CurrentPhaseSummary(
                workflowRun.UsId,
                WorkflowPresentation.ToPhaseSlug(workflowRun.CurrentPhase),
                WorkflowPresentation.ToStatusSlug(workflowRun.Status),
                false,
                false,
                workflowRun.Definition.RequiresApproval(workflowRun.CurrentPhase),
                "phase_execution_in_progress",
                WorkflowPresentation.ToPhaseSlug(workflowRun.CurrentPhase));
        }

        if (workflowRun.CurrentPhase == Workflow.PhaseId.Refinement)
        {
            var refinement = await ReadRefinementSessionAsync(paths, cancellationToken);
            var canAdvanceRefinement = UserStoryRefinementMarkdown.HasAllAnswers(refinement);
            return new CurrentPhaseSummary(
                workflowRun.UsId,
                WorkflowPresentation.ToPhaseSlug(workflowRun.CurrentPhase),
                WorkflowPresentation.ToStatusSlug(workflowRun.Status),
                canAdvanceRefinement,
                false,
                false,
                canAdvanceRefinement ? null : "refinement_pending_answers");
        }

        if (workflowRun.Status == UserStoryStatus.Completed)
        {
            return new CurrentPhaseSummary(
                workflowRun.UsId,
                WorkflowPresentation.ToPhaseSlug(workflowRun.CurrentPhase),
                WorkflowPresentation.ToStatusSlug(workflowRun.Status),
                CanAdvance: false,
                CanApprove: false,
                RequiresApproval: false,
                BlockingReason: "workflow_completed");
        }

        if (workflowRun.Status == UserStoryStatus.WaitingChildren ||
            string.Equals(workflowRun.WorkflowKind, "aggregate", StringComparison.Ordinal))
        {
            var childSummaries = await BuildChildStorySummariesAsync(workspaceRoot, workflowRun, cancellationToken);
            if (childSummaries.Count > 0 && childSummaries.All(static child => child.Status == "completed"))
            {
                workflowRun.CompleteAggregate();
                await fileStore.SaveAsync(workflowRun, paths.RootDirectory, cancellationToken);

                return new CurrentPhaseSummary(
                    workflowRun.UsId,
                    WorkflowPresentation.ToPhaseSlug(workflowRun.CurrentPhase),
                    WorkflowPresentation.ToStatusSlug(workflowRun.Status),
                    CanAdvance: false,
                    CanApprove: false,
                    RequiresApproval: false,
                    BlockingReason: "workflow_completed");
            }

            return new CurrentPhaseSummary(
                workflowRun.UsId,
                WorkflowPresentation.ToPhaseSlug(workflowRun.CurrentPhase),
                WorkflowPresentation.ToStatusSlug(UserStoryStatus.WaitingChildren),
                CanAdvance: false,
                CanApprove: false,
                RequiresApproval: false,
                BlockingReason: "aggregate_waiting_children");
        }

        var dependencies = await userStoryDependencyService.GetDependencySummariesAsync(
            workspaceRoot,
            paths.MainArtifactPath,
            workflowRun.UsId,
            cancellationToken);
        if (UserStoryDependencyService.HasBlockingDependencies(workflowRun.Status, dependencies))
        {
            return new CurrentPhaseSummary(
                workflowRun.UsId,
                WorkflowPresentation.ToPhaseSlug(workflowRun.CurrentPhase),
                "blocked",
                CanAdvance: false,
                CanApprove: false,
                RequiresApproval: false,
                DependencyBlockingReason);
        }

        var requiresApproval = workflowRun.Definition.RequiresApproval(workflowRun.CurrentPhase);
        var canAdvance = !requiresApproval || workflowRun.IsPhaseApproved(workflowRun.CurrentPhase);
        var canApprove = requiresApproval && !canAdvance;
        string? blockingReason = null;
        string? executionPhase = null;
        PhaseExecutionReadiness? executionReadiness = null;
        if (canApprove && workflowRun.CurrentPhase == Workflow.PhaseId.Spec)
        {
            var decomposition = await ReadDecompositionDetailsAsync(paths, cancellationToken);
            if (decomposition?.State == UserStoryDecomposition.StatePendingApproval)
            {
                canApprove = false;
                blockingReason = "decomposition_pending_user_approval";
            }

            var specPath = paths.GetLatestExistingPhaseArtifactPath(Workflow.PhaseId.Spec);
            if (blockingReason is null && (string.IsNullOrWhiteSpace(specPath) || !File.Exists(specPath)))
            {
                canApprove = false;
            }
            else if (blockingReason is null)
            {
                var specMarkdown = await File.ReadAllTextAsync(specPath!, cancellationToken);
                canApprove = SpecBaselineSchemaValidator.Validate(specMarkdown).IsValid;
                if (canApprove)
                {
                    var specDocument = await LoadCurrentSpecDocumentAsync(paths, cancellationToken);
                    canApprove = SpecJson.GetUnresolvedQuestions(specDocument).Count == 0;
                }
            }
        }

        if (workflowRun.CurrentPhase == Workflow.PhaseId.Review)
        {
            var replayPending = WorkflowRunner.IsReviewReplayPending(paths);
            var readiness = workflowRunner.GetPhaseExecutionReadiness(Workflow.PhaseId.Review);
            if (replayPending)
            {
                canAdvance = readiness.CanExecute;
                blockingReason = readiness.BlockingReason;
                var replayExecutionPhase = readiness.CanExecute
                    ? WorkflowPresentation.ToPhaseSlug(Workflow.PhaseId.Review)
                    : null;
                SpecForgeDiagnostics.Log(
                    $"[app.current_phase] usId={usId} review replay is pending after rewind; canExecute={readiness.CanExecute} blockingReason='{readiness.BlockingReason ?? "none"}'.");
                return new CurrentPhaseSummary(
                    workflowRun.UsId,
                    WorkflowPresentation.ToPhaseSlug(workflowRun.CurrentPhase),
                    WorkflowPresentation.ToStatusSlug(workflowRun.Status),
                    canAdvance,
                    canApprove,
                    requiresApproval,
                    blockingReason,
                    replayExecutionPhase,
                    readiness);
            }
            else
            {
                var reviewPath = paths.GetLatestExistingPhaseArtifactPath(Workflow.PhaseId.Review);
                if (string.IsNullOrWhiteSpace(reviewPath) || !File.Exists(reviewPath))
                {
                    canAdvance = false;
                    blockingReason = "review_missing_artifact";
                    executionPhase = WorkflowPresentation.ToPhaseSlug(Workflow.PhaseId.Review);
                    executionReadiness = readiness;
                }
                else
                {
                    var reviewResult = WorkflowRunner.TryReadReviewResult(await File.ReadAllTextAsync(reviewPath, cancellationToken));
                    if (reviewResult != "pass")
                    {
                        canAdvance = false;
                        blockingReason = reviewResult == "fail"
                            ? "review_failed"
                            : "review_result_missing";
                        executionPhase = WorkflowPresentation.ToPhaseSlug(Workflow.PhaseId.Review);
                        executionReadiness = readiness;
                    }
                }
            }
        }

        if (!canAdvance)
        {
            blockingReason ??= $"{WorkflowPresentation.ToPhaseSlug(workflowRun.CurrentPhase)}_pending_user_approval";
        }
        else
        {
            var readiness = ResolveNextPhaseExecutionReadiness(workflowRun);
            if (!readiness.CanExecute)
            {
                canAdvance = false;
                blockingReason = readiness.BlockingReason;
                executionPhase = WorkflowPresentation.ToPhaseSlug(readiness.PhaseId);
                executionReadiness = readiness;
            }
            else if (readiness.RequiredPermissions?.ModelExecutionRequired == true)
            {
                executionPhase = WorkflowPresentation.ToPhaseSlug(readiness.PhaseId);
                executionReadiness = readiness;
            }
        }

        return new CurrentPhaseSummary(
            workflowRun.UsId,
            WorkflowPresentation.ToPhaseSlug(workflowRun.CurrentPhase),
            WorkflowPresentation.ToStatusSlug(workflowRun.Status),
            canAdvance,
            canApprove,
            requiresApproval,
            blockingReason,
            executionPhase,
            executionReadiness);
    }

    public async Task<ContinuePhaseResponse> GenerateNextPhaseAsync(
        string workspaceRoot,
        string usId,
        string actor = "user",
        CancellationToken cancellationToken = default)
    {
        await using var diagnostics = SpecForgeDiagnostics.StartProgressScope(
            $"[app.generate_next_phase] usId={usId} actor={actor}",
            interval: TimeSpan.FromSeconds(20));
        var currentPhase = await GetCurrentPhaseAsync(workspaceRoot, usId, cancellationToken);
        if (!currentPhase.CanAdvance && !CanReplayCurrentReview(currentPhase))
        {
            throw new WorkflowDomainException(
                $"Workflow cannot continue from phase '{currentPhase.CurrentPhase}' because '{currentPhase.BlockingReason ?? "phase_cannot_advance"}'.");
        }

        if (!currentPhase.CanAdvance && currentPhase.ExecutionReadiness is { CanExecute: false } readiness)
        {
            throw new WorkflowDomainException(
                $"Phase '{currentPhase.CurrentPhase}' cannot run because '{readiness.BlockingReason ?? currentPhase.BlockingReason ?? "phase_execution_not_ready"}'.");
        }

        var paths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, usId);
        await using var operation = await runtimeStatusStore.StartOperationAsync(
            paths.RootDirectory,
            usId,
            currentPhase.CurrentPhase,
            "generate-next-phase",
            cancellationToken);

        try
        {
            SpecForgeDiagnostics.Log(
                $"[app.generate_next_phase] usId={usId} currentPhase={currentPhase.CurrentPhase} status={currentPhase.Status} canAdvance={currentPhase.CanAdvance} requiresApproval={currentPhase.RequiresApproval}");
            var result = await workflowRunner.ContinuePhaseAsync(workspaceRoot, usId, actor, cancellationToken);
            var resultPhase = WorkflowPresentation.ToPhaseSlug(result.CurrentPhase);
            operation.UpdatePhase(resultPhase);
            await operation.CompleteAsync(resultPhase, cancellationToken);
            diagnostics.MarkCompleted($"resultPhase={resultPhase} status={WorkflowPresentation.ToStatusSlug(result.Status)}");
            return new ContinuePhaseResponse(
                result.UsId,
                resultPhase,
                WorkflowPresentation.ToStatusSlug(result.Status),
                result.GeneratedArtifactPath,
                result.Usage,
                result.Execution);
        }
        catch (Exception exception)
        {
            await operation.FailAsync(currentPhase.CurrentPhase, exception.Message, cancellationToken);
            diagnostics.MarkFailed(exception);
            throw;
        }
    }

    private PhaseExecutionReadiness ResolveNextPhaseExecutionReadiness(WorkflowRun workflowRun)
    {
        if (!workflowRun.Definition.CanAdvanceFrom(workflowRun.CurrentPhase) ||
            workflowRun.CurrentPhase == Workflow.PhaseId.PrPreparation)
        {
            return new PhaseExecutionReadiness(workflowRun.CurrentPhase, CanExecute: true);
        }

        var nextPhase = workflowRun.Definition.GetNextPhase(workflowRun.CurrentPhase);
        return workflowRunner.GetPhaseExecutionReadiness(nextPhase);
    }

    private static bool CanReplayCurrentReview(CurrentPhaseSummary currentPhase) =>
        currentPhase.CurrentPhase == WorkflowPresentation.ToPhaseSlug(Workflow.PhaseId.Review)
        && currentPhase.BlockingReason is "review_failed" or "review_result_missing" or "review_missing_artifact";

    public async Task<UserStoryRuntimeStatus> GetUserStoryRuntimeStatusAsync(
        string workspaceRoot,
        string usId,
        CancellationToken cancellationToken = default)
    {
        var currentPhase = await GetCurrentPhaseAsync(workspaceRoot, usId, cancellationToken);
        var paths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, usId);
        var runtime = await runtimeStatusStore.GetAsync(paths.RootDirectory, usId, currentPhase.CurrentPhase, cancellationToken);
        return new UserStoryRuntimeStatus(
            runtime.UsId,
            ToRuntimeStatusSlug(runtime.Status),
            runtime.ActiveOperation,
            runtime.CurrentPhase,
            runtime.StartedAtUtc?.UtcDateTime.ToString("O"),
            runtime.LastHeartbeatUtc?.UtcDateTime.ToString("O"),
            runtime.LastOutcome,
            runtime.LastCompletedAtUtc?.UtcDateTime.ToString("O"),
            runtime.Message,
            runtime.IsStale);
    }

    public async Task<ApprovalResult> ApprovePhaseAsync(
        string workspaceRoot,
        string usId,
        string? baseBranch,
        string? workBranch = null,
        string actor = "user",
        CancellationToken cancellationToken = default)
    {
        await workflowRunner.ApproveCurrentPhaseAsync(workspaceRoot, usId, baseBranch, workBranch, actor, cancellationToken);
        var summary = await GetUserStorySummaryAsync(workspaceRoot, usId, cancellationToken);
        return new ApprovalResult(summary.UsId, summary.Status, summary.CurrentPhase, baseBranch, summary.WorkBranch);
    }

    public Task<UserStoryDecompositionApprovalResult> ApproveDecompositionAsync(
        string workspaceRoot,
        string usId,
        string actor = "user",
        CancellationToken cancellationToken = default) =>
        workflowRunner.ApproveDecompositionAsync(workspaceRoot, usId, actor, cancellationToken);

    public Task<UserStoryDecompositionApprovalResult> RejectDecompositionAsync(
        string workspaceRoot,
        string usId,
        string actor = "user",
        CancellationToken cancellationToken = default) =>
        workflowRunner.RejectDecompositionAsync(workspaceRoot, usId, actor, cancellationToken);

    public Task<RequestRegressionResult> RequestRegressionAsync(
        string workspaceRoot,
        string usId,
        string targetPhase,
        string? reason = null,
        bool destructive = false,
        string actor = "user",
        CancellationToken cancellationToken = default)
    {
        var phaseId = WorkflowPresentation.ParsePhaseSlug(targetPhase);
        return workflowRunner.RequestRegressionAsync(workspaceRoot, usId, phaseId, reason, destructive, actor, cancellationToken);
    }

    public Task<ContinuePhaseResult> ApproveReviewAnywayAsync(
        string workspaceRoot,
        string usId,
        string reason,
        string actor = "user",
        CancellationToken cancellationToken = default) =>
        workflowRunner.ApproveReviewAnywayAsync(workspaceRoot, usId, reason, actor, cancellationToken);

    public Task<RestartUserStoryResult> RestartUserStoryFromSourceAsync(
        string workspaceRoot,
        string usId,
        string? reason = null,
        string actor = "user",
        CancellationToken cancellationToken = default) =>
        workflowRunner.RestartUserStoryFromSourceAsync(workspaceRoot, usId, reason, actor, cancellationToken);

    public Task<RewindWorkflowResult> RewindWorkflowAsync(
        string workspaceRoot,
        string usId,
        string targetPhase,
        bool destructive = false,
        string actor = "user",
        CancellationToken cancellationToken = default)
    {
        var phaseId = WorkflowPresentation.ParsePhaseSlug(targetPhase);
        return workflowRunner.RewindWorkflowAsync(workspaceRoot, usId, phaseId, destructive, actor, cancellationToken);
    }

    public Task<RequestRegressionResult> ReopenCompletedWorkflowAsync(
        string workspaceRoot,
        string usId,
        string reasonKind,
        string description,
        string actor = "user",
        CancellationToken cancellationToken = default)
    {
        var targetPhase = MapCompletedWorkflowReopenTarget(reasonKind);
        return workflowRunner.ReopenCompletedWorkflowAsync(workspaceRoot, usId, targetPhase, reasonKind, description, actor, cancellationToken);
    }

    public Task<ResetUserStoryResult> ResetUserStoryToCaptureAsync(
        string workspaceRoot,
        string usId,
        CancellationToken cancellationToken = default) =>
        workflowRunner.ResetUserStoryToCaptureAsync(workspaceRoot, usId, cancellationToken);

    public Task<SubmitRefinementAnswersResult> SubmitRefinementAnswersAsync(
        string workspaceRoot,
        string usId,
        IReadOnlyList<string> answers,
        string actor = "user",
        CancellationToken cancellationToken = default) =>
        workflowRunner.SubmitRefinementAnswersAsync(workspaceRoot, usId, answers, actor, cancellationToken);

    public Task<SubmitApprovalAnswerResult> SubmitApprovalAnswerAsync(
        string workspaceRoot,
        string usId,
        string question,
        string answer,
        string actor = "user",
        CancellationToken cancellationToken = default) =>
        workflowRunner.SubmitApprovalAnswerAsync(workspaceRoot, usId, question, answer, actor, cancellationToken);

    public Task<ApprovalAnswerSuggestionResult> SuggestApprovalAnswerAsync(
        string workspaceRoot,
        string usId,
        string question,
        string actor = "user",
        CancellationToken cancellationToken = default) =>
        workflowRunner.SuggestApprovalAnswerAsync(workspaceRoot, usId, question, actor, cancellationToken);

    public Task<OperateCurrentPhaseArtifactResult> OperateCurrentPhaseArtifactAsync(
        string workspaceRoot,
        string usId,
        string prompt,
        bool includeReviewArtifactInContext = true,
        string actor = "user",
        CancellationToken cancellationToken = default) =>
        workflowRunner.OperateCurrentPhaseArtifactAsync(workspaceRoot, usId, prompt, includeReviewArtifactInContext, actor, cancellationToken);

    public Task<UserStoryFilesResult> ListUserStoryFilesAsync(
        string workspaceRoot,
        string usId,
        CancellationToken cancellationToken = default) =>
        userStoryFilesService.ListUserStoryFilesAsync(workspaceRoot, usId, cancellationToken);

    public async Task<UserStoryFilesResult> AddUserStoryFilesAsync(
        string workspaceRoot,
        string usId,
        IReadOnlyCollection<string> sourcePaths,
        string kind,
        CancellationToken cancellationToken = default) =>
        await userStoryFilesService.AddUserStoryFilesAsync(workspaceRoot, usId, sourcePaths, kind, cancellationToken);

    public async Task<UserStoryFilesResult> SetUserStoryFileKindAsync(
        string workspaceRoot,
        string usId,
        string filePath,
        string kind,
        CancellationToken cancellationToken = default) =>
        await userStoryFilesService.SetUserStoryFileKindAsync(workspaceRoot, usId, filePath, kind, cancellationToken);

    private static async Task<string> ReadTitleAsync(string filePath, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        return UserStoryMarkdown.ReadTitle(content, Path.GetFileName(Path.GetDirectoryName(filePath) ?? filePath));
    }

    private async Task<IReadOnlyCollection<WorkflowPhaseDetails>> BuildPhaseDetailsAsync(
        Workflow.WorkflowRun workflowRun,
        UserStoryFilePaths paths,
        IReadOnlyCollection<TimelineEventDetails> timelineEvents,
        CancellationToken cancellationToken)
    {
        var phases = new[]
        {
            Workflow.PhaseId.Capture,
            Workflow.PhaseId.Refinement,
            Workflow.PhaseId.Spec,
            Workflow.PhaseId.TechnicalDesign,
            Workflow.PhaseId.Implementation,
            Workflow.PhaseId.Review,
            Workflow.PhaseId.ReleaseApproval,
            Workflow.PhaseId.PrPreparation
        };

        var materializedPhases = new List<WorkflowPhaseDetails>();

        foreach (var (phaseId, index) in phases.Select((phaseId, index) => (phaseId, index)))
        {
            var requiresApproval = workflowRun.Definition.RequiresApproval(phaseId);
            var isCompletedWorkflow = workflowRun.Status == UserStoryStatus.Completed;
            var isCurrent = isCompletedWorkflow
                ? false
                : workflowRun.CurrentPhase == phaseId;
            var phaseSlug = WorkflowPresentation.ToPhaseSlug(phaseId);
            var executionBoundary = DescribeExecutionBoundary(phaseId);
            var captureRecord = phaseId == PhaseId.Capture
                ? await TryReadCaptureExecutionRecordAsync(paths, cancellationToken)
                : null;
            var executionReadiness = workflowRunner.GetPhaseExecutionReadiness(phaseId);
            var executionPolicy = workflowRunner.GetPhaseExecutionPolicy(phaseId);
            var executionEnvelope = workflowRunner.GetPhaseExecutionEnvelope(phaseId);
            var specApprovalPolicy = phaseId == PhaseId.Spec
                ? await SpecPhaseApprovalPolicyBuilder.BuildAsync(workflowRun, paths, cancellationToken)
                : null;
            var latestExecutionInspection = await TryReadLatestExecutionInspectionAsync(
                timelineEvents,
                phaseSlug,
                cancellationToken);

            materializedPhases.Add(new WorkflowPhaseDetails(
                phaseSlug,
                ToPhaseTitle(phaseId),
                index,
                requiresApproval,
                WorkflowPresentation.ExpectsHumanIntervention(phaseId, requiresApproval),
                workflowRun.IsPhaseApproved(phaseId),
                isCurrent,
                ResolvePhaseState(workflowRun, phaseId),
                TryGetLatestArtifactPath(paths, phaseId),
                TryGetLatestOperationLogPath(paths, phaseId),
                TryGetExecutePromptPath(paths, phaseId),
                TryGetApprovePromptPath(paths, phaseId),
                TryGetExecuteSystemPromptPath(paths, phaseId),
                TryGetApproveSystemPromptPath(paths, phaseId),
                executionBoundary,
                captureRecord,
                executionReadiness,
                executionPolicy,
                executionEnvelope,
                specApprovalPolicy,
                latestExecutionInspection));
        }

        if (workflowRun.Status == UserStoryStatus.Completed)
        {
            materializedPhases.Add(new WorkflowPhaseDetails(
                "completed",
                "Completed",
                materializedPhases.Count,
                RequiresApproval: false,
                ExpectsHumanIntervention: false,
                IsApproved: true,
                IsCurrent: true,
                State: "current",
                ArtifactPath: null,
                OperationLogPath: null,
                ExecutePromptPath: null,
                ApprovePromptPath: null,
                ExecuteSystemPromptPath: null,
                ApproveSystemPromptPath: null,
                ExecutionBoundary: null,
                CaptureRecord: null,
                ExecutionReadiness: null,
                ExecutionPolicy: null,
                ExecutionEnvelope: null,
                SpecApprovalPolicy: null,
                LatestExecutionInspection: null));
        }

        return materializedPhases;
    }

    private static PhaseExecutionBoundarySummary DescribeExecutionBoundary(PhaseId phaseId) =>
        phaseId == PhaseId.Capture
            ? new PhaseExecutionBoundarySummary(
                BoundaryKind: "workflow-entry",
                IsModelBacked: false,
                Summary: "Capture is the workflow entry phase. It materializes the user story and runtime state but does not execute a phase model or prompt pipeline.")
            : new PhaseExecutionBoundarySummary(
                BoundaryKind: "model-phase",
                IsModelBacked: true,
                Summary: $"Phase '{WorkflowPresentation.ToPhaseSlug(phaseId)}' is a model-backed workflow phase.");

    private static async Task<PhaseExecutionInspectionDetails?> TryReadLatestExecutionInspectionAsync(
        IReadOnlyCollection<TimelineEventDetails> timelineEvents,
        string phaseSlug,
        CancellationToken cancellationToken)
    {
        var receiptPath = timelineEvents
            .Where(timelineEvent =>
                string.Equals(timelineEvent.Phase, phaseSlug, StringComparison.Ordinal) &&
                timelineEvent.Execution?.ReceiptPath is not null)
            .OrderByDescending(timelineEvent => timelineEvent.TimestampUtc)
            .Select(timelineEvent => timelineEvent.Execution?.ReceiptPath)
            .FirstOrDefault(static path => !string.IsNullOrWhiteSpace(path));

        if (string.IsNullOrWhiteSpace(receiptPath))
        {
            return null;
        }

        try
        {
            var receipt = await PhaseExecutionReceiptStore.TryLoadAsync(receiptPath, cancellationToken);
            if (receipt?.EffectivePrompt is null &&
                receipt?.EffectiveContext is null &&
                receipt?.EvidenceRecord is null &&
                receipt?.RefinementPolicySnapshot is null &&
                receipt?.RefinementSkillPreselection is null &&
                receipt?.RefinementGraphScopeRequest is null &&
                receipt?.SpecApprovalPolicySnapshot is null)
            {
                return null;
            }

            return new PhaseExecutionInspectionDetails(
                receiptPath,
                receipt?.EvidenceRecord,
                receipt?.RefinementPolicySnapshot,
                receipt?.RefinementSkillPreselection,
                receipt?.RefinementGraphScopeRequest,
                receipt?.SpecApprovalPolicySnapshot,
                receipt?.EffectivePrompt,
                receipt?.EffectiveContext);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static async Task<CaptureExecutionRecord?> TryReadCaptureExecutionRecordAsync(
        UserStoryFilePaths paths,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.CaptureRecordPath))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(paths.CaptureRecordPath);
            return await JsonSerializer.DeserializeAsync<CaptureExecutionRecord>(
                stream,
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private async Task<IReadOnlyCollection<UserStorySummary>> BuildChildStorySummariesAsync(
        string workspaceRoot,
        WorkflowRun workflowRun,
        CancellationToken cancellationToken)
    {
        if (workflowRun.ChildUsIds.Count == 0)
        {
            return [];
        }

        var children = new List<UserStorySummary>();
        foreach (var childUsId in workflowRun.ChildUsIds)
        {
            try
            {
                children.Add(await GetUserStorySummaryAsync(workspaceRoot, childUsId, cancellationToken));
            }
            catch (DirectoryNotFoundException)
            {
                children.Add(new UserStorySummary(
                    childUsId,
                    childUsId,
                    "Missing child user story.",
                    string.Empty,
                    [],
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "missing",
                    null,
                    []));
            }
        }

        return children;
    }

    private static async Task<DecompositionDetails?> ReadDecompositionDetailsAsync(
        UserStoryFilePaths paths,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.DecompositionJsonPath))
        {
            return null;
        }

        var document = UserStoryDecomposition.Deserialize(
            await File.ReadAllTextAsync(paths.DecompositionJsonPath, cancellationToken));
        return new DecompositionDetails(
            document.State,
            document.Decision,
            document.ComplexityScore,
            document.Threshold,
            document.Tolerance,
            document.Rationale,
            File.Exists(paths.DecompositionMarkdownPath) ? paths.DecompositionMarkdownPath : null,
            document.ProposedChildren,
            document.CreatedChildUsIds);
    }

    private static string ResolvePhaseState(Workflow.WorkflowRun workflowRun, Workflow.PhaseId phaseId)
    {
        if (workflowRun.CurrentPhase == phaseId)
        {
            return "current";
        }

        return phaseId < workflowRun.CurrentPhase ? "completed" : "pending";
    }

    private static string ToPhaseTitle(Workflow.PhaseId phaseId) => phaseId switch
    {
        Workflow.PhaseId.Capture => "Capture",
        Workflow.PhaseId.Refinement => "Refinement",
        Workflow.PhaseId.Spec => "Spec",
        Workflow.PhaseId.TechnicalDesign => "Technical Design",
        Workflow.PhaseId.Implementation => "Implementation",
        Workflow.PhaseId.Review => "Review",
        Workflow.PhaseId.ReleaseApproval => "Release Approval",
        Workflow.PhaseId.PrPreparation => "PR Preparation",
        _ => throw new ArgumentOutOfRangeException(nameof(phaseId), phaseId, null)
    };

    private static string? TryGetLatestArtifactPath(UserStoryFilePaths paths, Workflow.PhaseId phaseId)
    {
        if (phaseId is Workflow.PhaseId.Capture)
        {
            return null;
        }

        return paths.GetLatestExistingPhaseArtifactPath(phaseId);
    }

    private static string? TryGetLatestOperationLogPath(UserStoryFilePaths paths, Workflow.PhaseId phaseId)
    {
        if (phaseId is Workflow.PhaseId.Capture or Workflow.PhaseId.Refinement)
        {
            return null;
        }

        return paths.GetLatestExistingPhaseOperationLogPath(phaseId);
    }

    private static string? TryGetExecutePromptPath(UserStoryFilePaths paths, Workflow.PhaseId phaseId)
    {
        var promptPaths = new PromptFilePaths(FindWorkspaceRoot(paths));
        var candidate = phaseId switch
        {
            Workflow.PhaseId.Refinement => promptPaths.RefinementExecutePromptPath,
            Workflow.PhaseId.Spec => promptPaths.SpecExecutePromptPath,
            Workflow.PhaseId.TechnicalDesign => promptPaths.TechnicalDesignExecutePromptPath,
            Workflow.PhaseId.Implementation => promptPaths.ImplementationExecutePromptPath,
            Workflow.PhaseId.Review => promptPaths.ReviewExecutePromptPath,
            Workflow.PhaseId.ReleaseApproval => promptPaths.ReleaseApprovalExecutePromptPath,
            Workflow.PhaseId.PrPreparation => promptPaths.PrPreparationExecutePromptPath,
            _ => null
        };

        return candidate is not null && File.Exists(candidate) ? candidate : null;
    }

    private static string? TryGetApprovePromptPath(UserStoryFilePaths paths, Workflow.PhaseId phaseId)
    {
        var promptPaths = new PromptFilePaths(FindWorkspaceRoot(paths));
        var candidate = phaseId switch
        {
            Workflow.PhaseId.Spec => promptPaths.SpecApprovePromptPath,
            Workflow.PhaseId.ReleaseApproval => promptPaths.ReleaseApprovalApprovePromptPath,
            _ => null
        };

        return candidate is not null && File.Exists(candidate) ? candidate : null;
    }

    private static string? TryGetExecuteSystemPromptPath(UserStoryFilePaths paths, Workflow.PhaseId phaseId)
    {
        var promptPaths = new PromptFilePaths(FindWorkspaceRoot(paths));
        var candidate = phaseId switch
        {
            Workflow.PhaseId.Refinement => promptPaths.RefinementExecuteSystemPromptPath,
            Workflow.PhaseId.Spec => promptPaths.SpecExecuteSystemPromptPath,
            Workflow.PhaseId.TechnicalDesign => promptPaths.TechnicalDesignExecuteSystemPromptPath,
            Workflow.PhaseId.Implementation => promptPaths.ImplementationExecuteSystemPromptPath,
            Workflow.PhaseId.Review => promptPaths.ReviewExecuteSystemPromptPath,
            Workflow.PhaseId.ReleaseApproval => promptPaths.ReleaseApprovalExecuteSystemPromptPath,
            Workflow.PhaseId.PrPreparation => promptPaths.PrPreparationExecuteSystemPromptPath,
            _ => null
        };

        return candidate is not null && File.Exists(candidate) ? candidate : null;
    }

    private static string? TryGetApproveSystemPromptPath(UserStoryFilePaths paths, Workflow.PhaseId phaseId)
    {
        var promptPaths = new PromptFilePaths(FindWorkspaceRoot(paths));
        var candidate = phaseId switch
        {
            Workflow.PhaseId.Spec => promptPaths.SpecApproveSystemPromptPath,
            Workflow.PhaseId.ReleaseApproval => promptPaths.ReleaseApprovalApproveSystemPromptPath,
            _ => null
        };

        return candidate is not null && File.Exists(candidate) ? candidate : null;
    }

    private static string FindWorkspaceRoot(UserStoryFilePaths paths)
    {
        var current = new DirectoryInfo(paths.RootDirectory);
        while (current.Parent is not null)
        {
            var parent = current.Parent;
            var grandParent = parent.Parent;
            if (string.Equals(parent.Name, UserStoryFilePaths.UserStoriesDirectoryName, StringComparison.Ordinal)
                && grandParent is not null
                && string.Equals(grandParent.Name, UserStoryFilePaths.SpecsDirectoryName, StringComparison.Ordinal))
            {
                return grandParent.Parent?.FullName
                    ?? throw new InvalidOperationException("Workspace root is invalid.");
            }

            current = parent;
        }

        throw new InvalidOperationException("User story directory root is invalid.");
    }

    private static IReadOnlyCollection<string> BuildRegressionTargets(Workflow.WorkflowRun workflowRun)
    {
        var candidates = new[]
        {
            Workflow.PhaseId.Spec,
            Workflow.PhaseId.TechnicalDesign,
            Workflow.PhaseId.Implementation
        };

        return candidates
            .Where(target => workflowRun.Definition.CanRegress(workflowRun.CurrentPhase, target))
            .Select(WorkflowPresentation.ToPhaseSlug)
            .ToArray();
    }

    private static IReadOnlyCollection<string> BuildRewindTargets(Workflow.WorkflowRun workflowRun)
    {
        var candidates = new[]
        {
            Workflow.PhaseId.Refinement,
            Workflow.PhaseId.Spec,
            Workflow.PhaseId.TechnicalDesign,
            Workflow.PhaseId.Implementation,
            Workflow.PhaseId.Review,
            Workflow.PhaseId.ReleaseApproval
        };

        return candidates
            .Where(target => target < workflowRun.CurrentPhase)
            .Select(WorkflowPresentation.ToPhaseSlug)
            .ToArray();
    }

    private static Workflow.PhaseId MapCompletedWorkflowReopenTarget(string reasonKind) =>
        reasonKind.Trim().ToLowerInvariant() switch
        {
            "merge-conflict" => Workflow.PhaseId.Implementation,
            "defect" => Workflow.PhaseId.Implementation,
            "functional-issue" => Workflow.PhaseId.Spec,
            "technical-issue" => Workflow.PhaseId.TechnicalDesign,
            _ => throw new WorkflowDomainException(
                $"Unsupported completed workflow reopen reason '{reasonKind}'. Expected 'merge-conflict', 'defect', 'functional-issue', or 'technical-issue'.")
        };

    private static async Task<RefinementSession?> ReadRefinementSessionAsync(
        UserStoryFilePaths paths,
        CancellationToken cancellationToken)
    {
        if (File.Exists(paths.RefinementFilePath))
        {
            var refinementMarkdown = await File.ReadAllTextAsync(paths.RefinementFilePath, cancellationToken);
            var session = UserStoryRefinementMarkdown.Parse(refinementMarkdown);
            if (session is not null)
            {
                return session;
            }
        }

        if (!File.Exists(paths.MainArtifactPath))
        {
            return null;
        }

        var userStoryMarkdown = await File.ReadAllTextAsync(paths.MainArtifactPath, cancellationToken);
        return UserStoryRefinementMarkdown.Parse(userStoryMarkdown);
    }

    private static async Task<SpecDocument> LoadCurrentSpecDocumentAsync(
        UserStoryFilePaths paths,
        CancellationToken cancellationToken)
    {
        var jsonPath = paths.GetLatestExistingPhaseArtifactJsonPath(Workflow.PhaseId.Spec);
        if (!string.IsNullOrWhiteSpace(jsonPath) && File.Exists(jsonPath))
        {
            return SpecJson.Parse(await File.ReadAllTextAsync(jsonPath, cancellationToken));
        }

        var markdownPath = paths.GetLatestExistingPhaseArtifactPath(Workflow.PhaseId.Spec)
            ?? throw new WorkflowDomainException("The spec artifact does not exist yet.");
        return SpecMarkdownImporter.Import(await File.ReadAllTextAsync(markdownPath, cancellationToken));
    }

    private static async Task<IReadOnlyCollection<ApprovalQuestionDetails>> ReadApprovalQuestionsAsync(
        UserStoryFilePaths paths,
        CancellationToken cancellationToken)
    {
        var specPath = paths.GetLatestExistingPhaseArtifactPath(Workflow.PhaseId.Spec);
        if (string.IsNullOrWhiteSpace(specPath))
        {
            return [];
        }

        var specDocument = await LoadCurrentSpecDocumentAsync(paths, cancellationToken);
        return specDocument.HumanApprovalQuestions
            .Select((item, index) => new ApprovalQuestionDetails(
                index + 1,
                item.Question,
                item.Status,
                SpecJson.IsResolved(item),
                item.Answer,
                item.AnsweredBy,
                item.AnsweredAtUtc))
            .ToArray();
    }

    private static string ToRuntimeStatusSlug(RuntimeStatus status) => status switch
    {
        RuntimeStatus.Idle => "idle",
        RuntimeStatus.Running => "running",
        RuntimeStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
