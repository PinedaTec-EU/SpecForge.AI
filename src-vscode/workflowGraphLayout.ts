import * as fs from "node:fs";
import * as path from "node:path";

export type WorkflowGraphLayoutMode = "horizontal" | "vertical";
export type WorkflowGraphPhaseId =
  | "capture"
  | "refinement"
  | "spec"
  | "technical-design"
  | "implementation"
  | "review"
  | "release-approval"
  | "pr-preparation"
  | "completed";

export interface WorkflowGraphPhasePosition {
  readonly x: number;
  readonly y: number;
}

export interface WorkflowGraphLegendPosition {
  readonly x: number;
  readonly y: number;
}

export interface WorkflowGraphEdgeConnection {
  readonly from: string;
  readonly to: string;
}

export type WorkflowGraphLoopSide = "top" | "right" | "bottom" | "left";

export interface WorkflowGraphLoopDefinition {
  readonly fromPhaseId: WorkflowGraphPhaseId;
  readonly toPhaseId: WorkflowGraphPhaseId;
  readonly side: WorkflowGraphLoopSide;
}

export type AggregateWorkflowGraphAnchorId = "capture" | "refinement" | "spec" | "split";

export interface AggregateWorkflowGraphLayoutConfig {
  readonly positions: Record<AggregateWorkflowGraphAnchorId, WorkflowGraphPhasePosition>;
  readonly spacing: {
    readonly horizontalPadding: number;
    readonly topRowTop: number;
    readonly topRowGap: number;
    readonly rowGap: number;
    readonly childGap: number;
    readonly maxChildrenPerRow: number;
  };
}

export type AggregateWorkflowGraphLayoutByUserStory = Readonly<Record<string, AggregateWorkflowGraphLayoutConfig>>;

export interface WorkflowGraphLayoutConfig {
  readonly horizontal: Record<string, WorkflowGraphPhasePosition>;
  readonly vertical: Record<string, WorkflowGraphPhasePosition>;
  readonly legend: {
    readonly horizontal: WorkflowGraphLegendPosition;
    readonly vertical: WorkflowGraphLegendPosition;
  };
  readonly connections: {
    readonly horizontal: Record<string, WorkflowGraphEdgeConnection>;
    readonly vertical: Record<string, WorkflowGraphEdgeConnection>;
  };
  readonly loops: {
    readonly horizontal: Record<string, WorkflowGraphLoopDefinition>;
    readonly vertical: Record<string, WorkflowGraphLoopDefinition>;
  };
  readonly aggregate: AggregateWorkflowGraphLayoutConfig;
  readonly aggregateUserStories: AggregateWorkflowGraphLayoutByUserStory;
}

const workflowGraphPhaseIds: readonly WorkflowGraphPhaseId[] = [
  "capture",
  "refinement",
  "spec",
  "technical-design",
  "implementation",
  "review",
  "release-approval",
  "pr-preparation",
  "completed"
] as const;

const aggregateWorkflowGraphAnchorIds: readonly AggregateWorkflowGraphAnchorId[] = [
  "capture",
  "refinement",
  "spec",
  "split"
] as const;

// Keep this comment aligned with workflowView.ts card constants.
// Card dimensions used by the renderer: desktop 240x118, mobile 206x118.
export const defaultHorizontalWorkflowGraphPositions: Record<string, WorkflowGraphPhasePosition> = {
  capture: { x: 80, y: 120 },
  refinement: { x: 390, y: 120 },
  spec: { x: 700, y: 120 },
  "technical-design": { x: 1010, y: 120 },
  implementation: { x: 1010, y: 340 },
  review: { x: 1010, y: 560 },
  "release-approval": { x: 1320, y: 560 },
  "pr-preparation": { x: 1630, y: 560 },
  completed: { x: 1940, y: 560 }
};

export const defaultVerticalWorkflowGraphPositions: Record<string, WorkflowGraphPhasePosition> = {
  capture: { x: 80, y: 60 },
  refinement: { x: 390, y: 60 },
  spec: { x: 390, y: 240 },
  "technical-design": { x: 390, y: 420 },
  implementation: { x: 390, y: 600 },
  review: { x: 390, y: 780 },
  "release-approval": { x: 390, y: 960 },
  "pr-preparation": { x: 390, y: 1140 },
  completed: { x: 390, y: 1320 }
};

