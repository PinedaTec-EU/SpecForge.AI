"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.formatExecutionLabel = formatExecutionLabel;
exports.findConfiguredModelForProfile = findConfiguredModelForProfile;
function normalizeExecutionIdentity(value) {
    return value?.trim().toLowerCase() ?? "";
}
function isSuspiciousExecutionModel(execution, options) {
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
function formatExecutionLabel(execution, options) {
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
function findConfiguredModelForProfile(state, profileName) {
    if (!profileName) {
        return null;
    }
    const model = state.modelProfiles?.find((profile) => profile.name === profileName)?.model?.trim();
    return model && model.length > 0 ? model : null;
}
//# sourceMappingURL=executionLabels.js.map