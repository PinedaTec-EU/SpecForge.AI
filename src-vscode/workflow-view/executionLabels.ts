import type { WorkflowViewState } from "./models";

function normalizeExecutionIdentity(value: string | null | undefined): string {
  return value?.trim().toLowerCase() ?? "";
}

function isSuspiciousExecutionModel(
  execution: { model: string; profileName?: string | null } | null | undefined,
  options?: {
    readonly actor?: string | null;
    readonly configuredModel?: string | null;
  }
): boolean {
  const model = normalizeExecutionIdentity(execution?.model);
  if (!model) {
    return false;
  }

  const actor = normalizeExecutionIdentity(options?.actor);
  const profileName = normalizeExecutionIdentity(execution?.profileName);
  const configuredModel = normalizeExecutionIdentity(options?.configuredModel);

  return model === actor
    || model === profileName
    || (configuredModel.length > 0 && model !== configuredModel);
}

export function formatExecutionLabel(
  execution: { model: string; profileName?: string | null } | null | undefined,
  options?: {
    readonly actor?: string | null;
    readonly configuredModel?: string | null;
  }
): string | null {
  const configuredModel = options?.configuredModel?.trim() ?? "";
  if (execution?.profileName && configuredModel.length > 0) {
    return `${execution.profileName} / ${configuredModel}`;
  }

  if (!execution?.model || isSuspiciousExecutionModel(execution, options)) {
    return execution?.profileName?.trim() || configuredModel || null;
  }

  return execution.profileName
    ? `${execution.profileName} / ${execution.model}`
    : execution.model;
}

export function findConfiguredModelForProfile(
  state: WorkflowViewState,
  profileName: string | null | undefined
): string | null {
  if (!profileName) {
    return null;
  }

  const model = state.modelProfiles?.find((profile) => profile.name === profileName)?.model?.trim();
  return model && model.length > 0 ? model : null;
}