export const defaultWorkflowGraphLegendPositions: {
  readonly horizontal: WorkflowGraphLegendPosition;
  readonly vertical: WorkflowGraphLegendPosition;
} = {
  horizontal: { x: 20, y: 720 },
  vertical: { x: 20, y: 250 }
};

export const defaultHorizontalWorkflowGraphConnections: Record<string, WorkflowGraphEdgeConnection> = {
  "capture->refinement": { from: "R3", to: "L3" },
  "refinement->spec": { from: "R3", to: "L3" },
  "spec->technical-design": { from: "R3", to: "L3" },
  "technical-design->implementation": { from: "B3", to: "T3" },
  "implementation->review": { from: "B3", to: "T3" },
  "review->release-approval": { from: "R3", to: "L3" },
  "release-approval->pr-preparation": { from: "R3", to: "L3" },
  "pr-preparation->completed": { from: "R3", to: "L3" }
};

export const defaultVerticalWorkflowGraphConnections: Record<string, WorkflowGraphEdgeConnection> = {
  "capture->refinement": { from: "R3", to: "L3" },
  "refinement->spec": { from: "B3", to: "T3" },
  "spec->technical-design": { from: "B3", to: "T3" },
  "technical-design->implementation": { from: "B3", to: "T3" },
  "implementation->review": { from: "B3", to: "T3" },
  "review->release-approval": { from: "B3", to: "T3" },
  "release-approval->pr-preparation": { from: "B3", to: "T3" },
  "pr-preparation->completed": { from: "B3", to: "T3" }
};

export const defaultHorizontalWorkflowGraphLoops: Record<string, WorkflowGraphLoopDefinition> = {
  "implementation-review": { fromPhaseId: "implementation", toPhaseId: "review", side: "right" }
};

export const defaultVerticalWorkflowGraphLoops: Record<string, WorkflowGraphLoopDefinition> = {
  "implementation-review": { fromPhaseId: "implementation", toPhaseId: "review", side: "right" }
};

export const defaultAggregateWorkflowGraphLayout: AggregateWorkflowGraphLayoutConfig = {
  positions: {
    capture: { x: 56, y: 140 },
    refinement: { x: 336, y: 140 },
    spec: { x: 336, y: 332 },
    split: { x: 56, y: 524 }
  },
  spacing: {
    horizontalPadding: 56,
    topRowTop: 140,
    topRowGap: 56,
    rowGap: 192,
    childGap: 56,
    maxChildrenPerRow: 2
  }
};

export function getWorkflowGraphLayoutPath(workspaceRoot: string): string {
  return path.join(workspaceRoot, ".specs", "workflow-graph-layout.yaml");
}

function appendWorkflowGraphLayoutLog(message: string): void {
  const { appendSpecForgeLog } = require("./outputChannel") as typeof import("./outputChannel");
  appendSpecForgeLog(message);
}

function appendWorkflowGraphLayoutDebugLog(message: string): void {
  const { appendSpecForgeDebugLog } = require("./outputChannel") as typeof import("./outputChannel");
  appendSpecForgeDebugLog(message);
}

function cloneDefaultAggregateWorkflowGraphLayout(): AggregateWorkflowGraphLayoutConfig {
  return {
    positions: {
      capture: { ...defaultAggregateWorkflowGraphLayout.positions.capture },
      refinement: { ...defaultAggregateWorkflowGraphLayout.positions.refinement },
      spec: { ...defaultAggregateWorkflowGraphLayout.positions.spec },
      split: { ...defaultAggregateWorkflowGraphLayout.positions.split }
    },
    spacing: { ...defaultAggregateWorkflowGraphLayout.spacing }
  };
}

function cloneAggregateWorkflowGraphLayoutConfig(config: AggregateWorkflowGraphLayoutConfig): AggregateWorkflowGraphLayoutConfig {
  return {
    positions: {
      capture: { ...config.positions.capture },
      refinement: { ...config.positions.refinement },
      spec: { ...config.positions.spec },
      split: { ...config.positions.split }
    },
    spacing: { ...config.spacing }
  };
}

