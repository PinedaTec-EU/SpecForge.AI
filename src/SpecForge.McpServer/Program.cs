using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using SpecForge.Domain.Application;
using SpecForge.Domain.Persistence;
using SpecForge.McpServer;

var serverVersion = typeof(Program).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
    ?.InformationalVersion ?? "0.0.1";

var refinementTolerance = (Environment.GetEnvironmentVariable("SPECFORGE_REFINEMENT_TOLERANCE")
    ?? Environment.GetEnvironmentVariable("SPECFORGE_CAPTURE_TOLERANCE"))?.Trim().ToLowerInvariant();
refinementTolerance = refinementTolerance is "strict" or "balanced" or "inferential" ? refinementTolerance : "balanced";
var reviewEvidencePolicy = Environment.GetEnvironmentVariable("SPECFORGE_REVIEW_EVIDENCE_POLICY")?.Trim().ToLowerInvariant();
reviewEvidencePolicy = reviewEvidencePolicy is "strict" or "balanced" or "release" or "advisory" ? reviewEvidencePolicy : "balanced";
var completedUsLockOnCompleted = string.Equals(
    Environment.GetEnvironmentVariable("SPECFORGE_COMPLETED_US_LOCK_ON_COMPLETED")?.Trim(),
    "true",
    StringComparison.OrdinalIgnoreCase);

var phaseExecutionProvider = PhaseExecutionProviderFactory.Create();
var workflowRunner = new WorkflowRunner(phaseExecutionProvider, serverVersion, refinementTolerance, completedUsLockOnCompleted, reviewEvidencePolicy);
var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), workflowRunner, runtimeVersion: serverVersion, completedUsLockOnCompleted: completedUsLockOnCompleted);
var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var stdin = Console.OpenStandardInput();
var stdout = Console.OpenStandardOutput();

while (true)
{
    var payload = await McpJsonRpcStdioTransport.ReadMessageAsync(stdin);
    if (payload is null)
    {
        break;
    }

    JsonNode? response;

    try
    {
        response = await HandleAsync(payload, applicationService, serializerOptions, serverVersion);
    }
    catch (Exception exception)
    {
        response = BuildErrorResponse(payload["id"], code: -32000, exception.Message);
    }

    if (response is not null)
    {
        await McpJsonRpcStdioTransport.WriteMessageAsync(stdout, response.ToJsonString(serializerOptions));
    }
}

static async Task<JsonNode?> HandleAsync(
    JsonNode payload,
    SpecForgeApplicationService applicationService,
    JsonSerializerOptions serializerOptions,
    string serverVersion)
{
    var method = payload["method"]?.GetValue<string>();
    if (string.IsNullOrWhiteSpace(method))
    {
        return BuildErrorResponse(payload["id"], code: -32600, "Invalid request.");
    }

    return method switch
    {
        "initialize" => BuildSuccessResponse(
            payload["id"],
            new JsonObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["serverInfo"] = new JsonObject
                {
                    ["name"] = "SpecForge MCP Server",
                    ["version"] = serverVersion
                },
                ["capabilities"] = new JsonObject
                {
                    ["tools"] = new JsonObject()
                }
            }),
        "notifications/initialized" => null,
        "tools/list" => BuildSuccessResponse(payload["id"], McpToolRegistry.BuildToolsList()),
        "tools/call" => await HandleToolCallAsync(payload, applicationService, serializerOptions),
        _ => BuildErrorResponse(payload["id"], code: -32601, $"Method '{method}' was not found.")
    };
}

