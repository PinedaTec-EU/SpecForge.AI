import {
  defaultHorizontalWorkflowGraphPositions,
  defaultVerticalWorkflowGraphPositions,
  type WorkflowGraphEdgeConnection,
  type WorkflowGraphPhasePosition
} from "../workflowGraphLayout";
import type { WorkflowPhaseDetails } from "../backendClient";

export type PhasePosition = { left: number; top: number };
export type GraphLegendPosition = { left: number; top: number };
export type LayoutPhaseDescriptor = Pick<WorkflowPhaseDetails, "phaseId" | "expectsHumanIntervention">;
export type PhaseGraphLayout = {
  readonly positions: Record<string, PhasePosition>;
  readonly width: number;
  readonly height: number;
};

type GraphAnchor =
  | "entry-top"
  | "entry-top-left"
  | "entry-top-right"
  | "entry-left"
  | "entry-center-right"
  | "entry-right"
  | "entry-center-left"
  | "exit-right"
  | "exit-center-right"
  | "exit-left"
  | "exit-center-left"
  | "exit-bottom-left"
  | "exit-bottom-mid"
  | "exit-bottom-right";

export const workflowGraphNodeHeight = 102;

export function buildGraphLegendPosition(x: number, y: number, compact = false): GraphLegendPosition {
  const scale = compact ? 0.72 : 1;

  return {
    left: Math.round(x * scale),
    top: Math.round(y * scale)
  };
}

export function buildHorizontalPhaseLayout(
  phases: readonly LayoutPhaseDescriptor[],
  nodeWidth: number,
  compact = false,
  sourcePositions: Record<string, WorkflowGraphPhasePosition> = defaultHorizontalWorkflowGraphPositions
): PhaseGraphLayout {
  const positions: Record<string, PhasePosition> = {};
  const scale = compact ? 0.72 : 1;

  for (const phase of phases) {
    const source = sourcePositions[phase.phaseId];
    positions[phase.phaseId] = source
      ? { left: Math.round(source.x * scale), top: Math.round(source.y * scale) }
      : { left: Math.round(150 * scale), top: Math.round(120 * scale) };
  }

  return {
    positions,
    width: computeGraphWidth(positions, nodeWidth, compact ? 92 : 186),
    height: computeGraphHeight(positions, workflowGraphNodeHeight, compact ? 98 : 162)
  };
}

export function buildVerticalPhaseLayout(
  phases: readonly LayoutPhaseDescriptor[],
  nodeWidth: number,
  compact = false,
  sourcePositions: Record<string, WorkflowGraphPhasePosition> = defaultVerticalWorkflowGraphPositions
): PhaseGraphLayout {
  const positions: Record<string, PhasePosition> = {};
  const scale = compact ? 0.72 : 1;

  for (const phase of phases) {
    const source = sourcePositions[phase.phaseId];
    positions[phase.phaseId] = source
      ? { left: Math.round(source.x * scale), top: Math.round(source.y * scale) }
      : { left: Math.round(72 * scale), top: Math.round(36 * scale) };
  }

  return {
    positions,
    width: computeGraphWidth(positions, nodeWidth, compact ? 84 : 152),
    height: computeGraphHeight(positions, workflowGraphNodeHeight, compact ? 128 : 178)
  };
}

export function graphPath(
  fromPhaseId: string,
  toPhaseId: string,
  positions: Record<string, PhasePosition>,
  nodeWidth: number,
  graphLayoutMode: "horizontal" | "vertical",
  edgeConnection?: WorkflowGraphEdgeConnection
): string {
  const fromPosition = positions[fromPhaseId];
  const toPosition = positions[toPhaseId];

  if (!fromPosition || !toPosition) {
    return "";
  }

  return buildOrthogonalGraphPath(
    fromPosition,
    toPosition,
    nodeWidth,
    graphLayoutMode,
    edgeConnection
  );
}

function computeGraphHeight(positions: Record<string, PhasePosition>, nodeHeight: number, bottomPadding: number): number {
  const maxTop = Math.max(...Object.values(positions).map((position) => position.top));

  return maxTop + nodeHeight + bottomPadding;
}

function computeGraphWidth(positions: Record<string, PhasePosition>, nodeWidth: number, rightPadding: number): number {
  const maxLeft = Math.max(...Object.values(positions).map((position) => position.left));

  return maxLeft + nodeWidth + rightPadding;
}