export async function ensureWorkflowGraphLayoutConfigExistsAsync(workspaceRoot: string): Promise<void> {
  const filePath = getWorkflowGraphLayoutPath(workspaceRoot);
  try {
    await fs.promises.access(filePath, fs.constants.F_OK);
    appendWorkflowGraphLayoutDebugLog(`Workflow graph layout already exists at '${filePath}'.`);
    return;
  } catch {
    // Create below.
  }

  await fs.promises.mkdir(path.dirname(filePath), { recursive: true });
  await fs.promises.writeFile(filePath, serializeWorkflowGraphLayoutConfig({
    horizontal: defaultHorizontalWorkflowGraphPositions,
    vertical: defaultVerticalWorkflowGraphPositions,
    legend: defaultWorkflowGraphLegendPositions,
    connections: {
      horizontal: defaultHorizontalWorkflowGraphConnections,
      vertical: defaultVerticalWorkflowGraphConnections
    },
    loops: {
      horizontal: defaultHorizontalWorkflowGraphLoops,
      vertical: defaultVerticalWorkflowGraphLoops
    },
    aggregate: cloneDefaultAggregateWorkflowGraphLayout(),
    aggregateUserStories: {}
  }), "utf8");
  appendWorkflowGraphLayoutLog(`Created workflow graph layout bootstrap at '${filePath}'.`);
}

export async function readWorkflowGraphLayoutConfigAsync(workspaceRoot: string): Promise<WorkflowGraphLayoutConfig> {
  await ensureWorkflowGraphLayoutConfigExistsAsync(workspaceRoot);
  const filePath = getWorkflowGraphLayoutPath(workspaceRoot);

  try {
    const raw = await fs.promises.readFile(filePath, "utf8");
    return parseWorkflowGraphLayoutConfig(raw);
  } catch (error) {
    appendWorkflowGraphLayoutLog(
      `Workflow graph layout read failed for '${filePath}'. Falling back to defaults. ${error instanceof Error ? error.message : String(error)}`
    );
    return {
      horizontal: { ...defaultHorizontalWorkflowGraphPositions },
      vertical: { ...defaultVerticalWorkflowGraphPositions },
      legend: {
        horizontal: { ...defaultWorkflowGraphLegendPositions.horizontal },
        vertical: { ...defaultWorkflowGraphLegendPositions.vertical }
      },
      connections: {
        horizontal: { ...defaultHorizontalWorkflowGraphConnections },
        vertical: { ...defaultVerticalWorkflowGraphConnections }
      },
      loops: {
        horizontal: { ...defaultHorizontalWorkflowGraphLoops },
        vertical: { ...defaultVerticalWorkflowGraphLoops }
      },
      aggregate: cloneDefaultAggregateWorkflowGraphLayout(),
      aggregateUserStories: {}
    };
  }
}

export async function writeWorkflowGraphLayoutConfigAsync(
  workspaceRoot: string,
  config: WorkflowGraphLayoutConfig
): Promise<void> {
  const filePath = getWorkflowGraphLayoutPath(workspaceRoot);
  await fs.promises.mkdir(path.dirname(filePath), { recursive: true });
  await fs.promises.writeFile(filePath, serializeWorkflowGraphLayoutConfig(config), "utf8");
  appendWorkflowGraphLayoutLog(`Saved workflow graph layout at '${filePath}'.`);
}

export async function updateWorkflowGraphLayoutPositionsAsync(
  workspaceRoot: string,
  mode: WorkflowGraphLayoutMode,
  positions: Readonly<Record<string, WorkflowGraphPhasePosition>>
): Promise<WorkflowGraphLayoutConfig> {
  const current = await readWorkflowGraphLayoutConfigAsync(workspaceRoot);
  const next: WorkflowGraphLayoutConfig = {
    ...current,
    horizontal: { ...current.horizontal },
    vertical: { ...current.vertical },
    legend: {
      horizontal: { ...current.legend.horizontal },
      vertical: { ...current.legend.vertical }
    },
    connections: {
      horizontal: { ...current.connections.horizontal },
      vertical: { ...current.connections.vertical }
    },
    loops: {
      horizontal: { ...current.loops.horizontal },
      vertical: { ...current.loops.vertical }
    }
  };

  const target = mode === "horizontal" ? next.horizontal : next.vertical;
  for (const phaseId of workflowGraphPhaseIds) {
    const position = positions[phaseId];
    if (!position) {
      continue;
    }

    target[phaseId] = {
      x: Math.round(position.x),
      y: Math.round(position.y)
    };
  }

  await writeWorkflowGraphLayoutConfigAsync(workspaceRoot, next);
  return next;
}

