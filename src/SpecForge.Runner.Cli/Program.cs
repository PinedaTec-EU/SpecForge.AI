using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;
using SpecForge.Domain.Application;
using SpecForge.Domain.Persistence;
using SpecForge.OpenAICompatible;

if (args.Length == 0)
{
    return ExitWithError("A command is required.");
}

try
{
    var command = args[0];

    switch (command)
    {
        case "create-us":
        {
            var applicationService = CreateApplicationService(args);
            await HandleCreateUserStoryAsync(applicationService, args);
            return 0;
        }
        case "import-us":
        {
            var applicationService = CreateApplicationService(args);
            await HandleImportUserStoryAsync(applicationService, args);
            return 0;
        }
        case "continue-phase":
        {
            var applicationService = CreateApplicationService(args);
            await HandleContinuePhaseAsync(applicationService, args);
            return 0;
        }
        case "list-user-stories":
        {
            var applicationService = CreateApplicationService(args);
            await HandleListUserStoriesAsync(applicationService, args);
            return 0;
        }
        case "get-user-story-summary":
        {
            var applicationService = CreateApplicationService(args);
            await HandleGetUserStorySummaryAsync(applicationService, args);
            return 0;
        }
        case "approve-phase":
        {
            var applicationService = CreateApplicationService(args);
            await HandleApprovePhaseAsync(applicationService, args);
            return 0;
        }
        case "graph-status":
            HandleGraphStatus(args);
            return 0;
        case "graph-global":
            await HandleGraphGlobalAsync(args);
            return 0;
        case "graph-impact":
            await HandleGraphImpactAsync(args);
            return 0;
        case "graph-query":
            HandleGraphQuery(args);
            return 0;
        case "serve-workflow":
            await HandleServeWorkflowAsync(args);
            return 0;
        default:
            return ExitWithError($"Unknown command '{command}'.");
    }
}
catch (Exception exception)
{
    return ExitWithError(exception.Message);
}

static async Task HandleCreateUserStoryAsync(SpecForgeApplicationService applicationService, IReadOnlyList<string> args)
{
    EnsureArgumentCount(args, expectedCount: 7);

    var workspaceRoot = args[1];
    var usId = args[2];
    var title = args[3];
    var kind = args[4];
    var category = args[5];
    var sourceText = args[6];
    var result = await applicationService.CreateUserStoryAsync(workspaceRoot, usId, title, kind, category, sourceText, ResolveCurrentGitOwner(workspaceRoot));

    WriteJson(new
    {
        result.UsId,
        result.RootDirectory,
        result.MainArtifactPath
    });
}

static async Task HandleImportUserStoryAsync(SpecForgeApplicationService applicationService, IReadOnlyList<string> args)
{
    EnsureArgumentCount(args, expectedCount: 7);

    var workspaceRoot = args[1];
    var usId = args[2];
    var sourcePath = args[3];
    var title = args[4];
    var kind = args[5];
    var category = args[6];
    var result = await applicationService.ImportUserStoryAsync(workspaceRoot, usId, sourcePath, title, kind, category, ResolveCurrentGitOwner(workspaceRoot));

    WriteJson(new
    {
        result.UsId,
        result.RootDirectory,
        result.MainArtifactPath
    });
}

static async Task HandleContinuePhaseAsync(SpecForgeApplicationService applicationService, IReadOnlyList<string> args)
{
    EnsureArgumentCount(args, expectedCount: 3);

    var workspaceRoot = args[1];
    var usId = args[2];
    var result = await applicationService.GenerateNextPhaseAsync(workspaceRoot, usId, ResolveCurrentGitOwner(workspaceRoot));

    WriteJson(new
    {
        result.UsId,
        currentPhase = result.CurrentPhase,
        status = result.Status,
        result.GeneratedArtifactPath
    });
}

static async Task HandleListUserStoriesAsync(SpecForgeApplicationService applicationService, IReadOnlyList<string> args)
{
    EnsureArgumentCount(args, expectedCount: 2);

    var workspaceRoot = args[1];
    var items = await applicationService.ListUserStoriesAsync(workspaceRoot);
    WriteJson(new { items });
}

static async Task HandleGetUserStorySummaryAsync(SpecForgeApplicationService applicationService, IReadOnlyList<string> args)
{
    EnsureArgumentCount(args, expectedCount: 3);

    var workspaceRoot = args[1];
    var usId = args[2];
    var summary = await applicationService.GetUserStorySummaryAsync(workspaceRoot, usId);
    WriteJson(summary);
}

static async Task HandleApprovePhaseAsync(
    SpecForgeApplicationService applicationService,
    IReadOnlyList<string> args)
{
    EnsureArgumentCount(args, expectedCount: 5);

    var workspaceRoot = args[1];
    var usId = args[2];
    var baseBranch = args[3];
    var workBranch = args[4];
    var normalizedBaseBranch = string.Equals(baseBranch, "-", StringComparison.Ordinal) ? null : baseBranch;
    var normalizedWorkBranch = string.Equals(workBranch, "-", StringComparison.Ordinal) ? null : workBranch;
    var result = await applicationService.ApprovePhaseAsync(
        workspaceRoot,
        usId,
        normalizedBaseBranch,
        normalizedWorkBranch,
        ResolveCurrentGitOwner(workspaceRoot));
    WriteJson(result);
}

static void HandleGraphStatus(IReadOnlyList<string> args)
{
    if (args.Count is < 2 or > 3)
    {
        throw new InvalidOperationException("Expected workspace root and optional user story id for command 'graph-status'.");
    }

    var workspaceRoot = args[1];
    var usId = args.Count == 3 ? NormalizeOptionalArgument(args[2]) : null;
    WriteJson(SemanticGraphOperations.DescribeStatus(workspaceRoot, usId));
}

static async Task HandleGraphGlobalAsync(IReadOnlyList<string> args)
{
    if (args.Count is < 5 or > 7)
    {
        throw new InvalidOperationException("Expected workspace root, mode, actor, reason, optional confirm-overwrite, and optional dry-run for command 'graph-global'.");
    }

    var request = new SemanticGraphGlobalOperationRequest(
        Mode: args[2],
        Actor: args[3],
        Reason: args[4],
        DryRun: args.Count >= 7 && bool.TryParse(args[6], out var dryRun) && dryRun,
        ConfirmOverwrite: args.Count >= 6 && bool.TryParse(args[5], out var confirmOverwrite) && confirmOverwrite,
        TriggerSurface: "cli");
    var result = await SemanticGraphOperations.RunGlobalOperationAsync(args[1], request);
    WriteJson(result);
}

static async Task HandleGraphImpactAsync(IReadOnlyList<string> args)
{
    if (args.Count is < 5 or > 6)
    {
        throw new InvalidOperationException("Expected workspace root, user story id, actor, reason, and optional dry-run for command 'graph-impact'.");
    }

    var request = new SemanticGraphImpactOperationRequest(
        UsId: args[2],
        Actor: args[3],
        Reason: args[4],
        DryRun: args.Count == 6 && bool.TryParse(args[5], out var dryRun) && dryRun,
        TriggerSurface: "cli");
    var result = await SemanticGraphOperations.MaterializeImpactGraphAsync(args[1], request);
    WriteJson(result);
}

static void HandleGraphQuery(IReadOnlyList<string> args)
{
    if (args.Count is < 8 or > 10)
    {
        throw new InvalidOperationException("Expected workspace root, query kind, actor, user story id or '-', file path or '-', phase or '-', reason or '-', and optional max-depth plus include-tests for command 'graph-query'.");
    }

    var request = new SemanticGraphQueryRequest(
        QueryKind: args[2],
        Actor: args[3],
        UsId: NormalizeOptionalArgument(args[4]),
        FilePath: NormalizeOptionalArgument(args[5]),
        Phase: NormalizeOptionalArgument(args[6]),
        Reason: NormalizeOptionalArgument(args[7]),
        MaxDepth: args.Count >= 9 && int.TryParse(args[8], out var maxDepth) ? maxDepth : 1,
        IncludeTests: args.Count >= 10 && bool.TryParse(args[9], out var includeTests) && includeTests,
        TriggerSurface: "cli");
    WriteJson(SemanticGraphOperations.ExecuteQuery(args[1], request));
}

static async Task HandleServeWorkflowAsync(IReadOnlyList<string> args)
{
    if (args.Count is < 2 or > 4)
    {
        throw new InvalidOperationException("Expected workspace root, optional user story id, and optional URL prefix for command 'serve-workflow'.");
    }

    var workspaceRoot = Path.GetFullPath(args[1]);
    var applicationService = CreateApplicationServiceForWorkspace(workspaceRoot);
    var usId = args.Count >= 3 && !LooksLikeHttpPrefix(args[2])
        ? args[2]
        : await ResolveDefaultWorkflowPortalUserStoryIdAsync(applicationService, workspaceRoot);
    var prefix = args.Count switch
    {
        4 => NormalizeHttpPrefix(args[3]),
        3 when LooksLikeHttpPrefix(args[2]) => NormalizeHttpPrefix(args[2]),
        _ => "http://localhost:5128/"
    };
    var renderCache = new WorkflowPortalRenderCache();

    using var listener = new HttpListener();
    listener.Prefixes.Add(prefix);
    listener.Start();
    Console.WriteLine($"SpecForge workflow portal listening at {prefix}");
    Console.WriteLine($"Workspace: {workspaceRoot}");
    Console.WriteLine($"User story: {usId}");

    while (listener.IsListening)
    {
        var context = await listener.GetContextAsync();
        _ = Task.Run(() => HandleWorkflowPortalRequestAsync(context, applicationService, workspaceRoot, usId, renderCache));
    }
}

static string? NormalizeOptionalArgument(string? value) =>
    string.IsNullOrWhiteSpace(value) || string.Equals(value, "-", StringComparison.Ordinal)
        ? null
        : value;

static async Task<T> ReadJsonRequestAsync<T>(
    HttpListenerRequest request,
    string errorMessage)
{
    using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
    var payload = await reader.ReadToEndAsync();
    return JsonSerializer.Deserialize<T>(payload, SpecForgePortalSettingsStore.JsonOptions)
        ?? throw new InvalidOperationException(errorMessage);
}

static async Task<T> ReadJsonRequestWithParserAsync<T>(
    HttpListenerRequest request,
    Func<string, T> parser,
    string errorMessage)
{
    using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
    var payload = await reader.ReadToEndAsync();

    try
    {
        return parser(payload);
    }
    catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
    {
        throw new InvalidOperationException(errorMessage, exception);
    }
}

