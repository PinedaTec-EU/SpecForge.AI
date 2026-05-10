import test from "node:test";
import assert from "node:assert/strict";
import * as fs from "node:fs";
import * as path from "node:path";

const programPath = path.join(process.cwd(), "src", "SpecForge.Runner.Cli", "Program.cs");
const renderCachePath = path.join(process.cwd(), "src", "SpecForge.Runner.Cli", "WorkflowPortalRenderCache.cs");

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

  assert.match(source, /serve-configuration[\s\S]*?"http:\/\/localhost:5127\/"/);
  assert.match(source, /serve-workflow[\s\S]*?"http:\/\/localhost:5128\/"/);
  assert.match(source, /var renderCache = new WorkflowPortalRenderCache\(\)/);
  assert.match(source, /renderCache\.TryGet\(signature, resolvedSelectedPhaseId, selectedPhase, out var cachedHtml\)/);
  assert.match(source, /renderCache\.Store\(signature, resolvedSelectedPhaseId, selectedPhase, html\)/);
  assert.match(renderCacheSource, /ConcurrentDictionary<string, CacheEntry> entries/);
  assert.match(renderCacheSource, /File\.GetLastWriteTimeUtc\(path\)\.Ticks/);
  assert.match(renderCacheSource, /private void Trim\(\)/);
});

test("CLI workflow portal refresh signature includes workflow runtime versions", async () => {
  const source = await fs.promises.readFile(programPath, "utf8");

  assert.match(source, /workflow\.CreatedWithRuntimeVersion/);
  assert.match(source, /workflow\.LastRuntimeVersion/);
  assert.match(source, /BuildWorkflowSignature\(\s*UserStoryWorkflowDetails workflow,\s*IReadOnlyCollection<UserStorySummary> userStories\s*\)/);
  assert.match(source, /userStories = userStories[\s\S]*?story\.CurrentPhase[\s\S]*?story\.Status/);
});

test("CLI workflow portal payload includes sidebar stories and configuration URL", async () => {
  const source = await fs.promises.readFile(programPath, "utf8");

  assert.match(source, /userStories = await applicationService\.ListUserStoriesAsync\(workspaceRoot\)/);
  assert.match(source, /configurationPortalUrl = BuildConfigurationPortalUrl\(workflowPortalOrigin\)/);
  assert.match(source, /configurationProvidersUrl = BuildConfigurationPortalUrl\(workflowPortalOrigin, "providers"\)/);
  assert.match(source, /configurationAdvancedUrl = BuildConfigurationPortalUrl\(workflowPortalOrigin, "advanced"\)/);
  assert.match(source, /<section class="panel" id="providers">/);
  assert.match(source, /<section class="panel" id="advanced">/);
  assert.match(source, /ResolveWorkflowPortalUserStoryId\(context\.Request, usId\)/);
});

test("CLI writes JSON with web serializer options for record responses", async () => {
  const source = await fs.promises.readFile(programPath, "utf8");

  assert.match(source, /JsonSerializer\.Serialize\(payload, SpecForgePortalSettingsStore\.JsonOptions\)/);
  assert.doesNotMatch(source, /Console\.WriteLine\(JsonSerializer\.Serialize\(payload\)\)/);
});
