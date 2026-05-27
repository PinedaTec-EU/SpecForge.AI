#!/usr/bin/env node
"use strict";

const fs = require("node:fs");
const path = require("node:path");

const workflowGraphPhaseIds = [
  "capture",
  "refinement",
  "spec",
  "technical-design",
  "implementation",
  "review",
  "release-approval",
  "pr-preparation",
  "completed"
];

const aggregateWorkflowGraphAnchorIds = [
  "capture",
  "refinement",
  "spec",
  "split"
];

const defaultHorizontalWorkflowGraphPositions = {
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

const defaultVerticalWorkflowGraphPositions = {
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

const defaultWorkflowGraphLegendPositions = {
  horizontal: { x: 20, y: 720 },
  vertical: { x: 20, y: 250 }
};

const defaultHorizontalWorkflowGraphConnections = {
  "capture->refinement": { from: "R3", to: "L3" },
  "refinement->spec": { from: "R3", to: "L3" },
  "spec->technical-design": { from: "R3", to: "L3" },
  "technical-design->implementation": { from: "B3", to: "T3" },
  "implementation->review": { from: "B3", to: "T3" },
  "review->release-approval": { from: "R3", to: "L3" },
  "release-approval->pr-preparation": { from: "R3", to: "L3" },
  "pr-preparation->completed": { from: "R3", to: "L3" }
};

const defaultVerticalWorkflowGraphConnections = {
  "capture->refinement": { from: "R3", to: "L3" },
  "refinement->spec": { from: "B3", to: "T3" },
  "spec->technical-design": { from: "B3", to: "T3" },
  "technical-design->implementation": { from: "B3", to: "T3" },
  "implementation->review": { from: "B3", to: "T3" },
  "review->release-approval": { from: "B3", to: "T3" },
  "release-approval->pr-preparation": { from: "B3", to: "T3" },
  "pr-preparation->completed": { from: "B3", to: "T3" }
};

const defaultHorizontalWorkflowGraphLoops = {
  "implementation-review": { fromPhaseId: "implementation", toPhaseId: "review", side: "right" }
};

const defaultVerticalWorkflowGraphLoops = {
  "implementation-review": { fromPhaseId: "implementation", toPhaseId: "review", side: "right" }
};

const defaultAggregateWorkflowGraphLayout = {
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

function getWorkflowGraphLayoutPath(workspaceRoot) {
  return path.join(workspaceRoot, ".specs", "workflow-graph-layout.yaml");
}

function cloneDefaultAggregateWorkflowGraphLayout() {
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

function cloneAggregateWorkflowGraphLayoutConfig(config) {
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

function buildDefaultConfig() {
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
    aggregateUserStories: {},
    userStoryLayoutModes: {}
  };
}

function cloneUserStoryLayoutModes(userStoryLayoutModes) {
  return Object.fromEntries(
    Object.entries(userStoryLayoutModes || {}).filter(([, value]) => value === "horizontal" || value === "vertical")
  );
}

async function ensureWorkflowGraphLayoutConfigExistsAsync(workspaceRoot) {
  const filePath = getWorkflowGraphLayoutPath(workspaceRoot);
  try {
    await fs.promises.access(filePath, fs.constants.F_OK);
    return;
  } catch {}

  await fs.promises.mkdir(path.dirname(filePath), { recursive: true });
  await fs.promises.writeFile(filePath, serializeWorkflowGraphLayoutConfig(buildDefaultConfig()), "utf8");
}

async function readWorkflowGraphLayoutConfigAsync(workspaceRoot) {
  await ensureWorkflowGraphLayoutConfigExistsAsync(workspaceRoot);
  const filePath = getWorkflowGraphLayoutPath(workspaceRoot);
  try {
    return parseWorkflowGraphLayoutConfig(await fs.promises.readFile(filePath, "utf8"));
  } catch {
    return buildDefaultConfig();
  }
}

async function writeWorkflowGraphLayoutConfigAsync(workspaceRoot, config) {
  const filePath = getWorkflowGraphLayoutPath(workspaceRoot);
  await fs.promises.mkdir(path.dirname(filePath), { recursive: true });
  await fs.promises.writeFile(filePath, serializeWorkflowGraphLayoutConfig(config), "utf8");
}

async function updateWorkflowGraphLayoutPositionsAsync(workspaceRoot, mode, positions) {
  const current = await readWorkflowGraphLayoutConfigAsync(workspaceRoot);
  const next = {
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
    aggregate: cloneAggregateWorkflowGraphLayoutConfig(current.aggregate),
    aggregateUserStories: Object.fromEntries(
      Object.entries(current.aggregateUserStories).map(([key, value]) => [key, cloneAggregateWorkflowGraphLayoutConfig(value)])
    ),
    userStoryLayoutModes: cloneUserStoryLayoutModes(current.userStoryLayoutModes)
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

async function updateWorkflowGraphLegendPositionAsync(workspaceRoot, mode, position) {
  const current = await readWorkflowGraphLayoutConfigAsync(workspaceRoot);
  const next = {
    ...current,
    horizontal: { ...current.horizontal },
    vertical: { ...current.vertical },
    legend: {
      horizontal: mode === "horizontal"
        ? { x: Math.round(position.x), y: Math.round(position.y) }
        : { ...current.legend.horizontal },
      vertical: mode === "vertical"
        ? { x: Math.round(position.x), y: Math.round(position.y) }
        : { ...current.legend.vertical }
    },
    connections: {
      horizontal: { ...current.connections.horizontal },
      vertical: { ...current.connections.vertical }
    },
    loops: {
      horizontal: { ...current.loops.horizontal },
      vertical: { ...current.loops.vertical }
    },
    aggregate: cloneAggregateWorkflowGraphLayoutConfig(current.aggregate),
    aggregateUserStories: Object.fromEntries(
      Object.entries(current.aggregateUserStories).map(([key, value]) => [key, cloneAggregateWorkflowGraphLayoutConfig(value)])
    ),
    userStoryLayoutModes: cloneUserStoryLayoutModes(current.userStoryLayoutModes)
  };

  await writeWorkflowGraphLayoutConfigAsync(workspaceRoot, next);
  return next;
}

async function updateAggregateWorkflowGraphLayoutAsync(workspaceRoot, aggregate, userStoryId) {
  const current = await readWorkflowGraphLayoutConfigAsync(workspaceRoot);
  const nextAggregate = cloneAggregateWorkflowGraphLayoutConfig(current.aggregate);
  const nextAggregateUserStories = Object.fromEntries(
    Object.entries(current.aggregateUserStories).map(([key, value]) => [key, cloneAggregateWorkflowGraphLayoutConfig(value)])
  );
  const normalizedUserStoryId = typeof userStoryId === "string" ? userStoryId.trim() : "";
  if (normalizedUserStoryId) {
    nextAggregateUserStories[normalizedUserStoryId] = cloneAggregateWorkflowGraphLayoutConfig(aggregate);
  } else {
    Object.assign(nextAggregate, cloneAggregateWorkflowGraphLayoutConfig(aggregate));
  }

  const next = {
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
    aggregateUserStories: nextAggregateUserStories,
    userStoryLayoutModes: cloneUserStoryLayoutModes(current.userStoryLayoutModes)
  };

  await writeWorkflowGraphLayoutConfigAsync(workspaceRoot, next);
  return next;
}

async function updateWorkflowGraphLayoutModeOverrideAsync(workspaceRoot, userStoryId, mode, fallbackMode) {
  const normalizedUserStoryId = typeof userStoryId === "string" ? userStoryId.trim() : "";
  if (!normalizedUserStoryId) {
    return readWorkflowGraphLayoutConfigAsync(workspaceRoot);
  }

  const current = await readWorkflowGraphLayoutConfigAsync(workspaceRoot);
  const nextUserStoryLayoutModes = cloneUserStoryLayoutModes(current.userStoryLayoutModes);
  const normalizedFallbackMode = fallbackMode === "horizontal" ? "horizontal" : "vertical";
  if (mode === "horizontal" || mode === "vertical") {
    if (mode === normalizedFallbackMode) {
      delete nextUserStoryLayoutModes[normalizedUserStoryId];
    } else {
      nextUserStoryLayoutModes[normalizedUserStoryId] = mode;
    }
  } else {
    delete nextUserStoryLayoutModes[normalizedUserStoryId];
  }

  const next = {
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
    aggregate: cloneAggregateWorkflowGraphLayoutConfig(current.aggregate),
    aggregateUserStories: Object.fromEntries(
      Object.entries(current.aggregateUserStories).map(([key, value]) => [key, cloneAggregateWorkflowGraphLayoutConfig(value)])
    ),
    userStoryLayoutModes: nextUserStoryLayoutModes
  };

  await writeWorkflowGraphLayoutConfigAsync(workspaceRoot, next);
  return next;
}

function parseWorkflowGraphLayoutConfig(raw) {
  const config = buildDefaultConfig();
  const { horizontal, vertical, legend, connections, loops, aggregate, aggregateUserStories, userStoryLayoutModes } = config;
  let currentMode = null;
  let currentSection = "positions";
  let currentPhaseId = null;
  let currentEdgeId = null;
  let currentLoopId = null;
  let currentLegendTarget = null;
  let inAggregate = false;
  let inAggregateUserStories = false;
  let inUserStoryLayoutModes = false;
  let aggregateSection = null;
  let currentAggregateAnchorId = null;
  let currentAggregateUserStoryId = null;
  let pendingX = null;
  let pendingY = null;
  let pendingFromAnchor = null;
  let pendingToAnchor = null;
  let pendingLoopFromPhaseId = null;
  let pendingLoopToPhaseId = null;
  let pendingLoopSide = null;

  const commitPending = () => {
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
    if (!trimmed || trimmed.startsWith("#")) {
      continue;
    }

    const modeMatch = /^(horizontal|vertical):\s*$/.exec(trimmed);
    if (modeMatch && !inUserStoryLayoutModes) {
      commitPending();
      inAggregate = false;
      inAggregateUserStories = false;
      inUserStoryLayoutModes = false;
      aggregateSection = null;
      currentAggregateAnchorId = null;
      currentAggregateUserStoryId = null;
      currentMode = modeMatch[1];
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
      inUserStoryLayoutModes = false;
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
      inUserStoryLayoutModes = false;
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

    if (trimmed === "userStoryLayoutModes:") {
      commitPending();
      inAggregate = false;
      inAggregateUserStories = false;
      inUserStoryLayoutModes = true;
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

    if (inUserStoryLayoutModes) {
      const userStoryLayoutModeMatch = /^([A-Za-z0-9._-]+):\s*(horizontal|vertical)\s*$/.exec(trimmed);
      if (userStoryLayoutModeMatch) {
        userStoryLayoutModes[userStoryLayoutModeMatch[1]] = userStoryLayoutModeMatch[2];
      }
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
    if (phaseMatch && currentMode && currentSection === "positions" && workflowGraphPhaseIds.includes(phaseMatch[1])) {
      commitPending();
      currentPhaseId = phaseMatch[1];
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
        const key = spacingMatch[1];
        const value = Number.parseInt(spacingMatch[2], 10);
        const aggregateTarget = currentAggregateUserStoryId
          ? (aggregateUserStories[currentAggregateUserStoryId] ??= cloneDefaultAggregateWorkflowGraphLayout())
          : aggregate;
        aggregateTarget.spacing[key] = value;
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
      pendingLoopSide = sideMatch[1];
      continue;
    }
  }

  commitPending();
  return config;
}

function serializeWorkflowGraphLayoutConfig(config) {
  const serializeMode = (mode) => {
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
    "userStoryLayoutModes:",
    ...Object.keys(config.userStoryLayoutModes).sort().map((userStoryId) =>
      `  ${userStoryId}: ${config.userStoryLayoutModes[userStoryId] === "horizontal" ? "horizontal" : "vertical"}`
    ),
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

function isAnchorCode(value) {
  return Boolean(value && /^[TLRB][1-5]$/.test(value));
}

function isWorkflowGraphPhaseId(value) {
  return workflowGraphPhaseIds.includes(value);
}

function isAggregateWorkflowGraphAnchorId(value) {
  return aggregateWorkflowGraphAnchorIds.includes(value);
}

module.exports = {
  getWorkflowGraphLayoutPath,
  ensureWorkflowGraphLayoutConfigExistsAsync,
  readWorkflowGraphLayoutConfigAsync,
  updateWorkflowGraphLayoutPositionsAsync,
  updateWorkflowGraphLegendPositionAsync,
  updateAggregateWorkflowGraphLayoutAsync,
  updateWorkflowGraphLayoutModeOverrideAsync
};
