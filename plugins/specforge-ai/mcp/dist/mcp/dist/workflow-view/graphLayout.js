"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.workflowGraphNodeHeight = void 0;
exports.buildGraphLegendPosition = buildGraphLegendPosition;
exports.buildHorizontalPhaseLayout = buildHorizontalPhaseLayout;
exports.buildVerticalPhaseLayout = buildVerticalPhaseLayout;
exports.graphPath = graphPath;
const workflowGraphLayout_1 = require("../workflowGraphLayout");
exports.workflowGraphNodeHeight = 102;
function buildGraphLegendPosition(x, y, compact = false) {
    const scale = compact ? 0.72 : 1;
    return {
        left: Math.round(x * scale),
        top: Math.round(y * scale)
    };
}
function buildHorizontalPhaseLayout(phases, nodeWidth, compact = false, sourcePositions = workflowGraphLayout_1.defaultHorizontalWorkflowGraphPositions) {
    const positions = {};
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
        height: computeGraphHeight(positions, exports.workflowGraphNodeHeight, compact ? 98 : 162)
    };
}
function buildVerticalPhaseLayout(phases, nodeWidth, compact = false, sourcePositions = workflowGraphLayout_1.defaultVerticalWorkflowGraphPositions) {
    const positions = {};
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
        height: computeGraphHeight(positions, exports.workflowGraphNodeHeight, compact ? 128 : 178)
    };
}
function graphPath(fromPhaseId, toPhaseId, positions, nodeWidth, graphLayoutMode, edgeConnection) {
    const fromPosition = positions[fromPhaseId];
    const toPosition = positions[toPhaseId];
    if (!fromPosition || !toPosition) {
        return "";
    }
    return buildOrthogonalGraphPath(fromPosition, toPosition, nodeWidth, graphLayoutMode, edgeConnection);
}
function computeGraphHeight(positions, nodeHeight, bottomPadding) {
    const maxTop = Math.max(...Object.values(positions).map((position) => position.top));
    return maxTop + nodeHeight + bottomPadding;
}
function computeGraphWidth(positions, nodeWidth, rightPadding) {
    const maxLeft = Math.max(...Object.values(positions).map((position) => position.left));
    return maxLeft + nodeWidth + rightPadding;
}
function buildOrthogonalGraphPath(fromPosition, toPosition, nodeWidth, graphLayoutMode, edgeConnection) {
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
function resolveAnchorsForLayout(from, to, graphLayoutMode, edgeConnection) {
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
function resolvePreferredAxis(graphLayoutMode, fromAnchor, toAnchor, startLead, endLead) {
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
function buildOrthogonalWaypoints(from, to, preferredAxis) {
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
function simplifyOrthogonalPoints(points) {
    const normalized = [];
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
function buildRoundedPath(points, radius) {
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
function movePointTowards(from, to, distance) {
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
function isAxisAligned(first, second) {
    return first.x === second.x || first.y === second.y;
}
function arePointsCollinear(first, second, third) {
    return (first.x === second.x && second.x === third.x) || (first.y === second.y && second.y === third.y);
}
function resolveAnchors(from, to, edgeConnection) {
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
function getAnchorPointFromCodeOrAnchor(position, anchor, nodeWidth, isExit) {
    return isAnchorCode(anchor)
        ? getAnchorPointFromCode(position, anchor, nodeWidth)
        : getAnchorPoint(position, anchor, nodeWidth);
}
function getAnchorPointFromCode(position, anchorCode, nodeWidth) {
    const face = anchorCode[0];
    const slot = Number.parseInt(anchorCode[1], 10);
    const fraction = slot / 6;
    switch (face) {
        case "T":
            return { x: position.left + nodeWidth * fraction, y: position.top };
        case "R":
            return { x: position.left + nodeWidth, y: position.top + exports.workflowGraphNodeHeight * fraction };
        case "B":
            return { x: position.left + nodeWidth * fraction, y: position.top + exports.workflowGraphNodeHeight };
        case "L":
            return { x: position.left, y: position.top + exports.workflowGraphNodeHeight * fraction };
        default:
            return { x: position.left + nodeWidth * 0.5, y: position.top + exports.workflowGraphNodeHeight * 0.5 };
    }
}
function toGraphAnchor(anchor, isExit) {
    if (!isAnchorCode(anchor)) {
        return anchor;
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
function isAnchorCode(anchor) {
    return /^[TLRB][1-5]$/.test(anchor);
}
function getAnchorPoint(position, anchor, nodeWidth) {
    switch (anchor) {
        case "entry-top":
            return { x: position.left + nodeWidth * 0.5, y: position.top };
        case "entry-top-left":
            return { x: position.left + nodeWidth * 0.26, y: position.top };
        case "entry-top-right":
            return { x: position.left + nodeWidth * 0.74, y: position.top };
        case "entry-left":
            return { x: position.left, y: position.top + exports.workflowGraphNodeHeight * 0.36 };
        case "entry-center-left":
            return { x: position.left, y: position.top + exports.workflowGraphNodeHeight * 0.5 };
        case "entry-right":
            return { x: position.left + nodeWidth, y: position.top + exports.workflowGraphNodeHeight * 0.34 };
        case "entry-center-right":
            return { x: position.left + nodeWidth, y: position.top + exports.workflowGraphNodeHeight * 0.5 };
        case "exit-right":
            return { x: position.left + nodeWidth, y: position.top + exports.workflowGraphNodeHeight * 0.78 };
        case "exit-center-right":
            return { x: position.left + nodeWidth, y: position.top + exports.workflowGraphNodeHeight * 0.5 };
        case "exit-left":
            return { x: position.left, y: position.top + exports.workflowGraphNodeHeight * 0.78 };
        case "exit-center-left":
            return { x: position.left, y: position.top + exports.workflowGraphNodeHeight * 0.5 };
        case "exit-bottom-left":
            return { x: position.left + nodeWidth * 0.1, y: position.top + exports.workflowGraphNodeHeight * 0.96 };
        case "exit-bottom-mid":
            return { x: position.left + nodeWidth * 0.62, y: position.top + exports.workflowGraphNodeHeight };
        case "exit-bottom-right":
            return { x: position.left + nodeWidth * 0.9, y: position.top + exports.workflowGraphNodeHeight * 0.96 };
    }
}
function projectAwayFromNode(position, anchor, point, offset) {
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
            return { x: point.x, y: position.top + exports.workflowGraphNodeHeight + offset };
    }
}
function isDownwardFlowAnchor(anchor) {
    return anchor === "exit-bottom-left" || anchor === "exit-bottom-mid" || anchor === "exit-bottom-right";
}
function isTopEntryAnchor(anchor) {
    return anchor === "entry-top" || anchor === "entry-top-left" || anchor === "entry-top-right";
}
function isVerticalAnchor(anchor) {
    return isDownwardFlowAnchor(anchor) || isTopEntryAnchor(anchor);
}
function isHorizontalAnchor(anchor) {
    return !isVerticalAnchor(anchor);
}
//# sourceMappingURL=graphLayout.js.map