export async function updateWorkflowGraphLegendPositionAsync(
  workspaceRoot: string,
  mode: WorkflowGraphLayoutMode,
  position: WorkflowGraphLegendPosition
): Promise<WorkflowGraphLayoutConfig> {
  const current = await readWorkflowGraphLayoutConfigAsync(workspaceRoot);
  const nextLegendHorizontal = mode === "horizontal"
    ? {
      x: Math.round(position.x),
      y: Math.round(position.y)
    }
    : { ...current.legend.horizontal };
  const nextLegendVertical = mode === "vertical"
    ? {
      x: Math.round(position.x),
      y: Math.round(position.y)
    }
    : { ...current.legend.vertical };
  const next: WorkflowGraphLayoutConfig = {
    ...current,
    horizontal: { ...current.horizontal },
    vertical: { ...current.vertical },
    legend: {
      horizontal: nextLegendHorizontal,
      vertical: nextLegendVertical
    },
    connections: {
      horizontal: { ...current.connections.horizontal },
      vertical: { ...current.connections.vertical }
    },
    loops: {
      horizontal: { ...current.loops.horizontal },
      vertical: { ...current.loops.vertical }
    }
  };

  await writeWorkflowGraphLayoutConfigAsync(workspaceRoot, next);
  return next;
}

export async function updateAggregateWorkflowGraphLayoutAsync(
  workspaceRoot: string,
  aggregate: AggregateWorkflowGraphLayoutConfig,
  userStoryId?: string | null
): Promise<WorkflowGraphLayoutConfig> {
  const current = await readWorkflowGraphLayoutConfigAsync(workspaceRoot);
  const nextAggregate = cloneAggregateWorkflowGraphLayoutConfig(current.aggregate);
  const nextAggregateUserStories = Object.fromEntries(
    Object.entries(current.aggregateUserStories).map(([key, value]) => [key, cloneAggregateWorkflowGraphLayoutConfig(value)])
  ) as Record<string, AggregateWorkflowGraphLayoutConfig>;
  const normalizedUserStoryId = userStoryId?.trim();
  if (normalizedUserStoryId) {
    nextAggregateUserStories[normalizedUserStoryId] = cloneAggregateWorkflowGraphLayoutConfig(aggregate);
  } else {
    Object.assign(nextAggregate, cloneAggregateWorkflowGraphLayoutConfig(aggregate));
  }
  const next: WorkflowGraphLayoutConfig = {
    ...current,
    horizontal: { ...current.horizontal },
    vertical: { ...current.vertical },
    legend: {
      horizontal: { ...current.legend.horizontal },
      vertical: { ...current.legend.vertical }
    },
    connections: {
      horizontal: { ...current.connections.horizontal },
      vertical: { ...current.connections.vertical }
    },
    loops: {
      horizontal: { ...current.loops.horizontal },
      vertical: { ...current.loops.vertical }
    },
    aggregate: nextAggregate,
    aggregateUserStories: nextAggregateUserStories
  };

  await writeWorkflowGraphLayoutConfigAsync(workspaceRoot, next);
  return next;
}

