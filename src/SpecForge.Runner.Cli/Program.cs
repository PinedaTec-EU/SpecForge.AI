using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    var result = await applicationService.CreateUserStoryAsync(workspaceRoot, usId, title, kind, category, sourceText, "cli-user");

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
    var result = await applicationService.ImportUserStoryAsync(workspaceRoot, usId, sourcePath, title, kind, category, "cli-user");

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
    var result = await applicationService.GenerateNextPhaseAsync(workspaceRoot, usId, "cli-user");

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
        "cli-user");
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
    var runner = new WorkflowRunner(CreatePhaseExecutionProvider(workspaceRoot));
    var portalSettings = SpecForgePortalSettingsStore.LoadOrDefault(workspaceRoot);
    var harnessProfileSettings = new HarnessProfileRuntimeSettings(
        DefaultProfile: portalSettings.DefaultHarnessProfile,
        PhaseProfiles: portalSettings.PhaseHarnessProfiles ?? HarnessProfileRuntimeSettings.Default.PhaseProfiles,
        Governance: new HarnessProfileGovernance(
            portalSettings.HarnessProfileAuthority,
            portalSettings.HarnessProfileLockMode,
            portalSettings.AllowPerUserStoryHarnessProfileOverrides,
            portalSettings.LockedHarnessPhaseIds));
    var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), runner, harnessProfileSettings: harnessProfileSettings);
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

        var requestUsId = ResolveWorkflowPortalUserStoryId(context.Request, usId);
        var requestSidebarVisibility = ResolveWorkflowPortalSidebarVisibility(context.Request);
        var requestShowCompletedUserStories = string.Equals(
            context.Request.QueryString["sidebarCompleted"],
            "true",
            StringComparison.OrdinalIgnoreCase);
        var requestShowBlockedUserStories = string.Equals(
            context.Request.QueryString["sidebarBlocked"],
            "true",
            StringComparison.OrdinalIgnoreCase);

        switch ((context.Request.HttpMethod, path))
        {
            case ("GET", "/"):
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
                        context.Request.Url?.GetLeftPart(UriPartial.Authority) ?? "http://localhost:5128",
                        renderCache));
                return;
            case ("GET", "/api/workflow"):
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
                await WriteJsonResponseAsync(context.Response, await applicationService.GetUserStoryRuntimeStatusAsync(workspaceRoot, requestUsId));
                return;
            case ("GET", "/configuration"):
                await WriteHtmlResponseAsync(context.Response, BuildConfigurationPortalHtml());
                return;
            case ("GET", "/api/settings"):
                await WriteJsonResponseAsync(context.Response, SpecForgePortalSettingsStore.LoadOrDefault(workspaceRoot));
                return;
            case ("PUT", "/api/settings"):
            {
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                var payload = await reader.ReadToEndAsync();
                var settings = SpecForgePortalSettingsStore.Deserialize(payload);
                SpecForgePortalSettingsStore.Save(workspaceRoot, settings);
                await WriteJsonResponseAsync(context.Response, settings);
                return;
            }
            case ("GET", "/api/summary"):
                await WriteJsonResponseAsync(context.Response, await applicationService.GetUserStorySummaryAsync(workspaceRoot, requestUsId));
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
            case ("POST", "/api/continue"):
                await WriteJsonResponseAsync(
                    context.Response,
                    await applicationService.GenerateNextPhaseAsync(workspaceRoot, requestUsId, "cli-user"));
                return;
            case ("POST", "/api/approval-answer"):
            {
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                var payload = await reader.ReadToEndAsync();
                var request = JsonSerializer.Deserialize<ApprovalAnswerSubmitRequest>(
                    payload,
                    SpecForgePortalSettingsStore.JsonOptions)
                    ?? throw new InvalidOperationException("Approval answer payload could not be parsed.");
                await WriteJsonResponseAsync(
                    context.Response,
                    await applicationService.SubmitApprovalAnswerAsync(
                        workspaceRoot,
                        requestUsId,
                        request.Question,
                        request.Answer,
                        request.Actor ?? "cli-user"));
                return;
            }
            case ("POST", "/api/refinement-answers"):
            {
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                var payload = await reader.ReadToEndAsync();
                var request = JsonSerializer.Deserialize<RefinementAnswersSubmitRequest>(
                    payload,
                    SpecForgePortalSettingsStore.JsonOptions)
                    ?? throw new InvalidOperationException("Refinement answers payload could not be parsed.");
                await WriteJsonResponseAsync(
                    context.Response,
                    await applicationService.SubmitRefinementAnswersAsync(
                        workspaceRoot,
                        requestUsId,
                        request.Answers,
                        request.Actor ?? "cli-user"));
                return;
            }
            case ("POST", "/api/attach-files"):
            {
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                var payload = await reader.ReadToEndAsync();
                var request = JsonSerializer.Deserialize<AttachWorkflowFilesRequest>(
                    payload,
                    SpecForgePortalSettingsStore.JsonOptions)
                    ?? throw new InvalidOperationException("Attach files payload could not be parsed.");
                await WriteJsonResponseAsync(
                    context.Response,
                    await AttachWorkflowFilesAsync(workspaceRoot, requestUsId, request));
                return;
            }
            case ("POST", "/api/add-context-files"):
            {
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                var payload = await reader.ReadToEndAsync();
                var request = JsonSerializer.Deserialize<AddContextFilesRequest>(
                    payload,
                    SpecForgePortalSettingsStore.JsonOptions)
                    ?? throw new InvalidOperationException("Add context files payload could not be parsed.");
                await WriteJsonResponseAsync(
                    context.Response,
                    await AddContextFilesAsync(workspaceRoot, requestUsId, request));
                return;
            }
            case ("POST", "/api/workflow-graph-layout"):
            {
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                var payload = await reader.ReadToEndAsync();
                var request = JsonSerializer.Deserialize<SaveWorkflowGraphLayoutRequest>(
                    payload,
                    SpecForgePortalSettingsStore.JsonOptions)
                    ?? throw new InvalidOperationException("Workflow graph layout payload could not be parsed.");
                await WriteJsonResponseAsync(
                    context.Response,
                    await SaveWorkflowGraphLayoutAsync(workspaceRoot, request));
                return;
            }
            case ("POST", "/api/approve"):
            {
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                var payload = await reader.ReadToEndAsync();
                var request = JsonSerializer.Deserialize<ApprovalSubmitRequest>(
                    payload,
                    SpecForgePortalSettingsStore.JsonOptions)
                    ?? throw new InvalidOperationException("Approval payload could not be parsed.");
                await WriteJsonResponseAsync(
                    context.Response,
                    await applicationService.ApprovePhaseAsync(
                        workspaceRoot,
                        requestUsId,
                        request.BaseBranch,
                        request.WorkBranch,
                        request.Actor ?? "cli-user"));
                return;
            }
            case ("POST", "/api/decomposition-approval"):
            {
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                var payload = await reader.ReadToEndAsync();
                var request = JsonSerializer.Deserialize<DecompositionApprovalSubmitRequest>(
                    payload,
                    SpecForgePortalSettingsStore.JsonOptions)
                    ?? throw new InvalidOperationException("Decomposition approval payload could not be parsed.");
                await WriteJsonResponseAsync(
                    context.Response,
                    string.Equals(request.Decision, "approve", StringComparison.OrdinalIgnoreCase)
                        ? await applicationService.ApproveDecompositionAsync(workspaceRoot, requestUsId, request.Actor ?? "cli-user")
                        : await applicationService.RejectDecompositionAsync(workspaceRoot, requestUsId, request.Actor ?? "cli-user"));
                return;
            }
            case ("POST", "/api/suggest-approval-answer"):
            {
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                var payload = await reader.ReadToEndAsync();
                var request = JsonSerializer.Deserialize<ApprovalAnswerSuggestionRequest>(
                    payload,
                    SpecForgePortalSettingsStore.JsonOptions)
                    ?? throw new InvalidOperationException("Suggestion payload could not be parsed.");
                await WriteJsonResponseAsync(
                    context.Response,
                    await applicationService.SuggestApprovalAnswerAsync(workspaceRoot, requestUsId, request.Question, request.Actor ?? "user"));
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
    string usId,
    string? selectedPhaseId,
    string? sidebarVisibility,
    bool showCompletedUserStories,
    bool showBlockedUserStories,
    string workflowPortalOrigin,
    WorkflowPortalRenderCache renderCache)
{
    var normalizedSidebarVisibility = string.Equals(sidebarVisibility, "dropped", StringComparison.OrdinalIgnoreCase)
        ? "dropped"
        : "active";
    var activeSidebarUserStories = await applicationService.ListUserStoriesAsync(workspaceRoot);
    var droppedSidebarUserStories = await applicationService.ListUserStoriesAsync(workspaceRoot, "dropped");
    var sidebarUserStories = normalizedSidebarVisibility == "dropped"
        ? droppedSidebarUserStories
        : activeSidebarUserStories;
    var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, usId);
    var resolvedSelectedPhaseId = ResolveSelectedWorkflowPhaseId(workflow, selectedPhaseId);
    var selectedPhase = ResolveSelectedWorkflowPhase(workflow, resolvedSelectedPhaseId);
    var droppedUserStoryCount = droppedSidebarUserStories.Count;
    var workflowGraphLayoutSignature = await ReadWorkflowGraphLayoutSignatureAsync(workspaceRoot);
    var signature = BuildWorkflowSignature(
        workflow,
        activeSidebarUserStories,
        droppedSidebarUserStories,
        workflowGraphLayoutSignature);
    if (renderCache.TryGet(signature, resolvedSelectedPhaseId, selectedPhase, out var cachedHtml))
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
            runtimeVersion = GetRuntimeVersion() ?? workflow.LastRuntimeVersion ?? workflow.CreatedWithRuntimeVersion,
            userStories = activeSidebarUserStories,
            sidebarUserStories,
            activeSidebarUserStories,
            droppedSidebarUserStories,
            showDroppedUserStories = normalizedSidebarVisibility == "dropped",
            showCompletedUserStories,
            showBlockedUserStories,
            droppedUserStoryCount,
            configurationPortalUrl = BuildConfigurationPortalUrl(workflowPortalOrigin),
            configurationProvidersUrl = BuildConfigurationPortalUrl(workflowPortalOrigin, "providers"),
            configurationAdvancedUrl = BuildConfigurationPortalUrl(workflowPortalOrigin, "advanced"),
            workspaceRoot,
            signature
        },
        SpecForgePortalSettingsStore.JsonOptions);

    var html = await RenderWorkflowHtmlWithNodeAsync(payload);
    renderCache.Store(signature, resolvedSelectedPhaseId, selectedPhase, html);
    return html;
}

