import test from "node:test";
import assert from "node:assert/strict";
import * as fs from "node:fs/promises";
import * as os from "node:os";
import * as path from "node:path";

test("aggregate workflow layouts persist per user story with global fallback preserved", async () => {
  const Module = require("node:module") as {
    _load: (request: string, parent: unknown, isMain: boolean) => unknown;
  };
  const originalLoad = Module._load;
  Module._load = function patchedLoad(request: string, parent: unknown, isMain: boolean) {
    if (request === "vscode") {
      return {
        window: {
          createOutputChannel: () => ({
            appendLine: () => undefined,
            show: () => undefined
          })
        }
      };
    }

    return originalLoad.call(this, request, parent, isMain);
  };

  const workspaceRoot = await fs.mkdtemp(path.join(os.tmpdir(), "specforge-graph-layout-"));
  const {
    getWorkflowGraphLayoutPath,
    readWorkflowGraphLayoutConfigAsync,
    updateAggregateWorkflowGraphLayoutAsync,
    updateWorkflowGraphLayoutModeOverrideAsync,
    updateWorkflowGraphLegendPositionAsync
  } = require("../src-vscode/workflowGraphLayout") as typeof import("../src-vscode/workflowGraphLayout");

  try {
    await updateAggregateWorkflowGraphLayoutAsync(workspaceRoot, {
      positions: {
        capture: { x: 10, y: 20 },
        refinement: { x: 30, y: 40 },
        spec: { x: 50, y: 60 },
        split: { x: 70, y: 80 }
      },
      spacing: {
        horizontalPadding: 10,
        topRowTop: 20,
        topRowGap: 30,
        rowGap: 40,
        childGap: 50,
        maxChildrenPerRow: 3
      }
    });

    await updateAggregateWorkflowGraphLayoutAsync(workspaceRoot, {
      positions: {
        capture: { x: 101, y: 202 },
        refinement: { x: 303, y: 404 },
        spec: { x: 505, y: 606 },
        split: { x: 707, y: 808 }
      },
      spacing: {
        horizontalPadding: 11,
        topRowTop: 22,
        topRowGap: 33,
        rowGap: 44,
        childGap: 55,
        maxChildrenPerRow: 4
      }
    }, "US-0015");
    await updateWorkflowGraphLayoutModeOverrideAsync(workspaceRoot, "US-0015", "horizontal");
    await updateWorkflowGraphLegendPositionAsync(workspaceRoot, "vertical", { x: 222, y: 333 });

    const config = await readWorkflowGraphLayoutConfigAsync(workspaceRoot);
    assert.deepEqual(config.aggregate.positions.capture, { x: 10, y: 20 });
    assert.deepEqual(config.aggregateUserStories["US-0015"]?.positions.capture, { x: 101, y: 202 });
    assert.equal(config.aggregateUserStories["US-0015"]?.spacing.maxChildrenPerRow, 4);
    assert.equal(config.userStoryLayoutModes["US-0015"], "horizontal");
    assert.deepEqual(config.legend.vertical, { x: 222, y: 333 });

    const raw = await fs.readFile(getWorkflowGraphLayoutPath(workspaceRoot), "utf8");
    assert.match(raw, /userStoryLayoutModes:/);
    assert.match(raw, /US-0015: horizontal/);
    assert.match(raw, /aggregateUserStories:/);
    assert.match(raw, /US-0015:/);
    assert.match(raw, /capture:\n\s+x: 101\n\s+y: 202/);
    assert.match(raw, /vertical:[\s\S]*?legend:\n\s+x: 222\n\s+y: 333/);
  } finally {
    Module._load = originalLoad;
  }
});
