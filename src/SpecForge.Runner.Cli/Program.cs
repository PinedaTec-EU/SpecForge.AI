using System.Net;
using System.Text;
using System.Text.Json;
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
            var runner = CreateWorkflowRunner(args);
            await HandleCreateUserStoryAsync(runner, args);
            return 0;
        }
        case "import-us":
        {
            var runner = CreateWorkflowRunner(args);
            await HandleImportUserStoryAsync(runner, args);
            return 0;
        }
        case "continue-phase":
        {
            var runner = CreateWorkflowRunner(args);
            await HandleContinuePhaseAsync(runner, args);
            return 0;
        }
        case "list-user-stories":
        {
            var runner = CreateWorkflowRunner(args);
            var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), runner);
            await HandleListUserStoriesAsync(applicationService, args);
            return 0;
        }
        case "get-user-story-summary":
        {
            var runner = CreateWorkflowRunner(args);
            var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), runner);
            await HandleGetUserStorySummaryAsync(applicationService, args);
            return 0;
        }
        case "approve-phase":
        {
            var runner = CreateWorkflowRunner(args);
            var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), runner);
            await HandleApprovePhaseAsync(runner, applicationService, args);
            return 0;
        }
        case "serve-configuration":
            await HandleServeConfigurationAsync(args);
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

static async Task HandleCreateUserStoryAsync(WorkflowRunner runner, IReadOnlyList<string> args)
{
    EnsureArgumentCount(args, expectedCount: 7);

    var workspaceRoot = args[1];
    var usId = args[2];
    var title = args[3];
    var kind = args[4];
    var category = args[5];
    var sourceText = args[6];
    var rootDirectory = await runner.CreateUserStoryAsync(workspaceRoot, usId, title, kind, category, sourceText);

    WriteJson(new
    {
        usId,
        rootDirectory,
        mainArtifactPath = Path.Combine(rootDirectory, "us.md")
    });
}

static async Task HandleImportUserStoryAsync(WorkflowRunner runner, IReadOnlyList<string> args)
{
    EnsureArgumentCount(args, expectedCount: 7);

    var workspaceRoot = args[1];
    var usId = args[2];
    var sourcePath = args[3];
    var title = args[4];
    var kind = args[5];
    var category = args[6];
    var sourceText = await File.ReadAllTextAsync(sourcePath);
    var rootDirectory = await runner.CreateUserStoryAsync(workspaceRoot, usId, title, kind, category, sourceText);

    WriteJson(new
    {
        usId,
        rootDirectory,
        mainArtifactPath = Path.Combine(rootDirectory, "us.md")
    });
}