static async Task<JsonNode> HandleToolCallAsync(
    JsonNode payload,
    SpecForgeApplicationService applicationService,
    JsonSerializerOptions serializerOptions)
{
    var parameters = payload["params"]?.AsObject() ?? throw new InvalidOperationException("Missing tool call parameters.");
    var toolName = parameters["name"]?.GetValue<string>() ?? throw new InvalidOperationException("Missing tool name.");
    var arguments = parameters["arguments"]?.AsObject() ?? new JsonObject();
    var toolRequestId = payload["id"]?.ToJsonString() ?? "null";
    await using var diagnostics = SpecForgeDiagnostics.StartProgressScope(
        $"[mcp.tool] {toolName} requestId={toolRequestId}",
        interval: TimeSpan.FromSeconds(15));

    try
    {
        object result = toolName switch
        {
            "specforge_query" => await HandleSpecForgeQueryAsync(arguments, applicationService),
            "specforge_action" => await HandleSpecForgeActionAsync(arguments, applicationService),
            "specforge_prompts" => await HandleSpecForgePromptsAsync(arguments, applicationService),
            "create_us_from_chat" => await applicationService.CreateUserStoryAsync(
                workspaceRoot: GetRequired(arguments, "workspaceRoot"),
                usId: GetRequired(arguments, "usId"),
                title: GetRequired(arguments, "title"),
                kind: GetRequired(arguments, "kind"),
                category: GetRequired(arguments, "category"),
                sourceText: GetRequired(arguments, "sourceText"),
                actor: GetOptional(arguments, "actor") ?? "user"),
            "import_us_from_markdown" => await applicationService.ImportUserStoryAsync(
                workspaceRoot: GetRequired(arguments, "workspaceRoot"),
                usId: GetRequired(arguments, "usId"),
                sourcePath: GetRequired(arguments, "sourcePath"),
                title: GetRequired(arguments, "title"),
                kind: GetRequired(arguments, "kind"),
                category: GetRequired(arguments, "category"),
                actor: GetOptional(arguments, "actor") ?? "user"),
            "initialize_repo_prompts" => await applicationService.InitializeRepoPromptsAsync(
                workspaceRoot: GetRequired(arguments, "workspaceRoot"),
                overwrite: GetOptionalBoolean(arguments, "overwrite")),
            "export_prompt_template" => await applicationService.ExportPromptTemplateAsync(
                workspaceRoot: GetRequired(arguments, "workspaceRoot"),
                promptPath: GetRequired(arguments, "promptPath"),
                overwrite: GetOptionalBoolean(arguments, "overwrite")),
            "list_user_stories" => new
            {
                items = await applicationService.ListUserStoriesAsync(
                    workspaceRoot: GetRequired(arguments, "workspaceRoot"))
            },
            "get_user_story_summary" => await applicationService.GetUserStorySummaryAsync(
                workspaceRoot: GetRequired(arguments, "workspaceRoot"),
                usId: GetRequired(arguments, "usId")),
            "get_user_story_workflow" => await applicationService.GetUserStoryWorkflowAsync(
                workspaceRoot: GetRequired(arguments, "workspaceRoot"),
                usId: GetRequired(arguments, "usId")),
            "get_current_phase" => await applicationService.GetCurrentPhaseAsync(
                workspaceRoot: GetRequired(arguments, "workspaceRoot"),
                usId: GetRequired(arguments, "usId")),
            "get_user_story_runtime_status" => await applicationService.GetUserStoryRuntimeStatusAsync(
                workspaceRoot: GetRequired(arguments, "workspaceRoot"),
                usId: GetRequired(arguments, "usId")),
            "analyze_user_story_lineage" => await applicationService.AnalyzeUserStoryLineageAsync(
                workspaceRoot: GetRequired(arguments, "workspaceRoot"),
                usId: GetRequired(arguments, "usId")),
            "repair_user_story_lineage" => await applicationService.RepairUserStoryLineageAsync(
                workspaceRoot: GetRequired(arguments, "workspaceRoot"),
                usId: GetRequired(arguments, "usId"),
                actor: GetOptional(arguments, "actor") ?? "user"),
            "generate_next_phase" => await applicationService.GenerateNextPhaseAsync(
                workspaceRoot: GetRequired(arguments, "workspaceRoot"),
                usId: GetRequired(arguments, "usId"),
                actor: GetOptional(arguments, "actor") ?? "user"),
            "approve_review_anyway" => await applicationService.ApproveReviewAnywayAsync(
                workspaceRoot: GetRequired(arguments, "workspaceRoot"),
                usId: GetRequired(arguments, "usId"),
                reason: GetRequired(arguments, "reason"),
                actor: GetOptional(arguments, "actor") ?? "user"),
            "approve_phase" => await applicationService.ApprovePhaseAsync(
                workspaceRoot: GetRequired(arguments, "workspaceRoot"),
                usId: GetRequired(arguments, "usId"),
                baseBranch: GetOptional(arguments, "baseBranch"),
                workBranch: GetOptional(arguments, "workBranch"),
                actor: GetOptional(arguments, "actor") ?? "user"),
            "request_regression" => await applicationService.RequestRegressionAsync(
                workspaceRoot: GetRequired(arguments, "workspaceRoot"),
                usId: GetRequired(arguments, "usId"),
                targetPhase: GetRequired(arguments, "targetPhase"),
                reason: GetOptional(arguments, "reason"),
                destructive: GetOptionalBoolean(arguments, "destructive"),
                actor: GetOptional(arguments, "actor") ?? "user"),
            "restart_user_story_from_source" => await applicationService.RestartUserStoryFromSourceAsync(
                workspaceRoot: GetRequired(arguments, "workspaceRoot"),
                usId: GetRequired(arguments, "usId"),
                reason: GetOptional(arguments, "reason"),
                actor: GetOptional(arguments, "actor") ?? "user"),
            "rewind_workflow" => await applicationService.RewindWorkflowAsync(
                workspaceRoot: GetRequired(arguments, "workspaceRoot"),
                usId: GetRequired(arguments, "usId"),
                targetPhase: GetRequired(arguments, "targetPhase"),
                destructive: GetOptionalBoolean(arguments, "destructive"),
                actor: GetOptional(arguments, "actor") ?? "user"),
            "reopen_completed_workflow" => await applicationService.ReopenCompletedWorkflowAsync(
                workspaceRoot: GetRequired(arguments, "workspaceRoot"),
                usId: GetRequired(arguments, "usId"),
                reasonKind: GetRequired(arguments, "reasonKind"),
                description: GetRequired(arguments, "description"),
                actor: GetOptional(arguments, "actor") ?? "user"),
            "reset_user_story_to_capture" => await applicationService.ResetUserStoryToCaptureAsync(
                workspaceRoot: GetRequired(arguments, "workspaceRoot"),
                usId: GetRequired(arguments, "usId")),
            "submit_refinement_answers" => await applicationService.SubmitRefinementAnswersAsync(
                workspaceRoot: GetRequired(arguments, "workspaceRoot"),
                usId: GetRequired(arguments, "usId"),
                answers: GetStringArray(arguments, "answers"),
                actor: GetOptional(arguments, "actor") ?? "user"),
            "submit_approval_answer" => await applicationService.SubmitApprovalAnswerAsync(
                workspaceRoot: GetRequired(arguments, "workspaceRoot"),
                usId: GetRequired(arguments, "usId"),
                question: GetRequired(arguments, "question"),
                answer: GetRequired(arguments, "answer"),
                actor: GetOptional(arguments, "actor") ?? "user"),
            "suggest_approval_answer" => await applicationService.SuggestApprovalAnswerAsync(
                workspaceRoot: GetRequired(arguments, "workspaceRoot"),
                usId: GetRequired(arguments, "usId"),
                question: GetRequired(arguments, "question"),
                actor: GetOptional(arguments, "actor") ?? "user"),
            "operate_current_phase_artifact" => await applicationService.OperateCurrentPhaseArtifactAsync(
                workspaceRoot: GetRequired(arguments, "workspaceRoot"),
                usId: GetRequired(arguments, "usId"),
                prompt: GetRequired(arguments, "prompt"),
                includeReviewArtifactInContext: GetOptionalBoolean(arguments, "includeReviewArtifactInContext", defaultValue: true),
                actor: GetOptional(arguments, "actor") ?? "user"),
            "list_user_story_files" => await applicationService.ListUserStoryFilesAsync(
                workspaceRoot: GetRequired(arguments, "workspaceRoot"),
                usId: GetRequired(arguments, "usId")),
            "add_user_story_files" => await applicationService.AddUserStoryFilesAsync(
                workspaceRoot: GetRequired(arguments, "workspaceRoot"),
                usId: GetRequired(arguments, "usId"),
                sourcePaths: GetStringArray(arguments, "sourcePaths"),
                kind: GetRequired(arguments, "kind")),
            "set_user_story_file_kind" => await applicationService.SetUserStoryFileKindAsync(
                workspaceRoot: GetRequired(arguments, "workspaceRoot"),
                usId: GetRequired(arguments, "usId"),
                filePath: GetRequired(arguments, "filePath"),
                kind: GetRequired(arguments, "kind")),
            _ => throw new InvalidOperationException($"Tool '{toolName}' is not supported.")
        };

        diagnostics.MarkCompleted();

        var resultJson = JsonSerializer.Serialize(result, serializerOptions);
        return BuildSuccessResponse(
            payload["id"],
            new JsonObject
            {
                ["content"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "text",
                        ["text"] = resultJson
                    }
                }
            });
    }
    catch (Exception exception)
    {
        diagnostics.MarkFailed(exception);
        throw;
    }
}

