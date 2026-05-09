import test from "node:test";
import assert from "node:assert/strict";
import * as fs from "node:fs";
import * as path from "node:path";

const programPath = path.join(process.cwd(), "src", "SpecForge.Runner.Cli", "Program.cs");

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

  assert.match(source, /serve-configuration[\s\S]*?"http:\/\/localhost:5127\/"/);
  assert.match(source, /serve-workflow[\s\S]*?"http:\/\/localhost:5128\/"/);
  assert.match(source, /ConcurrentDictionary<string, string> renderCache/);
  assert.match(source, /renderCache\.TryGetValue\(cacheKey, out var cachedHtml\)/);
  assert.match(source, /BuildWorkflowPortalCacheKey\(signature, resolvedSelectedPhaseId, selectedPhase\)/);
  assert.match(source, /File\.GetLastWriteTimeUtc\(path\)\.Ticks/);
  assert.match(source, /TrimWorkflowPortalRenderCache\(renderCache\)/);
});

test("CLI writes JSON with web serializer options for record responses", async () => {
  const source = await fs.promises.readFile(programPath, "utf8");

  assert.match(source, /JsonSerializer\.Serialize\(payload, SpecForgePortalSettingsStore\.JsonOptions\)/);
  assert.doesNotMatch(source, /Console\.WriteLine\(JsonSerializer\.Serialize\(payload\)\)/);
});