static async Task HandleWorkflowPortalRequestAsync(
    HttpListenerContext context,
    SpecForgeApplicationService applicationService,
    string workspaceRoot,
    string usId,
    WorkflowPortalRenderCache renderCache)
{
    try
    {
        var path = context.Request.Url?.AbsolutePath.TrimEnd('/') ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = "/";
        }

        var requestSidebarVisibility = ResolveWorkflowPortalSidebarVisibility(context.Request);
        var requestShowCompletedUserStories = ResolveWorkflowPortalQueryFlag(context.Request, "sidebarCompleted");
        var requestShowBlockedUserStories = ResolveWorkflowPortalQueryFlag(context.Request, "sidebarBlocked");
        var requestShowHiddenUserStories = ResolveWorkflowPortalQueryFlag(context.Request, "sidebarHiddenVisible");
        var requestIncludeOtherOwners = ResolveWorkflowPortalQueryFlag(context.Request, "sidebarOtherOwners");
        var requestShowCreateForm = ResolveWorkflowPortalQueryFlag(context.Request, "create")
            || ResolveWorkflowPortalQueryFlag(context.Request, "sidebarCreate");
        var requestSidebarWatchingUserStoryIds = ResolveWorkflowPortalUserStoryIdList(context.Request, "sidebarWatching");
        var requestSidebarHiddenUserStoryIds = ResolveWorkflowPortalUserStoryIdList(context.Request, "sidebarHidden");
        var requestUsId = await ResolveWorkflowPortalUserStoryIdAsync(
            applicationService,
            workspaceRoot,
            context.Request,
            requestSidebarVisibility,
            requestShowCompletedUserStories,
            requestShowBlockedUserStories,
            requestShowHiddenUserStories,
            requestIncludeOtherOwners,
            requestSidebarWatchingUserStoryIds,
            requestSidebarHiddenUserStoryIds);

        switch ((context.Request.HttpMethod, path))
        {
            case ("GET", "/"):
                if (TryBuildWorkflowPortalConfigurationRedirect(
                        workspaceRoot,
                        context.Request.Url?.GetLeftPart(UriPartial.Authority) ?? "http://localhost:5128",
                        out var redirectLocation))
                {
                    context.Response.StatusCode = 302;
                    context.Response.RedirectLocation = redirectLocation;
                    context.Response.Close();
                    return;
                }
                await WriteHtmlResponseAsync(
                    context.Response,
                    await BuildWorkflowPortalHtmlAsync(
                        applicationService,
                        workspaceRoot,
                        requestUsId,
                        context.Request.QueryString["selectedPhaseId"],
                        requestSidebarVisibility,
                        requestShowCompletedUserStories,
                        requestShowBlockedUserStories,
                        requestShowHiddenUserStories,
                        requestIncludeOtherOwners,
                        requestShowCreateForm,
                        requestSidebarWatchingUserStoryIds,
                        requestSidebarHiddenUserStoryIds,
                        context.Request.Url?.GetLeftPart(UriPartial.Authority) ?? "http://localhost:5128",
                        renderCache));
                return;
            case ("GET", "/api/sidebar-html"):
                await WriteHtmlResponseAsync(
                    context.Response,
                    await BuildWorkflowPortalSidebarHtmlAsync(
                        applicationService,
                        workspaceRoot,
                        requestUsId,
                        requestSidebarVisibility,
                        requestShowCompletedUserStories,
                        requestShowBlockedUserStories,
                        requestShowHiddenUserStories,
                        requestIncludeOtherOwners,
                        requestSidebarWatchingUserStoryIds,
                        requestSidebarHiddenUserStoryIds,
                        context.Request.Url?.GetLeftPart(UriPartial.Authority) ?? "http://localhost:5128"));
                return;
            case ("POST", "/api/create-form-html"):
                await WriteHtmlResponseAsync(
                    context.Response,
                    await BuildWorkflowPortalCreateFormHtmlAsync(
                        context,
                        workspaceRoot,
                        requestUsId));
                return;
            case ("GET", "/api/workflow"):
                if (string.IsNullOrWhiteSpace(requestUsId))
                {
                    context.Response.StatusCode = 404;
                    await WriteTextResponseAsync(context.Response, "No selected user story.", "text/plain");
                    return;
                }
                await WriteJsonResponseAsync(context.Response, await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, requestUsId));
                return;
            case ("GET", "/api/workflow-signature"):
                await WriteTextResponseAsync(
                    context.Response,
                    await BuildWorkflowPortalSignatureAsync(
                        applicationService,
                        workspaceRoot,
                        requestUsId,
                        requestSidebarVisibility,
                        requestShowCompletedUserStories,
                        requestShowBlockedUserStories),
                    "text/plain");
                return;
            case ("GET", "/api/runtime-status"):
                if (string.IsNullOrWhiteSpace(requestUsId))
                {
                    context.Response.StatusCode = 404;
                    await WriteTextResponseAsync(context.Response, "No selected user story.", "text/plain");
                    return;
                }
                await WriteJsonResponseAsync(context.Response, await applicationService.GetUserStoryRuntimeStatusAsync(workspaceRoot, requestUsId));
                return;
            case ("GET", "/api/file"):
                await WriteHtmlResponseAsync(
                    context.Response,
                    await BuildWorkflowPortalFileHtmlAsync(workspaceRoot, context.Request.QueryString["path"]));
                return;
            case ("GET", "/configuration"):
                await WriteHtmlResponseAsync(context.Response, BuildConfigurationPortalHtml());
                return;
            case ("GET", "/api/settings"):
                await WriteJsonResponseAsync(context.Response, BuildConfigurationSettingsResponse(workspaceRoot));
                return;
            case ("PUT", "/api/settings"):
            {
                var settings = await ReadJsonRequestWithParserAsync(
                    context.Request,
                    payload => SpecForgePortalSettingsStore.Deserialize(payload, workspaceRoot),
                    "Settings payload could not be parsed.");
                SpecForgePortalSettingsStore.Save(workspaceRoot, settings);
                await WriteJsonResponseAsync(context.Response, BuildConfigurationSettingsResponse(workspaceRoot, settings));
                return;
            }
            case ("GET", "/api/summary"):
                await WriteJsonResponseAsync(
                    context.Response,
                    await applicationService.GetUserStorySummaryAsync(
                        workspaceRoot,
                        RequireSelectedWorkflowPortalUserStoryId(requestUsId)));
                return;
            case ("GET", "/api/user-stories"):
                await WriteJsonResponseAsync(
                    context.Response,
                    new
                    {
                        items = await applicationService.ListUserStoriesAsync(
                            workspaceRoot,
                            context.Request.QueryString["visibility"] ?? "active")
                    });
                return;
            case ("POST", "/api/drop-user-story"):
                await HandleDropOrRecoverUserStoryAsync(context, workspaceRoot, drop: true);
                return;
            case ("POST", "/api/recover-user-story"):
                await HandleDropOrRecoverUserStoryAsync(context, workspaceRoot, drop: false);
                return;
            case ("POST", "/api/create-user-story"):
                await HandleCreateUserStoryRequestAsync(context, applicationService, workspaceRoot);
                return;
            case ("POST", "/api/update-user-story-info"):
                await HandleUpdateUserStoryInfoAsync(context, applicationService, workspaceRoot);
                return;
            case ("POST", "/api/reset-user-story-to-capture"):
                await HandleResetUserStoryToCaptureAsync(context, applicationService, workspaceRoot);
                return;
            case ("POST", "/api/analyze-user-story-lineage"):
                await HandleAnalyzeUserStoryLineageAsync(context, applicationService, workspaceRoot);
                return;
            case ("POST", "/api/repair-user-story-lineage"):
                await HandleRepairUserStoryLineageAsync(context, applicationService, workspaceRoot);
                return;
            case ("POST", "/api/continue"):
                await WriteJsonResponseAsync(
                    context.Response,
                    await applicationService.GenerateNextPhaseAsync(
                        workspaceRoot,
                        RequireSelectedWorkflowPortalUserStoryId(requestUsId),
                        ResolveCurrentGitOwner(workspaceRoot)));
                return;
            case ("POST", "/api/approval-answer"):
            {
                var request = await ReadJsonRequestAsync<ApprovalAnswerSubmitRequest>(
                    context.Request,
                    "Approval answer payload could not be parsed.");
                await WriteJsonResponseAsync(
                    context.Response,
                    await applicationService.SubmitApprovalAnswerAsync(
                        workspaceRoot,
                        RequireSelectedWorkflowPortalUserStoryId(requestUsId),
                        request.Question,
                        request.Answer,
                        request.Actor ?? ResolveCurrentGitOwner(workspaceRoot)));
                return;
            }
            case ("POST", "/api/refinement-answers"):
            {
                var request = await ReadJsonRequestAsync<RefinementAnswersSubmitRequest>(
                    context.Request,
                    "Refinement answers payload could not be parsed.");
                await WriteJsonResponseAsync(
                    context.Response,
                    await applicationService.SubmitRefinementAnswersAsync(
                        workspaceRoot,
                        RequireSelectedWorkflowPortalUserStoryId(requestUsId),
                        request.Answers,
                        request.Actor ?? ResolveCurrentGitOwner(workspaceRoot)));
                return;
            }
            case ("POST", "/api/attach-files"):
            {
                var request = await ReadJsonRequestAsync<AttachWorkflowFilesRequest>(
                    context.Request,
                    "Attach files payload could not be parsed.");
                await WriteJsonResponseAsync(
                    context.Response,
                    await AttachWorkflowFilesAsync(
                        workspaceRoot,
                        RequireSelectedWorkflowPortalUserStoryId(requestUsId),
                        request));
                return;
            }
            case ("POST", "/api/add-context-files"):
            {
                var request = await ReadJsonRequestAsync<AddContextFilesRequest>(
                    context.Request,
                    "Add context files payload could not be parsed.");
                await WriteJsonResponseAsync(
                    context.Response,
                    await AddContextFilesAsync(
                        workspaceRoot,
                        RequireSelectedWorkflowPortalUserStoryId(requestUsId),
                        request));
                return;
            }
            case ("POST", "/api/workflow-graph-layout"):
            {
                var request = await ReadJsonRequestAsync<SaveWorkflowGraphLayoutRequest>(
                    context.Request,
                    "Workflow graph layout payload could not be parsed.");
                await WriteJsonResponseAsync(
                    context.Response,
                    await SaveWorkflowGraphLayoutAsync(workspaceRoot, request));
                return;
            }
            case ("POST", "/api/client-log"):
            {
                var request = await ReadJsonRequestAsync<PortalClientLogRequest>(
                    context.Request,
                    "Client log payload could not be parsed.");
                LogWorkflowPortalClientEvent(request);
                await WriteJsonResponseAsync(context.Response, new { ok = true });
                return;
            }
            case ("POST", "/api/approve"):
            {
                var request = await ReadJsonRequestAsync<ApprovalSubmitRequest>(
                    context.Request,
                    "Approval payload could not be parsed.");
                await WriteJsonResponseAsync(
                    context.Response,
                    await applicationService.ApprovePhaseAsync(
                        workspaceRoot,
                        RequireSelectedWorkflowPortalUserStoryId(requestUsId),
                        request.BaseBranch,
                        request.WorkBranch,
                        request.Actor ?? ResolveCurrentGitOwner(workspaceRoot)));
                return;
            }
            case ("POST", "/api/decomposition-approval"):
            {
                var request = await ReadJsonRequestAsync<DecompositionApprovalSubmitRequest>(
                    context.Request,
                    "Decomposition approval payload could not be parsed.");
                await WriteJsonResponseAsync(
                    context.Response,
                    string.Equals(request.Decision, "approve", StringComparison.OrdinalIgnoreCase)
                        ? await applicationService.ApproveDecompositionAsync(
                            workspaceRoot,
                            RequireSelectedWorkflowPortalUserStoryId(requestUsId),
                            request.Actor ?? ResolveCurrentGitOwner(workspaceRoot))
                        : await applicationService.RejectDecompositionAsync(
                            workspaceRoot,
                            RequireSelectedWorkflowPortalUserStoryId(requestUsId),
                            request.Actor ?? ResolveCurrentGitOwner(workspaceRoot)));
                return;
            }
            case ("POST", "/api/suggest-approval-answer"):
            {
                var request = await ReadJsonRequestAsync<ApprovalAnswerSuggestionRequest>(
                    context.Request,
                    "Suggestion payload could not be parsed.");
                await WriteJsonResponseAsync(
                    context.Response,
                    await applicationService.SuggestApprovalAnswerAsync(
                        workspaceRoot,
                        RequireSelectedWorkflowPortalUserStoryId(requestUsId),
                        request.Question,
                        request.Actor ?? ResolveCurrentGitOwner(workspaceRoot)));
                return;
            }
            default:
                context.Response.StatusCode = 404;
                await WriteTextResponseAsync(context.Response, "Not found", "text/plain");
                return;
        }
    }
    catch (Exception exception)
    {
        context.Response.StatusCode = 500;
        await WriteTextResponseAsync(context.Response, exception.Message, "text/plain");
    }
}

static async Task<string> BuildWorkflowPortalHtmlAsync(
    SpecForgeApplicationService applicationService,
    string workspaceRoot,
    string? usId,
    string? selectedPhaseId,
    string? sidebarVisibility,
    bool showCompletedUserStories,
    bool showBlockedUserStories,
    bool showHiddenUserStories,
    bool includeOtherOwners,
    bool showCreateForm,
    IReadOnlyList<string> watchingUserStoryIds,
    IReadOnlyList<string> hiddenUserStoryIds,
    string workflowPortalOrigin,
    WorkflowPortalRenderCache renderCache)
{
    var normalizedSidebarVisibility = string.Equals(sidebarVisibility, "dropped", StringComparison.OrdinalIgnoreCase)
        ? "dropped"
        : "active";
    var currentActor = ResolveCurrentGitOwner(workspaceRoot);
    var activeSidebarUserStories = await applicationService.ListUserStoriesAsync(workspaceRoot);
    var droppedSidebarUserStories = await applicationService.ListUserStoriesAsync(workspaceRoot, "dropped");
    var sidebarUserStories = normalizedSidebarVisibility == "dropped"
        ? droppedSidebarUserStories
        : activeSidebarUserStories;
    UserStoryWorkflowDetails? workflow = null;
    string? resolvedSelectedPhaseId = null;
    WorkflowPhaseDetails? selectedPhase = null;
    if (!string.IsNullOrWhiteSpace(usId))
    {
        workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, usId);
        resolvedSelectedPhaseId = ResolveSelectedWorkflowPhaseId(workflow, selectedPhaseId);
        selectedPhase = ResolveSelectedWorkflowPhase(workflow, resolvedSelectedPhaseId);
    }
    var droppedUserStoryCount = droppedSidebarUserStories.Count;
    var portalSettings = SpecForgePortalSettingsStore.LoadOrDefault(workspaceRoot);
    var workflowGraphLayoutSignature = await ReadWorkflowGraphLayoutSignatureAsync(workspaceRoot);
    var signature = BuildWorkflowSignature(
        workflow,
        activeSidebarUserStories,
        droppedSidebarUserStories,
        workflowGraphLayoutSignature);
    var renderCacheSignature = BuildWorkflowPortalRenderCacheSignature(
        signature,
        normalizedSidebarVisibility,
        showCompletedUserStories,
        showBlockedUserStories,
        showHiddenUserStories,
        includeOtherOwners,
        showCreateForm,
        watchingUserStoryIds,
        hiddenUserStoryIds,
        currentActor,
        portalSettings.WorkflowGraphLayoutMode,
        portalSettings.WorkflowGraphInitialZoomMode);
    var cachePhaseId = resolvedSelectedPhaseId ?? "__none__";
    if (renderCache.TryGet(renderCacheSignature, cachePhaseId, selectedPhase, out var cachedHtml))
    {
        return cachedHtml;
    }

    var payload = JsonSerializer.Serialize(
        new
        {
            workflow,
            selectedPhaseId = resolvedSelectedPhaseId,
            selectedArtifactContent = await ReadFileContentOrNullAsync(selectedPhase?.ArtifactPath),
            selectedOperationContent = await ReadFileContentOrNullAsync(selectedPhase?.OperationLogPath),
            runtimeVersion = GetRuntimeVersion() ?? workflow?.LastRuntimeVersion ?? workflow?.CreatedWithRuntimeVersion,
            userStories = activeSidebarUserStories,
            sidebarUserStories,
            activeSidebarUserStories,
            droppedSidebarUserStories,
            showDroppedUserStories = normalizedSidebarVisibility == "dropped",
            showCompletedUserStories,
            showBlockedUserStories,
            showHiddenUserStories,
            includeOtherOwners,
            showCreateForm,
            watchingUserStoryIds,
            hiddenUserStoryIds,
            droppedUserStoryCount,
            configurationPortalUrl = BuildConfigurationPortalUrl(workflowPortalOrigin),
            configurationProvidersUrl = BuildConfigurationPortalUrl(workflowPortalOrigin, "providers"),
            configurationAdvancedUrl = BuildConfigurationPortalUrl(workflowPortalOrigin, "advanced"),
            categories = new RepositoryCategoryCatalog().GetCategories(workspaceRoot),
            currentActor,
            selectedUsId = usId,
            noSelectionReason = ResolveWorkflowPortalNoSelectionReason(usId, sidebarUserStories),
            workflowGraphLayoutMode = portalSettings.WorkflowGraphLayoutMode,
            workflowGraphInitialZoomMode = portalSettings.WorkflowGraphInitialZoomMode,
            workspaceRoot,
            signature
        },
        SpecForgePortalSettingsStore.JsonOptions);

    var html = await RenderWorkflowHtmlWithNodeAsync(payload);
    renderCache.Store(renderCacheSignature, cachePhaseId, selectedPhase, html);
    return html;
}

static async Task<string> BuildWorkflowPortalSidebarHtmlAsync(
    SpecForgeApplicationService applicationService,
    string workspaceRoot,
    string? usId,
    string? sidebarVisibility,
    bool showCompletedUserStories,
    bool showBlockedUserStories,
    bool showHiddenUserStories,
    bool includeOtherOwners,
    IReadOnlyList<string> watchingUserStoryIds,
    IReadOnlyList<string> hiddenUserStoryIds,
    string workflowPortalOrigin)
{
    var normalizedSidebarVisibility = string.Equals(sidebarVisibility, "dropped", StringComparison.OrdinalIgnoreCase)
        ? "dropped"
        : "active";
    var activeSidebarUserStories = await applicationService.ListUserStoriesAsync(workspaceRoot);
    var droppedSidebarUserStories = await applicationService.ListUserStoriesAsync(workspaceRoot, "dropped");
    var sidebarUserStories = normalizedSidebarVisibility == "dropped"
        ? droppedSidebarUserStories
        : activeSidebarUserStories;
    var currentActor = ResolveCurrentGitOwner(workspaceRoot);
    var payload = JsonSerializer.Serialize(
        new
        {
            renderSidebarOnly = true,
            userStories = activeSidebarUserStories,
            sidebarUserStories,
            activeSidebarUserStories,
            droppedSidebarUserStories,
            showDroppedUserStories = normalizedSidebarVisibility == "dropped",
            showCompletedUserStories,
            showBlockedUserStories,
            showHiddenUserStories,
            includeOtherOwners,
            watchingUserStoryIds,
            hiddenUserStoryIds,
            droppedUserStoryCount = droppedSidebarUserStories.Count,
            configurationPortalUrl = BuildConfigurationPortalUrl(workflowPortalOrigin),
            configurationProvidersUrl = BuildConfigurationPortalUrl(workflowPortalOrigin, "providers"),
            configurationAdvancedUrl = BuildConfigurationPortalUrl(workflowPortalOrigin, "advanced"),
            currentActor,
            selectedUsId = usId
        },
        SpecForgePortalSettingsStore.JsonOptions);
    return await RenderWorkflowHtmlWithNodeAsync(payload);
}