static async Task<object> HandleSpecForgeQueryAsync(
    JsonObject arguments,
    SpecForgeApplicationService applicationService)
{
    var workspaceRoot = GetRequired(arguments, "workspaceRoot");
    var query = GetRequired(arguments, "query");

    return query switch
    {
        "list_user_stories" => new
        {
            items = await applicationService.ListUserStoriesAsync(workspaceRoot)
        },
        "summary" => await applicationService.GetUserStorySummaryAsync(
            workspaceRoot,
            GetRequired(arguments, "usId")),
        "workflow" => await applicationService.GetUserStoryWorkflowAsync(
            workspaceRoot,
            GetRequired(arguments, "usId")),
        "current_phase" => await applicationService.GetCurrentPhaseAsync(
            workspaceRoot,
            GetRequired(arguments, "usId")),
        "runtime_status" => await applicationService.GetUserStoryRuntimeStatusAsync(
            workspaceRoot,
            GetRequired(arguments, "usId")),
        "lineage" => await applicationService.AnalyzeUserStoryLineageAsync(
            workspaceRoot,
            GetRequired(arguments, "usId")),
        "files" => await applicationService.ListUserStoryFilesAsync(
            workspaceRoot,
            GetRequired(arguments, "usId")),
        _ => throw new InvalidOperationException($"SpecForge query '{query}' is not supported.")
    };
}