function parseWorkflowGraphLayoutConfig(raw: string): WorkflowGraphLayoutConfig {
  const horizontal = { ...defaultHorizontalWorkflowGraphPositions };
  const vertical = { ...defaultVerticalWorkflowGraphPositions };
  const legend = {
    horizontal: { ...defaultWorkflowGraphLegendPositions.horizontal },
    vertical: { ...defaultWorkflowGraphLegendPositions.vertical }
  };
  const connections = {
    horizontal: { ...defaultHorizontalWorkflowGraphConnections },
    vertical: { ...defaultVerticalWorkflowGraphConnections }
  };
  const loops = {
    horizontal: { ...defaultHorizontalWorkflowGraphLoops },
    vertical: { ...defaultVerticalWorkflowGraphLoops }
  };
  const aggregate = cloneDefaultAggregateWorkflowGraphLayout();
  const aggregateUserStories: Record<string, AggregateWorkflowGraphLayoutConfig> = {};
  let currentMode: WorkflowGraphLayoutMode | null = null;
  let currentSection: "positions" | "connections" | "loops" = "positions";
  let currentPhaseId: WorkflowGraphPhaseId | null = null;
  let currentEdgeId: string | null = null;
  let currentLoopId: string | null = null;
  let currentLegendTarget: WorkflowGraphLayoutMode | null = null;
  let inAggregate = false;
  let inAggregateUserStories = false;
  let aggregateSection: "positions" | "spacing" | null = null;
  let currentAggregateAnchorId: AggregateWorkflowGraphAnchorId | null = null;
  let currentAggregateUserStoryId: string | null = null;
  let pendingX: number | null = null;
  let pendingY: number | null = null;
  let pendingFromAnchor: string | null = null;
  let pendingToAnchor: string | null = null;
  let pendingLoopFromPhaseId: WorkflowGraphPhaseId | null = null;
  let pendingLoopToPhaseId: WorkflowGraphPhaseId | null = null;
  let pendingLoopSide: WorkflowGraphLoopSide | null = null;

  const commitPending = (): void => {
    if (inAggregate || inAggregateUserStories) {
      const aggregateTarget = currentAggregateUserStoryId
        ? (aggregateUserStories[currentAggregateUserStoryId] ??= cloneDefaultAggregateWorkflowGraphLayout())
        : aggregate;
      if (aggregateSection === "positions" && currentAggregateAnchorId && pendingX !== null && pendingY !== null) {
        aggregateTarget.positions[currentAggregateAnchorId] = { x: pendingX, y: pendingY };
      }
      return;
    }

    if (currentSection === "positions") {
      if (currentLegendTarget && pendingX !== null && pendingY !== null) {
        legend[currentLegendTarget] = { x: pendingX, y: pendingY };
        return;
      }

      if (!currentMode || !currentPhaseId || pendingX === null || pendingY === null) {
        return;
      }

      const target = currentMode === "horizontal" ? horizontal : vertical;
      target[currentPhaseId] = { x: pendingX, y: pendingY };
      return;
    }

    if (currentSection === "connections") {
      if (!currentMode || !currentEdgeId || !isAnchorCode(pendingFromAnchor) || !isAnchorCode(pendingToAnchor)) {
        return;
      }

      const target = currentMode === "horizontal" ? connections.horizontal : connections.vertical;
      target[currentEdgeId] = { from: pendingFromAnchor, to: pendingToAnchor };
      return;
    }

    if (!currentMode || !currentLoopId || !pendingLoopFromPhaseId || !pendingLoopToPhaseId || !pendingLoopSide) {
      return;
    }

    const target = currentMode === "horizontal" ? loops.horizontal : loops.vertical;
    target[currentLoopId] = {
      fromPhaseId: pendingLoopFromPhaseId,
      toPhaseId: pendingLoopToPhaseId,
      side: pendingLoopSide
    };
  };

  for (const rawLine of raw.replace(/\r\n/g, "\n").split("\n")) {
    const trimmed = rawLine.trim();
    if (trimmed.length === 0 || trimmed.startsWith("#")) {
      continue;
    }

    const modeMatch = /^(horizontal|vertical):\s*$/.exec(trimmed);
    if (modeMatch) {
      commitPending();
      inAggregate = false;
      inAggregateUserStories = false;
      aggregateSection = null;
      currentAggregateAnchorId = null;
      currentAggregateUserStoryId = null;
      currentMode = modeMatch[1] as WorkflowGraphLayoutMode;
      currentSection = "positions";
      currentPhaseId = null;
      currentEdgeId = null;
      currentLoopId = null;
      currentLegendTarget = null;
      pendingX = null;
      pendingY = null;
      pendingFromAnchor = null;
      pendingToAnchor = null;
      pendingLoopFromPhaseId = null;
      pendingLoopToPhaseId = null;
      pendingLoopSide = null;
      continue;
    }

    if (trimmed === "aggregate:") {
      commitPending();
      inAggregate = true;
      inAggregateUserStories = false;
      aggregateSection = null;
      currentAggregateAnchorId = null;
      currentAggregateUserStoryId = null;
      currentMode = null;
      currentPhaseId = null;
      currentEdgeId = null;
      currentLoopId = null;
      currentLegendTarget = null;
      pendingX = null;
      pendingY = null;
      pendingFromAnchor = null;
      pendingToAnchor = null;
      pendingLoopFromPhaseId = null;
      pendingLoopToPhaseId = null;
      pendingLoopSide = null;
      continue;
    }

    if (trimmed === "aggregateUserStories:") {
      commitPending();
      inAggregate = false;
      inAggregateUserStories = true;
      aggregateSection = null;
      currentAggregateAnchorId = null;
      currentAggregateUserStoryId = null;
      currentMode = null;
      currentPhaseId = null;
      currentEdgeId = null;
      currentLoopId = null;
      currentLegendTarget = null;
      pendingX = null;
      pendingY = null;
      pendingFromAnchor = null;
      pendingToAnchor = null;
      pendingLoopFromPhaseId = null;
      pendingLoopToPhaseId = null;
      pendingLoopSide = null;
      continue;
    }

    if (inAggregateUserStories) {
      const aggregateUserStoryMatch = /^([A-Za-z0-9._-]+):\s*$/.exec(trimmed);
      if (aggregateUserStoryMatch && aggregateSection === null && aggregateUserStoryMatch[1] !== "positions" && aggregateUserStoryMatch[1] !== "spacing") {
        commitPending();
        currentAggregateUserStoryId = aggregateUserStoryMatch[1];
        aggregateUserStories[currentAggregateUserStoryId] ??= cloneDefaultAggregateWorkflowGraphLayout();
        currentAggregateAnchorId = null;
        pendingX = null;
        pendingY = null;
        continue;
      }
    }

    if ((inAggregate || inAggregateUserStories) && trimmed === "positions:") {
      commitPending();
      aggregateSection = "positions";
      currentAggregateAnchorId = null;
      pendingX = null;
      pendingY = null;
      continue;
    }

    if ((inAggregate || inAggregateUserStories) && trimmed === "spacing:") {
      commitPending();
      aggregateSection = "spacing";
      currentAggregateAnchorId = null;
      pendingX = null;
      pendingY = null;
      continue;
    }

    if ((inAggregate || inAggregateUserStories) && aggregateSection === "positions") {
      const aggregateAnchorMatch = /^([a-z0-9-]+):\s*$/.exec(trimmed);
      if (aggregateAnchorMatch && isAggregateWorkflowGraphAnchorId(aggregateAnchorMatch[1])) {
        commitPending();
        currentAggregateAnchorId = aggregateAnchorMatch[1];
        pendingX = null;
        pendingY = null;
        continue;
      }
    }

    if (trimmed === "connections:") {
      commitPending();
      currentSection = "connections";
      currentPhaseId = null;
      currentEdgeId = null;
      currentLoopId = null;
      currentLegendTarget = null;
      pendingX = null;
      pendingY = null;
      pendingFromAnchor = null;
      pendingToAnchor = null;
      pendingLoopFromPhaseId = null;
      pendingLoopToPhaseId = null;
      pendingLoopSide = null;
      continue;
    }

    if (trimmed === "loops:") {
      commitPending();
      currentSection = "loops";
      currentPhaseId = null;
      currentEdgeId = null;
      currentLoopId = null;
      currentLegendTarget = null;
      pendingX = null;
      pendingY = null;
      pendingFromAnchor = null;
      pendingToAnchor = null;
      pendingLoopFromPhaseId = null;
      pendingLoopToPhaseId = null;
      pendingLoopSide = null;
      continue;
    }

    const phaseMatch = /^([a-z0-9-]+):\s*$/.exec(trimmed);
    if (phaseMatch && currentMode && currentSection === "positions" && workflowGraphPhaseIds.includes(phaseMatch[1] as WorkflowGraphPhaseId)) {
      commitPending();
      currentPhaseId = phaseMatch[1] as WorkflowGraphPhaseId;
      currentEdgeId = null;
      currentLoopId = null;
      currentLegendTarget = null;
      pendingX = null;
      pendingY = null;
      pendingFromAnchor = null;
      pendingToAnchor = null;
      pendingLoopFromPhaseId = null;
      pendingLoopToPhaseId = null;
      pendingLoopSide = null;
      continue;
    }

    const edgeMatch = /^([a-z0-9-]+->[a-z0-9-]+):\s*$/.exec(trimmed);
    if (edgeMatch && currentMode && currentSection === "connections") {
      commitPending();
      currentEdgeId = edgeMatch[1];
      currentPhaseId = null;
      currentLoopId = null;
      currentLegendTarget = null;
      pendingX = null;
      pendingY = null;
      pendingFromAnchor = null;
      pendingToAnchor = null;
      pendingLoopFromPhaseId = null;
      pendingLoopToPhaseId = null;
      pendingLoopSide = null;
      continue;
    }

    const loopMatch = /^([a-z0-9-]+):\s*$/.exec(trimmed);
    if (loopMatch && currentMode && currentSection === "loops") {
      commitPending();
      currentLoopId = loopMatch[1];
      currentPhaseId = null;
      currentEdgeId = null;
      currentLegendTarget = null;
      pendingX = null;
      pendingY = null;
      pendingFromAnchor = null;
      pendingToAnchor = null;
      pendingLoopFromPhaseId = null;
      pendingLoopToPhaseId = null;
      pendingLoopSide = null;
      continue;
    }

    if (trimmed === "legend:") {
      commitPending();
      currentLegendTarget = currentMode;
      currentPhaseId = null;
      currentEdgeId = null;
      currentLoopId = null;
      pendingX = null;
      pendingY = null;
      pendingFromAnchor = null;
      pendingToAnchor = null;
      pendingLoopFromPhaseId = null;
      pendingLoopToPhaseId = null;
      pendingLoopSide = null;
      continue;
    }

    const xMatch = /^x:\s*(-?\d+)\s*$/.exec(trimmed);
    if (xMatch && (currentPhaseId || currentLegendTarget || currentAggregateAnchorId)) {
      pendingX = Number.parseInt(xMatch[1], 10);
      continue;
    }

    const yMatch = /^y:\s*(-?\d+)\s*$/.exec(trimmed);
    if (yMatch && (currentPhaseId || currentLegendTarget || currentAggregateAnchorId)) {
      pendingY = Number.parseInt(yMatch[1], 10);
      continue;
    }

    if ((inAggregate || inAggregateUserStories) && aggregateSection === "spacing") {
      const spacingMatch = /^(horizontalPadding|topRowTop|topRowGap|rowGap|childGap|maxChildrenPerRow):\s*(-?\d+)\s*$/.exec(trimmed);
      if (spacingMatch) {
        const key = spacingMatch[1] as keyof AggregateWorkflowGraphLayoutConfig["spacing"];
        const value = Number.parseInt(spacingMatch[2], 10);
        const aggregateTarget = currentAggregateUserStoryId
          ? (aggregateUserStories[currentAggregateUserStoryId] ??= cloneDefaultAggregateWorkflowGraphLayout())
          : aggregate;
        (aggregateTarget.spacing as Record<string, number>)[key] = value;
        continue;
      }
    }

    const fromMatch = /^from:\s*([TLRB][1-5])\s*$/.exec(trimmed);
    if (fromMatch && currentEdgeId) {
      pendingFromAnchor = fromMatch[1];
      continue;
    }

    const toMatch = /^to:\s*([TLRB][1-5])\s*$/.exec(trimmed);
    if (toMatch && currentEdgeId) {
      pendingToAnchor = toMatch[1];
      continue;
    }

    const fromPhaseMatch = /^fromPhaseId:\s*([a-z0-9-]+)\s*$/.exec(trimmed);
    if (fromPhaseMatch && currentLoopId && isWorkflowGraphPhaseId(fromPhaseMatch[1])) {
      pendingLoopFromPhaseId = fromPhaseMatch[1];
      continue;
    }

    const toPhaseMatch = /^toPhaseId:\s*([a-z0-9-]+)\s*$/.exec(trimmed);
    if (toPhaseMatch && currentLoopId && isWorkflowGraphPhaseId(toPhaseMatch[1])) {
      pendingLoopToPhaseId = toPhaseMatch[1];
      continue;
    }

    const sideMatch = /^side:\s*(top|right|bottom|left)\s*$/.exec(trimmed);
    if (sideMatch && currentLoopId) {
      pendingLoopSide = sideMatch[1] as WorkflowGraphLoopSide;
      continue;
    }
  }

  commitPending();
  return { horizontal, vertical, legend, connections, loops, aggregate, aggregateUserStories };
}

