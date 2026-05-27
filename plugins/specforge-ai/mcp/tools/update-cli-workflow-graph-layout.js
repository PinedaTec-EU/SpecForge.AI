#!/usr/bin/env node
"use strict";

const fs = require("node:fs");
const {
  readWorkflowGraphLayoutConfigAsync,
  updateAggregateWorkflowGraphLayoutAsync,
  updateWorkflowGraphLayoutModeOverrideAsync,
  updateWorkflowGraphLayoutPositionsAsync,
  updateWorkflowGraphLegendPositionAsync
} = require("./workflow-graph-layout-portable");

async function main() {
  const payload = JSON.parse(fs.readFileSync(0, "utf8"));
  const workspaceRoot = String(payload.workspaceRoot || "").trim();
  if (!workspaceRoot) {
    throw new Error("workspaceRoot is required.");
  }

  const layoutMode = payload.layoutMode === "horizontal" ? "horizontal" : "vertical";
  if (payload.layoutKind === "aggregate" && payload.aggregate) {
    await updateAggregateWorkflowGraphLayoutAsync(
      workspaceRoot,
      payload.aggregate,
      typeof payload.userStoryId === "string" ? payload.userStoryId : undefined
    );
  } else if (payload.positions && typeof payload.positions === "object") {
    await updateWorkflowGraphLayoutPositionsAsync(workspaceRoot, layoutMode, payload.positions);
  }

  if (payload.layoutKind !== "aggregate" && typeof payload.userStoryId === "string" && payload.userStoryId.trim()) {
    await updateWorkflowGraphLayoutModeOverrideAsync(workspaceRoot, payload.userStoryId, layoutMode);
  }

  if (payload.legendPosition && typeof payload.legendPosition === "object") {
    await updateWorkflowGraphLegendPositionAsync(workspaceRoot, layoutMode, payload.legendPosition);
  }

  const config = await readWorkflowGraphLayoutConfigAsync(workspaceRoot);
  process.stdout.write(JSON.stringify(config));
}

main().catch((error) => {
  process.stderr.write(error instanceof Error ? (error.stack || error.message) : String(error));
  process.exit(1);
});