static async Task<object> HandleSpecForgeActionAsync(
    JsonObject arguments,
    SpecForgeApplicationService applicationService)
{
    var workspaceRoot = GetRequired(arguments, "workspaceRoot");
    var action = GetRequired(arguments, "action");
    var parameters = arguments["params"]?.AsObject() ?? new JsonObject();

    return action switch
    {
        "create_user_story" => await applicationService.CreateUserStoryAsync(
            workspaceRoot,
            GetRequired(parameters, "usId"),
            GetRequired(parameters, "title"),
            GetRequired(parameters, "kind"),
            GetRequired(parameters, "category"),
            GetRequired(parameters, "sourceText"),
            GetOptional(parameters, "actor") ?? "user"),
        "create_user_stories_from_goal" => await applicationService.CreateUserStoriesFromGoalAsync(
            workspaceRoot,
            GetRequired(parameters, "goalText"),
            GetGoalUserStoryDrafts(parameters),
            GetOptional(parameters, "goalId"),
            GetOptional(parameters, "strategy"),
            GetOptional(parameters, "actor") ?? "model-on-behalf-of-user"),
        "import_user_story" => await applicationService.ImportUserStoryAsync(
            workspaceRoot,
            GetRequired(parameters, "usId"),
            GetRequired(parameters, "sourcePath"),
            GetRequired(parameters, "title"),
            GetRequired(parameters, "kind"),
            GetRequired(parameters, "category"),
            GetOptional(parameters, "actor") ?? "user"),
        "advance_phase" => await applicationService.GenerateNextPhaseAsync(
            workspaceRoot,
            GetRequired(arguments, "usId"),
            GetOptional(parameters, "actor") ?? "user"),
        "approve_phase" => await applicationService.ApprovePhaseAsync(
            workspaceRoot,
            GetRequired(arguments, "usId"),
            GetOptional(parameters, "baseBranch"),
            GetOptional(parameters, "workBranch"),
            GetOptional(parameters, "actor") ?? "user"),
        "approve_review_anyway" => await applicationService.ApproveReviewAnywayAsync(
            workspaceRoot,
            GetRequired(arguments, "usId"),
            GetRequired(parameters, "reason"),
            GetOptional(parameters, "actor") ?? "user"),
        "request_regression" => await applicationService.RequestRegressionAsync(
            workspaceRoot,
            GetRequired(arguments, "usId"),
            GetRequired(parameters, "targetPhase"),
            GetOptional(parameters, "reason"),
            GetOptionalBoolean(parameters, "destructive"),
            GetOptional(parameters, "actor") ?? "user"),
        "restart_from_source" => await applicationService.RestartUserStoryFromSourceAsync(
            workspaceRoot,
            GetRequired(arguments, "usId"),
            GetOptional(parameters, "reason"),
            GetOptional(parameters, "actor") ?? "user"),
        "rewind_workflow" => await applicationService.RewindWorkflowAsync(
            workspaceRoot,
            GetRequired(arguments, "usId"),
            GetRequired(parameters, "targetPhase"),
            GetOptionalBoolean(parameters, "destructive"),
            GetOptional(parameters, "actor") ?? "user"),
        "reopen_completed" => await applicationService.ReopenCompletedWorkflowAsync(
            workspaceRoot,
            GetRequired(arguments, "usId"),
            GetRequired(parameters, "reasonKind"),
            GetRequired(parameters, "description"),
            GetOptional(parameters, "actor") ?? "user"),
        "reset_to_capture" => await applicationService.ResetUserStoryToCaptureAsync(
            workspaceRoot,
            GetRequired(arguments, "usId")),
        "submit_refinement_answers" => await applicationService.SubmitRefinementAnswersAsync(
            workspaceRoot,
            GetRequired(arguments, "usId"),
            GetStringArray(parameters, "answers"),
            GetOptional(parameters, "actor") ?? "user"),
        "submit_approval_answer" => await applicationService.SubmitApprovalAnswerAsync(
            workspaceRoot,
            GetRequired(arguments, "usId"),
            GetRequired(parameters, "question"),
            GetRequired(parameters, "answer"),
            GetOptional(parameters, "actor") ?? "user"),
        "suggest_approval_answer" => await applicationService.SuggestApprovalAnswerAsync(
            workspaceRoot,
            GetRequired(arguments, "usId"),
            GetRequired(parameters, "question"),
            GetOptional(parameters, "actor") ?? "user"),
        "operate_artifact" => await applicationService.OperateCurrentPhaseArtifactAsync(
            workspaceRoot,
            GetRequired(arguments, "usId"),
            GetRequired(parameters, "prompt"),
            GetOptionalBoolean(parameters, "includeReviewArtifactInContext", defaultValue: true),
            GetOptional(parameters, "actor") ?? "user"),
        "add_files" => await applicationService.AddUserStoryFilesAsync(
            workspaceRoot,
            GetRequired(arguments, "usId"),
            GetStringArray(parameters, "sourcePaths"),
            GetRequired(parameters, "kind")),
        "set_file_kind" => await applicationService.SetUserStoryFileKindAsync(
            workspaceRoot,
            GetRequired(arguments, "usId"),
            GetRequired(parameters, "filePath"),
            GetRequired(parameters, "kind")),
        "repair_lineage" => await applicationService.RepairUserStoryLineageAsync(
            workspaceRoot,
            GetRequired(arguments, "usId"),
            GetOptional(parameters, "actor") ?? "user"),
        _ => throw new InvalidOperationException($"SpecForge action '{action}' is not supported.")
    };
}

