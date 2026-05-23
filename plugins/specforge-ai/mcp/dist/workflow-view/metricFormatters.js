"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.formatDuration = formatDuration;
exports.formatMetricNumber = formatMetricNumber;
exports.formatTokensPerSecond = formatTokensPerSecond;
function formatDuration(durationMs) {
    if (durationMs < 1_000) {
        return `${durationMs} ms`;
    }
    if (durationMs < 60_000) {
        return `${(durationMs / 1_000).toFixed(durationMs >= 10_000 ? 1 : 2)} s`;
    }
    const minutes = Math.floor(durationMs / 60_000);
    const seconds = ((durationMs % 60_000) / 1_000).toFixed(1);
    return `${minutes}m ${seconds}s`;
}
function formatMetricNumber(value) {
    return new Intl.NumberFormat("en-US").format(value);
}
function formatTokensPerSecond(outputTokens, durationMs) {
    if (durationMs <= 0) {
        return "n/a";
    }
    return `${(outputTokens / (durationMs / 1_000)).toFixed(1)} tok/s`;
}
//# sourceMappingURL=metricFormatters.js.map