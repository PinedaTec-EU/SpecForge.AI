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
  assert.match(source, /renderCache\.TryGet\(signature, resolvedSelectedPhaseId, selectedPhase, out var cachedHtml\)/);
  assert.match(source, /renderCache\.Store\(signature, resolvedSelectedPhaseId, selectedPhase, html\)/);
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

  assert.match(source, /workflow\.CreatedWithRuntimeVersion/);
  assert.match(source, /workflow\.LastRuntimeVersion/);
  assert.match(source, /BuildWorkflowSignature\(\s*UserStoryWorkflowDetails workflow,\s*IReadOnlyCollection<UserStorySummary> userStories,\s*string sidebarVisibility,\s*bool showCompletedUserStories,\s*IReadOnlyCollection<UserStorySummary> sidebarUserStories,\s*int droppedUserStoryCount\s*\)/);
  assert.match(source, /userStories = userStories[\s\S]*?story\.CurrentPhase[\s\S]*?story\.Status/);
  assert.match(source, /sidebarVisibility/);
  assert.match(source, /showCompletedUserStories/);
  assert.match(source, /droppedUserStoryCount/);
  assert.match(source, /sidebarUserStories = sidebarUserStories[\s\S]*?story\.CurrentPhase[\s\S]*?story\.Status/);
});

test("CLI workflow portal payload includes sidebar stories and configuration URL", async () => {
  const source = await fs.promises.readFile(programPath, "utf8");

  assert.match(source, /activeSidebarUserStories = await applicationService\.ListUserStoriesAsync\(workspaceRoot\)/);
  assert.match(source, /droppedSidebarUserStories = await applicationService\.ListUserStoriesAsync\(workspaceRoot, "dropped"\)/);
  assert.match(source, /resolvedUsId = ResolveSidebarVisibleUserStoryId\(usId, sidebarUserStories\)/);
  assert.match(source, /GetUserStoryWorkflowAsync\(workspaceRoot, resolvedUsId\)/);
  assert.match(source, /userStories = activeSidebarUserStories/);
  assert.match(source, /activeSidebarUserStories/);
  assert.match(source, /droppedSidebarUserStories/);
  assert.match(source, /configurationPortalUrl = BuildConfigurationPortalUrl\(workflowPortalOrigin\)/);
  assert.match(source, /configurationProvidersUrl = BuildConfigurationPortalUrl\(workflowPortalOrigin, "providers"\)/);
  assert.match(source, /configurationAdvancedUrl = BuildConfigurationPortalUrl\(workflowPortalOrigin, "advanced"\)/);
  assert.match(source, /requestSidebarVisibility = ResolveWorkflowPortalSidebarVisibility\(context\.Request\)/);
  assert.match(source, /BuildWorkflowPortalSignatureAsync\([\s\S]*?requestUsId,[\s\S]*?requestSidebarVisibility,[\s\S]*?requestShowCompletedUserStories\)/);
  assert.match(source, /requestShowCompletedUserStories = string\.Equals\(/);
  assert.match(source, /context\.Request\.QueryString\["sidebarCompleted"\]/);
  assert.match(source, /static string\? ResolveWorkflowPortalSidebarVisibility\(HttpListenerRequest request\)/);
  assert.match(source, /ParseQueryValue\(referer\.Query, "sidebarVisibility"\)/);
  assert.match(source, /case \("GET", "\/configuration"\):/);
  assert.match(source, /case \("GET", "\/api\/settings"\):/);
  assert.match(source, /case \("PUT", "\/api\/settings"\):/);
  assert.match(source, /fetch\("api\/settings"/);
  assert.doesNotMatch(source, /fetch\("\/api\/settings"/);
  assert.match(source, /Path = "\/configuration"/);
  assert.match(source, /<section class="panel" id="providers">/);
  assert.match(source, /<section class="panel" id="advanced">/);
  assert.match(source, /ResolveWorkflowPortalUserStoryId\(context\.Request, usId\)/);
});

test("CLI workflow portal signature uses the current sidebar visibility", async () => {
  const source = await fs.promises.readFile(programPath, "utf8");

  assert.match(source, /BuildWorkflowPortalSignatureAsync\(\s*SpecForgeApplicationService applicationService,\s*string workspaceRoot,\s*string usId,\s*string\? sidebarVisibility,\s*bool showCompletedUserStories\s*\)/);
  assert.match(source, /normalizedSidebarVisibility = string\.Equals\(sidebarVisibility, "dropped", StringComparison\.OrdinalIgnoreCase\)/);
  assert.match(source, /sidebarUserStories = normalizedSidebarVisibility == "dropped"[\s\S]*?\? droppedSidebarUserStories[\s\S]*?: activeSidebarUserStories/);
  assert.match(source, /resolvedUsId = ResolveSidebarVisibleUserStoryId\(usId, sidebarUserStories\)/);
  assert.match(source, /GetUserStoryWorkflowAsync\(workspaceRoot, resolvedUsId\)/);
  assert.match(source, /BuildWorkflowSignature\([\s\S]*?activeSidebarUserStories,[\s\S]*?normalizedSidebarVisibility,[\s\S]*?showCompletedUserStories,[\s\S]*?sidebarUserStories,[\s\S]*?droppedSidebarUserStories\.Count\)/);
  assert.match(source, /showCompletedUserStories,/);
});

test("CLI writes JSON with web serializer options for record responses", async () => {
  const source = await fs.promises.readFile(programPath, "utf8");

  assert.match(source, /JsonSerializer\.Serialize\(payload, SpecForgePortalSettingsStore\.JsonOptions\)/);
  assert.doesNotMatch(source, /Console\.WriteLine\(JsonSerializer\.Serialize\(payload\)\)/);
});