static IReadOnlyList<GoalUserStoryDraft> GetGoalUserStoryDrafts(JsonObject parameters)
{
    if (parameters["stories"] is not JsonArray storiesNode)
    {
        throw new InvalidOperationException("Missing or invalid 'stories' array.");
    }

    var stories = storiesNode.Deserialize<IReadOnlyList<GoalUserStoryDraft>>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
    if (stories is null || stories.Count == 0)
    {
        throw new InvalidOperationException("At least one goal user story draft is required.");
    }

    return stories;
}

static async Task<object> HandleSpecForgePromptsAsync(
    JsonObject arguments,
    SpecForgeApplicationService applicationService)
{
    var workspaceRoot = GetRequired(arguments, "workspaceRoot");
    var operation = GetRequired(arguments, "operation");

    return operation switch
    {
        "initialize_repo_prompts" => await applicationService.InitializeRepoPromptsAsync(
            workspaceRoot,
            GetOptionalBoolean(arguments, "overwrite")),
        "export_prompt_template" => await applicationService.ExportPromptTemplateAsync(
            workspaceRoot,
            GetRequired(arguments, "promptPath"),
            GetOptionalBoolean(arguments, "overwrite")),
        _ => throw new InvalidOperationException($"SpecForge prompt operation '{operation}' is not supported.")
    };
}

static string GetRequired(JsonObject arguments, string key)
{
    var value = arguments[key]?.GetValue<string>();
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Missing required argument '{key}'.");
    }

    return value;
}

static string? GetOptional(JsonObject arguments, string key)
{
    var value = arguments[key]?.GetValue<string>();
    return string.IsNullOrWhiteSpace(value) ? null : value;
}

static bool GetOptionalBoolean(JsonObject arguments, string key, bool defaultValue = false)
{
    var value = arguments[key];
    return value is not null ? value.GetValue<bool>() : defaultValue;
}

static string[] GetStringArray(JsonObject arguments, string key)
{
    if (arguments[key] is not JsonArray array)
    {
        throw new InvalidOperationException($"Missing required array argument '{key}'.");
    }

    var values = array
        .Select(static item => item?.GetValue<string>()?.Trim())
        .Where(static item => !string.IsNullOrWhiteSpace(item))
        .Cast<string>()
        .ToArray();
    if (values.Length == 0)
    {
        throw new InvalidOperationException($"Required array argument '{key}' must contain at least one non-empty value.");
    }

    return values;
}

static JsonObject BuildSuccessResponse(JsonNode? id, JsonNode result)
{
    return new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["result"] = result
    };
}

static JsonObject BuildErrorResponse(JsonNode? id, int code, string message)
{
    return new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["error"] = new JsonObject
        {
            ["code"] = code,
            ["message"] = message
        }
    };
}