static string ResolveWorkflowPortalUserStoryId(HttpListenerRequest request, string fallbackUsId)
{
    var queryUsId = request.QueryString["usId"];
    if (!string.IsNullOrWhiteSpace(queryUsId))
    {
        return queryUsId;
    }

    var referer = request.UrlReferrer;
    var refererUsId = referer is null ? null : ParseQueryValue(referer.Query, "usId");
    return string.IsNullOrWhiteSpace(refererUsId) ? fallbackUsId : refererUsId;
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
    string usId,
    string? sidebarVisibility,
    bool showCompletedUserStories,
    bool showBlockedUserStories)
{
    var activeSidebarUserStories = await applicationService.ListUserStoriesAsync(workspaceRoot);
    var droppedSidebarUserStories = await applicationService.ListUserStoriesAsync(workspaceRoot, "dropped");
    var workflow = await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, usId);
    var workflowGraphLayoutSignature = await ReadWorkflowGraphLayoutSignatureAsync(workspaceRoot);

    return BuildWorkflowSignature(
        workflow,
        activeSidebarUserStories,
        droppedSidebarUserStories,
        workflowGraphLayoutSignature);
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
    UserStoryWorkflowDetails workflow,
    IReadOnlyCollection<UserStorySummary> userStories,
    IReadOnlyCollection<UserStorySummary> droppedUserStories,
    string workflowGraphLayoutSignature)
{
    var payload = JsonSerializer.Serialize(
        new
        {
            workflow.UsId,
            workflow.Status,
            workflow.CurrentPhase,
            workflow.CreatedWithRuntimeVersion,
            workflow.LastRuntimeVersion,
            workflow.Controls,
            eventCount = workflow.Events.Count,
            latestEvent = workflow.Events.LastOrDefault(),
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
    var portalSettings = string.IsNullOrWhiteSpace(workspaceRoot)
        ? null
        : SpecForgePortalSettingsStore.LoadOrDefault(workspaceRoot);
    var harnessProfileSettings = portalSettings is null
        ? HarnessProfileRuntimeSettings.Default
        : new HarnessProfileRuntimeSettings(
            DefaultProfile: portalSettings.DefaultHarnessProfile,
            PhaseProfiles: portalSettings.PhaseHarnessProfiles ?? HarnessProfileRuntimeSettings.Default.PhaseProfiles,
            Governance: new HarnessProfileGovernance(
                portalSettings.HarnessProfileAuthority,
                portalSettings.HarnessProfileLockMode,
                portalSettings.AllowPerUserStoryHarnessProfileOverrides,
                portalSettings.LockedHarnessPhaseIds));
    var runner = new WorkflowRunner(
        CreatePhaseExecutionProvider(workspaceRoot),
        refinementTolerance: portalSettings?.RefinementTolerance ?? "balanced",
        maxRefinementCycles: portalSettings?.MaxRefinementCycles ?? 3,
        maxImplementationReviewCycles: portalSettings?.MaxImplementationReviewCycles ?? 5,
        decompositionOptions: new UserStoryDecompositionOptions(
            Enabled: portalSettings?.DecompositionEnabled ?? true,
            Threshold: portalSettings?.DecompositionThreshold ?? 0.60,
            Tolerance: portalSettings?.DecompositionTolerance ?? 0.10,
            MaxChildren: portalSettings?.DecompositionMaxChildren ?? 5));

    return new SpecForgeApplicationService(new UserStoryFileStore(), runner, harnessProfileSettings: harnessProfileSettings);
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
              <h2>Workflow Behavior</h2>
              <div class="grid">
                <label><span class="field-label">Refinement tolerance</span><span class="field-control"><select id="refinementTolerance"><option>strict</option><option>balanced</option><option>inferential</option></select><button class="help-button" type="button" aria-label="Refinement tolerance details" aria-expanded="false" data-help="Controls how much ambiguity refinement tolerates before spec can continue. Strict asks more questions; inferential allows the model to proceed with more assumptions.">?</button></span></label>
                <label><span class="field-label">MVP rigor</span><span class="field-control"><select id="mvpRigor"><option>low</option><option>medium</option><option>high</option></select><button class="help-button" type="button" aria-label="MVP rigor details" aria-expanded="false" data-help="Controls how much product detail refinement requires before a user story can become a buildable MVP slice. Low is lean; high is exacting.">?</button></span></label>
                <label><span class="field-label">Review tolerance</span><span class="field-control"><select id="reviewTolerance"><option>strict</option><option>balanced</option><option>inferential</option></select><button class="help-button" type="button" aria-label="Review tolerance details" aria-expanded="false" data-help="Controls how demanding review is before it passes or fails delivered work. Strict requires stronger evidence; inferential is more permissive.">?</button></span></label>
                <label><span class="field-label">Review evidence policy</span><span class="field-control"><select id="reviewEvidencePolicy"><option>strict</option><option>balanced</option><option>release</option><option>advisory</option></select><button class="help-button" type="button" aria-label="Review evidence policy details" aria-expanded="false" data-help="Controls how missing automated, static, operational, or deferred validation evidence affects review readiness.">?</button></span></label>
                <label><span class="field-label">Auto-refinement agent</span><span class="field-control"><select id="autoRefinementAnswersProfile"></select><button class="help-button" type="button" aria-label="Auto-refinement agent details" aria-expanded="false" data-help="Agent used to answer refinement questions automatically before the workflow hands the phase back to the user.">?</button></span></label>
                <label><span class="field-label">Review learning skill path</span><span class="field-control"><input id="reviewLearningSkillPath"><button class="help-button" type="button" aria-label="Review learning skill path details" aria-expanded="false" data-help="Workspace-relative skill file where generalized lessons from failed reviews can be persisted.">?</button></span></label>
                <label><span class="field-label">Max implementation/review cycles</span><span class="field-control"><input id="maxImplementationReviewCycles" type="number" min="1"><button class="help-button" type="button" aria-label="Max implementation/review cycles details" aria-expanded="false" data-help="Maximum implementation attempts allowed in the implementation/review loop before automatic continuation stops.">?</button></span></label>
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
            <button type="submit">Save Configuration</button>
            <button type="button" class="secondary" id="reload">Reload</button>
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
          ["destructiveRewindEnabled", "Destructive rewind"],
          ["pauseOnFailedReview", "Pause on failed review"],
          ["useSemanticGraphWhenAvailable", "Use semantic graph when available"],
          ["allowGraphBuildRefreshForTouchedUserStoryScope", "Allow graph build/refresh for touched US scope"],
          ["reviewLearningEnabled", "Review learning"],
          ["completedUsLockOnCompleted", "Lock completed user stories"],
          ["decompositionEnabled", "Complexity decomposition"]
        ];
        const configurationTabs = ["providers", "advanced", "central"];
        const helpDescriptions = {
          "model.name": "Stable profile name used by agent routing and phase assignments.",
          "model.provider": "Provider kind for this model profile. Codex, Claude, and Copilot use native/local CLI identity; openai-compatible uses an HTTP endpoint.",
          "model.baseUrl": "Base URL for openai-compatible endpoints. Native CLI providers usually leave this empty.",
          "model.apiKey": "API key for remote openai-compatible endpoints. Local endpoints and native CLI providers can leave it empty.",
          "model.model": "Concrete model identifier for endpoint-based profiles. Native CLI providers can leave this empty to use their local default.",
          "model.reasoningEffort": "Optional reasoning effort override sent to providers that support it.",
          "model.repositoryAccess": "Repository access granted by this model profile when agents are derived directly from models.",
          "agent.name": "Stable agent name used by phase routing and auto-refinement settings.",
          "agent.role": "Operational role injected into prompts, such as planner, implementer, reviewer, or release-preparer.",
          "agent.modelProfile": "Model profile this agent runs on.",
          "agent.repositoryAccess": "Repository permissions granted to this agent. Implementation and review require read-write.",
          "agent.reasoningEffort": "Optional reasoning effort override for this agent.",
          "agent.instructions": "Additional behavior instructions injected into this agent's effective phase prompt.",
          "assignment.defaultAgent": "Fallback agent used when a phase does not declare its own specific agent.",
          "assignment.captureAgent": "Optional agent override for capture.",
          "assignment.refinementAgent": "Agent used to resolve refinement and clarify source intent.",
          "assignment.specAgent": "Agent used to produce and revise the functional spec.",
          "assignment.technicalDesignAgent": "Agent used to produce the technical design.",
          "assignment.implementationAgent": "Agent used to make repository changes. Requires read-write access.",
          "assignment.reviewAgent": "Agent used to inspect implementation and decide review readiness. Requires read-write access.",
          "assignment.releaseApprovalAgent": "Agent used to prepare the release-readiness approval artifact.",
          "assignment.prPreparationAgent": "Agent used to prepare PR handoff content.",
          "technicalDesignSubagentsEnabled": "Runs specialist design subagents before synthesizing the final technical design artifact.",
          "reviewSubagentsEnabled": "Runs specialist review subagents before synthesizing the final review verdict.",
          "autoRefinementAnswersEnabled": "Lets the selected model try to answer pending refinement questions once before handing control back to the user.",
          "autoPlayEnabled": "Automatically resumes workflow playback after manual actions when the next phase can continue.",
          "autoReviewEnabled": "Automatically continues from implementation into review after implementation artifacts are generated or updated.",
          "destructiveRewindEnabled": "When enabled, rewinds and regressions delete later derived artifacts and branch metadata.",
          "pauseOnFailedReview": "Automatically pauses workflow playback when review fails so the developer can inspect the result.",
          "useSemanticGraphWhenAvailable": "Reuses semantic graph artifacts during workflow runtime when they already exist and are compatible.",
          "allowGraphBuildRefreshForTouchedUserStoryScope": "Allows SpecForge to build or refresh the impact graph for the touched user story scope when graph state needs to be materialized.",
          "reviewLearningEnabled": "Allows implementation retries after failed review to persist generalized lessons into local skills or prompt guardrails.",
          "completedUsLockOnCompleted": "Keeps completed user stories locked against direct rewind or artifact modification unless explicitly reopened.",
          "decompositionEnabled": "Evaluates generated specs for complexity and can propose or require child user stories before normal spec approval."
        };
        let state = null;

        async function load() {
          const response = await fetch("api/settings");
          state = await response.json();
          applyDefaultSettings();
          render();
          scrollToHashSection();
          setStatus("Configuration loaded.");
        }

        function render() {
          renderModels();
          renderAgents();
          renderAssignments();
          renderBehavior();
          renderTabState(resolveActiveTabFromHash());
        }

        function scrollToHashSection() {
          renderTabState(resolveActiveTabFromHash());
        }

        function resolveActiveTabFromHash() {
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
          for (const id of ["refinementTolerance", "mvpRigor", "reviewTolerance", "reviewEvidencePolicy", "autoRefinementAnswersProfile", "reviewLearningSkillPath", "maxImplementationReviewCycles", "decompositionThreshold", "decompositionTolerance", "decompositionMaxChildren"]) {
            const element = document.getElementById(id);
            if (!element) continue;
            if (id === "autoRefinementAnswersProfile") {
              element.innerHTML = ["", ...state.agentProfiles.map(agent => agent.name).filter(Boolean)].map(value => `<option value="${escapeAttr(value)}">${escapeText(value || "None")}</option>`).join("");
            }
            element.value = state[id] ?? "";
          }
          document.getElementById("toggles").innerHTML = toggleFields.map(([field, label]) =>
            `<label class="toggle"><span class="toggle__label">${escapeText(label)}</span><span class="toggle__control"><input type="checkbox" data-toggle="${field}" ${state[field] ? "checked" : ""}><span class="toggle__switch" aria-hidden="true"></span>${helpButton(field, label)}</span></label>`).join("");
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
          for (const id of ["refinementTolerance", "mvpRigor", "reviewTolerance", "reviewEvidencePolicy", "autoRefinementAnswersProfile", "reviewLearningSkillPath"]) {
            const element = document.getElementById(id);
            if (element) state[id] = element.value || null;
          }
          state.maxImplementationReviewCycles = Number(document.getElementById("maxImplementationReviewCycles")?.value) || 5;
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

        document.addEventListener("click", event => {
          const target = event.target;
          if (!(target instanceof HTMLElement)) return;
          if (target.dataset.tabTarget) {
            event.preventDefault();
            closeHelpPopover();
            sync();
            window.location.hash = target.dataset.tabTarget;
            renderTabState(target.dataset.tabTarget);
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
          if (target.id === "reload") {
            load();
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

        window.addEventListener("hashchange", () => {
          renderTabState(resolveActiveTabFromHash());
        });

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
          }
        });

        document.addEventListener("change", event => {
          const target = event.target;
          if (target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement || target instanceof HTMLSelectElement) {
            const refreshNeeded = syncField(target);
            if (refreshNeeded) {
              updateDependentSelectOptions();
            }
          }
        });

        document.getElementById("settings-form").addEventListener("submit", async event => {
          event.preventDefault();
          sync();
          normalizeConfigurationReferences();
          const response = await fetch("api/settings", { method: "PUT", headers: { "content-type": "application/json" }, body: JSON.stringify(state) });
          if (!response.ok) {
            setStatus(await response.text());
            return;
          }
          state = await response.json();
          applyDefaultSettings();
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

internal sealed record AttachWorkflowFilesRequest(string Kind, IReadOnlyList<WorkflowFileUploadItem> Files, string? Actor);

internal sealed record AddContextFilesRequest(IReadOnlyList<string> Paths, string? Actor);

internal sealed record SaveWorkflowGraphLayoutRequest(
    string? LayoutKind,
    string? UserStoryId,
    string? LayoutMode,
    Dictionary<string, WorkflowGraphLayoutPoint>? Positions,
    WorkflowGraphLayoutPoint? LegendPosition,
    WorkflowAggregateGraphLayoutRequest? Aggregate);

internal sealed record WorkflowGraphLayoutPoint(int X, int Y);

internal sealed record WorkflowAggregateGraphLayoutRequest(
    Dictionary<string, WorkflowGraphLayoutPoint> Positions,
    Dictionary<string, int> Spacing);

internal sealed record ApprovalSubmitRequest(string? BaseBranch, string? WorkBranch, string? Actor);

internal sealed record DecompositionApprovalSubmitRequest(string Decision, string? Actor);

internal sealed record UserStoryVisibilityRequest(string UsId);
