export function formatDuration(durationMs: number): string {
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

export function formatMetricNumber(value: number): string {
  return new Intl.NumberFormat("en-US").format(value);
}

export function formatTokensPerSecond(outputTokens: number, durationMs: number): string {
  if (durationMs <= 0) {
    return "n/a";
  }

  return `${(outputTokens / (durationMs / 1_000)).toFixed(1)} tok/s`;
}