function buildOrthogonalGraphPath(
  fromPosition: PhasePosition,
  toPosition: PhasePosition,
  nodeWidth: number,
  graphLayoutMode: "horizontal" | "vertical",
  edgeConnection?: WorkflowGraphEdgeConnection
): string {
  const resolvedAnchors = resolveAnchorsForLayout(fromPosition, toPosition, graphLayoutMode, edgeConnection);
  const from = getAnchorPointFromCodeOrAnchor(fromPosition, resolvedAnchors.fromAnchor, nodeWidth, true);
  const to = getAnchorPointFromCodeOrAnchor(toPosition, resolvedAnchors.toAnchor, nodeWidth, false);
  const fromAnchor = toGraphAnchor(resolvedAnchors.fromAnchor, true);
  const toAnchor = toGraphAnchor(resolvedAnchors.toAnchor, false);
  const leadDistance = Math.max(34, Math.round(nodeWidth * 0.16));
  const startLead = projectAwayFromNode(fromPosition, fromAnchor, from, leadDistance);
  const endLead = projectAwayFromNode(toPosition, toAnchor, to, leadDistance);
  const preferredAxis = resolvePreferredAxis(graphLayoutMode, fromAnchor, toAnchor, startLead, endLead);
  const points = simplifyOrthogonalPoints([
    from,
    startLead,
    ...buildOrthogonalWaypoints(startLead, endLead, preferredAxis),
    endLead,
    to
  ]);

  return buildRoundedPath(points, Math.max(12, Math.round(nodeWidth * 0.06)));
}

function resolveAnchorsForLayout(
  from: PhasePosition,
  to: PhasePosition,
  graphLayoutMode: "horizontal" | "vertical",
  edgeConnection?: WorkflowGraphEdgeConnection
): { fromAnchor: string; toAnchor: string } {
  if (edgeConnection?.from && edgeConnection?.to) {
    return {
      fromAnchor: edgeConnection.from,
      toAnchor: edgeConnection.to
    };
  }

  if (graphLayoutMode === "horizontal") {
    if (Math.abs(to.left - from.left) >= Math.abs(to.top - from.top)) {
      return to.left >= from.left
        ? { fromAnchor: "R3", toAnchor: "L3" }
        : { fromAnchor: "L3", toAnchor: "R3" };
    }

    return to.top >= from.top
      ? { fromAnchor: "B3", toAnchor: "T3" }
      : { fromAnchor: "T3", toAnchor: "B3" };
  }

  if (Math.abs(to.top - from.top) >= Math.abs(to.left - from.left)) {
    return to.top >= from.top
      ? { fromAnchor: "B3", toAnchor: "T3" }
      : { fromAnchor: "T3", toAnchor: "B3" };
  }

  return to.left >= from.left
    ? { fromAnchor: "R3", toAnchor: "L3" }
    : { fromAnchor: "L3", toAnchor: "R3" };
}

function resolvePreferredAxis(
  graphLayoutMode: "horizontal" | "vertical",
  fromAnchor: GraphAnchor,
  toAnchor: GraphAnchor,
  startLead: { x: number; y: number },
  endLead: { x: number; y: number }
): "horizontal" | "vertical" {
  const anchorsAreVertical = (isVerticalAnchor(fromAnchor) && isVerticalAnchor(toAnchor))
    || startLead.x === endLead.x;
  const anchorsAreHorizontal = (isHorizontalAnchor(fromAnchor) && isHorizontalAnchor(toAnchor))
    || startLead.y === endLead.y;

  if (anchorsAreVertical && !anchorsAreHorizontal) {
    return "vertical";
  }

  if (anchorsAreHorizontal && !anchorsAreVertical) {
    return "horizontal";
  }

  return graphLayoutMode;
}

function buildOrthogonalWaypoints(
  from: { x: number; y: number },
  to: { x: number; y: number },
  preferredAxis: "horizontal" | "vertical"
): { x: number; y: number }[] {
  if (from.x === to.x || from.y === to.y) {
    return [];
  }

  if (preferredAxis === "horizontal") {
    const midX = Math.round((from.x + to.x) * 0.5);
    return [
      { x: midX, y: from.y },
      { x: midX, y: to.y }
    ];
  }

  const midY = Math.round((from.y + to.y) * 0.5);
  return [
    { x: from.x, y: midY },
    { x: to.x, y: midY }
  ];
}