function serializeWorkflowGraphLayoutConfig(config: WorkflowGraphLayoutConfig): string {
  const serializeMode = (mode: WorkflowGraphLayoutMode): string => {
    const positions = mode === "horizontal" ? config.horizontal : config.vertical;
    const legendPosition = mode === "horizontal" ? config.legend.horizontal : config.legend.vertical;
    const edges = mode === "horizontal" ? config.connections.horizontal : config.connections.vertical;
    const modeLoops = mode === "horizontal" ? config.loops.horizontal : config.loops.vertical;
    const lines = [`${mode}:`];
    for (const phaseId of workflowGraphPhaseIds) {
      const position = positions[phaseId];
      lines.push(`  ${phaseId}:`);
      lines.push(`    x: ${Math.round(position.x)}`);
      lines.push(`    y: ${Math.round(position.y)}`);
    }
    lines.push("  legend:");
    lines.push(`    x: ${Math.round(legendPosition.x)}`);
    lines.push(`    y: ${Math.round(legendPosition.y)}`);
    lines.push("  connections:");
    for (const edgeId of Object.keys(edges)) {
      lines.push(`    ${edgeId}:`);
      lines.push(`      from: ${edges[edgeId].from}`);
      lines.push(`      to: ${edges[edgeId].to}`);
    }
    lines.push("  loops:");
    for (const loopId of Object.keys(modeLoops)) {
      lines.push(`    ${loopId}:`);
      lines.push(`      fromPhaseId: ${modeLoops[loopId].fromPhaseId}`);
      lines.push(`      toPhaseId: ${modeLoops[loopId].toPhaseId}`);
      lines.push(`      side: ${modeLoops[loopId].side}`);
    }
    return lines.join("\n");
  };

  return [
    "# SpecForge workflow graph layout",
    "# Edit x/y coordinates to reposition cards in the workflow graph.",
    "# Card dimensions used by the renderer: desktop 240x118, mobile 206x118.",
    "# Connection anchors use T1..T5, R1..R5, B1..B5, L1..L5.",
    serializeMode("horizontal"),
    "",
    serializeMode("vertical"),
    "",
    "aggregate:",
    "  positions:",
    ...aggregateWorkflowGraphAnchorIds.flatMap((anchorId) => [
      `    ${anchorId}:`,
      `      x: ${Math.round(config.aggregate.positions[anchorId].x)}`,
      `      y: ${Math.round(config.aggregate.positions[anchorId].y)}`
    ]),
    "  spacing:",
    `    horizontalPadding: ${config.aggregate.spacing.horizontalPadding}`,
    `    topRowTop: ${config.aggregate.spacing.topRowTop}`,
    `    topRowGap: ${config.aggregate.spacing.topRowGap}`,
    `    rowGap: ${config.aggregate.spacing.rowGap}`,
    `    childGap: ${config.aggregate.spacing.childGap}`,
    `    maxChildrenPerRow: ${config.aggregate.spacing.maxChildrenPerRow}`,
    "",
    "aggregateUserStories:",
    ...Object.keys(config.aggregateUserStories).sort().flatMap((userStoryId) => {
      const aggregateLayout = config.aggregateUserStories[userStoryId];
      return [
        `  ${userStoryId}:`,
        "    positions:",
        ...aggregateWorkflowGraphAnchorIds.flatMap((anchorId) => [
          `      ${anchorId}:`,
          `        x: ${Math.round(aggregateLayout.positions[anchorId].x)}`,
          `        y: ${Math.round(aggregateLayout.positions[anchorId].y)}`
        ]),
        "    spacing:",
        `      horizontalPadding: ${aggregateLayout.spacing.horizontalPadding}`,
        `      topRowTop: ${aggregateLayout.spacing.topRowTop}`,
        `      topRowGap: ${aggregateLayout.spacing.topRowGap}`,
        `      rowGap: ${aggregateLayout.spacing.rowGap}`,
        `      childGap: ${aggregateLayout.spacing.childGap}`,
        `      maxChildrenPerRow: ${aggregateLayout.spacing.maxChildrenPerRow}`
      ];
    }),
    ""
  ].join("\n");
}

function isAnchorCode(value: string | null): value is string {
  return Boolean(value && /^[TLRB][1-5]$/.test(value));
}

function isWorkflowGraphPhaseId(value: string): value is WorkflowGraphPhaseId {
  return workflowGraphPhaseIds.includes(value as WorkflowGraphPhaseId);
}

function isAggregateWorkflowGraphAnchorId(value: string): value is AggregateWorkflowGraphAnchorId {
  return aggregateWorkflowGraphAnchorIds.includes(value as AggregateWorkflowGraphAnchorId);
}
