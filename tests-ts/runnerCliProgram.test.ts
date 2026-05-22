import test from "node:test";
import assert from "node:assert/strict";
import * as fs from "node:fs";
import * as path from "node:path";

const programPath = path.join(process.cwd(), "src", "SpecForge.Runner.Cli", "Program.cs");
const renderCachePath = path.join(process.cwd(), "src", "SpecForge.Runner.Cli", "WorkflowPortalRenderCache.cs");
const extensionPath = path.join(process.cwd(), "src-vscode", "extension.ts");
const packagedExtensionPath = path.join(process.cwd(), "plugins", "specforge-ai", "mcp", "dist", "extension.js");

test("CLI phase commands route through the application service", async () => {
  const source = await fs.promises.readFile(programPath, "utf8");

  assert.match(source, /case "create-us":[\s\S]*?CreateApplicationService\(args\)/);
  assert.match(source, /case "import-us":[\s\S]*?CreateApplicationService\(args\)/);
  assert.match(source, /case "continue-phase":[\s\S]*?CreateApplicationService\(args\)/);
  assert.match(source, /case "approve-phase":[\s\S]*?CreateApplicationService\(args\)/);
  assert.match(source, /GenerateNextPhaseAsync\(workspaceRoot, usId, "cli-user"\)/);
  assert.match(source, /ApprovePhaseAsync\([\s\S]*?"cli-user"\)/);
  assert.doesNotMatch(source, /HandleContinuePhaseAsync\(WorkflowRunner/);
  assert.doesNotMatch(source, /HandleApprovePhaseAsync\(\s*WorkflowRunner/);
});

test("CLI workflow portal exposes refinement answer submission endpoint", async () => {
  const source = await fs.promises.readFile(programPath, "utf8");

  assert.match(source, /case \("POST", "\/api\/refinement-answers"\):/);
  assert.match(source, /JsonSerializer\.Deserialize<RefinementAnswersSubmitRequest>/);
  assert.match(source, /SubmitRefinementAnswersAsync\([\s\S]*?request\.Answers[\s\S]*?request\.Actor \?\? "cli-user"/);
  assert.match(source, /internal sealed record RefinementAnswersSubmitRequest\(IReadOnlyList<string> Answers, string\? Actor\);/);
});

test("CLI workflow portal uses a distinct default port and caches rendered workflow HTML", async () => {
  const source = await fs.promises.readFile(programPath, "utf8");
  const renderCacheSource = await fs.promises.readFile(renderCachePath, "utf8");

  assert.match(source, /serve-workflow[\s\S]*?"http:\/\/localhost:5128\/"/);
  assert.doesNotMatch(source, /serve-configuration/);
  assert.match(source, /Expected workspace root, optional user story id, and optional URL prefix for command 'serve-workflow'\./);
  assert.match(source, /ResolveDefaultWorkflowPortalUserStoryIdAsync\(applicationService, workspaceRoot\)/);
  assert.match(source, /LooksLikeHttpPrefix\(args\[2\]\)/);
  assert.match(source, /var renderCache = new WorkflowPortalRenderCache\(\)/);
  assert.match(source, /var renderCacheSignature = BuildWorkflowPortalRenderCacheSignature\(/);
  assert.match(source, /var cachePhaseId = resolvedSelectedPhaseId \?\? "__none__"/);
  assert.match(source, /renderCache\.TryGet\(renderCacheSignature, cachePhaseId, selectedPhase, out var cachedHtml\)/);
  assert.match(source, /renderCache\.Store\(renderCacheSignature, cachePhaseId, selectedPhase, html\)/);
  assert.match(renderCacheSource, /ConcurrentDictionary<string, CacheEntry> entries/);
  assert.match(renderCacheSource, /File\.GetLastWriteTimeUtc\(path\)\.Ticks/);
  assert.match(renderCacheSource, /private void Trim\(\)/);
});

test("VS Code command opens the CLI workflow portal on the workflow port", async () => {
  const source = await fs.promises.readFile(extensionPath, "utf8");
  const packagedSource = await fs.promises.readFile(packagedExtensionPath, "utf8");

  assert.match(source, /openCliWorkflowPortal:[\s\S]*?const url = "http:\/\/localhost:5128\/"/);
  assert.match(packagedSource, /openCliWorkflowPortal:[\s\S]*?const url = "http:\/\/localhost:5128\/"/);
  assert.match(source, /serve-workflow "\$\{workspaceRoot\}" "\$\{usId\}" "\$\{url\}"/);
  assert.match(packagedSource, /serve-workflow "\$\{workspaceRoot\}" "\$\{usId\}" "\$\{url\}"/);
  assert.doesNotMatch(source, /openCliWorkflowPortal:[\s\S]*?const url = "http:\/\/localhost:5127\/"/);
  assert.doesNotMatch(packagedSource, /openCliWorkflowPortal:[\s\S]*?const url = "http:\/\/localhost:5127\/"/);
});

test("CLI workflow portal refresh signature includes workflow runtime versions", async () => {
  const source = await fs.promises.readFile(programPath, "utf8");

  assert.match(source, /workflowCreatedWithRuntimeVersion = workflow\?\.CreatedWithRuntimeVersion/);
  assert.match(source, /workflowLastRuntimeVersion = workflow\?\.LastRuntimeVersion/);
  assert.match(source, /runtimeVersion = GetRuntimeVersion\(\) \?\? workflow\?\.LastRuntimeVersion \?\? workflow\?\.CreatedWithRuntimeVersion/);
  assert.match(source, /typeof\(SpecForgeApplicationService\)\.Assembly\.GetName\(\)\.Version\?\.ToString\(\)/);
  assert.match(source, /BuildWorkflowSignature\(\s*UserStoryWorkflowDetails\? workflow,\s*IReadOnlyCollection<UserStorySummary> userStories,\s*IReadOnlyCollection<UserStorySummary> droppedUserStories,\s*string workflowGraphLayoutSignature\s*\)/);
  assert.match(source, /userStories = userStories[\s\S]*?story\.CurrentPhase[\s\S]*?story\.Status/);
  assert.match(source, /droppedUserStories = droppedUserStories[\s\S]*?story\.CurrentPhase[\s\S]*?story\.Status/);
});

test("CLI workflow portal payload includes sidebar stories and configuration URL", async () => {
  const source = await fs.promises.readFile(programPath, "utf8");

  assert.match(source, /activeSidebarUserStories = await applicationService\.ListUserStoriesAsync\(workspaceRoot\)/);
  assert.match(source, /droppedSidebarUserStories = await applicationService\.ListUserStoriesAsync\(workspaceRoot, "dropped"\)/);
  assert.match(source, /GetUserStoryWorkflowAsync\(workspaceRoot, usId\)/);
  assert.doesNotMatch(source, /resolvedUsId = ResolveSidebarVisibleUserStoryId\(usId, sidebarUserStories\)/);
  assert.match(source, /userStories = activeSidebarUserStories/);
  assert.match(source, /activeSidebarUserStories/);
  assert.match(source, /droppedSidebarUserStories/);
  assert.match(source, /configurationPortalUrl = BuildConfigurationPortalUrl\(workflowPortalOrigin\)/);
  assert.match(source, /configurationProvidersUrl = BuildConfigurationPortalUrl\(workflowPortalOrigin, "providers"\)/);
  assert.match(source, /configurationAdvancedUrl = BuildConfigurationPortalUrl\(workflowPortalOrigin, "advanced"\)/);
  assert.match(source, /requestSidebarVisibility = ResolveWorkflowPortalSidebarVisibility\(context\.Request\)/);
  assert.match(source, /BuildWorkflowPortalSignatureAsync\([\s\S]*?requestUsId,[\s\S]*?requestSidebarVisibility,[\s\S]*?requestShowCompletedUserStories,[\s\S]*?requestShowBlockedUserStories\)/);
  assert.match(source, /requestShowCompletedUserStories = ResolveWorkflowPortalQueryFlag\(context\.Request, "sidebarCompleted"\)/);
  assert.match(source, /requestShowBlockedUserStories = ResolveWorkflowPortalQueryFlag\(context\.Request, "sidebarBlocked"\)/);
  assert.match(source, /requestShowHiddenUserStories = ResolveWorkflowPortalQueryFlag\(context\.Request, "sidebarHiddenVisible"\)/);
  assert.match(source, /requestIncludeOtherOwners = ResolveWorkflowPortalQueryFlag\(context\.Request, "sidebarOtherOwners"\)/);
  assert.match(source, /requestSidebarWatchingUserStoryIds = ResolveWorkflowPortalUserStoryIdList\(context\.Request, "sidebarWatching"\)/);
  assert.match(source, /requestSidebarHiddenUserStoryIds = ResolveWorkflowPortalUserStoryIdList\(context\.Request, "sidebarHidden"\)/);
  assert.match(source, /static string\? ResolveWorkflowPortalSidebarVisibility\(HttpListenerRequest request\)/);
  assert.match(source, /static bool ResolveWorkflowPortalQueryFlag\(HttpListenerRequest request, string key\)/);
  assert.match(source, /static IReadOnlyList<string> ResolveWorkflowPortalUserStoryIdList\(HttpListenerRequest request, string key\)/);
  assert.match(source, /ParseQueryValue\(referer\.Query, "sidebarVisibility"\)/);
  assert.match(source, /NormalizeUserStoryIds\(queryValue\)/);
  assert.match(source, /case \("GET", "\/configuration"\):/);
  assert.match(source, /case \("GET", "\/api\/settings"\):/);
  assert.match(source, /case \("PUT", "\/api\/settings"\):/);
  assert.match(source, /fetch\("api\/settings"/);
  assert.doesNotMatch(source, /fetch\("\/api\/settings"/);
  assert.match(source, /Path = "\/configuration"/);
  assert.match(source, /<span class="runtime-version">v\.__RUNTIME_VERSION__<\/span>/);
  assert.match(source, /Replace\("__RUNTIME_VERSION__", WebUtility\.HtmlEncode\(GetRuntimeVersion\(\) \?\? "unknown"\), StringComparison\.Ordinal\)/);
  assert.match(source, /<button class="tab-button"[\s\S]*?data-tab-target="providers">Models<\/button>/);
  assert.match(source, /<button class="tab-button"[\s\S]*?data-tab-target="advanced">Client Basics<\/button>/);
  assert.match(source, /<button class="tab-button"[\s\S]*?data-tab-target="central">SpecForge Central<\/button>/);
  assert.match(source, /<div class="tab-panel" id="providers" role="tabpanel">/);
  assert.match(source, /<div class="tab-panel" id="advanced" role="tabpanel" hidden>/);
  assert.match(source, /<div class="tab-panel" id="central" role="tabpanel" hidden>/);
  assert.match(source, /const configurationTabs = \["providers", "advanced", "central"\]/);
  assert.match(source, /ResolveWorkflowPortalUserStoryIdAsync\(\s*applicationService,\s*workspaceRoot,\s*context\.Request,\s*requestSidebarVisibility,/);
  assert.match(source, /ResolveVisibleWorkflowPortalUserStoryIdAsync\(/);
  assert.match(source, /IsWorkflowPortalStoryVisible\(/);
  assert.match(source, /if \(string\.IsNullOrWhiteSpace\(requestUsId\)\)[\s\S]*?"No selected user story\."/);
});

test("CLI workflow portal infers a visible user story when possible and allows a no-selection state otherwise", async () => {
  const source = await fs.promises.readFile(programPath, "utf8");

  assert.match(source, /if \(!string\.IsNullOrWhiteSpace\(requestedUsId\) && availableStoryById\.TryGetValue\(requestedUsId\.Trim\(\), out var requestedStory\)\)/);
  assert.match(source, /var firstVisible = availableStories[\s\S]*?IsWorkflowPortalStoryVisible/);
  assert.match(source, /if \(firstVisible is not null\)[\s\S]*?return firstVisible\.UsId;/);
  assert.match(source, /if \(availableStories\.Count == 0\)[\s\S]*?return null;/);
  assert.match(source, /if \(preferExplicitSelection && !string\.IsNullOrWhiteSpace\(requestedUsId\)\)[\s\S]*?return null;/);
  assert.match(source, /if \(!showCompletedUserStories && string\.Equals\(story\.Status, "completed", StringComparison\.OrdinalIgnoreCase\)\)/);
  assert.match(source, /if \(!showBlockedUserStories && isBlocked\)/);
  assert.match(source, /if \(!showHiddenUserStories && hiddenUserStoryIds\.Contains\(story\.UsId\)\)/);
  assert.match(source, /return includeOtherOwners[\s\S]*?\|\| watchingUserStoryIds\.Contains\(story\.UsId\)/);
  assert.match(source, /ResolveWorkflowPortalNoSelectionReason\(usId, sidebarUserStories\)/);
});

test("CLI workflow portal signature ignores local sidebar visibility", async () => {
  const source = await fs.promises.readFile(programPath, "utf8");

  assert.match(source, /BuildWorkflowPortalSignatureAsync\(\s*SpecForgeApplicationService applicationService,\s*string workspaceRoot,\s*string\? usId,\s*string\? sidebarVisibility,\s*bool showCompletedUserStories,\s*bool showBlockedUserStories\s*\)/);
  assert.match(source, /var workflow = string\.IsNullOrWhiteSpace\(usId\)[\s\S]*?await applicationService\.GetUserStoryWorkflowAsync\(workspaceRoot, usId\)/);
  assert.doesNotMatch(source, /resolvedUsId = ResolveSidebarVisibleUserStoryId\(usId, sidebarUserStories\)/);
  assert.match(source, /BuildWorkflowSignature\([\s\S]*?activeSidebarUserStories,[\s\S]*?droppedSidebarUserStories,[\s\S]*?workflowGraphLayoutSignature\)/);
});

test("CLI workflow portal exposes reset and lineage endpoints for sidebar actions", async () => {
  const source = await fs.promises.readFile(programPath, "utf8");

  assert.match(source, /case \("POST", "\/api\/reset-user-story-to-capture"\):/);
  assert.match(source, /case \("POST", "\/api\/analyze-user-story-lineage"\):/);
  assert.match(source, /case \("POST", "\/api\/repair-user-story-lineage"\):/);
  assert.match(source, /JsonSerializer\.Deserialize<UserStoryActionRequest>/);
  assert.match(source, /ResetUserStoryToCaptureAsync\(workspaceRoot, request\.UsId\)/);
  assert.match(source, /AnalyzeUserStoryLineageAsync\(workspaceRoot, request\.UsId\)/);
  assert.match(source, /RepairUserStoryLineageAsync\(workspaceRoot, request\.UsId, request\.Actor \?\? "cli-user"\)/);
  assert.match(source, /internal sealed record UserStoryActionRequest\(string UsId, string\? Actor\);/);
});

test("CLI workflow portal exposes user-story metadata update endpoint for sidebar editing", async () => {
  const source = await fs.promises.readFile(programPath, "utf8");

  assert.match(source, /case \("POST", "\/api\/update-user-story-info"\):/);
  assert.match(source, /JsonSerializer\.Deserialize<UpdateUserStoryInfoRequest>/);
  assert.match(source, /UpdateUserStoryInfoAsync\([\s\S]*?request\.UsId,[\s\S]*?request\.Title,[\s\S]*?request\.Kind,[\s\S]*?request\.Owner,[\s\S]*?request\.Category,[\s\S]*?request\.Tags,[\s\S]*?request\.Actor \?\? ResolveCurrentGitOwner\(workspaceRoot\)/);
  assert.match(source, /internal sealed record UpdateUserStoryInfoRequest\(\s*string UsId,\s*string\? Title,\s*string\? Kind,\s*string\? Owner,\s*string\? Category,\s*IReadOnlyList<string>\? Tags,\s*string\? Actor\);/);
});

test("CLI writes JSON with web serializer options for record responses", async () => {
  const source = await fs.promises.readFile(programPath, "utf8");

  assert.match(source, /JsonSerializer\.Serialize\(payload, SpecForgePortalSettingsStore\.JsonOptions\)/);
  assert.doesNotMatch(source, /Console\.WriteLine\(JsonSerializer\.Serialize\(payload\)\)/);
});