function simplifyOrthogonalPoints(points: readonly { x: number; y: number }[]): { x: number; y: number }[] {
  const normalized: { x: number; y: number }[] = [];

  for (const point of points) {
    const previous = normalized.at(-1);
    if (previous && previous.x === point.x && previous.y === point.y) {
      continue;
    }

    normalized.push(point);
    while (normalized.length >= 3) {
      const last = normalized.at(-1);
      const middle = normalized.at(-2);
      const first = normalized.at(-3);
      if (!last || !middle || !first) {
        break;
      }

      const sameX = first.x === middle.x && middle.x === last.x;
      const sameY = first.y === middle.y && middle.y === last.y;
      if (!sameX && !sameY) {
        break;
      }

      normalized.splice(normalized.length - 2, 1);
    }
  }

  return normalized;
}

function buildRoundedPath(points: readonly { x: number; y: number }[], radius: number): string {
  if (points.length === 0) {
    return "";
  }

  if (points.length === 1) {
    return `M ${points[0].x} ${points[0].y}`;
  }

  let path = `M ${points[0].x} ${points[0].y}`;

  for (let index = 1; index < points.length - 1; index += 1) {
    const previous = points[index - 1];
    const current = points[index];
    const next = points[index + 1];

    if (!isAxisAligned(previous, current) || !isAxisAligned(current, next)) {
      path += ` L ${current.x} ${current.y}`;
      continue;
    }

    const incoming = Math.abs(previous.x - current.x) + Math.abs(previous.y - current.y);
    const outgoing = Math.abs(next.x - current.x) + Math.abs(next.y - current.y);
    const cornerRadius = Math.min(radius, incoming * 0.5, outgoing * 0.5);

    if (cornerRadius <= 0.5 || arePointsCollinear(previous, current, next)) {
      path += ` L ${current.x} ${current.y}`;
      continue;
    }

    const cornerStart = movePointTowards(current, previous, cornerRadius);
    const cornerEnd = movePointTowards(current, next, cornerRadius);
    path += ` L ${cornerStart.x} ${cornerStart.y} Q ${current.x} ${current.y} ${cornerEnd.x} ${cornerEnd.y}`;
  }

  const last = points.at(-1);
  if (last) {
    path += ` L ${last.x} ${last.y}`;
  }

  return path;
}

function movePointTowards(
  from: { x: number; y: number },
  to: { x: number; y: number },
  distance: number
): { x: number; y: number } {
  if (from.x === to.x) {
    return {
      x: from.x,
      y: from.y + Math.sign(to.y - from.y) * distance
    };
  }

  return {
    x: from.x + Math.sign(to.x - from.x) * distance,
    y: from.y
  };
}

function isAxisAligned(first: { x: number; y: number }, second: { x: number; y: number }): boolean {
  return first.x === second.x || first.y === second.y;
}

function arePointsCollinear(
  first: { x: number; y: number },
  second: { x: number; y: number },
  third: { x: number; y: number }
): boolean {
  return (first.x === second.x && second.x === third.x) || (first.y === second.y && second.y === third.y);
}

function resolveAnchors(
  from: PhasePosition,
  to: PhasePosition,
  edgeConnection?: WorkflowGraphEdgeConnection
): { fromAnchor: string; toAnchor: string } {
  if (edgeConnection?.from && edgeConnection?.to) {
    return {
      fromAnchor: edgeConnection.from,
      toAnchor: edgeConnection.to
    };
  }

  const deltaX = to.left - from.left;
  const deltaY = to.top - from.top;

  if (deltaY > 0) {
    if (Math.abs(deltaX) <= 28) {
      return { fromAnchor: "exit-bottom-mid", toAnchor: "entry-top" };
    }

    if (deltaX > 0) {
      return { fromAnchor: "exit-bottom-right", toAnchor: "entry-top-left" };
    }

    return { fromAnchor: "exit-bottom-left", toAnchor: "entry-top-right" };
  }

  if (deltaX === 0) {
    return { fromAnchor: "exit-right", toAnchor: "entry-right" };
  }

  if (deltaX > 0) {
    return { fromAnchor: "exit-right", toAnchor: "entry-left" };
  }

  return { fromAnchor: "exit-left", toAnchor: "entry-right" };
}

function getAnchorPointFromCodeOrAnchor(
  position: PhasePosition,
  anchor: string,
  nodeWidth: number,
  isExit: boolean
): { x: number; y: number } {
  return isAnchorCode(anchor)
    ? getAnchorPointFromCode(position, anchor, nodeWidth)
    : getAnchorPoint(position, anchor as GraphAnchor, nodeWidth);
}