static async Task<string> BuildWorkflowPortalCreateFormHtmlAsync(
    HttpListenerContext context,
    string workspaceRoot,
    string? usId)
{
    var request = await ReadJsonRequestAsync<CreateUserStoryFormRenderRequest>(
        context.Request,
        "Create form render payload could not be parsed.");

    var payload = JsonSerializer.Serialize(
        new
        {
            renderCreateFormOnly = true,
            createFileMode = string.Equals(request.CreateFileMode, "attachment", StringComparison.OrdinalIgnoreCase)
                ? "attachment"
                : "context",
            createFiles = request.CreateFiles ?? [],
            createFormResetToken = request.CreateFormResetToken,
            categories = new RepositoryCategoryCatalog().GetCategories(workspaceRoot),
            currentActor = ResolveCurrentGitOwner(workspaceRoot),
            selectedUsId = usId,
            runtimeVersion = GetRuntimeVersion()
        },
        SpecForgePortalSettingsStore.JsonOptions);

    return await RenderWorkflowHtmlWithNodeAsync(payload);
}

static async Task<string> BuildWorkflowPortalFileHtmlAsync(
    string workspaceRoot,
    string? requestedPath)
{
    var resolvedPath = ResolveWorkflowPortalFilePath(workspaceRoot, requestedPath);
    var content = await File.ReadAllTextAsync(resolvedPath);
    var fileName = Path.GetFileName(resolvedPath);
    var escapedFileName = WebUtility.HtmlEncode(fileName);
    var escapedPath = WebUtility.HtmlEncode(resolvedPath);
    var escapedContent = WebUtility.HtmlEncode(content);

    return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>{{escapedFileName}} · SpecForge.AI</title>
  <style>
    :root { color-scheme: dark; }
    body {
      margin: 0;
      min-height: 100vh;
      font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", monospace;
      background:
        radial-gradient(circle at top, rgba(88, 214, 141, 0.14), transparent 42%),
        linear-gradient(180deg, #081116 0%, #0d1b22 100%);
      color: #e5fff5;
    }
    main {
      max-width: 1080px;
      margin: 0 auto;
      padding: 24px;
    }
    .meta {
      margin-bottom: 18px;
      padding: 18px 20px;
      border: 1px solid rgba(114, 241, 184, 0.18);
      border-radius: 16px;
      background: rgba(5, 15, 20, 0.82);
      box-shadow: 0 18px 48px rgba(0, 0, 0, 0.28);
    }
    h1 {
      margin: 0 0 8px;
      font-size: 1.1rem;
    }
    p {
      margin: 0;
      color: rgba(229, 255, 245, 0.72);
      word-break: break-all;
    }
    pre {
      margin: 0;
      padding: 20px;
      overflow: auto;
      border-radius: 18px;
      border: 1px solid rgba(114, 241, 184, 0.14);
      background: rgba(4, 12, 16, 0.92);
      line-height: 1.55;
      white-space: pre-wrap;
      word-break: break-word;
      box-shadow: inset 0 0 0 1px rgba(255, 255, 255, 0.02);
    }
  </style>
</head>
<body>
  <main>
    <section class="meta">
      <h1>{{escapedFileName}}</h1>
      <p>{{escapedPath}}</p>
    </section>
    <pre>{{escapedContent}}</pre>
  </main>
</body>
</html>
""";
}

static async Task<string?> ResolveWorkflowPortalUserStoryIdAsync(
    SpecForgeApplicationService applicationService,
    string workspaceRoot,
    HttpListenerRequest request,
    string? sidebarVisibility,
    bool showCompletedUserStories,
    bool showBlockedUserStories,
    bool showHiddenUserStories,
    bool includeOtherOwners,
    IReadOnlyList<string> watchingUserStoryIds,
    IReadOnlyList<string> hiddenUserStoryIds)
{
    var queryUsId = request.QueryString["usId"];
    if (!string.IsNullOrWhiteSpace(queryUsId))
    {
        return await ResolveVisibleWorkflowPortalUserStoryIdAsync(
            applicationService,
            workspaceRoot,
            queryUsId,
            preferExplicitSelection: true,
            sidebarVisibility,
            showCompletedUserStories,
            showBlockedUserStories,
            showHiddenUserStories,
            includeOtherOwners,
            watchingUserStoryIds,
            hiddenUserStoryIds);
    }

    var referer = request.UrlReferrer;
    var refererUsId = referer is null ? null : ParseQueryValue(referer.Query, "usId");
    return await ResolveVisibleWorkflowPortalUserStoryIdAsync(
        applicationService,
        workspaceRoot,
        refererUsId,
        preferExplicitSelection: true,
        sidebarVisibility,
        showCompletedUserStories,
        showBlockedUserStories,
        showHiddenUserStories,
        includeOtherOwners,
        watchingUserStoryIds,
        hiddenUserStoryIds);
}

static string ResolveWorkflowPortalFilePath(string workspaceRoot, string? requestedPath)
{
    if (string.IsNullOrWhiteSpace(requestedPath))
    {
        throw new InvalidOperationException("A file path is required.");
    }

    var normalizedWorkspaceRoot = Path.GetFullPath(workspaceRoot)
        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        + Path.DirectorySeparatorChar;
    var resolvedPath = Path.GetFullPath(requestedPath);

    if (!resolvedPath.StartsWith(normalizedWorkspaceRoot, StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Refusing to open a file outside the workspace root.");
    }

    if (!File.Exists(resolvedPath))
    {
        throw new FileNotFoundException("The requested file was not found.", resolvedPath);
    }

    return resolvedPath;
}

static string? ResolveWorkflowPortalSidebarVisibility(HttpListenerRequest request)
{
    var querySidebarVisibility = request.QueryString["sidebarVisibility"];
    if (!string.IsNullOrWhiteSpace(querySidebarVisibility))
    {
        return querySidebarVisibility;
    }

    var referer = request.UrlReferrer;
    return referer is null ? null : ParseQueryValue(referer.Query, "sidebarVisibility");
}

static bool ResolveWorkflowPortalQueryFlag(HttpListenerRequest request, string key)
{
    var queryValue = request.QueryString[key];
    if (!string.IsNullOrWhiteSpace(queryValue))
    {
        return string.Equals(queryValue, "true", StringComparison.OrdinalIgnoreCase);
    }

    var referer = request.UrlReferrer;
    return string.Equals(ParseQueryValue(referer?.Query ?? string.Empty, key), "true", StringComparison.OrdinalIgnoreCase);
}

static IReadOnlyList<string> ResolveWorkflowPortalUserStoryIdList(HttpListenerRequest request, string key)
{
    var queryValue = request.QueryString[key];
    if (string.IsNullOrWhiteSpace(queryValue))
    {
        var referer = request.UrlReferrer;
        queryValue = referer is null ? null : ParseQueryValue(referer.Query, key);
    }

    return NormalizeUserStoryIds(queryValue);
}

static IReadOnlyList<string> NormalizeUserStoryIds(string? value) =>
    (value ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(item => item.Trim().ToUpperInvariant())
        .Where(item => item.Length > 0)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

static async Task<string?> ResolveVisibleWorkflowPortalUserStoryIdAsync(
    SpecForgeApplicationService applicationService,
    string workspaceRoot,
    string? requestedUsId,
    bool preferExplicitSelection,
    string? sidebarVisibility,
    bool showCompletedUserStories,
    bool showBlockedUserStories,
    bool showHiddenUserStories,
    bool includeOtherOwners,
    IReadOnlyList<string> watchingUserStoryIds,
    IReadOnlyList<string> hiddenUserStoryIds)
{
    var normalizedSidebarVisibility = string.Equals(sidebarVisibility, "dropped", StringComparison.OrdinalIgnoreCase)
        ? "dropped"
        : "active";
    var availableStories = normalizedSidebarVisibility == "dropped"
        ? await applicationService.ListUserStoriesAsync(workspaceRoot, "dropped")
        : await applicationService.ListUserStoriesAsync(workspaceRoot);
    var availableStoryById = availableStories.ToDictionary(item => item.UsId, StringComparer.OrdinalIgnoreCase);
    if (!string.IsNullOrWhiteSpace(requestedUsId) && availableStoryById.TryGetValue(requestedUsId.Trim(), out var requestedStory))
    {
        return requestedStory.UsId;
    }

    if (availableStories.Count == 0)
    {
        return null;
    }

    var currentActor = ResolveCurrentGitOwner(workspaceRoot);
    var hiddenSet = new HashSet<string>(hiddenUserStoryIds, StringComparer.OrdinalIgnoreCase);
    var watchingSet = new HashSet<string>(watchingUserStoryIds, StringComparer.OrdinalIgnoreCase);
    var firstVisible = availableStories
        .Where(story => IsWorkflowPortalStoryVisible(
            story,
            currentActor,
            showCompletedUserStories,
            showBlockedUserStories,
            showHiddenUserStories,
            includeOtherOwners,
            watchingSet,
            hiddenSet))
        .OrderBy(story => story.UsId, StringComparer.Ordinal)
        .FirstOrDefault();
    if (firstVisible is not null)
    {
        return firstVisible.UsId;
    }

    if (preferExplicitSelection && !string.IsNullOrWhiteSpace(requestedUsId))
    {
        return null;
    }

    return null;
}

static bool IsWorkflowPortalStoryVisible(
    UserStorySummary story,
    string currentActor,
    bool showCompletedUserStories,
    bool showBlockedUserStories,
    bool showHiddenUserStories,
    bool includeOtherOwners,
    IReadOnlySet<string> watchingUserStoryIds,
    IReadOnlySet<string> hiddenUserStoryIds)
{
    if (!showHiddenUserStories && hiddenUserStoryIds.Contains(story.UsId))
    {
        return false;
    }

    var isBlocked = story.Dependencies.Any(static dependency => !dependency.IsSatisfied);
    if (!showBlockedUserStories && isBlocked)
    {
        return false;
    }

    if (!showCompletedUserStories && string.Equals(story.Status, "completed", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var normalizedOwner = story.Owner.Trim().ToLowerInvariant();
    var normalizedActor = currentActor.Trim().ToLowerInvariant();
    return includeOtherOwners
        || watchingUserStoryIds.Contains(story.UsId)
        || string.Equals(normalizedOwner, normalizedActor, StringComparison.Ordinal);
}

static async Task HandleDropOrRecoverUserStoryAsync(
    HttpListenerContext context,
    string workspaceRoot,
    bool drop)
{
    using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
    var payload = await reader.ReadToEndAsync();
    var request = JsonSerializer.Deserialize<UserStoryVisibilityRequest>(
        payload,
        SpecForgePortalSettingsStore.JsonOptions)
        ?? throw new InvalidOperationException("User story visibility payload could not be parsed.");
    var paths = ResolveUserStoryDirectoryForPortal(workspaceRoot, request.UsId);
    var droppedMarkerPath = Path.Combine(paths.RootDirectory, ".dropped");

    if (drop)
    {
        await File.WriteAllTextAsync(droppedMarkerPath, $"Dropped at {DateTimeOffset.UtcNow:O} by cli-user.{Environment.NewLine}");
    }
    else if (File.Exists(droppedMarkerPath))
    {
        File.Delete(droppedMarkerPath);
    }

    await WriteJsonResponseAsync(context.Response, new { usId = request.UsId, dropped = drop });
}

static async Task HandleResetUserStoryToCaptureAsync(
    HttpListenerContext context,
    SpecForgeApplicationService applicationService,
    string workspaceRoot)
{
    using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
    var payload = await reader.ReadToEndAsync();
    var request = JsonSerializer.Deserialize<UserStoryActionRequest>(
        payload,
        SpecForgePortalSettingsStore.JsonOptions)
        ?? throw new InvalidOperationException("Reset request payload could not be parsed.");

    await WriteJsonResponseAsync(
        context.Response,
        await applicationService.ResetUserStoryToCaptureAsync(workspaceRoot, request.UsId));
}

static async Task HandleUpdateUserStoryInfoAsync(
    HttpListenerContext context,
    SpecForgeApplicationService applicationService,
    string workspaceRoot)
{
    using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
    var payload = await reader.ReadToEndAsync();
    var request = JsonSerializer.Deserialize<UpdateUserStoryInfoRequest>(
        payload,
        SpecForgePortalSettingsStore.JsonOptions)
        ?? throw new InvalidOperationException("Update user story info payload could not be parsed.");

    await WriteJsonResponseAsync(
        context.Response,
        await applicationService.UpdateUserStoryInfoAsync(
            workspaceRoot,
            request.UsId,
            request.Title,
            request.Kind,
            request.Owner,
            request.Category,
            request.Tags,
            request.ExternalReferences,
            request.Actor ?? ResolveCurrentGitOwner(workspaceRoot)));
}

static async Task HandleAnalyzeUserStoryLineageAsync(
    HttpListenerContext context,
    SpecForgeApplicationService applicationService,
    string workspaceRoot)
{
    using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
    var payload = await reader.ReadToEndAsync();
    var request = JsonSerializer.Deserialize<UserStoryActionRequest>(
        payload,
        SpecForgePortalSettingsStore.JsonOptions)
        ?? throw new InvalidOperationException("Lineage analysis payload could not be parsed.");

    await WriteJsonResponseAsync(
        context.Response,
        await applicationService.AnalyzeUserStoryLineageAsync(workspaceRoot, request.UsId));
}

static async Task HandleRepairUserStoryLineageAsync(
    HttpListenerContext context,
    SpecForgeApplicationService applicationService,
    string workspaceRoot)
{
    using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
    var payload = await reader.ReadToEndAsync();
    var request = JsonSerializer.Deserialize<UserStoryActionRequest>(
        payload,
        SpecForgePortalSettingsStore.JsonOptions)
        ?? throw new InvalidOperationException("Lineage repair payload could not be parsed.");

    await WriteJsonResponseAsync(
        context.Response,
        await applicationService.RepairUserStoryLineageAsync(workspaceRoot, request.UsId, request.Actor ?? ResolveCurrentGitOwner(workspaceRoot)));
}

static async Task<object> AttachWorkflowFilesAsync(
    string workspaceRoot,
    string usId,
    AttachWorkflowFilesRequest request)
{
    var normalizedKind = string.Equals(request.Kind, "context", StringComparison.OrdinalIgnoreCase)
        ? "context"
        : "attachment";
    var paths = ResolveUserStoryDirectoryForPortal(workspaceRoot, usId);
    var targetDirectoryPath = Path.Combine(paths.RootDirectory, normalizedKind == "context" ? "context" : "attachments");
    Directory.CreateDirectory(targetDirectoryPath);

    var addedPaths = new List<string>();
    foreach (var file in request.Files)
    {
        var safeName = Path.GetFileName(file.Name?.Trim());
        if (string.IsNullOrWhiteSpace(safeName) || string.IsNullOrWhiteSpace(file.Base64Content))
        {
            continue;
        }

        var bytes = Convert.FromBase64String(file.Base64Content);
        var targetPath = GetNextPortalFilePath(targetDirectoryPath, safeName);
        await File.WriteAllBytesAsync(targetPath, bytes);
        addedPaths.Add(targetPath);
    }

    return new
    {
        usId,
        kind = normalizedKind,
        addedCount = addedPaths.Count,
        addedPaths
    };
}

static async Task HandleCreateUserStoryRequestAsync(
    HttpListenerContext context,
    SpecForgeApplicationService applicationService,
    string workspaceRoot)
{
    var request = await ReadJsonRequestAsync<CreateUserStoryRequest>(
        context.Request,
        "Create user story payload could not be parsed.");

    if (string.IsNullOrWhiteSpace(request.Title)
        || string.IsNullOrWhiteSpace(request.Kind)
        || string.IsNullOrWhiteSpace(request.Category)
        || string.IsNullOrWhiteSpace(request.SourceText))
    {
        throw new InvalidOperationException("Title, kind, category, and source are required.");
    }

    var usId = await ResolveNextPortalUserStoryIdAsync(applicationService, workspaceRoot);
    var result = await applicationService.CreateUserStoryAsync(
        workspaceRoot,
        usId,
        request.Title.Trim(),
        request.Kind.Trim(),
        request.Category.Trim(),
        request.SourceText.Trim(),
        request.Actor ?? ResolveCurrentGitOwner(workspaceRoot),
        request.Tags ?? [],
        request.ExternalReferences ?? []);

    await MaterializeCreateUserStoryFilesAsync(result.RootDirectory, request.Files);

    await WriteJsonResponseAsync(
        context.Response,
        new
        {
            result.UsId,
            result.RootDirectory,
            result.MainArtifactPath
        });
}

static async Task<string> ResolveNextPortalUserStoryIdAsync(
    SpecForgeApplicationService applicationService,
    string workspaceRoot)
{
    var allUserStories = (await applicationService.ListUserStoriesAsync(workspaceRoot))
        .Concat(await applicationService.ListUserStoriesAsync(workspaceRoot, "dropped"));
    var maxId = 0;

    foreach (var summary in allUserStories)
    {
        if (!summary.UsId.StartsWith("US-", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (int.TryParse(summary.UsId[3..], out var numericId) && numericId > maxId)
        {
            maxId = numericId;
        }
    }

    return $"US-{maxId + 1:0000}";
}

static async Task MaterializeCreateUserStoryFilesAsync(
    string userStoryDirectoryPath,
    IReadOnlyList<CreateUserStoryFileUploadItem>? files)
{
    if (files is null || files.Count == 0)
    {
        return;
    }

    foreach (var file in files)
    {
        var safeName = Path.GetFileName(file.Name?.Trim());
        if (string.IsNullOrWhiteSpace(safeName) || string.IsNullOrWhiteSpace(file.Base64Content))
        {
            continue;
        }

        var targetDirectoryPath = Path.Combine(
            userStoryDirectoryPath,
            string.Equals(file.Kind, "context", StringComparison.OrdinalIgnoreCase) ? "context" : "attachments");
        Directory.CreateDirectory(targetDirectoryPath);
        var targetPath = GetNextPortalFilePath(targetDirectoryPath, safeName);
        await File.WriteAllBytesAsync(targetPath, Convert.FromBase64String(file.Base64Content));
    }
}

static void LogWorkflowPortalClientEvent(PortalClientLogRequest request)
{
    var action = string.IsNullOrWhiteSpace(request.Action) ? "unknown" : request.Action.Trim();
    var reason = string.IsNullOrWhiteSpace(request.Reason) ? "unspecified" : request.Reason.Trim();
    var url = string.IsNullOrWhiteSpace(request.Url) ? "(none)" : request.Url.Trim();
    var targetUrl = string.IsNullOrWhiteSpace(request.TargetUrl) ? null : request.TargetUrl.Trim();
    var selectedPhaseId = string.IsNullOrWhiteSpace(request.SelectedPhaseId) ? null : request.SelectedPhaseId.Trim();
    var renderedWorkflowUsId = string.IsNullOrWhiteSpace(request.RenderedWorkflowUsId) ? null : request.RenderedWorkflowUsId.Trim();
    var triggerCommand = string.IsNullOrWhiteSpace(request.TriggerCommand) ? null : request.TriggerCommand.Trim();
    var signature = string.IsNullOrWhiteSpace(request.Signature) ? null : request.Signature.Trim();
    var nextSignature = string.IsNullOrWhiteSpace(request.NextSignature) ? null : request.NextSignature.Trim();
    var detail = string.IsNullOrWhiteSpace(request.Detail) ? null : request.Detail.Trim();
    var timestampUtc = string.IsNullOrWhiteSpace(request.TimestampUtc)
        ? DateTimeOffset.UtcNow.ToString("O")
        : request.TimestampUtc.Trim();

    var payload = JsonSerializer.Serialize(
        new
        {
            action,
            reason,
            url,
            targetUrl,
            selectedPhaseId,
            renderedWorkflowUsId,
            triggerCommand,
            signature,
            nextSignature,
            detail,
            timestampUtc
        },
        SpecForgePortalSettingsStore.JsonOptions);

    Console.WriteLine($"[portal.reload] {payload}");
}

static async Task<object> AddContextFilesAsync(
    string workspaceRoot,
    string usId,
    AddContextFilesRequest request)
{
    var paths = ResolveUserStoryDirectoryForPortal(workspaceRoot, usId);
    var contextDirectoryPath = Path.Combine(paths.RootDirectory, "context");
    Directory.CreateDirectory(contextDirectoryPath);

    var addedPaths = new List<string>();
    foreach (var sourcePath in request.Paths
                 .Where(static item => !string.IsNullOrWhiteSpace(item))
                 .Select(Path.GetFullPath)
                 .Distinct(StringComparer.Ordinal))
    {
        if (!File.Exists(sourcePath))
        {
            continue;
        }

        var targetPath = GetNextPortalFilePath(contextDirectoryPath, Path.GetFileName(sourcePath));
        await CopyFileAsync(sourcePath, targetPath);
        addedPaths.Add(targetPath);
    }

    return new
    {
        usId,
        kind = "context",
        addedCount = addedPaths.Count,
        addedPaths
    };
}

static async Task<object> SaveWorkflowGraphLayoutAsync(
    string workspaceRoot,
    SaveWorkflowGraphLayoutRequest request)
{
    var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "tools", "update-cli-workflow-graph-layout.js");
    if (!File.Exists(scriptPath))
    {
        scriptPath = Path.Combine(AppContext.BaseDirectory, "tools", "update-cli-workflow-graph-layout.js");
    }

    if (!File.Exists(scriptPath))
    {
        throw new InvalidOperationException("Workflow graph layout helper script was not found. Expected tools/update-cli-workflow-graph-layout.js.");
    }

    var payload = JsonSerializer.Serialize(
        new
        {
            workspaceRoot,
            request.LayoutKind,
            request.UserStoryId,
            request.LayoutMode,
            request.DefaultLayoutMode,
            request.Positions,
            request.LegendPosition,
            request.Aggregate
        },
        SpecForgePortalSettingsStore.JsonOptions);

    using var process = new Process();
    process.StartInfo = new ProcessStartInfo
    {
        FileName = "node",
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    process.StartInfo.ArgumentList.Add(scriptPath);
    process.Start();

    await process.StandardInput.WriteAsync(payload);
    process.StandardInput.Close();
    var output = await process.StandardOutput.ReadToEndAsync();
    var error = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"Workflow graph layout update failed: {error}");
    }

    return JsonSerializer.Deserialize<object>(output, SpecForgePortalSettingsStore.JsonOptions)
        ?? new { saved = true };
}

static UserStoryFilePaths ResolveUserStoryDirectoryForPortal(string workspaceRoot, string usId)
{
    var paths = UserStoryFilePaths.ResolveFromWorkspaceRoot(workspaceRoot, usId);
    var storiesRoot = Path.GetFullPath(Path.Combine(workspaceRoot, UserStoryFilePaths.SpecsDirectoryName, UserStoryFilePaths.UserStoriesDirectoryName))
        + Path.DirectorySeparatorChar;
    var targetPath = Path.GetFullPath(paths.RootDirectory);
    if (!targetPath.StartsWith(storiesRoot, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Refusing to update '{usId}' because its path is outside .specs/us.");
    }

    return paths;
}

static async Task CopyFileAsync(string sourcePath, string targetPath)
{
    await using var source = File.OpenRead(sourcePath);
    await using var target = File.Create(targetPath);
    await source.CopyToAsync(target);
}

static string GetNextPortalFilePath(string targetDirectoryPath, string fileName)
{
    var normalizedFileName = Path.GetFileName(fileName);
    if (string.IsNullOrWhiteSpace(normalizedFileName))
    {
        throw new InvalidOperationException("Cannot attach a file without a valid name.");
    }

    var stem = Path.GetFileNameWithoutExtension(normalizedFileName);
    var extension = Path.GetExtension(normalizedFileName);
    var candidatePath = Path.Combine(targetDirectoryPath, normalizedFileName);
    if (!File.Exists(candidatePath))
    {
        return candidatePath;
    }

    for (var index = 2; index < 10_000; index++)
    {
        candidatePath = Path.Combine(targetDirectoryPath, $"{stem}-{index}{extension}");
        if (!File.Exists(candidatePath))
        {
            return candidatePath;
        }
    }

    throw new InvalidOperationException($"Could not allocate a unique target path for '{normalizedFileName}'.");
}

static string? ParseQueryValue(string query, string key)
{
    var trimmedQuery = query.TrimStart('?');
    foreach (var part in trimmedQuery.Split('&', StringSplitOptions.RemoveEmptyEntries))
    {
        var separatorIndex = part.IndexOf('=', StringComparison.Ordinal);
        var name = separatorIndex < 0 ? part : part[..separatorIndex];
        if (!string.Equals(Uri.UnescapeDataString(name), key, StringComparison.Ordinal))
        {
            continue;
        }

        var value = separatorIndex < 0 ? string.Empty : part[(separatorIndex + 1)..];
        return Uri.UnescapeDataString(value.Replace("+", " ", StringComparison.Ordinal));
    }

    return null;
}

static string? GetRuntimeVersion()
{
    return typeof(SpecForgeApplicationService).Assembly.GetName().Version?.ToString();
}

static string BuildConfigurationPortalUrl(string workflowPortalOrigin, string? fragment = null)
{
    if (!Uri.TryCreate(workflowPortalOrigin, UriKind.Absolute, out var uri))
    {
        return string.IsNullOrWhiteSpace(fragment)
            ? "http://localhost:5128/configuration"
            : $"http://localhost:5128/configuration#{fragment}";
    }

    var builder = new UriBuilder(uri)
    {
        Path = "/configuration",
        Query = string.Empty,
        Fragment = string.IsNullOrWhiteSpace(fragment) ? string.Empty : fragment
    };
    return builder.Uri.ToString();
}

static async Task<string> BuildWorkflowPortalSignatureAsync(
    SpecForgeApplicationService applicationService,
    string workspaceRoot,
    string? usId,
    string? sidebarVisibility,
    bool showCompletedUserStories,
    bool showBlockedUserStories)
{
    var activeSidebarUserStories = await applicationService.ListUserStoriesAsync(workspaceRoot);
    var droppedSidebarUserStories = await applicationService.ListUserStoriesAsync(workspaceRoot, "dropped");
    var workflow = string.IsNullOrWhiteSpace(usId)
        ? null
        : await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, usId);
    var workflowGraphLayoutSignature = await ReadWorkflowGraphLayoutSignatureAsync(workspaceRoot);

    return BuildWorkflowSignature(
        workflow,
        activeSidebarUserStories,
        droppedSidebarUserStories,
        workflowGraphLayoutSignature);
}

static string BuildWorkflowPortalRenderCacheSignature(
    string workflowSignature,
    string sidebarVisibility,
    bool showCompletedUserStories,
    bool showBlockedUserStories,
    bool showHiddenUserStories,
    bool includeOtherOwners,
    bool showCreateForm,
    IReadOnlyList<string> watchingUserStoryIds,
    IReadOnlyList<string> hiddenUserStoryIds,
    string currentActor,
    string workflowGraphLayoutMode,
    string workflowGraphInitialZoomMode)
{
    var viewState = JsonSerializer.Serialize(
        new
        {
            sidebarVisibility,
            showCompletedUserStories,
            showBlockedUserStories,
            showHiddenUserStories,
            includeOtherOwners,
            showCreateForm,
            watchingUserStoryIds,
            hiddenUserStoryIds,
            currentActor,
            workflowGraphLayoutMode,
            workflowGraphInitialZoomMode
        },
        SpecForgePortalSettingsStore.JsonOptions);
    return $"{workflowSignature}:{viewState}";
}

static string ResolveCurrentGitOwner(string workspaceRoot) =>
    WorkspaceActorResolver.ResolveForWorkspace(workspaceRoot);

static JsonNode BuildConfigurationSettingsResponse(string workspaceRoot, SpecForgePortalSettings? settings = null)
{
    settings ??= SpecForgePortalSettingsStore.LoadOrDefault(workspaceRoot);
    var executionValidation = settings.ValidateLinkedExecutionConfiguration();
    var detectedGitUser = WorkspaceActorResolver.TryDetectGitUser(workspaceRoot);
    var configuredUser = WorkspaceActorResolver.NormalizeConfiguredUser(settings.DefaultUser);
    var identityError = string.IsNullOrWhiteSpace(configuredUser)
        ? "SpecForge could not determine a workspace user automatically. Configure 'User by default' before using local user-dependent flows."
        : null;
    var gitUserMismatch = !string.IsNullOrWhiteSpace(configuredUser)
        && !string.IsNullOrWhiteSpace(detectedGitUser)
        && !string.Equals(configuredUser, detectedGitUser, StringComparison.OrdinalIgnoreCase);

    var node = JsonSerializer.SerializeToNode(settings, SpecForgePortalSettingsStore.JsonOptions)?.AsObject()
        ?? new JsonObject();
    node["detectedGitUser"] = detectedGitUser;
    node["identityConfigured"] = !string.IsNullOrWhiteSpace(configuredUser);
    node["identityError"] = identityError;
    node["gitUserMismatch"] = gitUserMismatch;
    node["gitUserMismatchWarning"] = gitUserMismatch
        ? $"Configured user '{configuredUser}' differs from detected git user '{detectedGitUser}'. Owner filtering and local workflow actions will use the configured user."
        : null;
    node["workflowExecutionConfigured"] = executionValidation.IsValid;
    node["workflowExecutionError"] = executionValidation.Message;
    return node;
}

static bool TryBuildWorkflowPortalConfigurationRedirect(
    string workspaceRoot,
    string workflowPortalOrigin,
    out string redirectLocation)
{
    var executionValidation = SpecForgePortalSettingsStore.LoadOrDefault(workspaceRoot).ValidateLinkedExecutionConfiguration();
    if (executionValidation.IsValid)
    {
        redirectLocation = string.Empty;
        return false;
    }

    redirectLocation = BuildConfigurationPortalUrl(workflowPortalOrigin, "providers");
    return true;
}

static async Task<string> RenderWorkflowHtmlWithNodeAsync(string payload)
{
    var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "tools", "render-cli-workflow-html.js");
    if (!File.Exists(scriptPath))
    {
        scriptPath = Path.Combine(AppContext.BaseDirectory, "tools", "render-cli-workflow-html.js");
    }

    if (!File.Exists(scriptPath))
    {
        throw new InvalidOperationException("Workflow renderer script was not found. Expected tools/render-cli-workflow-html.js.");
    }

    using var process = new Process();
    process.StartInfo = new ProcessStartInfo
    {
        FileName = "node",
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    process.StartInfo.ArgumentList.Add(scriptPath);
    process.Start();

    await process.StandardInput.WriteAsync(payload);
    process.StandardInput.Close();
    var html = await process.StandardOutput.ReadToEndAsync();
    var error = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"Workflow renderer failed: {error}");
    }

    return html;
}

static string ResolveSelectedWorkflowPhaseId(UserStoryWorkflowDetails workflow, string? selectedPhaseId)
{
    if (!string.IsNullOrWhiteSpace(selectedPhaseId) &&
        workflow.Phases.Any(phase => string.Equals(phase.PhaseId, selectedPhaseId, StringComparison.Ordinal)))
    {
        return selectedPhaseId;
    }

    return workflow.CurrentPhase;
}

static WorkflowPhaseDetails? ResolveSelectedWorkflowPhase(UserStoryWorkflowDetails workflow, string selectedPhaseId)
{
    return workflow.Phases.FirstOrDefault(phase => string.Equals(phase.PhaseId, selectedPhaseId, StringComparison.Ordinal))
        ?? workflow.Phases.FirstOrDefault(phase => phase.IsCurrent)
        ?? workflow.Phases.FirstOrDefault();
}

static async Task<string?> ReadFileContentOrNullAsync(string? path)
{
    if (path is null || !File.Exists(path))
    {
        return null;
    }

    return await File.ReadAllTextAsync(path);
}

static async Task<string> ReadWorkflowGraphLayoutSignatureAsync(string workspaceRoot)
{
    var layoutPath = Path.Combine(workspaceRoot, ".specs", "workflow-graph-layout.yaml");
    if (!File.Exists(layoutPath))
    {
        return string.Empty;
    }

    return await File.ReadAllTextAsync(layoutPath);
}

static string BuildWorkflowSignature(
    UserStoryWorkflowDetails? workflow,
    IReadOnlyCollection<UserStorySummary> userStories,
    IReadOnlyCollection<UserStorySummary> droppedUserStories,
    string workflowGraphLayoutSignature)
{
    var payload = JsonSerializer.Serialize(
        new
        {
            selectedUserStoryId = workflow?.UsId,
            workflowStatus = workflow?.Status,
            workflowCurrentPhase = workflow?.CurrentPhase,
            workflowCreatedWithRuntimeVersion = workflow?.CreatedWithRuntimeVersion,
            workflowLastRuntimeVersion = workflow?.LastRuntimeVersion,
            workflowControls = workflow?.Controls,
            eventCount = workflow?.Events.Count ?? 0,
            latestEvent = workflow?.Events.LastOrDefault(),
            userStories = userStories
                .OrderBy(story => story.UsId, StringComparer.Ordinal)
                .Select(story => new
                {
                    story.UsId,
                    story.Title,
                    story.Description,
                    story.Category,
                    story.CurrentPhase,
                    story.Status,
                    story.WorkBranch
                })
                .ToArray(),
            droppedUserStories = droppedUserStories
                .OrderBy(story => story.UsId, StringComparer.Ordinal)
                .Select(story => new
                {
                    story.UsId,
                    story.Title,
                    story.Description,
                    story.Category,
                    story.CurrentPhase,
                    story.Status,
                    story.WorkBranch
                })
                .ToArray(),
            workflowGraphLayoutSignature
        },
        SpecForgePortalSettingsStore.JsonOptions);
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
    return Convert.ToHexString(hash);
}

static string? ResolveWorkflowPortalNoSelectionReason(string? usId, IReadOnlyCollection<UserStorySummary> sidebarUserStories)
{
    if (!string.IsNullOrWhiteSpace(usId))
    {
        return null;
    }

    return sidebarUserStories.Count == 0
        ? "No user stories exist in this backlog scope yet."
        : "No visible user story matches the current scope. Adjust view options or select a story directly.";
}

static string RequireSelectedWorkflowPortalUserStoryId(string? usId)
{
    if (!string.IsNullOrWhiteSpace(usId))
    {
        return usId;
    }

    throw new InvalidOperationException("No selected user story.");
}

static async Task<string> ResolveDefaultWorkflowPortalUserStoryIdAsync(
    SpecForgeApplicationService applicationService,
    string workspaceRoot)
{
    var stories = await applicationService.ListUserStoriesAsync(workspaceRoot);
    var story = stories
        .OrderBy(static item => item.UsId, StringComparer.Ordinal)
        .FirstOrDefault();

    if (story is null)
    {
        throw new InvalidOperationException("The workspace does not contain any SpecForge user stories to show in the portal.");
    }

    return story.UsId;
}

static void EnsureArgumentCount(IReadOnlyList<string> args, int expectedCount)
{
    if (args.Count != expectedCount)
    {
        throw new InvalidOperationException($"Expected {expectedCount - 1} argument(s) for command '{args[0]}'.");
    }
}

static void WriteJson<T>(T payload)
{
    Console.WriteLine(JsonSerializer.Serialize(payload, SpecForgePortalSettingsStore.JsonOptions));
}

static string NormalizeHttpPrefix(string value)
{
    var prefix = value.Trim();
    if (prefix.Length == 0)
    {
        throw new InvalidOperationException("The portal URL prefix cannot be empty.");
    }

    return prefix.EndsWith("/", StringComparison.Ordinal) ? prefix : $"{prefix}/";
}

static bool LooksLikeHttpPrefix(string value) =>
    value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
    value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

static Task WriteJsonResponseAsync<T>(HttpListenerResponse response, T payload)
{
    var json = JsonSerializer.Serialize(payload, SpecForgePortalSettingsStore.JsonOptions);

    return WriteTextResponseAsync(response, json, "application/json; charset=utf-8");
}

static Task WriteHtmlResponseAsync(HttpListenerResponse response, string html) =>
    WriteTextResponseAsync(response, html, "text/html; charset=utf-8");

static async Task WriteTextResponseAsync(HttpListenerResponse response, string content, string contentType)
{
    response.ContentType = contentType;
    var buffer = Encoding.UTF8.GetBytes(content);
    response.ContentLength64 = buffer.Length;
    await response.OutputStream.WriteAsync(buffer);
    response.OutputStream.Close();
}

static int ExitWithError(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}

static SpecForgeApplicationService CreateApplicationService(IReadOnlyList<string> args)
{
    var workspaceRoot = args.Count > 1 ? args[1] : null;
    return CreateApplicationServiceForWorkspace(workspaceRoot);
}

static SpecForgeApplicationService CreateApplicationServiceForWorkspace(string? workspaceRoot)
{
    var portalSettings = string.IsNullOrWhiteSpace(workspaceRoot)
        ? null
        : SpecForgePortalSettingsStore.LoadOrDefault(workspaceRoot);
    var harnessProfileSettings = CreateHarnessProfileSettings(portalSettings);
    var runner = new WorkflowRunner(
        CreatePhaseExecutionProvider(workspaceRoot),
        refinementTolerance: portalSettings?.RefinementTolerance ?? "balanced",
        maxRefinementCycles: portalSettings?.MaxRefinementCycles ?? 5,
        maxImplementationReviewCycles: portalSettings?.MaxImplementationReviewCycles ?? 5,
        phaseQualityGateThresholdPercent: portalSettings?.PhaseQualityGateThresholdPercent ?? 85,
        refinementQualityGateMaxRetries: portalSettings?.RefinementQualityGateMaxRetries ?? 5,
        reviewQualityGateMaxRetries: portalSettings?.ReviewQualityGateMaxRetries ?? 3,
        keepBestPhaseArtifactOnQualityRegression: portalSettings?.KeepBestPhaseArtifactOnQualityRegression ?? true,
        decompositionOptions: new UserStoryDecompositionOptions(
            Enabled: portalSettings?.DecompositionEnabled ?? true,
            Threshold: portalSettings?.DecompositionThreshold ?? 0.60,
            Tolerance: portalSettings?.DecompositionTolerance ?? 0.10,
            MaxChildren: portalSettings?.DecompositionMaxChildren ?? 5));

    return new SpecForgeApplicationService(new UserStoryFileStore(), runner, harnessProfileSettings: harnessProfileSettings);
}

static HarnessProfileRuntimeSettings CreateHarnessProfileSettings(SpecForgePortalSettings? portalSettings)
{
    return portalSettings is null
        ? HarnessProfileRuntimeSettings.Default
        : new HarnessProfileRuntimeSettings(
            DefaultProfile: portalSettings.DefaultHarnessProfile,
            PhaseProfiles: portalSettings.PhaseHarnessProfiles ?? HarnessProfileRuntimeSettings.Default.PhaseProfiles,
            Governance: new HarnessProfileGovernance(
                portalSettings.HarnessProfileAuthority,
                portalSettings.HarnessProfileLockMode,
                portalSettings.AllowPerUserStoryHarnessProfileOverrides,
                portalSettings.LockedHarnessPhaseIds));
}

static IPhaseExecutionProvider CreatePhaseExecutionProvider(string? workspaceRoot)
{
    var portalSettings = string.IsNullOrWhiteSpace(workspaceRoot)
        ? null
        : SpecForgePortalSettingsStore.Load(workspaceRoot);

    return OpenAiCompatiblePhaseExecutionProviderFactory.Create(key =>
    {
        if (portalSettings is null)
        {
            return null;
        }

        return key switch
        {
            OpenAiCompatiblePhaseExecutionProviderFactory.ModelProfilesJsonEnvVar when portalSettings.ModelProfiles.Count > 0 =>
                JsonSerializer.Serialize(portalSettings.ModelProfiles, SpecForgePortalSettingsStore.JsonOptions),
            OpenAiCompatiblePhaseExecutionProviderFactory.AgentProfilesJsonEnvVar =>
                JsonSerializer.Serialize(portalSettings.ResolveAgentProfiles(), SpecForgePortalSettingsStore.JsonOptions),
            OpenAiCompatiblePhaseExecutionProviderFactory.PhaseAgentAssignmentsJsonEnvVar =>
                JsonSerializer.Serialize(portalSettings.PhaseAgentAssignments, SpecForgePortalSettingsStore.JsonOptions),
            OpenAiCompatiblePhaseExecutionProviderFactory.TechnicalDesignSubagentsEnabledEnvVar =>
                portalSettings.TechnicalDesignSubagentsEnabled.ToString(),
            OpenAiCompatiblePhaseExecutionProviderFactory.ReviewSubagentsEnabledEnvVar =>
                portalSettings.ReviewSubagentsEnabled.ToString(),
            OpenAiCompatiblePhaseExecutionProviderFactory.RefinementToleranceEnvVar => portalSettings.RefinementTolerance,
            OpenAiCompatiblePhaseExecutionProviderFactory.MvpRigorEnvVar => portalSettings.MvpRigor,
            OpenAiCompatiblePhaseExecutionProviderFactory.ReviewToleranceEnvVar => portalSettings.ReviewTolerance,
            OpenAiCompatiblePhaseExecutionProviderFactory.ReviewEvidencePolicyEnvVar => portalSettings.ReviewEvidencePolicy,
            OpenAiCompatiblePhaseExecutionProviderFactory.AutoRefinementAnswersEnabledEnvVar =>
                portalSettings.AutoRefinementAnswersEnabled.ToString(),
            OpenAiCompatiblePhaseExecutionProviderFactory.AutoRefinementAnswersProfileEnvVar =>
                portalSettings.AutoRefinementAnswersProfile,
            OpenAiCompatiblePhaseExecutionProviderFactory.ReviewLearningEnabledEnvVar =>
                portalSettings.ReviewLearningEnabled.ToString(),
            OpenAiCompatiblePhaseExecutionProviderFactory.ReviewLearningSkillPathEnvVar =>
                portalSettings.ReviewLearningSkillPath,
            _ => null
        };
    });
}

static string BuildConfigurationPortalHtml() =>
    """
    <!doctype html>
    <html lang="en">
    <head>
      <meta charset="utf-8">
      <meta name="viewport" content="width=device-width, initial-scale=1">
      <title>SpecForge Configuration</title>
      <style>
        * { box-sizing: border-box; }
        body { margin: 0; font-family: ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; background: #0f1720; color: #e8eef5; }
        main { max-width: 1040px; margin: 0 auto; padding: 28px; }
        h1 { margin: 0 0 8px; font-size: 2rem; }
        h2 { margin: 28px 0 12px; font-size: 1.1rem; color: #b8c7d6; }
        label { display: grid; gap: 6px; color: #b8c7d6; font-size: 0.82rem; font-weight: 700; text-transform: uppercase; }
        .field-label { display: inline-flex; align-items: center; gap: 6px; min-width: 0; }
        .field-control { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: 8px; align-items: center; }
        .field-control .help-button { align-self: center; }
        .help-button { display: inline-grid; place-items: center; width: 18px; height: 18px; padding: 0; border-radius: 50%; background: #26384b; border: 1px solid #43586f; color: #d7e5f2; font-size: 0.72rem; font-weight: 800; line-height: 1; }
        .help-button:hover, .help-button[aria-expanded="true"] { background: #1d4f7a; border-color: #5f8fbd; }
        .help-popover { position: fixed; z-index: 20; max-width: min(320px, calc(100vw - 32px)); padding: 10px 12px; border: 1px solid #43586f; border-radius: 8px; background: #0b121a; color: #d7e5f2; box-shadow: 0 12px 32px rgba(0, 0, 0, 0.42); font-size: 0.82rem; font-weight: 500; line-height: 1.45; text-transform: none; }
        .help-popover[hidden] { display: none; }
        input, select, textarea { width: 100%; border: 1px solid #344456; border-radius: 8px; padding: 10px 12px; background: #111c27; color: #e8eef5; font: inherit; }
        textarea { min-height: 74px; resize: vertical; }
        button { border: 0; border-radius: 8px; padding: 10px 14px; background: #22664a; color: white; font-weight: 700; cursor: pointer; }
        button.secondary { background: #1d4f7a; }
        button.danger { background: #8f2f38; }
        .lead { margin: 0; color: #9fb0c1; }
        .title-row { display: flex; align-items: baseline; gap: 10px; flex-wrap: wrap; }
        .runtime-version { color: #7f8da0; font-size: 0.95rem; font-weight: 600; }
        .section-copy { margin: -4px 0 4px; color: #9fb0c1; line-height: 1.45; max-width: 78ch; }
        .toolbar { display: flex; gap: 10px; flex-wrap: wrap; margin-top: 18px; }
        .tabs { display: flex; gap: 8px; flex-wrap: wrap; margin-top: 22px; border-bottom: 1px solid #253447; }
        .tab-button { border-radius: 8px 8px 0 0; background: transparent; color: #9fb0c1; border: 1px solid transparent; border-bottom: 0; }
        .tab-button[aria-selected="true"] { background: #121d28; color: #e8eef5; border-color: #253447; }
        .tab-panel[hidden] { display: none; }
        .panel { border: 1px solid #253447; border-radius: 8px; padding: 18px; background: #121d28; margin-top: 16px; }
        .grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 14px; }
        .cards { display: grid; gap: 12px; }
        .card { border: 1px solid #2a3a4d; border-radius: 8px; padding: 14px; background: #0f1924; }
        .card-header { display: flex; justify-content: space-between; gap: 12px; align-items: center; margin-bottom: 12px; }
        .card-title { font-weight: 800; }
        .toggles { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; margin-top: 16px; }
        .toggle { display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: center; gap: 14px; min-height: 46px; padding: 10px 12px; border: 1px solid #2a3a4d; border-radius: 8px; background: #0f1924; text-transform: none; cursor: pointer; }
        .toggle input { position: absolute; inline-size: 1px; block-size: 1px; opacity: 0; pointer-events: none; }
        .toggle__label { color: #d5e0eb; font-size: 0.9rem; font-weight: 750; text-transform: none; }
        .toggle__control { display: inline-flex; align-items: center; gap: 8px; }
        .toggle__switch { position: relative; flex: 0 0 auto; width: 46px; height: 26px; border-radius: 999px; background: #334255; border: 1px solid #47576b; transition: background 0.16s ease, border-color 0.16s ease; }
        .toggle__switch::after { content: ""; position: absolute; width: 20px; height: 20px; left: 2px; top: 2px; border-radius: 50%; background: #e8eef5; box-shadow: 0 1px 4px rgba(0, 0, 0, 0.35); transition: transform 0.16s ease; }
        .toggle input:checked + .toggle__switch { background: #22664a; border-color: #3ba66d; }
        .toggle input:checked + .toggle__switch::after { transform: translateX(20px); }
        .toggle input:focus-visible + .toggle__switch { outline: 2px solid #f6d365; outline-offset: 3px; }
        .status { min-height: 24px; margin-top: 14px; color: #f6d365; }
        .notice-stack { display: grid; gap: 10px; margin-bottom: 16px; }
        .notice { border-radius: 8px; padding: 12px 14px; border: 1px solid #43586f; background: #122131; color: #d7e5f2; line-height: 1.45; }
        .notice--error { border-color: #8f2f38; background: #34171c; color: #ffd8dc; }
        .notice--warning { border-color: #8f7230; background: #2e2514; color: #ffe7ad; }
        @media (max-width: 760px) { main { padding: 18px; } .grid, .toggles { grid-template-columns: 1fr; } }
      </style>
    </head>
    <body>
      <main>
        <div class="title-row">
          <h1>SpecForge Configuration</h1>
          <span class="runtime-version">v.__RUNTIME_VERSION__</span>
        </div>
        <p class="lead">Configure the CLI-served workflow runtime for Codex without depending on the Visual Studio configuration surface.</p>
        <form id="settings-form">
          <nav class="tabs" aria-label="Configuration sections">
            <button class="tab-button" type="button" role="tab" aria-controls="providers" aria-selected="true" data-tab-target="providers">Models</button>
            <button class="tab-button" type="button" role="tab" aria-controls="advanced" aria-selected="false" data-tab-target="advanced">Client Basics</button>
            <button class="tab-button" type="button" role="tab" aria-controls="central" aria-selected="false" data-tab-target="central">SpecForge Central</button>
          </nav>
          <div class="tab-panel" id="providers" role="tabpanel">
            <section class="panel">
              <h2>Model Profiles</h2>
              <p class="section-copy">Model profiles describe the available model runtimes: provider type, endpoint credentials when needed, model identifier, reasoning effort, and default repository access.</p>
              <div id="models" class="cards"></div>
              <div class="toolbar"><button type="button" class="secondary" id="add-model">Add Model</button></div>
            </section>
            <section class="panel" id="agent-profiles">
              <h2>Agent Profiles</h2>
              <p class="section-copy">Agent profiles define the workflow roles that use those models, including phase-specific instructions and the repository permissions each role is allowed to use.</p>
              <div id="agents" class="cards"></div>
              <div class="toolbar"><button type="button" class="secondary" id="add-agent">Add Agent</button></div>
            </section>
            <section class="panel" id="routing">
              <h2>Phase Routing</h2>
              <div id="assignments" class="grid"></div>
            </section>
          </div>
          <div class="tab-panel" id="advanced" role="tabpanel" hidden>
            <section class="panel">
              <h2>Workspace Identity</h2>
              <p class="section-copy">This configured user is the local identity source for owner filtering and local workflow actions. SpecForge bootstraps it from git when possible, but it remains a workspace setting once saved.</p>
              <div id="identity-notices" class="notice-stack"></div>
              <div class="grid">
                <label><span class="field-label">User by default</span><span class="field-control"><input id="defaultUser"><button class="help-button" type="button" aria-label="User by default details" aria-expanded="false" data-help="Workspace-level user identity used by local flows such as owner filtering and local workflow actions. If git user detection is unavailable, set this field manually.">?</button></span></label>
                <label><span class="field-label">Detected git user</span><span class="field-control"><input id="detectedGitUser" disabled><button class="help-button" type="button" aria-label="Detected git user details" aria-expanded="false" data-help="Best-effort git identity detected from the workspace. This value is informational and is used only to bootstrap or warn about mismatches.">?</button></span></label>
              </div>
            </section>
            <section class="panel">
              <h2>Workflow Behavior</h2>
              <div class="grid">
                <label><span class="field-label">Workflow graph layout</span><span class="field-control"><select id="workflowGraphLayoutMode"><option value="vertical">Vertical</option><option value="horizontal">Horizontal</option></select><button class="help-button" type="button" aria-label="Workflow graph layout details" aria-expanded="false" data-help="Default graph orientation used when a user story does not have its own saved layout override. Matching per-story choices fall back to this setting instead of being stored separately.">?</button></span></label>
                <label><span class="field-label">Workflow graph initial zoom</span><span class="field-control"><select id="workflowGraphInitialZoomMode"><option value="actual-size">100%</option><option value="fit-width">Fit to width</option></select><button class="help-button" type="button" aria-label="Workflow graph initial zoom details" aria-expanded="false" data-help="Default zoom mode used when opening a workflow graph before any manual zoom interaction.">?</button></span></label>
                <label><span class="field-label">Refinement tolerance</span><span class="field-control"><select id="refinementTolerance"><option>strict</option><option>balanced</option><option>inferential</option></select><button class="help-button" type="button" aria-label="Refinement tolerance details" aria-expanded="false" data-help="Controls how much ambiguity refinement tolerates before spec can continue. Strict asks more questions; inferential allows the model to proceed with more assumptions.">?</button></span></label>
                <label><span class="field-label">MVP rigor</span><span class="field-control"><select id="mvpRigor"><option>low</option><option>medium</option><option>high</option></select><button class="help-button" type="button" aria-label="MVP rigor details" aria-expanded="false" data-help="Controls how much product detail refinement requires before a user story can become a buildable MVP slice. Low is lean; high is exacting.">?</button></span></label>
                <label><span class="field-label">Review tolerance</span><span class="field-control"><select id="reviewTolerance"><option>strict</option><option>balanced</option><option>inferential</option></select><button class="help-button" type="button" aria-label="Review tolerance details" aria-expanded="false" data-help="Controls how demanding review is before it passes or fails delivered work. Strict requires stronger evidence; inferential is more permissive.">?</button></span></label>
                <label><span class="field-label">Review evidence policy</span><span class="field-control"><select id="reviewEvidencePolicy"><option>strict</option><option>balanced</option><option>release</option><option>advisory</option></select><button class="help-button" type="button" aria-label="Review evidence policy details" aria-expanded="false" data-help="Controls how missing automated, static, operational, or deferred validation evidence affects review readiness.">?</button></span></label>
                <label><span class="field-label">Auto-refinement agent</span><span class="field-control"><select id="autoRefinementAnswersProfile"></select><button class="help-button" type="button" aria-label="Auto-refinement agent details" aria-expanded="false" data-help="Agent used to answer refinement questions automatically before the workflow hands the phase back to the user.">?</button></span></label>
                <label><span class="field-label">Review learning skill path</span><span class="field-control"><input id="reviewLearningSkillPath"><button class="help-button" type="button" aria-label="Review learning skill path details" aria-expanded="false" data-help="Workspace-relative skill file where generalized lessons from failed reviews can be persisted.">?</button></span></label>
                <label><span class="field-label">Max refinement cycles</span><span class="field-control"><input id="maxRefinementCycles" type="number" min="1"><button class="help-button" type="button" aria-label="Max refinement cycles details" aria-expanded="false" data-help="Maximum refinement iterations allowed before automatic continuation stops and the workflow waits for the user.">?</button></span></label>
                <label><span class="field-label">Max implementation/review cycles</span><span class="field-control"><input id="maxImplementationReviewCycles" type="number" min="1"><button class="help-button" type="button" aria-label="Max implementation/review cycles details" aria-expanded="false" data-help="Maximum implementation attempts allowed in the implementation/review loop before automatic continuation stops.">?</button></span></label>
                <label><span class="field-label">Phase quality threshold (%)</span><span class="field-control"><input id="phaseQualityGateThresholdPercent" type="number" min="0" max="100" step="1"><button class="help-button" type="button" aria-label="Phase quality threshold details" aria-expanded="false" data-help="Minimum quality score required for refinement and review to pass their quality gate. Values below this threshold keep the workflow in the current phase.">?</button></span></label>
                <label><span class="field-label">Refinement quality max retries</span><span class="field-control"><input id="refinementQualityGateMaxRetries" type="number" min="1"><button class="help-button" type="button" aria-label="Refinement quality max retries details" aria-expanded="false" data-help="Maximum low-quality refinement retries the orchestrator should tolerate before it stops automatic progression and waits for the user.">?</button></span></label>
                <label><span class="field-label">Review quality max retries</span><span class="field-control"><input id="reviewQualityGateMaxRetries" type="number" min="1"><button class="help-button" type="button" aria-label="Review quality max retries details" aria-expanded="false" data-help="Maximum low-quality review retries the orchestrator should tolerate before it stops automatic progression and waits for the user.">?</button></span></label>
                <label><span class="field-label">Decomposition threshold</span><span class="field-control"><input id="decompositionThreshold" type="number" min="0" max="1" step="0.01"><button class="help-button" type="button" aria-label="Decomposition threshold details" aria-expanded="false" data-help="Complexity score at or above this value requires splitting the spec into child user stories. Default is 0.60.">?</button></span></label>
                <label><span class="field-label">Decomposition tolerance</span><span class="field-control"><input id="decompositionTolerance" type="number" min="0" max="1" step="0.01"><button class="help-button" type="button" aria-label="Decomposition tolerance details" aria-expanded="false" data-help="Tolerance below the threshold where SpecForge suggests, but does not require, splitting. With 0.60 and 0.10, suggested starts at 0.50.">?</button></span></label>
                <label><span class="field-label">Max decomposition children</span><span class="field-control"><input id="decompositionMaxChildren" type="number" min="1"><button class="help-button" type="button" aria-label="Max decomposition children details" aria-expanded="false" data-help="Maximum child user stories a decomposition proposal may create.">?</button></span></label>
              </div>
              <div class="toggles" id="toggles"></div>
            </section>
          </div>
          <div class="tab-panel" id="central" role="tabpanel" hidden>
            <section class="panel">
              <h2>SpecForge Central</h2>
              <p class="section-copy">Centralized configuration sync is reserved for the shared SpecForge Central workflow. Local client settings remain editable in the other tabs until central policy management is defined.</p>
            </section>
          </div>
          <div class="toolbar">
            <button type="submit" id="save" disabled>Save Configuration</button>
            <button type="button" class="secondary" id="close">Close</button>
          </div>
          <div id="status" class="status" role="status"></div>
        </form>
      </main>
      <script>
        const phaseFields = [
          ["defaultAgent", "Default / fallback"],
          ["captureAgent", "Capture"],
          ["refinementAgent", "Refinement"],
          ["specAgent", "Spec"],
          ["technicalDesignAgent", "Technical Design"],
          ["implementationAgent", "Implementation"],
          ["reviewAgent", "Review"],
          ["releaseApprovalAgent", "Release Approval"],
          ["prPreparationAgent", "PR Preparation"]
        ];
        const toggleFields = [
          ["technicalDesignSubagentsEnabled", "Technical design subagents"],
          ["reviewSubagentsEnabled", "Review subagents"],
          ["autoRefinementAnswersEnabled", "Auto-refinement answers"],
          ["autoPlayEnabled", "Auto-play workflow"],
          ["autoReviewEnabled", "Auto-review after implementation"],
          ["keepBestPhaseArtifactOnQualityRegression", "Keep best artifact on quality drop"],
          ["destructiveRewindEnabled", "Destructive rewind"],
          ["pauseOnFailedReview", "Pause on failed review"],
          ["useSemanticGraphWhenAvailable", "Use semantic graph when available"],
          ["allowGraphBuildRefreshForTouchedUserStoryScope", "Allow graph build/refresh for touched US scope"],
          ["reviewLearningEnabled", "Review learning"],
          ["completedUsLockOnCompleted", "Lock completed user stories"],
          ["decompositionEnabled", "Complexity decomposition"]
        ];
        const configurationTabs = ["providers", "advanced", "central"];
        const embeddedConfigurationState = window.__specforgeEmbeddedConfigurationState ?? null;
        const helpDescriptions = {
          "model.name": "Stable profile name used by agent routing and phase assignments.",
          "model.provider": "Provider kind for this model profile. Codex, Claude, and Copilot use native/local CLI identity; openai-compatible uses an HTTP endpoint.",
          "model.baseUrl": "Base URL for openai-compatible endpoints. Native CLI providers usually leave this empty.",
          "model.apiKey": "API key for remote openai-compatible endpoints. Local endpoints and native CLI providers can leave it empty.",
          "model.model": "Concrete model identifier for endpoint-based profiles. Native CLI providers can leave this empty to use their local default.",
          "model.reasoningEffort": "Optional reasoning effort override sent to providers that support it.",
          "model.repositoryAccess": "Repository access granted by this model profile when agents are derived directly from models.",
          "defaultUser": "Workspace-level user identity used by local user-dependent flows. This value becomes the local source of truth once configured.",
          "workflowGraphLayoutMode": "Default graph orientation used when a user story does not have its own saved layout override. Matching per-story choices fall back to this setting instead of being stored separately.",
          "workflowGraphInitialZoomMode": "Default zoom mode applied when the workflow graph opens before any manual zoom action.",
          "agent.name": "Stable agent name used by phase routing and auto-refinement settings.",
          "agent.role": "Operational role injected into prompts, such as planner, implementer, reviewer, or release-preparer.",
          "agent.modelProfile": "Model profile this agent runs on.",
          "agent.repositoryAccess": "Repository permissions granted to this agent. Implementation requires read-write; refinement, review, and release-facing agents should stay read-only.",
          "agent.reasoningEffort": "Optional reasoning effort override for this agent.",
          "agent.instructions": "Additional behavior instructions injected into this agent's effective phase prompt.",
          "assignment.defaultAgent": "Fallback agent used when a phase does not declare its own specific agent.",
          "assignment.captureAgent": "Optional agent override for capture.",
          "assignment.refinementAgent": "Agent used to resolve refinement and clarify source intent.",
          "assignment.specAgent": "Agent used to produce and revise the functional spec.",
          "assignment.technicalDesignAgent": "Agent used to produce the technical design.",
          "assignment.implementationAgent": "Agent used to make repository changes. Requires read-write access.",
          "assignment.reviewAgent": "Agent used to inspect implementation and decide review readiness. This should remain read-only.",
          "assignment.releaseApprovalAgent": "Agent used to prepare the release-readiness approval artifact.",
          "assignment.prPreparationAgent": "Agent used to prepare PR handoff content.",
          "technicalDesignSubagentsEnabled": "Runs specialist design subagents before synthesizing the final technical design artifact.",
          "reviewSubagentsEnabled": "Runs specialist review subagents before synthesizing the final review verdict.",
          "autoRefinementAnswersEnabled": "Lets the selected model try to answer pending refinement questions once before handing control back to the user.",
          "autoPlayEnabled": "Automatically resumes workflow playback after manual actions when the next phase can continue.",
          "autoReviewEnabled": "Automatically continues from implementation into review after implementation artifacts are generated or updated.",
          "keepBestPhaseArtifactOnQualityRegression": "If a refinement or review iteration drops below the quality threshold and a better comparable iteration already exists, keep that previous best artifact selected as the current phase artifact.",
          "destructiveRewindEnabled": "When enabled, rewinds and regressions delete later derived artifacts and branch metadata.",
          "pauseOnFailedReview": "Automatically pauses workflow playback when review fails so the developer can inspect the result.",
          "useSemanticGraphWhenAvailable": "Reuses semantic graph artifacts during workflow runtime when they already exist and are compatible.",
          "allowGraphBuildRefreshForTouchedUserStoryScope": "Allows SpecForge to build or refresh the impact graph for the touched user story scope when graph state needs to be materialized.",
          "reviewLearningEnabled": "Allows implementation retries after failed review to persist generalized lessons into local skills or prompt guardrails.",
          "completedUsLockOnCompleted": "Keeps completed user stories locked against direct rewind or artifact modification unless explicitly reopened.",
          "decompositionEnabled": "Evaluates generated specs for complexity and can propose or require child user stories before normal spec approval."
        };
        let state = null;
        let persistedStateSnapshot = "";
        let savingConfiguration = false;

        async function load() {
          const response = await fetch("api/settings");
          state = await response.json();
          applyDefaultSettings();
          normalizeConfigurationReferences();
          persistedStateSnapshot = serializeState(state);
          render();
          scrollToHashSection();
          updateSaveButtonState();
          setStatus("Configuration loaded.");
        }

        function render() {
          renderModels();
          renderAgents();
          renderAssignments();
          renderBehavior();
          renderTabState(resolveActiveTabFromHash());
          updateSaveButtonState();
        }

        function scrollToHashSection() {
          renderTabState(resolveActiveTabFromHash());
        }

        function resolveActiveTabFromHash() {
          const embeddedTab = typeof embeddedConfigurationState?.activeTab === "string"
            ? embeddedConfigurationState.activeTab
            : "";
          if (configurationTabs.includes(embeddedTab)) {
            return embeddedTab;
          }
          const hash = window.location.hash.slice(1);
          return configurationTabs.includes(hash) ? hash : "providers";
        }

        function renderTabState(activeTab) {
          for (const tab of configurationTabs) {
            const selected = tab === activeTab;
            const button = document.querySelector(`[data-tab-target="${tab}"]`);
            const panel = document.getElementById(tab);
            if (button instanceof HTMLButtonElement) {
              button.setAttribute("aria-selected", selected ? "true" : "false");
            }
            if (panel instanceof HTMLElement) {
              panel.hidden = !selected;
            }
          }
        }

        function renderModels() {
          document.getElementById("models").innerHTML = state.modelProfiles.map((profile, index) => `
            <article class="card">
              <div class="card-header"><span class="card-title">${escapeText(profile.name || "Model profile")}</span><button type="button" class="danger" data-remove-model="${index}">Remove</button></div>
              <div class="grid">
                ${input("model", index, "name", "Name", profile.name)}
                ${select("model", index, "provider", "Provider", profile.provider, ["openai-compatible", "codex", "copilot", "claude"])}
                ${input("model", index, "baseUrl", "Base URL", profile.baseUrl)}
                ${input("model", index, "apiKey", "API key", profile.apiKey || "", "password")}
                ${input("model", index, "model", "Model", profile.model)}
                ${select("model", index, "reasoningEffort", "Reasoning effort", profile.reasoningEffort || "", ["", "none", "minimal", "low", "medium", "high", "xhigh"])}
                ${select("model", index, "repositoryAccess", "Repository access", profile.repositoryAccess, ["none", "read", "read-write"])}
              </div>
            </article>`).join("");
        }

        function renderAgents() {
          document.getElementById("agents").innerHTML = state.agentProfiles.map((agent, index) => `
            <article class="card">
              <div class="card-header"><span class="card-title">${escapeText(agent.name || "Agent profile")}</span><button type="button" class="danger" data-remove-agent="${index}">Remove</button></div>
              <div class="grid">
                ${input("agent", index, "name", "Name", agent.name)}
                ${input("agent", index, "role", "Role", agent.role)}
                ${select("agent", index, "modelProfile", "Model profile", agent.modelProfile, state.modelProfiles.map(profile => profile.name))}
                ${select("agent", index, "repositoryAccess", "Repository access", agent.repositoryAccess, ["none", "read", "read-write"])}
                ${select("agent", index, "reasoningEffort", "Reasoning effort", agent.reasoningEffort || "", ["", "none", "minimal", "low", "medium", "high", "xhigh"])}
                <label><span class="field-label">Instructions</span><span class="field-control"><textarea data-kind="agent" data-index="${index}" data-field="instructions">${escapeText(agent.instructions || "")}</textarea>${helpButton("agent.instructions", "Instructions")}</span></label>
              </div>
            </article>`).join("");
        }

        function renderAssignments() {
          const agentNames = ["", ...state.agentProfiles.map(agent => agent.name).filter(Boolean)];
          document.getElementById("assignments").innerHTML = phaseFields.map(([field, label]) =>
            select("assignment", 0, field, label, state.phaseAgentAssignments[field] || "", agentNames)).join("");
        }

        function renderBehavior() {
          for (const id of ["defaultUser", "workflowGraphLayoutMode", "workflowGraphInitialZoomMode", "refinementTolerance", "mvpRigor", "reviewTolerance", "reviewEvidencePolicy", "autoRefinementAnswersProfile", "reviewLearningSkillPath", "maxRefinementCycles", "maxImplementationReviewCycles", "phaseQualityGateThresholdPercent", "refinementQualityGateMaxRetries", "reviewQualityGateMaxRetries", "decompositionThreshold", "decompositionTolerance", "decompositionMaxChildren"]) {
            const element = document.getElementById(id);
            if (!element) continue;
            if (id === "autoRefinementAnswersProfile") {
              element.innerHTML = ["", ...state.agentProfiles.map(agent => agent.name).filter(Boolean)].map(value => `<option value="${escapeAttr(value)}">${escapeText(value || "None")}</option>`).join("");
            }
            element.value = state[id] ?? "";
          }
          const detectedGitUser = document.getElementById("detectedGitUser");
          if (detectedGitUser instanceof HTMLInputElement) {
            detectedGitUser.value = state.detectedGitUser || "";
          }
          document.getElementById("toggles").innerHTML = toggleFields.map(([field, label]) =>
            `<label class="toggle"><span class="toggle__label">${escapeText(label)}</span><span class="toggle__control"><input type="checkbox" data-toggle="${field}" ${state[field] ? "checked" : ""}><span class="toggle__switch" aria-hidden="true"></span>${helpButton(field, label)}</span></label>`).join("");
          renderIdentityNotices();
        }

        function input(kind, index, field, label, value, type = "text") {
          return `<label><span class="field-label">${escapeText(label)}</span><span class="field-control"><input type="${type}" data-kind="${kind}" data-index="${index}" data-field="${field}" value="${escapeAttr(value || "")}">${helpButton(`${kind}.${field}`, label)}</span></label>`;
        }

        function select(kind, index, field, label, value, options) {
          return `<label><span class="field-label">${escapeText(label)}</span><span class="field-control"><select data-kind="${kind}" data-index="${index}" data-field="${field}">${options.map(option => `<option value="${escapeAttr(option)}" ${option === value ? "selected" : ""}>${escapeText(option || "None")}</option>`).join("")}</select>${helpButton(`${kind}.${field}`, label)}</span></label>`;
        }

        function helpButton(helpKey, label) {
          const helpText = helpDescriptions[helpKey];
          if (!helpText) {
            return "";
          }

          return `<button class="help-button" type="button" aria-label="${escapeAttr(label)} details" aria-expanded="false" data-help="${escapeAttr(helpText)}">?</button>`;
        }

        function sync() {
          if (!state) {
            return;
          }

          state.phaseAgentAssignments ||= {};
          document.querySelectorAll("[data-kind]").forEach(element => {
            const kind = element.dataset.kind;
            const index = Number(element.dataset.index);
            const field = element.dataset.field;
            if (kind === "model" && state.modelProfiles[index]) state.modelProfiles[index][field] = element.value;
            if (kind === "agent" && state.agentProfiles[index]) state.agentProfiles[index][field] = element.value;
            if (kind === "assignment") state.phaseAgentAssignments[field] = element.value || null;
          });
          for (const id of ["defaultUser", "workflowGraphLayoutMode", "workflowGraphInitialZoomMode", "refinementTolerance", "mvpRigor", "reviewTolerance", "reviewEvidencePolicy", "autoRefinementAnswersProfile", "reviewLearningSkillPath"]) {
            const element = document.getElementById(id);
            if (element) state[id] = element.value || null;
          }
          state.maxRefinementCycles = Number(document.getElementById("maxRefinementCycles")?.value) || 5;
          state.maxImplementationReviewCycles = Number(document.getElementById("maxImplementationReviewCycles")?.value) || 5;
          state.phaseQualityGateThresholdPercent = Number(document.getElementById("phaseQualityGateThresholdPercent")?.value);
          if (!Number.isFinite(state.phaseQualityGateThresholdPercent)) state.phaseQualityGateThresholdPercent = 85;
          state.phaseQualityGateThresholdPercent = Math.max(0, Math.min(100, Math.round(state.phaseQualityGateThresholdPercent)));
          state.refinementQualityGateMaxRetries = Number(document.getElementById("refinementQualityGateMaxRetries")?.value) || 5;
          state.reviewQualityGateMaxRetries = Number(document.getElementById("reviewQualityGateMaxRetries")?.value) || 3;
          state.decompositionThreshold = Number(document.getElementById("decompositionThreshold")?.value) || 0.60;
          state.decompositionTolerance = Number(document.getElementById("decompositionTolerance")?.value) || 0.10;
          state.decompositionMaxChildren = Number(document.getElementById("decompositionMaxChildren")?.value) || 5;
          document.querySelectorAll("[data-toggle]").forEach(element => state[element.dataset.toggle] = element.checked);
          normalizeConfigurationReferences();
        }

        function syncField(element) {
          if (!state || !element.dataset) {
            return false;
          }

          const kind = element.dataset.kind;
          const index = Number(element.dataset.index);
          const field = element.dataset.field;
          if (kind === "model" && state.modelProfiles[index]) {
            state.modelProfiles[index][field] = element.value;
            return field === "name";
          }
          if (kind === "agent" && state.agentProfiles[index]) {
            state.agentProfiles[index][field] = element.value;
            return field === "name";
          }
          if (kind === "assignment") {
            state.phaseAgentAssignments ||= {};
            state.phaseAgentAssignments[field] = element.value || null;
          }

          return false;
        }

        function updateDependentSelectOptions() {
          if (!state) {
            return;
          }

          const modelNames = state.modelProfiles.map(model => model.name).filter(Boolean);
          document.querySelectorAll('select[data-kind="agent"][data-field="modelProfile"]').forEach(selectElement => {
            updateSelectOptions(selectElement, modelNames, selectElement.value || modelNames[0] || "");
          });

          const agentNames = ["", ...state.agentProfiles.map(agent => agent.name).filter(Boolean)];
          document.querySelectorAll('select[data-kind="assignment"]').forEach(selectElement => {
            updateSelectOptions(selectElement, agentNames, selectElement.value || "");
          });

          const autoRefinementAnswersProfile = document.getElementById("autoRefinementAnswersProfile");
          if (autoRefinementAnswersProfile instanceof HTMLSelectElement) {
            updateSelectOptions(autoRefinementAnswersProfile, agentNames, autoRefinementAnswersProfile.value || "");
          }
        }

        function updateSelectOptions(selectElement, options, selectedValue) {
          const normalizedSelectedValue = options.includes(selectedValue) ? selectedValue : "";
          selectElement.innerHTML = options
            .map(option => `<option value="${escapeAttr(option)}" ${option === normalizedSelectedValue ? "selected" : ""}>${escapeText(option || "None")}</option>`)
            .join("");
        }

        function serializeState(value) {
          return JSON.stringify(value ?? null);
        }

        function updateSaveButtonState() {
          const saveButton = document.getElementById("save");
          if (!(saveButton instanceof HTMLButtonElement) || !state) {
            return;
          }

          sync();
          normalizeConfigurationReferences();
          const isDirty = serializeState(state) !== persistedStateSnapshot;
          saveButton.disabled = savingConfiguration || !isDirty || !isIdentityConfigurationValid();
          renderIdentityNotices();
        }

        function isIdentityConfigurationValid() {
          return Boolean(String(state?.defaultUser || "").trim());
        }

        function renderIdentityNotices() {
          const container = document.getElementById("identity-notices");
          if (!(container instanceof HTMLElement) || !state) {
            return;
          }

          const notices = [];
          const configuredUser = String(state.defaultUser || "").trim();
          const persistedUser = String(JSON.parse(persistedStateSnapshot || "null")?.defaultUser || "").trim();

          if (state.identityError && configuredUser.length === 0) {
            notices.push(`<div class="notice notice--error">${escapeText(state.identityError)}</div>`);
          }

          if (state.gitUserMismatchWarning && configuredUser.length > 0) {
            notices.push(`<div class="notice notice--warning">${escapeText(state.gitUserMismatchWarning)}</div>`);
          }

          if (persistedUser.length > 0 && configuredUser.length > 0 && persistedUser !== configuredUser) {
            notices.push(`<div class="notice notice--warning">Changing the configured workspace user does not rewrite existing user stories globally. Existing <code>Created By</code> and <code>Owner</code> values remain unchanged and may become inconsistent with the new identity.</div>`);
          }

          container.innerHTML = notices.join("");
        }

        function requestConfigurationClose() {
          try {
            if (typeof window.__specforgeCloseConfiguration === "function") {
              window.__specforgeCloseConfiguration();
              return;
            }
            window.parent?.postMessage({
              source: "specforge-cli-configuration",
              message: { command: "closeConfiguration" }
            }, "*");
          } catch {}
        }

        document.addEventListener("click", event => {
          const target = event.target;
          if (!(target instanceof HTMLElement)) return;
          if (target.dataset.tabTarget) {
            event.preventDefault();
            closeHelpPopover();
            sync();
            if (embeddedConfigurationState) {
              embeddedConfigurationState.activeTab = target.dataset.tabTarget;
              renderTabState(target.dataset.tabTarget);
            } else {
              window.location.hash = target.dataset.tabTarget;
              renderTabState(target.dataset.tabTarget);
            }
            return;
          }

          if (target.classList.contains("help-button")) {
            event.preventDefault();
            event.stopPropagation();
            toggleHelpPopover(target);
            return;
          }

          closeHelpPopover();
          sync();
          if (target.id === "add-model") {
            state.modelProfiles.push({ name: "", provider: "codex", baseUrl: "", apiKey: "", model: "", reasoningEffort: "", repositoryAccess: "none" });
            render();
            return;
          }
          if (target.id === "add-agent") {
            state.agentProfiles.push({ name: "", role: "", modelProfile: state.modelProfiles[0]?.name || "", instructions: "", repositoryAccess: "none", reasoningEffort: "" });
            render();
            return;
          }
          if (target.id === "close") {
            requestConfigurationClose();
            return;
          }
          if (target.dataset.removeModel) {
            state.modelProfiles.splice(Number(target.dataset.removeModel), 1);
            normalizeConfigurationReferences();
            render();
            return;
          }
          if (target.dataset.removeAgent) {
            state.agentProfiles.splice(Number(target.dataset.removeAgent), 1);
            normalizeConfigurationReferences();
            render();
          }
        });

        if (!embeddedConfigurationState) {
          window.addEventListener("hashchange", () => {
            renderTabState(resolveActiveTabFromHash());
          });
        }

        document.addEventListener("keydown", event => {
          if (event.key === "Escape") {
            closeHelpPopover();
          }
        });

        document.addEventListener("input", event => {
          const target = event.target;
          if (target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement) {
            const refreshNeeded = syncField(target);
            if (refreshNeeded) {
              updateDependentSelectOptions();
            }
            updateSaveButtonState();
          }
        });

        document.addEventListener("change", event => {
          const target = event.target;
          if (target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement || target instanceof HTMLSelectElement) {
            const refreshNeeded = syncField(target);
            if (refreshNeeded) {
              updateDependentSelectOptions();
            }
            updateSaveButtonState();
          }
        });

        document.getElementById("settings-form").addEventListener("submit", async event => {
          event.preventDefault();
          if (savingConfiguration) {
            return;
          }
          sync();
          normalizeConfigurationReferences();
          const persistedUser = String(JSON.parse(persistedStateSnapshot || "null")?.defaultUser || "").trim();
          const configuredUser = String(state.defaultUser || "").trim();
          if (persistedUser.length > 0 && configuredUser.length > 0 && persistedUser !== configuredUser) {
            const confirmed = window.confirm("Changing 'User by default' does not rewrite existing user stories globally and may create inconsistency with already persisted Created By / Owner values. Save anyway?");
            if (!confirmed) {
              updateSaveButtonState();
              return;
            }
          }
          if (serializeState(state) === persistedStateSnapshot) {
            updateSaveButtonState();
            return;
          }
          savingConfiguration = true;
          updateSaveButtonState();
          const response = await fetch("api/settings", { method: "PUT", headers: { "content-type": "application/json" }, body: JSON.stringify(state) });
          if (!response.ok) {
            savingConfiguration = false;
            updateSaveButtonState();
            setStatus(await response.text());
            return;
          }
          state = await response.json();
          applyDefaultSettings();
          normalizeConfigurationReferences();
          persistedStateSnapshot = serializeState(state);
          savingConfiguration = false;
          render();
          setStatus("Configuration saved.");
        });

        function applyDefaultSettings() {
          if (!state) {
            return;
          }

          if (typeof state.reviewLearningEnabled !== "boolean") {
            state.reviewLearningEnabled = true;
          }

          if (typeof state.pauseOnFailedReview !== "boolean") {
            state.pauseOnFailedReview = true;
          }
          if (!Number.isFinite(state.phaseQualityGateThresholdPercent)) {
            state.phaseQualityGateThresholdPercent = 85;
          }
          state.phaseQualityGateThresholdPercent = Math.max(0, Math.min(100, Math.round(state.phaseQualityGateThresholdPercent)));
          if (!Number.isFinite(state.refinementQualityGateMaxRetries) || state.refinementQualityGateMaxRetries <= 0) {
            state.refinementQualityGateMaxRetries = 5;
          }
          if (!Number.isFinite(state.reviewQualityGateMaxRetries) || state.reviewQualityGateMaxRetries <= 0) {
            state.reviewQualityGateMaxRetries = 3;
          }
          if (typeof state.keepBestPhaseArtifactOnQualityRegression !== "boolean") {
            state.keepBestPhaseArtifactOnQualityRegression = true;
          }

          if (typeof state.useSemanticGraphWhenAvailable !== "boolean") {
            state.useSemanticGraphWhenAvailable = true;
          }

          if (typeof state.allowGraphBuildRefreshForTouchedUserStoryScope !== "boolean") {
            state.allowGraphBuildRefreshForTouchedUserStoryScope = false;
          }

          if (typeof state.reviewSubagentsEnabled !== "boolean") {
            state.reviewSubagentsEnabled = true;
          }

          if (typeof state.autoPlayEnabled !== "boolean") {
            state.autoPlayEnabled = true;
          }

          if (typeof state.autoReviewEnabled !== "boolean") {
            state.autoReviewEnabled = true;
          }

          if (typeof state.decompositionEnabled !== "boolean") {
            state.decompositionEnabled = true;
          }

          state.decompositionThreshold ||= 0.60;
          state.decompositionTolerance ??= 0.10;
          state.decompositionMaxChildren ||= 5;
        }

        function normalizeConfigurationReferences() {
          if (!state) {
            return;
          }

          const modelNames = new Set(state.modelProfiles.map(model => model.name).filter(Boolean));
          for (const agent of state.agentProfiles) {
            if (agent.modelProfile && !modelNames.has(agent.modelProfile)) {
              agent.modelProfile = "";
            }
          }

          const agentNames = new Set(state.agentProfiles.map(agent => agent.name).filter(Boolean));
          state.phaseAgentAssignments ||= {};
          for (const key of Object.keys(state.phaseAgentAssignments)) {
            if (state.phaseAgentAssignments[key] && !agentNames.has(state.phaseAgentAssignments[key])) {
              state.phaseAgentAssignments[key] = null;
            }
          }

          if (state.autoRefinementAnswersProfile && !agentNames.has(state.autoRefinementAnswersProfile)) {
            state.autoRefinementAnswersProfile = null;
          }

          if (!state.autoRefinementAnswersProfile) {
            state.autoRefinementAnswersEnabled = false;
          }
        }

        function toggleHelpPopover(button) {
          const existing = document.querySelector(".help-popover");
          if (existing && existing.dataset.owner === button.dataset.help) {
            closeHelpPopover();
            return;
          }

          closeHelpPopover();
          const popover = document.createElement("div");
          popover.className = "help-popover";
          popover.dataset.owner = button.dataset.help || "";
          popover.textContent = button.dataset.help || "";
          document.body.appendChild(popover);
          const buttonBounds = button.getBoundingClientRect();
          const popoverBounds = popover.getBoundingClientRect();
          const left = Math.min(
            Math.max(16, buttonBounds.left),
            window.innerWidth - popoverBounds.width - 16);
          const top = Math.min(
            buttonBounds.bottom + 8,
            window.innerHeight - popoverBounds.height - 16);
          popover.style.left = `${left}px`;
          popover.style.top = `${Math.max(16, top)}px`;
          button.setAttribute("aria-expanded", "true");
        }

        function closeHelpPopover() {
          document.querySelectorAll(".help-button[aria-expanded='true']").forEach(button => {
            button.setAttribute("aria-expanded", "false");
          });
          document.querySelector(".help-popover")?.remove();
        }

        function setStatus(message) { document.getElementById("status").textContent = message; }
        function escapeText(value) { return String(value ?? "").replace(/[&<>]/g, character => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;" }[character])); }
        function escapeAttr(value) { return escapeText(value).replace(/"/g, "&quot;"); }
        load();
        window.addEventListener("hashchange", scrollToHashSection);
      </script>
    </body>
    </html>
    """.Replace("__RUNTIME_VERSION__", WebUtility.HtmlEncode(GetRuntimeVersion() ?? "unknown"), StringComparison.Ordinal);

internal sealed record ApprovalAnswerSuggestionRequest(string Question, string? Actor);

internal sealed record ApprovalAnswerSubmitRequest(string Question, string Answer, string? Actor);

internal sealed record RefinementAnswersSubmitRequest(IReadOnlyList<string> Answers, string? Actor);

internal sealed record WorkflowFileUploadItem(string Name, string Base64Content);

internal sealed record CreateUserStoryFileUploadItem(string Name, string Kind, string Base64Content);

internal sealed record CreateUserStoryFileDraftItem(string SourcePath, string Name, string Kind);

internal sealed record CreateUserStoryRequest(
    string Title,
    string Kind,
    string Category,
    string SourceText,
    IReadOnlyList<string>? Tags,
    IReadOnlyList<UserStoryExternalReference>? ExternalReferences,
    string? Actor,
    IReadOnlyList<CreateUserStoryFileUploadItem>? Files);

internal sealed record CreateUserStoryFormRenderRequest(
    string? CreateFileMode,
    IReadOnlyList<CreateUserStoryFileDraftItem>? CreateFiles,
    int CreateFormResetToken);

internal sealed record AttachWorkflowFilesRequest(string Kind, IReadOnlyList<WorkflowFileUploadItem> Files, string? Actor);

internal sealed record AddContextFilesRequest(IReadOnlyList<string> Paths, string? Actor);

internal sealed record SaveWorkflowGraphLayoutRequest(
    string? LayoutKind,
    string? UserStoryId,
    string? LayoutMode,
    string? DefaultLayoutMode,
    Dictionary<string, WorkflowGraphLayoutPoint>? Positions,
    WorkflowGraphLayoutPoint? LegendPosition,
    WorkflowAggregateGraphLayoutRequest? Aggregate);

internal sealed record PortalClientLogRequest(
    string? Action,
    string? Reason,
    string? Url,
    string? TargetUrl,
    string? SelectedPhaseId,
    string? RenderedWorkflowUsId,
    string? TriggerCommand,
    string? Signature,
    string? NextSignature,
    string? Detail,
    string? TimestampUtc);

internal sealed record WorkflowGraphLayoutPoint(int X, int Y);

internal sealed record WorkflowAggregateGraphLayoutRequest(
    Dictionary<string, WorkflowGraphLayoutPoint> Positions,
    Dictionary<string, int> Spacing);

internal sealed record ApprovalSubmitRequest(string? BaseBranch, string? WorkBranch, string? Actor);

internal sealed record DecompositionApprovalSubmitRequest(string Decision, string? Actor);

internal sealed record UserStoryActionRequest(string UsId, string? Actor);

internal sealed record UserStoryVisibilityRequest(string UsId);

internal sealed record UpdateUserStoryInfoRequest(
    string UsId,
    string? Title,
    string? Kind,
    string? Owner,
    string? Category,
    IReadOnlyList<string>? Tags,
    IReadOnlyList<UserStoryExternalReference>? ExternalReferences,
    string? Actor);
