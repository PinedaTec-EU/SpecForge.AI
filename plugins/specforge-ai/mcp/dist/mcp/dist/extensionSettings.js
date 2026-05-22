"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.recommendedBootstrapPhaseAgentAssignments = exports.recommendedBootstrapAgentProfiles = void 0;
exports.getSpecForgeSettings = getSpecForgeSettings;
exports.readSpecForgeSettings = readSpecForgeSettings;
exports.shouldBootstrapRecommendedAgentProfiles = shouldBootstrapRecommendedAgentProfiles;
exports.shouldBootstrapRecommendedPhaseAgentAssignments = shouldBootstrapRecommendedPhaseAgentAssignments;
exports.buildBackendEnvironment = buildBackendEnvironment;
exports.getSpecForgeSettingsStatus = getSpecForgeSettingsStatus;
const executionSettingsModel_1 = require("./executionSettingsModel");
exports.recommendedBootstrapAgentProfiles = [
    {
        name: "planner",
        role: "planner",
        modelProfile: "",
        instructions: "Focus on requirements, workflow consistency, and repository-aware planning.",
        repositoryAccess: "read"
    },
    {
        name: "implementer",
        role: "implementer",
        modelProfile: "",
        instructions: "Implement approved technical designs with focused code changes and matching tests.",
        repositoryAccess: "read-write"
    },
    {
        name: "reviewer",
        role: "reviewer",
        modelProfile: "",
        instructions: "Review implementation changes for correctness, regressions, missing tests, and release risk.",
        repositoryAccess: "read"
    },
    {
        name: "release-preparer",
        role: "release-preparer",
        modelProfile: "",
        instructions: "Prepare release and pull request artifacts from repository evidence.",
        repositoryAccess: "read"
    }
];
exports.recommendedBootstrapPhaseAgentAssignments = {
    defaultAgent: "planner",
    captureAgent: "planner",
    refinementAgent: "planner",
    specAgent: "planner",
    technicalDesignAgent: "planner",
    implementationAgent: "implementer",
    reviewAgent: "reviewer",
    releaseApprovalAgent: "release-preparer",
    prPreparationAgent: "release-preparer"
};
function getSpecForgeSettings() {
    const vscode = require("vscode");
    return readSpecForgeSettings(vscode.workspace.getConfiguration("specForge"));
}
function readSpecForgeSettings(configuration) {
    const modelProfiles = normalizeModelProfiles(configuration.get("execution.modelProfiles", []));
    const configuredAgentProfiles = normalizeAgentProfiles(configuration.get("execution.agentProfiles", []));
    const effectiveAgentProfiles = resolveEffectiveAgentProfiles(configuredAgentProfiles, modelProfiles);
    const configuredPhaseAgentAssignments = normalizePhaseAgentAssignments(configuration.get("execution.phaseAgents"));
    const configuredPhaseHarnessProfiles = normalizePhaseHarnessProfiles(configuration.get("execution.phaseHarnessProfiles"));
    const phaseAgentAssignments = resolveEffectivePhaseAgentConfiguration(configuredAgentProfiles, configuredPhaseAgentAssignments);
    const autoRefinementAnswersProfile = normalizeUnknownOptional(configuration.get("execution.autoRefinementAnswersProfile"));
    const reviewEvidencePolicy = normalizeReviewEvidencePolicy(configuration.get("execution.reviewEvidencePolicy", "balanced"));
    const userStoryListViewMode = configuration.get("ui.userStoryListViewMode") === "phase"
        ? "phase"
        : "category";
    const reviewLearningEnabled = configuration.get("features.reviewLearningEnabled", true);
    const reviewLearningSkillPath = normalizeUnknownOptional(configuration.get("features.reviewLearningSkillPath", ".codex/skills/sdd-phase-agents/SKILL.md"));
    return {
        modelProfiles,
        agentProfiles: effectiveAgentProfiles,
        phaseAgentAssignments,
        effectivePhaseAgentAssignments: resolveEffectivePhaseAgentAssignments(effectiveAgentProfiles, phaseAgentAssignments),
        defaultHarnessProfile: normalizeHarnessProfileKey(configuration.get("execution.defaultHarnessProfile", "balanced")),
        phaseHarnessProfiles: configuredPhaseHarnessProfiles,
        harnessProfileAuthority: normalizeHarnessProfileAuthority(configuration.get("execution.harnessProfileAuthority", "workspace")),
        harnessProfileLockMode: normalizeHarnessProfileLockMode(configuration.get("execution.harnessProfileLockMode", "none")),
        lockedHarnessPhaseIds: normalizeLockedHarnessPhaseIds(configuration.get("execution.lockedHarnessPhaseIds")),
        allowPerUserStoryHarnessProfileOverrides: configuration.get("execution.allowPerUserStoryHarnessProfileOverrides", true),
        autoRefinementAnswersProfile,
        refinementTolerance: normalizeTolerance(configuration.get("execution.refinementTolerance", "balanced")),
        mvpRigor: normalizeMvpRigor(configuration.get("execution.mvpRigor", "medium")),
        reviewTolerance: normalizeTolerance(configuration.get("execution.reviewTolerance", "balanced")),
        reviewEvidencePolicy,
        technicalDesignSubagentsEnabled: configuration.get("execution.technicalDesignSubagentsEnabled", false),
        reviewSubagentsEnabled: configuration.get("execution.reviewSubagentsEnabled", false),
        workflowGraphLayoutMode: configuration.get("ui.workflowGraphLayoutMode", "vertical") === "horizontal" ? "horizontal" : "vertical",
        workflowGraphInitialZoomMode: configuration.get("ui.workflowGraphInitialZoomMode", "actual-size") === "fit-width" ? "fit-width" : "actual-size",
        userStoryListViewMode,
        visualTimelineEnabled: configuration.get("ui.visualTimelineEnabled", false),
        watcherEnabled: configuration.get("ui.enableWatcher", true),
        attentionNotificationsEnabled: configuration.get("ui.notifyOnAttention", true),
        contextSuggestionsEnabled: configuration.get("features.enableContextSuggestions", true),
        requireExplicitApprovalBranchAcceptance: configuration.get("features.requireApprovalBranchAcceptance", false),
        autoRefinementAnswersEnabled: configuration.get("features.autoRefinementAnswersEnabled", false),
        phaseSkillUsageReportingEnabled: configuration.get("features.phaseSkillUsageReportingEnabled", true),
        autoPlayEnabled: configuration.get("features.autoPlayEnabled", false),
        autoReviewEnabled: configuration.get("features.autoReviewEnabled", false),
        maxRefinementCycles: normalizeOptionalPositiveInteger(configuration.get("features.maxRefinementCycles", 5)),
        maxImplementationReviewCycles: normalizeOptionalPositiveInteger(configuration.get("features.maxImplementationReviewCycles", 5)),
        destructiveRewindEnabled: configuration.get("features.destructiveRewindEnabled", false),
        pauseOnFailedReview: configuration.get("features.pauseOnFailedReview", false),
        useSemanticGraphWhenAvailable: configuration.get("features.useSemanticGraphWhenAvailable", true),
        allowGraphBuildRefreshForTouchedUserStoryScope: configuration.get("features.allowGraphBuildRefreshForTouchedUserStoryScope", false),
        reviewLearningEnabled,
        reviewLearningSkillPath,
        completedUsLockOnCompleted: configuration.get("features.completedUsLockOnCompleted", false)
    };
}
function shouldBootstrapRecommendedAgentProfiles(configuration) {
    return normalizeAgentProfiles(configuration.get("execution.agentProfiles", [])).length === 0;
}
function shouldBootstrapRecommendedPhaseAgentAssignments(configuration) {
    return !hasAnyPhaseAgentAssignment(normalizePhaseAgentAssignments(configuration.get("execution.phaseAgents")));
}
function buildBackendEnvironment(settings) {
    const env = {};
    if (settings.modelProfiles.length > 0) {
        env.SPECFORGE_OPENAI_MODEL_PROFILES_JSON = JSON.stringify(settings.modelProfiles);
        env.SPECFORGE_OPENAI_AGENT_PROFILES_JSON = JSON.stringify(resolveConfiguredOrDerivedAgentProfiles(settings));
        env.SPECFORGE_OPENAI_PHASE_AGENT_ASSIGNMENTS_JSON = JSON.stringify(settings.phaseAgentAssignments);
    }
    env.SPECFORGE_REFINEMENT_TOLERANCE = settings.refinementTolerance;
    env.SPECFORGE_MVP_RIGOR = settings.mvpRigor ?? "medium";
    env.SPECFORGE_REVIEW_TOLERANCE = settings.reviewTolerance;
    env.SPECFORGE_REVIEW_EVIDENCE_POLICY = settings.reviewEvidencePolicy ?? "balanced";
    env.SPECFORGE_TECHNICAL_DESIGN_SUBAGENTS_ENABLED = settings.technicalDesignSubagentsEnabled === true ? "true" : "false";
    env.SPECFORGE_REVIEW_SUBAGENTS_ENABLED = settings.reviewSubagentsEnabled === true ? "true" : "false";
    env.SPECFORGE_AUTO_REFINEMENT_ANSWERS_ENABLED = settings.autoRefinementAnswersEnabled ? "true" : "false";
    env.SPECFORGE_PHASE_SKILL_USAGE_REPORTING_ENABLED = settings.phaseSkillUsageReportingEnabled === false ? "false" : "true";
    env.SPECFORGE_USE_SEMANTIC_GRAPH_WHEN_AVAILABLE = settings.useSemanticGraphWhenAvailable ? "true" : "false";
    env.SPECFORGE_ALLOW_GRAPH_BUILD_REFRESH_FOR_TOUCHED_US_SCOPE =
        settings.allowGraphBuildRefreshForTouchedUserStoryScope ? "true" : "false";
    env.SPECFORGE_HARNESS_PROFILE_DEFAULT = settings.defaultHarnessProfile;
    env.SPECFORGE_HARNESS_PHASE_PROFILES_JSON = JSON.stringify(settings.phaseHarnessProfiles);
    env.SPECFORGE_HARNESS_PROFILE_AUTHORITY = settings.harnessProfileAuthority;
    env.SPECFORGE_HARNESS_PROFILE_LOCK_MODE = settings.harnessProfileLockMode;
    env.SPECFORGE_HARNESS_LOCKED_PHASE_IDS_JSON = JSON.stringify(settings.lockedHarnessPhaseIds);
    env.SPECFORGE_ALLOW_PER_US_HARNESS_PROFILE_OVERRIDES =
        settings.allowPerUserStoryHarnessProfileOverrides ? "true" : "false";
    env.SPECFORGE_REVIEW_LEARNING_ENABLED = settings.reviewLearningEnabled === false ? "false" : "true";
    env.SPECFORGE_REVIEW_LEARNING_SKILL_PATH =
        settings.reviewLearningSkillPath ?? ".codex/skills/sdd-phase-agents/SKILL.md";
    env.SPECFORGE_COMPLETED_US_LOCK_ON_COMPLETED = settings.completedUsLockOnCompleted ? "true" : "false";
    env.SPECFORGE_MAX_REFINEMENT_CYCLES = String(settings.maxRefinementCycles ?? 5);
    env.SPECFORGE_MAX_IMPLEMENTATION_REVIEW_CYCLES = String(settings.maxImplementationReviewCycles ?? 5);
    if (settings.autoRefinementAnswersProfile) {
        env.SPECFORGE_AUTO_REFINEMENT_ANSWERS_PROFILE = settings.autoRefinementAnswersProfile;
    }
    return env;
}
function getSpecForgeSettingsStatus(settings) {
    const diagnostics = buildSettingsDiagnostics(settings);
    const agentProfiles = resolveConfiguredOrDerivedAgentProfiles(settings);
    if (settings.modelProfiles.length === 0) {
        return {
            executionConfigured: false,
            message: "SpecForge.AI needs at least one configured model profile before workflow stages can run.",
            diagnostics
        };
    }
    if (agentProfiles.length === 0) {
        return {
            executionConfigured: false,
            message: "SpecForge.AI needs at least one configured agent profile before workflow stages can run.",
            diagnostics
        };
    }
    return getProfileSettingsStatus(settings, diagnostics);
}
const defaultModelProvider = "openai-compatible";
const supportedModelProviders = new Set(["openai-compatible", "codex", "copilot", "claude"]);
const nativeCliModelProviders = new Set(["codex", "copilot", "claude"]);
function getProfileSettingsStatus(settings, diagnostics) {
    const agentProfiles = resolveConfiguredOrDerivedAgentProfiles(settings);
    const modelsByName = new Map();
    for (const profile of settings.modelProfiles) {
        const duplicate = modelsByName.has(profile.name);
        modelsByName.set(profile.name, profile);
        if (!profile.name) {
            return { executionConfigured: false, message: "SpecForge.AI found a model profile without a name.", diagnostics };
        }
        if (!supportedModelProviders.has(profile.provider)) {
            return { executionConfigured: false, message: `SpecForge.AI model profile '${profile.name}' uses unsupported provider '${profile.provider}'.`, diagnostics };
        }
        if (duplicate) {
            return { executionConfigured: false, message: `SpecForge.AI found duplicate model profile name '${profile.name}'.`, diagnostics };
        }
        if (!isNativeCliModelProvider(profile.provider) && !profile.baseUrl) {
            return { executionConfigured: false, message: `SpecForge.AI model profile '${profile.name}' is missing base URL.`, diagnostics };
        }
        if (!isNativeCliModelProvider(profile.provider) && !profile.model) {
            return { executionConfigured: false, message: `SpecForge.AI model profile '${profile.name}' is missing model.`, diagnostics };
        }
        if (!isNativeCliModelProvider(profile.provider) && !profile.apiKey && !isLocalOpenAiCompatibleEndpoint(profile.baseUrl)) {
            return { executionConfigured: false, message: `SpecForge.AI model profile '${profile.name}' needs an API key for a remote base URL.`, diagnostics };
        }
    }
    const agentsByName = new Map();
    for (const agent of agentProfiles) {
        const duplicate = agentsByName.has(agent.name);
        agentsByName.set(agent.name, agent);
        if (!agent.name) {
            return { executionConfigured: false, message: "SpecForge.AI found an agent profile without a name.", diagnostics };
        }
        if (duplicate) {
            return { executionConfigured: false, message: `SpecForge.AI found duplicate agent profile name '${agent.name}'.`, diagnostics };
        }
        if (!agent.modelProfile || !modelsByName.has(agent.modelProfile)) {
            return { executionConfigured: false, message: `SpecForge.AI agent profile '${agent.name}' references unknown model profile '${agent.modelProfile}'.`, diagnostics };
        }
    }
    const defaultAgentName = settings.phaseAgentAssignments.defaultAgent
        ?? (agentProfiles.length === 1 ? agentProfiles[0]?.name ?? null : null);
    if (!defaultAgentName && !hasExplicitAgentsForAllModelDrivenPhases(settings.phaseAgentAssignments)) {
        return {
            executionConfigured: false,
            message: "SpecForge.AI needs either a default phase agent assignment or explicit agents for refinement, spec, technical design, implementation, and review.",
            diagnostics
        };
    }
    const namedAssignments = [
        ["default", defaultAgentName],
        ["capture", settings.phaseAgentAssignments.captureAgent],
        ["refinement", settings.phaseAgentAssignments.refinementAgent],
        ["spec", settings.phaseAgentAssignments.specAgent],
        ["technicalDesign", settings.phaseAgentAssignments.technicalDesignAgent],
        ["implementation", settings.phaseAgentAssignments.implementationAgent],
        ["review", settings.phaseAgentAssignments.reviewAgent],
        ["releaseApproval", settings.phaseAgentAssignments.releaseApprovalAgent],
        ["prPreparation", settings.phaseAgentAssignments.prPreparationAgent]
    ];
    for (const [assignmentName, agentName] of namedAssignments) {
        if (agentName && !agentsByName.has(agentName)) {
            return { executionConfigured: false, message: `SpecForge.AI phase agent assignment '${assignmentName}' references unknown agent '${agentName}'.`, diagnostics };
        }
    }
    if (settings.autoRefinementAnswersEnabled && !settings.autoRefinementAnswersProfile) {
        return { executionConfigured: false, message: "SpecForge.AI needs an auto-refinement answers agent when model-driven refinement answers are enabled.", diagnostics };
    }
    if (settings.autoRefinementAnswersProfile && !agentsByName.has(settings.autoRefinementAnswersProfile)) {
        return { executionConfigured: false, message: `SpecForge.AI auto-refinement answers agent references unknown agent '${settings.autoRefinementAnswersProfile}'.`, diagnostics };
    }
    const permissionIssues = (0, executionSettingsModel_1.validatePhasePermissionAssignments)(agentProfiles, settings.phaseAgentAssignments);
    if (permissionIssues.length > 0) {
        return {
            executionConfigured: false,
            message: permissionIssues[0]?.message ?? "SpecForge.AI found a phase agent permission mismatch.",
            diagnostics
        };
    }
    return { executionConfigured: true, message: null, diagnostics };
}
function isNativeCliModelProvider(provider) {
    return nativeCliModelProviders.has(provider);
}
function resolveConfiguredOrDerivedAgentProfiles(settings) {
    if (settings.agentProfiles && settings.agentProfiles.length > 0) {
        return settings.agentProfiles;
    }
    return resolveEffectiveAgentProfiles([], settings.modelProfiles);
}
function resolveEffectiveAgentProfiles(configuredAgentProfiles, modelProfiles) {
    if (configuredAgentProfiles.length > 0) {
        return configuredAgentProfiles;
    }
    if (modelProfiles.length === 0) {
        return exports.recommendedBootstrapAgentProfiles;
    }
    return modelProfiles.map((profile) => ({
        name: profile.name,
        role: profile.name,
        modelProfile: profile.name,
        instructions: "",
        repositoryAccess: profile.repositoryAccess,
        ...(profile.reasoningEffort ? { reasoningEffort: profile.reasoningEffort } : {})
    }));
}
function resolveEffectivePhaseAgentConfiguration(configuredAgentProfiles, assignments) {
    if (configuredAgentProfiles.length > 0 || hasAnyPhaseAgentAssignment(assignments)) {
        return assignments;
    }
    return exports.recommendedBootstrapPhaseAgentAssignments;
}
function hasAnyPhaseAgentAssignment(assignments) {
    return Boolean(assignments.defaultAgent
        || assignments.captureAgent
        || assignments.refinementAgent
        || assignments.specAgent
        || assignments.technicalDesignAgent
        || assignments.implementationAgent
        || assignments.reviewAgent
        || assignments.releaseApprovalAgent
        || assignments.prPreparationAgent);
}
function hasExplicitAgentsForAllModelDrivenPhases(assignments) {
    return [
        assignments.refinementAgent,
        assignments.specAgent,
        assignments.technicalDesignAgent,
        assignments.implementationAgent,
        assignments.reviewAgent
    ].every((value) => Boolean(value));
}
function normalizeOptionalPositiveInteger(value) {
    if (typeof value === "number" && Number.isFinite(value)) {
        const normalized = Math.trunc(value);
        return normalized > 0 ? normalized : null;
    }
    if (typeof value === "string") {
        const trimmed = value.trim();
        if (trimmed.length === 0) {
            return null;
        }
        const parsed = Number.parseInt(trimmed, 10);
        return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
    }
    return null;
}
function buildSettingsDiagnostics(settings) {
    const agentProfiles = resolveConfiguredOrDerivedAgentProfiles(settings);
    const models = settings.modelProfiles.map((profile) => `${profile.name || "<missing-name>"}{provider=${profile.provider || "<missing>"},baseUrl=${profile.baseUrl || "<missing>"},model=${profile.model || "<missing>"}${profile.reasoningEffort ? `,reasoningEffort=${profile.reasoningEffort}` : ""},apiKey=${profile.apiKey ? "set" : "empty"}}`);
    const agents = agentProfiles.map((agent) => `${agent.name || "<missing-name>"}{role=${agent.role || "<missing>"},modelProfile=${agent.modelProfile || "<missing>"},repositoryAccess=${agent.repositoryAccess || "<missing>"}}`);
    return [
        `modelProfiles=${settings.modelProfiles.length}`,
        `models=[${models.join(", ")}]`,
        `agentProfiles=${agentProfiles.length}`,
        `agents=[${agents.join(", ")}]`,
        `phaseAgents.default=${settings.phaseAgentAssignments.defaultAgent ?? "<unset>"}`,
        `phaseAgents.capture=${settings.phaseAgentAssignments.captureAgent ?? "<unset>"}`,
        `phaseAgents.refinement=${settings.phaseAgentAssignments.refinementAgent ?? "<unset>"}`,
        `phaseAgents.spec=${settings.phaseAgentAssignments.specAgent ?? "<unset>"}`,
        `phaseAgents.technicalDesign=${settings.phaseAgentAssignments.technicalDesignAgent ?? "<unset>"}`,
        `phaseAgents.implementation=${settings.phaseAgentAssignments.implementationAgent ?? "<unset>"}`,
        `phaseAgents.review=${settings.phaseAgentAssignments.reviewAgent ?? "<unset>"}`,
        `phaseAgents.releaseApproval=${settings.phaseAgentAssignments.releaseApprovalAgent ?? "<unset>"}`,
        `phaseAgents.prPreparation=${settings.phaseAgentAssignments.prPreparationAgent ?? "<unset>"}`,
        `subagents.technicalDesign=${settings.technicalDesignSubagentsEnabled === true}`,
        `subagents.review=${settings.reviewSubagentsEnabled === true}`,
        `mvpRigor=${settings.mvpRigor ?? "medium"}`,
        `autoRefinementAnswers.enabled=${settings.autoRefinementAnswersEnabled}`,
        `autoRefinementAnswers.agent=${settings.autoRefinementAnswersProfile ?? "<unset>"}`,
        `phaseSkillUsageReporting.enabled=${settings.phaseSkillUsageReportingEnabled !== false}`,
        `semanticGraph.useWhenAvailable=${settings.useSemanticGraphWhenAvailable}`,
        `semanticGraph.allowBuildRefreshForTouchedUs=${settings.allowGraphBuildRefreshForTouchedUserStoryScope}`,
        `autoReviewEnabled=${settings.autoReviewEnabled}`,
        `maxRefinementCycles=${settings.maxRefinementCycles ?? "<unset>"}`,
        `maxImplementationReviewCycles=${settings.maxImplementationReviewCycles ?? "<unset>"}`,
        `pauseOnFailedReview=${settings.pauseOnFailedReview}`,
        `reviewLearningEnabled=${settings.reviewLearningEnabled === false ? false : true}`,
        `reviewLearningSkillPath=${settings.reviewLearningSkillPath ?? "<unset>"}`,
        `effective.default=${settings.effectivePhaseAgentAssignments.defaultAgentName ?? "<unset>"}`,
        `effective.capture=${settings.effectivePhaseAgentAssignments.captureAgentName ?? "<unset>"}`,
        `effective.refinement=${settings.effectivePhaseAgentAssignments.refinementAgentName ?? "<unset>"}`,
        `effective.spec=${settings.effectivePhaseAgentAssignments.specAgentName ?? "<unset>"}`,
        `effective.technicalDesign=${settings.effectivePhaseAgentAssignments.technicalDesignAgentName ?? "<unset>"}`,
        `effective.implementation=${settings.effectivePhaseAgentAssignments.implementationAgentName ?? "<unset>"}`,
        `effective.review=${settings.effectivePhaseAgentAssignments.reviewAgentName ?? "<unset>"}`,
        `effective.releaseApproval=${settings.effectivePhaseAgentAssignments.releaseApprovalAgentName ?? "<unset>"}`,
        `effective.prPreparation=${settings.effectivePhaseAgentAssignments.prPreparationAgentName ?? "<unset>"}`
    ].join("; ");
}
function normalizeOptional(value) {
    const trimmed = value?.trim();
    return trimmed ? trimmed : null;
}
function normalizeRepositoryAccess(value) {
    const normalized = normalizeUnknownOptional(value)?.toLowerCase();
    return normalized === "read-write" || normalized === "readwrite" || normalized === "write"
        ? "read-write"
        : normalized === "read"
            ? "read"
            : normalized === "none"
                ? "none"
                : null;
}
function normalizeModelProfiles(value) {
    if (!Array.isArray(value)) {
        return [];
    }
    return value
        .map((entry) => normalizeModelProfile(entry))
        .filter((entry) => entry !== null);
}
function normalizeModelProfile(value) {
    if (!value || typeof value !== "object") {
        return null;
    }
    const candidate = value;
    const provider = normalizeUnknownOptional(candidate.provider)?.toLowerCase() ?? null;
    const name = normalizeUnknownOptional(candidate.name);
    const baseUrl = normalizeUnknownOptional(candidate.baseUrl);
    const apiKey = normalizeUnknownOptional(candidate.apiKey);
    const model = normalizeUnknownOptional(candidate.model);
    const reasoningEffort = normalizeReasoningEffort(candidate.reasoningEffort);
    const repositoryAccess = normalizeRepositoryAccess(candidate.repositoryAccess);
    if (!provider && !name && !baseUrl && !apiKey && !model && !reasoningEffort && !repositoryAccess) {
        return null;
    }
    return {
        name: name ?? "",
        provider: provider ?? defaultModelProvider,
        baseUrl: baseUrl ?? "",
        apiKey,
        model: model ?? "",
        ...(reasoningEffort ? { reasoningEffort } : {}),
        repositoryAccess: repositoryAccess ?? "none"
    };
}
function normalizeAgentProfiles(value) {
    if (!Array.isArray(value)) {
        return [];
    }
    return value
        .map((entry) => normalizeAgentProfile(entry))
        .filter((entry) => entry !== null);
}
function normalizeAgentProfile(value) {
    if (!value || typeof value !== "object") {
        return null;
    }
    const candidate = value;
    const name = normalizeUnknownOptional(candidate.name);
    const role = normalizeUnknownOptional(candidate.role);
    const modelProfile = normalizeUnknownOptional(candidate.modelProfile);
    const instructions = normalizeUnknownOptional(candidate.instructions);
    const reasoningEffort = normalizeReasoningEffort(candidate.reasoningEffort);
    const repositoryAccess = normalizeRepositoryAccess(candidate.repositoryAccess);
    if (!name && !role && !modelProfile && !instructions && !reasoningEffort && !repositoryAccess) {
        return null;
    }
    return {
        name: name ?? "",
        role: role ?? "",
        modelProfile: modelProfile ?? "",
        instructions: instructions ?? "",
        repositoryAccess: repositoryAccess ?? "none",
        ...(reasoningEffort ? { reasoningEffort } : {})
    };
}
function normalizePhaseAgentAssignments(value) {
    if (!value || typeof value !== "object") {
        return emptyPhaseAgentAssignments();
    }
    const candidate = value;
    return {
        defaultAgent: normalizeUnknownOptional(candidate.defaultAgent),
        captureAgent: normalizeUnknownOptional(candidate.captureAgent),
        refinementAgent: normalizeUnknownOptional(candidate.refinementAgent),
        specAgent: normalizeUnknownOptional(candidate.specAgent),
        technicalDesignAgent: normalizeUnknownOptional(candidate.technicalDesignAgent),
        implementationAgent: normalizeUnknownOptional(candidate.implementationAgent),
        reviewAgent: normalizeUnknownOptional(candidate.reviewAgent),
        releaseApprovalAgent: normalizeUnknownOptional(candidate.releaseApprovalAgent),
        prPreparationAgent: normalizeUnknownOptional(candidate.prPreparationAgent)
    };
}
function normalizePhaseHarnessProfiles(value) {
    if (!value || typeof value !== "object") {
        return emptyPhaseHarnessProfiles();
    }
    const candidate = value;
    return {
        defaultProfile: normalizeHarnessProfileOptional(candidate.defaultProfile),
        captureProfile: normalizeHarnessProfileOptional(candidate.captureProfile),
        refinementProfile: normalizeHarnessProfileOptional(candidate.refinementProfile),
        specProfile: normalizeHarnessProfileOptional(candidate.specProfile),
        technicalDesignProfile: normalizeHarnessProfileOptional(candidate.technicalDesignProfile),
        implementationProfile: normalizeHarnessProfileOptional(candidate.implementationProfile),
        reviewProfile: normalizeHarnessProfileOptional(candidate.reviewProfile),
        releaseApprovalProfile: normalizeHarnessProfileOptional(candidate.releaseApprovalProfile),
        prPreparationProfile: normalizeHarnessProfileOptional(candidate.prPreparationProfile)
    };
}
function emptyPhaseHarnessProfiles() {
    return {
        defaultProfile: null,
        captureProfile: null,
        refinementProfile: null,
        specProfile: null,
        technicalDesignProfile: null,
        implementationProfile: null,
        reviewProfile: null,
        releaseApprovalProfile: null,
        prPreparationProfile: null
    };
}
function emptyPhaseAgentAssignments() {
    return {
        defaultAgent: null,
        captureAgent: null,
        refinementAgent: null,
        specAgent: null,
        technicalDesignAgent: null,
        implementationAgent: null,
        reviewAgent: null,
        releaseApprovalAgent: null,
        prPreparationAgent: null
    };
}
function resolveEffectivePhaseAgentAssignments(agentProfiles, assignments) {
    const defaultAgent = resolveDefaultAgentProfile(agentProfiles, assignments);
    const defaultAgentName = defaultAgent?.name ?? null;
    return {
        defaultAgentName,
        captureAgentName: resolveAssignedAgentProfile(agentProfiles, assignments.captureAgent)?.name ?? defaultAgentName,
        refinementAgentName: resolveAssignedAgentProfile(agentProfiles, assignments.refinementAgent)?.name ?? defaultAgentName,
        specAgentName: resolveAssignedAgentProfile(agentProfiles, assignments.specAgent)?.name ?? defaultAgentName,
        technicalDesignAgentName: resolveAssignedAgentProfile(agentProfiles, assignments.technicalDesignAgent)?.name ?? defaultAgentName,
        implementationAgentName: resolveAssignedAgentProfile(agentProfiles, assignments.implementationAgent)?.name ?? defaultAgentName,
        reviewAgentName: resolveAssignedAgentProfile(agentProfiles, assignments.reviewAgent)?.name ?? defaultAgentName,
        releaseApprovalAgentName: resolveAssignedAgentProfile(agentProfiles, assignments.releaseApprovalAgent)?.name ?? defaultAgentName,
        prPreparationAgentName: resolveAssignedAgentProfile(agentProfiles, assignments.prPreparationAgent)?.name ?? defaultAgentName
    };
}
function resolveDefaultAgentProfile(agentProfiles, assignments) {
    const explicitDefault = resolveAssignedAgentProfile(agentProfiles, assignments.defaultAgent);
    if (explicitDefault) {
        return explicitDefault;
    }
    return agentProfiles.length === 1 ? agentProfiles[0] : null;
}
function resolveAssignedAgentProfile(agentProfiles, agentName) {
    if (!agentName) {
        return null;
    }
    return agentProfiles.find((profile) => profile.name === agentName) ?? null;
}
function normalizeUnknownOptional(value) {
    return typeof value === "string" ? normalizeOptional(value) : null;
}
function normalizeHarnessProfileKey(value) {
    const normalized = value?.trim().toLowerCase();
    return normalized === "strict" || normalized === "regulated" ? normalized : "balanced";
}
function normalizeHarnessProfileOptional(value) {
    const normalized = normalizeUnknownOptional(value)?.toLowerCase();
    return normalized === "strict" || normalized === "balanced" || normalized === "regulated"
        ? normalized
        : null;
}
function normalizeHarnessProfileAuthority(value) {
    return value?.trim().toLowerCase() === "central" ? "central" : "workspace";
}
function normalizeHarnessProfileLockMode(value) {
    const normalized = value?.trim().toLowerCase();
    return normalized === "phase" || normalized === "all" ? normalized : "none";
}
function normalizeLockedHarnessPhaseIds(value) {
    if (!Array.isArray(value)) {
        return [];
    }
    const normalized = value
        .map((entry) => normalizeUnknownOptional(entry)?.toLowerCase())
        .filter((phaseId) => phaseId === "capture"
        || phaseId === "refinement"
        || phaseId === "spec"
        || phaseId === "technical-design"
        || phaseId === "implementation"
        || phaseId === "review"
        || phaseId === "release-approval"
        || phaseId === "pr-preparation");
    return Array.from(new Set(normalized));
}
function normalizeTolerance(value) {
    const normalized = value?.trim().toLowerCase();
    return normalized === "strict" || normalized === "inferential" ? normalized : "balanced";
}
function normalizeMvpRigor(value) {
    const normalized = value?.trim().toLowerCase();
    return normalized === "low" || normalized === "high" ? normalized : "medium";
}
function normalizeReviewEvidencePolicy(value) {
    const normalized = value?.trim().toLowerCase();
    return normalized === "strict" || normalized === "release" || normalized === "advisory"
        ? normalized
        : "balanced";
}
function normalizeReasoningEffort(value) {
    const normalized = normalizeUnknownOptional(value)?.toLowerCase();
    return normalized === "none"
        || normalized === "minimal"
        || normalized === "low"
        || normalized === "medium"
        || normalized === "high"
        || normalized === "xhigh"
        ? normalized
        : null;
}
function isLocalOpenAiCompatibleEndpoint(baseUrl) {
    if (!baseUrl) {
        return false;
    }
    try {
        const parsed = new URL(baseUrl);
        return parsed.hostname === "localhost"
            || parsed.hostname === "127.0.0.1"
            || parsed.hostname === "::1"
            || parsed.hostname === "0.0.0.0";
    }
    catch {
        return false;
    }
}
//# sourceMappingURL=extensionSettings.js.map