static async Task HandleContinuePhaseAsync(WorkflowRunner runner, IReadOnlyList<string> args)
{
    EnsureArgumentCount(args, expectedCount: 3);

    var workspaceRoot = args[1];
    var usId = args[2];
    var result = await runner.ContinuePhaseAsync(workspaceRoot, usId);

    WriteJson(new
    {
        result.UsId,
        currentPhase = WorkflowPresentation.ToPhaseSlug(result.CurrentPhase),
        status = WorkflowPresentation.ToStatusSlug(result.Status),
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
    WorkflowRunner runner,
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
    await runner.ApproveCurrentPhaseAsync(workspaceRoot, usId, normalizedBaseBranch, normalizedWorkBranch);
    var summary = await applicationService.GetUserStorySummaryAsync(workspaceRoot, usId);
    WriteJson(summary);
}

static async Task HandleServeConfigurationAsync(IReadOnlyList<string> args)
{
    if (args.Count is < 2 or > 3)
    {
        throw new InvalidOperationException("Expected workspace root and optional URL prefix for command 'serve-configuration'.");
    }

    var workspaceRoot = Path.GetFullPath(args[1]);
    var prefix = args.Count == 3 ? NormalizeHttpPrefix(args[2]) : "http://localhost:5127/";
    using var listener = new HttpListener();
    listener.Prefixes.Add(prefix);
    listener.Start();
    Console.WriteLine($"SpecForge configuration portal listening at {prefix}");
    Console.WriteLine($"Workspace: {workspaceRoot}");

    while (listener.IsListening)
    {
        var context = await listener.GetContextAsync();
        _ = Task.Run(async () => await HandleConfigurationPortalRequestAsync(context, workspaceRoot));
    }
}

static async Task HandleServeWorkflowAsync(IReadOnlyList<string> args)
{
    if (args.Count is < 3 or > 4)
    {
        throw new InvalidOperationException("Expected workspace root, user story id, and optional URL prefix for command 'serve-workflow'.");
    }

    var workspaceRoot = Path.GetFullPath(args[1]);
    var usId = args[2];
    var prefix = args.Count == 4 ? NormalizeHttpPrefix(args[3]) : "http://localhost:5127/";
    var runner = new WorkflowRunner(CreatePhaseExecutionProvider(workspaceRoot));
    var applicationService = new SpecForgeApplicationService(new UserStoryFileStore(), runner);

    using var listener = new HttpListener();
    listener.Prefixes.Add(prefix);
    listener.Start();
    Console.WriteLine($"SpecForge workflow portal listening at {prefix}");
    Console.WriteLine($"Workspace: {workspaceRoot}");
    Console.WriteLine($"User story: {usId}");

    while (listener.IsListening)
    {
        var context = await listener.GetContextAsync();
        _ = Task.Run(() => HandleWorkflowPortalRequestAsync(context, applicationService, workspaceRoot, usId));
    }
}

static async Task HandleWorkflowPortalRequestAsync(
    HttpListenerContext context,
    SpecForgeApplicationService applicationService,
    string workspaceRoot,
    string usId)
{
    try
    {
        var path = context.Request.Url?.AbsolutePath.TrimEnd('/') ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = "/";
        }

        switch ((context.Request.HttpMethod, path))
        {
            case ("GET", "/"):
                await WriteHtmlResponseAsync(context.Response, BuildWorkflowPortalHtml(usId));
                return;
            case ("GET", "/api/workflow"):
                await WriteJsonResponseAsync(context.Response, await applicationService.GetUserStoryWorkflowAsync(workspaceRoot, usId));
                return;
            case ("GET", "/api/runtime-status"):
                await WriteJsonResponseAsync(context.Response, await applicationService.GetUserStoryRuntimeStatusAsync(workspaceRoot, usId));
                return;
            case ("GET", "/api/summary"):
                await WriteJsonResponseAsync(context.Response, await applicationService.GetUserStorySummaryAsync(workspaceRoot, usId));
                return;
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

static async Task HandleConfigurationPortalRequestAsync(HttpListenerContext context, string workspaceRoot)
{
    try
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";
        if (context.Request.HttpMethod == "GET" && path == "/")
        {
            await WriteHtmlResponseAsync(context.Response, BuildConfigurationPortalHtml());
            return;
        }

        if (context.Request.HttpMethod == "GET" && path == "/api/settings")
        {
            await WriteJsonResponseAsync(context.Response, SpecForgePortalSettingsStore.LoadOrDefault(workspaceRoot));
            return;
        }

        if (context.Request.HttpMethod == "PUT" && path == "/api/settings")
        {
            using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
            var payload = await reader.ReadToEndAsync();
            var settings = SpecForgePortalSettingsStore.Deserialize(payload);
            SpecForgePortalSettingsStore.Save(workspaceRoot, settings);
            await WriteJsonResponseAsync(context.Response, settings);
            return;
        }

        context.Response.StatusCode = 404;
        await WriteTextResponseAsync(context.Response, "Not found", "text/plain; charset=utf-8");
    }
    catch (Exception exception)
    {
        context.Response.StatusCode = 500;
        await WriteTextResponseAsync(context.Response, exception.Message, "text/plain; charset=utf-8");
    }
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
    Console.WriteLine(JsonSerializer.Serialize(payload));
}

static string NormalizeHttpPrefix(string value)
{
    var prefix = value.Trim();
    if (prefix.Length == 0)
    {
        throw new InvalidOperationException("The configuration portal URL prefix cannot be empty.");
    }

    return prefix.EndsWith("/", StringComparison.Ordinal) ? prefix : $"{prefix}/";
}

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

static WorkflowRunner CreateWorkflowRunner(IReadOnlyList<string> args)
{
    var workspaceRoot = args.Count > 1 ? args[1] : null;

    return new WorkflowRunner(CreatePhaseExecutionProvider(workspaceRoot));
}

static IPhaseExecutionProvider CreatePhaseExecutionProvider(string? workspaceRoot)
{
    const string modelProfilesEnvVar = "SPECFORGE_OPENAI_MODEL_PROFILES_JSON";
    const string agentProfilesEnvVar = "SPECFORGE_OPENAI_AGENT_PROFILES_JSON";
    const string phaseAgentsEnvVar = "SPECFORGE_OPENAI_PHASE_AGENT_ASSIGNMENTS_JSON";
    const string technicalDesignSubagentsEnabledEnvVar = "SPECFORGE_TECHNICAL_DESIGN_SUBAGENTS_ENABLED";
    const string reviewSubagentsEnabledEnvVar = "SPECFORGE_REVIEW_SUBAGENTS_ENABLED";
    const string refinementToleranceEnvVar = "SPECFORGE_REFINEMENT_TOLERANCE";
    const string legacyRefinementToleranceEnvVar = "SPECFORGE_CAPTURE_TOLERANCE";
    const string reviewToleranceEnvVar = "SPECFORGE_REVIEW_TOLERANCE";
    const string reviewEvidencePolicyEnvVar = "SPECFORGE_REVIEW_EVIDENCE_POLICY";
    const string autoRefinementAnswersEnabledEnvVar = "SPECFORGE_AUTO_REFINEMENT_ANSWERS_ENABLED";
    const string legacyAutoRefinementAnswersEnabledEnvVar = "SPECFORGE_AUTO_CLARIFICATION_ANSWERS_ENABLED";
    const string autoRefinementAnswersProfileEnvVar = "SPECFORGE_AUTO_REFINEMENT_ANSWERS_PROFILE";
    const string legacyAutoRefinementAnswersProfileEnvVar = "SPECFORGE_AUTO_CLARIFICATION_ANSWERS_PROFILE";
    const string reviewLearningEnabledEnvVar = "SPECFORGE_REVIEW_LEARNING_ENABLED";
    const string reviewLearningSkillPathEnvVar = "SPECFORGE_REVIEW_LEARNING_SKILL_PATH";
    const string systemPromptEnvVar = "SPECFORGE_OPENAI_SYSTEM_PROMPT";
    const string timeoutSecondsEnvVar = "SPECFORGE_OPENAI_TIMEOUT_SECONDS";

    var portalSettings = string.IsNullOrWhiteSpace(workspaceRoot)
        ? null
        : SpecForgePortalSettingsStore.Load(workspaceRoot);
    var payload = Environment.GetEnvironmentVariable(modelProfilesEnvVar)
        ?? (portalSettings?.ModelProfiles.Count > 0 ? JsonSerializer.Serialize(portalSettings.ModelProfiles, SpecForgePortalSettingsStore.JsonOptions) : null);
    if (string.IsNullOrWhiteSpace(payload))
    {
        return new DeterministicPhaseExecutionProvider();
    }

    var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
    var modelProfiles = JsonSerializer.Deserialize<List<OpenAiCompatibleModelProfile>>(payload, jsonOptions)
        ?? throw new InvalidOperationException($"{modelProfilesEnvVar} could not be parsed.");
    var agentProfiles = JsonSerializer.Deserialize<List<OpenAiCompatibleAgentProfile>>(
            Environment.GetEnvironmentVariable(agentProfilesEnvVar)
                ?? (portalSettings is null ? null : JsonSerializer.Serialize(portalSettings.ResolveAgentProfiles(), SpecForgePortalSettingsStore.JsonOptions))
                ?? "[]",
            jsonOptions)
        ?? [];
    var phaseAgents = JsonSerializer.Deserialize<OpenAiCompatiblePhaseAgentAssignments>(
        Environment.GetEnvironmentVariable(phaseAgentsEnvVar)
            ?? (portalSettings is null ? null : JsonSerializer.Serialize(portalSettings.PhaseAgentAssignments, SpecForgePortalSettingsStore.JsonOptions))
            ?? "{}",
        jsonOptions);
    var autoRefinementAnswersProfile = Environment.GetEnvironmentVariable(autoRefinementAnswersProfileEnvVar)
        ?? Environment.GetEnvironmentVariable(legacyAutoRefinementAnswersProfileEnvVar)
        ?? portalSettings?.AutoRefinementAnswersProfile;
    var reviewLearningSkillPath = Environment.GetEnvironmentVariable(reviewLearningSkillPathEnvVar)
        ?? portalSettings?.ReviewLearningSkillPath;

    return new OpenAiCompatiblePhaseExecutionProvider(
        new HttpClient { Timeout = ReadOpenAiTimeout(timeoutSecondsEnvVar) },
        new OpenAiCompatibleProviderOptions(
            SystemPrompt: Environment.GetEnvironmentVariable(systemPromptEnvVar)
                ?? "You generate SpecForge workflow artifacts. Follow the phase-specific Markdown output contract exactly and do not return JSON.",
            RefinementTolerance: Environment.GetEnvironmentVariable(refinementToleranceEnvVar)
                ?? Environment.GetEnvironmentVariable(legacyRefinementToleranceEnvVar)
                ?? portalSettings?.RefinementTolerance
                ?? "balanced",
            ReviewTolerance: Environment.GetEnvironmentVariable(reviewToleranceEnvVar) ?? portalSettings?.ReviewTolerance ?? "balanced",
            ReviewEvidencePolicy: Environment.GetEnvironmentVariable(reviewEvidencePolicyEnvVar) ?? portalSettings?.ReviewEvidencePolicy ?? "balanced",
            AutoRefinementAnswersEnabled: string.Equals(
                Environment.GetEnvironmentVariable(autoRefinementAnswersEnabledEnvVar)
                    ?? Environment.GetEnvironmentVariable(legacyAutoRefinementAnswersEnabledEnvVar)
                    ?? portalSettings?.AutoRefinementAnswersEnabled.ToString(),
                "true",
                StringComparison.OrdinalIgnoreCase),
            AutoRefinementAnswersProfile: string.IsNullOrWhiteSpace(autoRefinementAnswersProfile)
                ? null
                : autoRefinementAnswersProfile.Trim(),
            ReviewLearningEnabled: !string.Equals(
                Environment.GetEnvironmentVariable(reviewLearningEnabledEnvVar)
                    ?? portalSettings?.ReviewLearningEnabled.ToString(),
                "false",
                StringComparison.OrdinalIgnoreCase),
            ReviewLearningSkillPath: string.IsNullOrWhiteSpace(reviewLearningSkillPath)
                ? ".codex/skills/sdd-phase-agents/SKILL.md"
                : reviewLearningSkillPath.Trim(),
            ModelProfiles: modelProfiles,
            AgentProfiles: agentProfiles,
            PhaseAgentAssignments: phaseAgents,
            PhaseSubagents: new OpenAiCompatiblePhaseSubagentOptions(
                TechnicalDesignEnabled: IsEnabled(technicalDesignSubagentsEnabledEnvVar, portalSettings?.TechnicalDesignSubagentsEnabled),
                ReviewEnabled: IsEnabled(reviewSubagentsEnabledEnvVar, portalSettings?.ReviewSubagentsEnabled))));
}

static bool IsEnabled(string environmentVariable, bool? fallback = null)
{
    var configured = Environment.GetEnvironmentVariable(environmentVariable);
    if (string.IsNullOrWhiteSpace(configured))
    {
        return fallback == true;
    }

    return string.Equals(configured, "true", StringComparison.OrdinalIgnoreCase);
}

static TimeSpan ReadOpenAiTimeout(string timeoutSecondsEnvVar)
{
    var configured = Environment.GetEnvironmentVariable(timeoutSecondsEnvVar);
    if (string.IsNullOrWhiteSpace(configured))
    {
        return TimeSpan.FromMinutes(10);
    }

    if (int.TryParse(configured, out var seconds) && seconds > 0)
    {
        return TimeSpan.FromSeconds(seconds);
    }

    throw new InvalidOperationException(
        $"Environment variable '{timeoutSecondsEnvVar}' must be a positive integer number of seconds.");
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
        .section-copy { margin: -4px 0 4px; color: #9fb0c1; line-height: 1.45; max-width: 78ch; }
        .toolbar { display: flex; gap: 10px; flex-wrap: wrap; margin-top: 18px; }
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
        <h1>SpecForge Configuration</h1>
        <p class="lead">Configure the CLI-served workflow runtime for Codex without depending on the Visual Studio configuration surface.</p>
        <form id="settings-form">
          <section class="panel">
            <h2>Model Profiles</h2>
            <p class="section-copy">Model profiles describe the available model runtimes: provider type, endpoint credentials when needed, model identifier, reasoning effort, and default repository access.</p>
            <div id="models" class="cards"></div>
            <div class="toolbar"><button type="button" class="secondary" id="add-model">Add Model</button></div>
          </section>
          <section class="panel">
            <h2>Agent Profiles</h2>
            <p class="section-copy">Agent profiles define the workflow roles that use those models, including phase-specific instructions and the repository permissions each role is allowed to use.</p>
            <div id="agents" class="cards"></div>
            <div class="toolbar"><button type="button" class="secondary" id="add-agent">Add Agent</button></div>
          </section>
          <section class="panel">
            <h2>Phase Routing</h2>
            <div id="assignments" class="grid"></div>
          </section>
          <section class="panel">
            <h2>Workflow Behavior</h2>
            <div class="grid">
              <label><span class="field-label">Refinement tolerance</span><span class="field-control"><select id="refinementTolerance"><option>strict</option><option>balanced</option><option>inferential</option></select><button class="help-button" type="button" aria-label="Refinement tolerance details" aria-expanded="false" data-help="Controls how much ambiguity refinement tolerates before spec can continue. Strict asks more questions; inferential allows the model to proceed with more assumptions.">?</button></span></label>
              <label><span class="field-label">Review tolerance</span><span class="field-control"><select id="reviewTolerance"><option>strict</option><option>balanced</option><option>inferential</option></select><button class="help-button" type="button" aria-label="Review tolerance details" aria-expanded="false" data-help="Controls how demanding review is before it passes or fails delivered work. Strict requires stronger evidence; inferential is more permissive.">?</button></span></label>
              <label><span class="field-label">Review evidence policy</span><span class="field-control"><select id="reviewEvidencePolicy"><option>strict</option><option>balanced</option><option>release</option><option>advisory</option></select><button class="help-button" type="button" aria-label="Review evidence policy details" aria-expanded="false" data-help="Controls how missing automated, static, operational, or deferred validation evidence affects review readiness.">?</button></span></label>
              <label><span class="field-label">Auto-refinement agent</span><span class="field-control"><select id="autoRefinementAnswersProfile"></select><button class="help-button" type="button" aria-label="Auto-refinement agent details" aria-expanded="false" data-help="Agent used to answer refinement questions automatically before the workflow hands the phase back to the user.">?</button></span></label>
              <label><span class="field-label">Review learning skill path</span><span class="field-control"><input id="reviewLearningSkillPath"><button class="help-button" type="button" aria-label="Review learning skill path details" aria-expanded="false" data-help="Workspace-relative skill file where generalized lessons from failed reviews can be persisted.">?</button></span></label>
              <label><span class="field-label">Max implementation/review cycles</span><span class="field-control"><input id="maxImplementationReviewCycles" type="number" min="1"><button class="help-button" type="button" aria-label="Max implementation/review cycles details" aria-expanded="false" data-help="Maximum implementation attempts allowed in the implementation/review loop before automatic continuation stops.">?</button></span></label>
            </div>
            <div class="toggles" id="toggles"></div>
          </section>
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
          ["reviewLearningEnabled", "Review learning"],
          ["completedUsLockOnCompleted", "Lock completed user stories"]
        ];
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
          "reviewLearningEnabled": "Allows implementation retries after failed review to persist generalized lessons into local skills or prompt guardrails.",
          "completedUsLockOnCompleted": "Keeps completed user stories locked against direct rewind or artifact modification unless explicitly reopened."
        };
        let state = null;

        async function load() {
          const response = await fetch("/api/settings");
          state = await response.json();
          applyDefaultSettings();
          render();
          setStatus("Configuration loaded.");
        }

        function render() {
          renderModels();
          renderAgents();
          renderAssignments();
          renderBehavior();
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
          for (const id of ["refinementTolerance", "reviewTolerance", "reviewEvidencePolicy", "autoRefinementAnswersProfile", "reviewLearningSkillPath", "maxImplementationReviewCycles"]) {
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
          for (const id of ["refinementTolerance", "reviewTolerance", "reviewEvidencePolicy", "autoRefinementAnswersProfile", "reviewLearningSkillPath"]) {
            const element = document.getElementById(id);
            if (element) state[id] = element.value || null;
          }
          state.maxImplementationReviewCycles = Number(document.getElementById("maxImplementationReviewCycles")?.value) || 5;
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
          const response = await fetch("/api/settings", { method: "PUT", headers: { "content-type": "application/json" }, body: JSON.stringify(state) });
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

          if (typeof state.reviewSubagentsEnabled !== "boolean") {
            state.reviewSubagentsEnabled = true;
          }

          if (typeof state.autoPlayEnabled !== "boolean") {
            state.autoPlayEnabled = true;
          }

          if (typeof state.autoReviewEnabled !== "boolean") {
            state.autoReviewEnabled = true;
          }
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
      </script>
    </body>
    </html>
    """;

static string BuildWorkflowPortalHtml(string usId) =>
    $$"""
    <!doctype html>
    <html lang="en">
    <head>
      <meta charset="utf-8">
      <meta name="viewport" content="width=device-width, initial-scale=1">
      <title>SpecForge Workflow · {{EscapeHtml(usId)}}</title>
      <style>
        * { box-sizing: border-box; }
        body { margin: 0; font-family: ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; background: #0f1720; color: #e8eef5; }
        main { max-width: 1180px; margin: 0 auto; padding: 28px; }
        h1 { margin: 0; font-size: 1.8rem; }
        h2 { margin: 0 0 12px; font-size: 1rem; color: #b8c7d6; text-transform: uppercase; letter-spacing: 0.04em; }
        button { border: 0; border-radius: 8px; padding: 10px 14px; background: #1d4f7a; color: white; font-weight: 800; cursor: pointer; }
        .hero { display: grid; gap: 12px; margin-bottom: 18px; }
        .hero-row { display: flex; align-items: center; justify-content: space-between; gap: 14px; flex-wrap: wrap; }
        .meta { display: flex; gap: 8px; flex-wrap: wrap; color: #9fb0c1; }
        .pill { display: inline-flex; align-items: center; min-height: 28px; padding: 4px 10px; border: 1px solid #2a3a4d; border-radius: 999px; background: #121d28; font-size: 0.86rem; }
        .pill--attention { border-color: #866334; background: #332516; color: #ffd88b; }
        .pill--ok { border-color: #2c7a53; background: #123323; color: #9cf0bd; }
        .layout { display: grid; grid-template-columns: minmax(0, 1.35fr) minmax(320px, 0.65fr); gap: 16px; align-items: start; }
        .panel { border: 1px solid #253447; border-radius: 8px; padding: 16px; background: #121d28; }
        .phases { display: grid; gap: 10px; }
        .phase { display: grid; grid-template-columns: 40px minmax(0, 1fr) auto; gap: 12px; align-items: center; padding: 12px; border: 1px solid #2a3a4d; border-radius: 8px; background: #0f1924; }
        .phase--current { border-color: #2f8d60; box-shadow: 0 0 0 1px rgba(47, 141, 96, 0.28); }
        .phase-index { display: grid; place-items: center; width: 32px; height: 32px; border-radius: 50%; background: #26384b; color: #d7e5f2; font-weight: 900; }
        .phase-title { font-weight: 850; color: #e8eef5; }
        .phase-state { color: #9fb0c1; font-size: 0.86rem; }
        .details { display: grid; gap: 12px; }
        .kv { display: grid; grid-template-columns: 150px minmax(0, 1fr); gap: 10px; padding: 8px 0; border-bottom: 1px solid #253447; }
        .kv:last-child { border-bottom: 0; }
        .key { color: #9fb0c1; }
        .value { color: #e8eef5; overflow-wrap: anywhere; }
        .events { display: grid; gap: 10px; max-height: 420px; overflow: auto; }
        .event { padding: 10px 12px; border: 1px solid #2a3a4d; border-radius: 8px; background: #0f1924; }
        .event strong { display: block; margin-bottom: 4px; }
        .event span { color: #9fb0c1; font-size: 0.84rem; }
        .status { min-height: 22px; color: #9fb0c1; }
        @media (max-width: 860px) { main { padding: 18px; } .layout { grid-template-columns: 1fr; } .kv { grid-template-columns: 1fr; gap: 4px; } }
      </style>
    </head>
    <body>
      <main>
        <section class="hero">
          <div class="hero-row">
            <div>
              <h1 id="title">{{EscapeHtml(usId)}} workflow</h1>
              <div class="meta" id="meta"></div>
            </div>
            <button type="button" id="refresh">Refresh</button>
          </div>
          <div class="status" id="status">Loading workflow...</div>
        </section>
        <div class="layout">
          <section class="panel">
            <h2>Phases</h2>
            <div class="phases" id="phases"></div>
          </section>
          <aside class="details">
            <section class="panel">
              <h2>Runtime</h2>
              <div id="runtime"></div>
            </section>
            <section class="panel">
              <h2>Controls</h2>
              <div id="controls"></div>
            </section>
            <section class="panel">
              <h2>Recent Events</h2>
              <div class="events" id="events"></div>
            </section>
          </aside>
        </div>
      </main>
      <script>
        let lastSignature = "";
        async function loadWorkflow() {
          const [workflowResponse, runtimeResponse] = await Promise.all([
            fetch("/api/workflow"),
            fetch("/api/runtime-status")
          ]);

          if (!workflowResponse.ok) {
            throw new Error(await workflowResponse.text());
          }

          const workflow = await workflowResponse.json();
          const runtime = runtimeResponse.ok ? await runtimeResponse.json() : null;
          render(workflow, runtime);
        }

        function render(workflow, runtime) {
          document.getElementById("title").textContent = `${workflow.usId} · ${workflow.title || "Workflow"}`;
          document.getElementById("meta").innerHTML = [
            pill(workflow.status, workflow.status === "completed" ? "ok" : workflow.status === "waiting-user" ? "attention" : ""),
            pill(`current: ${workflow.currentPhase}`),
            workflow.workBranch ? pill(workflow.workBranch) : ""
          ].join("");

          document.getElementById("phases").innerHTML = (workflow.phases || []).map(phase => `
            <article class="phase ${phase.isCurrent ? "phase--current" : ""}">
              <div class="phase-index">${phase.order + 1}</div>
              <div>
                <div class="phase-title">${escapeText(phase.title || phase.phaseId)}</div>
                <div class="phase-state">${escapeText(phase.phaseId)} · ${escapeText(phase.state || "pending")}${phase.isApproved ? " · approved" : ""}</div>
              </div>
              ${phase.requiresApproval ? pill("approval") : ""}
            </article>`).join("");

          const controls = workflow.controls || {};
          document.getElementById("controls").innerHTML = [
            kv("Can continue", controls.canContinue),
            kv("Can approve", controls.canApprove),
            kv("Requires approval", controls.requiresApproval),
            kv("Blocking reason", controls.blockingReason || "none"),
            kv("Execution phase", controls.executionPhase || "none")
          ].join("");

          document.getElementById("runtime").innerHTML = runtime
            ? [
                kv("Status", runtime.status),
                kv("Active operation", runtime.activeOperation || "none"),
                kv("Current phase", runtime.currentPhase || "none"),
                kv("Last outcome", runtime.lastOutcome || "none"),
                kv("Message", runtime.message || "none"),
                kv("Stale", runtime.isStale)
              ].join("")
            : kv("Status", "not available");

          document.getElementById("events").innerHTML = (workflow.events || []).slice(-8).reverse().map(event => `
            <article class="event">
              <strong>${escapeText(event.code || "event")}</strong>
              <span>${escapeText(event.timestampUtc || "")} · ${escapeText(event.actor || "system")} · ${escapeText(event.phase || "workflow")}</span>
              <div>${escapeText(event.summary || "")}</div>
            </article>`).join("");

          const signature = JSON.stringify({
            status: workflow.status,
            currentPhase: workflow.currentPhase,
            controls,
            runtime,
            eventCount: (workflow.events || []).length
          });
          if (lastSignature && lastSignature !== signature) {
            setStatus(`Updated from persisted workflow state at ${new Date().toLocaleTimeString()}.`);
          } else if (!lastSignature) {
            setStatus(`Loaded at ${new Date().toLocaleTimeString()}. Polling for MCP changes.`);
          }
          lastSignature = signature;
        }

        function pill(text, tone = "") {
          return `<span class="pill ${tone ? `pill--${tone}` : ""}">${escapeText(String(text))}</span>`;
        }

        function kv(key, value) {
          return `<div class="kv"><div class="key">${escapeText(key)}</div><div class="value">${escapeText(String(value ?? ""))}</div></div>`;
        }

        function setStatus(message) {
          document.getElementById("status").textContent = message;
        }

        function escapeText(value) {
          return String(value ?? "").replace(/[&<>"']/g, char => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;", "'": "&#39;" }[char]));
        }

        document.getElementById("refresh").addEventListener("click", () => loadWorkflow().catch(error => setStatus(error.message)));
        loadWorkflow().catch(error => setStatus(error.message));
        setInterval(() => loadWorkflow().catch(error => setStatus(error.message)), 1000);
      </script>
    </body>
    </html>
    """;

static string EscapeHtml(string value) =>
    WebUtility.HtmlEncode(value);

internal sealed record SpecForgePortalSettings(
    IReadOnlyList<OpenAiCompatibleModelProfile> ModelProfiles,
    IReadOnlyList<OpenAiCompatibleAgentProfile> AgentProfiles,
    OpenAiCompatiblePhaseAgentAssignments? PhaseAgentAssignments,
    string RefinementTolerance,
    string ReviewTolerance,
    string ReviewEvidencePolicy,
    bool TechnicalDesignSubagentsEnabled,
    bool ReviewSubagentsEnabled,
    bool AutoRefinementAnswersEnabled,
    string? AutoRefinementAnswersProfile,
    bool AutoPlayEnabled,
    bool AutoReviewEnabled,
    int MaxImplementationReviewCycles,
    bool DestructiveRewindEnabled,
    bool PauseOnFailedReview,
    bool ReviewLearningEnabled,
    string ReviewLearningSkillPath,
    bool CompletedUsLockOnCompleted)
{
    public IReadOnlyList<OpenAiCompatibleAgentProfile> ResolveAgentProfiles() =>
        AgentProfiles.Count > 0
            ? AgentProfiles
            : ModelProfiles
                .Select(static profile => new OpenAiCompatibleAgentProfile(
                    Name: profile.Name,
                    Role: profile.Name,
                    ModelProfile: profile.Name,
                    Instructions: string.Empty,
                    RepositoryAccess: profile.RepositoryAccess,
                    ReasoningEffort: profile.ReasoningEffort))
                .ToList();
}

internal static class SpecForgePortalSettingsStore
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private const string SettingsPath = ".specs/configuration/settings.json";

    public static SpecForgePortalSettings? Load(string workspaceRoot)
    {
        var path = GetSettingsPath(workspaceRoot);
        if (!File.Exists(path))
        {
            return null;
        }

        return Deserialize(File.ReadAllText(path));
    }

    public static SpecForgePortalSettings LoadOrDefault(string workspaceRoot) =>
        Load(workspaceRoot) ?? CreateDefault();

    public static SpecForgePortalSettings Deserialize(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var settings = JsonSerializer.Deserialize<SpecForgePortalSettings>(payload, JsonOptions)
            ?? throw new InvalidOperationException("Configuration payload could not be parsed.");

        if (!document.RootElement.TryGetProperty("reviewLearningEnabled", out _))
        {
            settings = settings with { ReviewLearningEnabled = true };
        }

        if (!document.RootElement.TryGetProperty("pauseOnFailedReview", out _))
        {
            settings = settings with { PauseOnFailedReview = true };
        }

        if (!document.RootElement.TryGetProperty("reviewSubagentsEnabled", out _))
        {
            settings = settings with { ReviewSubagentsEnabled = true };
        }

        if (!document.RootElement.TryGetProperty("autoPlayEnabled", out _))
        {
            settings = settings with { AutoPlayEnabled = true };
        }

        if (!document.RootElement.TryGetProperty("autoReviewEnabled", out _))
        {
            settings = settings with { AutoReviewEnabled = true };
        }

        return settings;
    }

    public static void Save(string workspaceRoot, SpecForgePortalSettings settings)
    {
        var path = GetSettingsPath(workspaceRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, JsonOptions));
    }

    private static SpecForgePortalSettings CreateDefault() =>
        new(
            ModelProfiles: [],
            AgentProfiles: [],
            PhaseAgentAssignments: new OpenAiCompatiblePhaseAgentAssignments(),
            RefinementTolerance: "balanced",
            ReviewTolerance: "balanced",
            ReviewEvidencePolicy: "balanced",
            TechnicalDesignSubagentsEnabled: false,
            ReviewSubagentsEnabled: true,
            AutoRefinementAnswersEnabled: false,
            AutoRefinementAnswersProfile: null,
            AutoPlayEnabled: true,
            AutoReviewEnabled: true,
            MaxImplementationReviewCycles: 5,
            DestructiveRewindEnabled: false,
            PauseOnFailedReview: true,
            ReviewLearningEnabled: true,
            ReviewLearningSkillPath: ".codex/skills/sdd-phase-agents/SKILL.md",
            CompletedUsLockOnCompleted: false);

    private static string GetSettingsPath(string workspaceRoot) =>
        Path.Combine(workspaceRoot, SettingsPath);
}