function getAnchorPointFromCode(
  position: PhasePosition,
  anchorCode: string,
  nodeWidth: number
): { x: number; y: number } {
  const face = anchorCode[0];
  const slot = Number.parseInt(anchorCode[1], 10);
  const fraction = slot / 6;

  switch (face) {
    case "T":
      return { x: position.left + nodeWidth * fraction, y: position.top };
    case "R":
      return { x: position.left + nodeWidth, y: position.top + workflowGraphNodeHeight * fraction };
    case "B":
      return { x: position.left + nodeWidth * fraction, y: position.top + workflowGraphNodeHeight };
    case "L":
      return { x: position.left, y: position.top + workflowGraphNodeHeight * fraction };
    default:
      return { x: position.left + nodeWidth * 0.5, y: position.top + workflowGraphNodeHeight * 0.5 };
  }
}

function toGraphAnchor(anchor: string, isExit: boolean): GraphAnchor {
  if (!isAnchorCode(anchor)) {
    return anchor as GraphAnchor;
  }

  switch (anchor[0]) {
    case "T":
      return "entry-top";
    case "R":
      return isExit ? "exit-right" : "entry-right";
    case "B":
      return "exit-bottom-mid";
    case "L":
      return isExit ? "exit-left" : "entry-left";
    default:
      return isExit ? "exit-right" : "entry-left";
  }
}

function isAnchorCode(anchor: string): boolean {
  return /^[TLRB][1-5]$/.test(anchor);
}

function getAnchorPoint(position: PhasePosition, anchor: GraphAnchor, nodeWidth: number): { x: number; y: number } {
  switch (anchor) {
    case "entry-top":
      return { x: position.left + nodeWidth * 0.5, y: position.top };
    case "entry-top-left":
      return { x: position.left + nodeWidth * 0.26, y: position.top };
    case "entry-top-right":
      return { x: position.left + nodeWidth * 0.74, y: position.top };
    case "entry-left":
      return { x: position.left, y: position.top + workflowGraphNodeHeight * 0.36 };
    case "entry-center-left":
      return { x: position.left, y: position.top + workflowGraphNodeHeight * 0.5 };
    case "entry-right":
      return { x: position.left + nodeWidth, y: position.top + workflowGraphNodeHeight * 0.34 };
    case "entry-center-right":
      return { x: position.left + nodeWidth, y: position.top + workflowGraphNodeHeight * 0.5 };
    case "exit-right":
      return { x: position.left + nodeWidth, y: position.top + workflowGraphNodeHeight * 0.78 };
    case "exit-center-right":
      return { x: position.left + nodeWidth, y: position.top + workflowGraphNodeHeight * 0.5 };
    case "exit-left":
      return { x: position.left, y: position.top + workflowGraphNodeHeight * 0.78 };
    case "exit-center-left":
      return { x: position.left, y: position.top + workflowGraphNodeHeight * 0.5 };
    case "exit-bottom-left":
      return { x: position.left + nodeWidth * 0.1, y: position.top + workflowGraphNodeHeight * 0.96 };
    case "exit-bottom-mid":
      return { x: position.left + nodeWidth * 0.62, y: position.top + workflowGraphNodeHeight };
    case "exit-bottom-right":
      return { x: position.left + nodeWidth * 0.9, y: position.top + workflowGraphNodeHeight * 0.96 };
  }
}

function projectAwayFromNode(
  position: PhasePosition,
  anchor: GraphAnchor,
  point: { x: number; y: number },
  offset: number
): { x: number; y: number } {
  switch (anchor) {
    case "entry-top":
    case "entry-top-left":
    case "entry-top-right":
      return { x: point.x, y: position.top - offset };
    case "entry-left":
    case "entry-center-left":
      return { x: position.left - offset, y: point.y };
    case "entry-right":
    case "entry-center-right":
      return { x: point.x + offset, y: point.y };
    case "exit-right":
    case "exit-center-right":
      return { x: point.x + offset, y: point.y };
    case "exit-left":
    case "exit-center-left":
      return { x: position.left - offset, y: point.y };
    case "exit-bottom-left":
    case "exit-bottom-mid":
    case "exit-bottom-right":
      return { x: point.x, y: position.top + workflowGraphNodeHeight + offset };
  }
}

function isDownwardFlowAnchor(anchor: GraphAnchor): boolean {
  return anchor === "exit-bottom-left" || anchor === "exit-bottom-mid" || anchor === "exit-bottom-right";
}

function isTopEntryAnchor(anchor: GraphAnchor): boolean {
  return anchor === "entry-top" || anchor === "entry-top-left" || anchor === "entry-top-right";
}

function isVerticalAnchor(anchor: GraphAnchor): boolean {
  return isDownwardFlowAnchor(anchor) || isTopEntryAnchor(anchor);
}

function isHorizontalAnchor(anchor: GraphAnchor): boolean {
  return !isVerticalAnchor(anchor);
}
