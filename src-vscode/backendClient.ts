import type { SpecForgeSettings } from "./extensionSettings";
import type { ModelResponseDiagnostic } from "./mcpDiagnostics";
import { StdioMcpBackendClient } from "./stdioMcpBackendClient";

export interface UserStorySummary {
  readonly usId: string;
  readonly title: string;
  readonly description?: string;
  readonly createdBy: string;
  readonly owner: string;
  readonly category: string;
  readonly tags?: readonly string[];
  readonly directoryPath: string;
  readonly mainArtifactPath: string;
  readonly currentPhase: string;
  readonly status: string;
  readonly workBranch: string | null;
  readonly dependencies?: readonly UserStoryDependencySummary[];
  readonly workflowKind?: string;
  readonly parentUsId?: string | null;
  readonly childUsIds?: readonly string[] | null;
}

export interface UserStoryDependencySummary {
  readonly usId: string;
  readonly title: string | null;
  readonly currentPhase: string | null;
  readonly status: string | null;
  readonly isSatisfied: boolean;
  readonly missingReason: string | null;
}

export interface UserStoryRuntimeStatus {
  readonly usId: string;
  readonly status: string;
  readonly activeOperation: string | null;
  readonly currentPhase: string;
  readonly startedAtUtc: string | null;
  readonly lastHeartbeatUtc: string | null;
  readonly lastOutcome: string | null;
  readonly lastCompletedAtUtc: string | null;
  readonly message: string | null;
  readonly isStale: boolean;
}

export interface ContinuePhaseResult {
  readonly usId: string;
  readonly currentPhase: string;
  readonly status: string;
  readonly generatedArtifactPath: string | null;
  readonly usage: TokenUsage | null;
  readonly execution?: PhaseExecutionMetadata | null;
  readonly commit?: PhaseCommitResult | null;
}

export interface PhaseCommitResult {
  readonly isGitWorkspace: boolean;
  readonly commitCreated: boolean;
  readonly commitSha: string | null;
  readonly message: string | null;
  readonly stagedPaths: readonly string[];
}

export interface TokenUsage {
  readonly inputTokens: number;
  readonly outputTokens: number;
  readonly totalTokens: number;
}

export interface PhaseExecutionMetadata {
  readonly providerKind: string;
  readonly model: string;
  readonly profileName: string | null;
  readonly agentName?: string | null;
  readonly agentRole?: string | null;
  readonly baseUrl: string | null;
  readonly warnings?: readonly string[] | null;
  readonly inputSha256?: string | null;
  readonly outputSha256?: string | null;
  readonly structuredOutputSha256?: string | null;
  readonly receiptPath?: string | null;
  readonly usedSkills?: readonly string[] | null;
}

type ModelResponseListener = (diagnostic: ModelResponseDiagnostic) => void;

const modelResponseListeners = new Set<ModelResponseListener>();

export function onModelResponseDiagnostic(listener: ModelResponseListener): () => void {
  modelResponseListeners.add(listener);
  return () => {
    modelResponseListeners.delete(listener);
  };
}

export function notifyModelResponseDiagnostic(diagnostic: ModelResponseDiagnostic): void {
  for (const listener of modelResponseListeners) {
    listener(diagnostic);
  }
}

export interface WorkflowLineageFinding {
  readonly severity: string;
  readonly confidence: string;
  readonly code: string;
  readonly summary: string;
  readonly phaseId: string | null;
  readonly eventTimestampUtc: string | null;
  readonly affectedArtifacts: readonly string[];
}

export interface WorkflowLineageAnalysisResult {
  readonly usId: string;
  readonly status: string;
  readonly findings: readonly WorkflowLineageFinding[];
  readonly deprecatedCandidatePaths: readonly string[];
  readonly recommendedTargetPhase: string | null;
}

export interface WorkflowLineageRepairResult {
  readonly usId: string;
  readonly status: string;
  readonly currentPhase: string;
  readonly archiveDirectoryPath: string;
  readonly archivedPaths: readonly string[];
  readonly analysis: WorkflowLineageAnalysisResult;
}

export interface CreateOrImportUserStoryResult {
  readonly usId: string;
  readonly rootDirectory: string;
  readonly mainArtifactPath: string;
}

export interface OperateCurrentPhaseArtifactResult {
  readonly usId: string;
  readonly currentPhase: string;
  readonly status: string;
  readonly operationLogPath: string;
  readonly sourceArtifactPath: string;
  readonly generatedArtifactPath: string;
  readonly usage: TokenUsage | null;
  readonly execution?: PhaseExecutionMetadata | null;
  readonly commit?: PhaseCommitResult | null;
}

export interface SubmitApprovalAnswerResult {
  readonly usId: string;
  readonly currentPhase: string;
  readonly status: string;
  readonly generatedArtifactPath: string;
}

export interface ApprovalAnswerSuggestionResult {
  readonly usId: string;
  readonly currentPhase: string;
  readonly status: string;
  readonly question: string;
  readonly answer: string | null;
  readonly usage: TokenUsage | null;
  readonly durationMs: number;
  readonly execution?: PhaseExecutionMetadata | null;
}

export interface InitializeRepoPromptsResult {
  readonly workspaceRoot: string;
  readonly configPath: string;
  readonly promptManifestPath: string;
  readonly promptSystemHashesPath: string;
  readonly createdFiles: readonly string[];
  readonly skippedFiles: readonly string[];
}

export interface RequestRegressionResult {
  readonly usId: string;
  readonly currentPhase: string;
  readonly status: string;
}

export interface RestartUserStoryResult {
  readonly usId: string;
  readonly currentPhase: string;
  readonly status: string;
  readonly generatedArtifactPath: string | null;
}

export interface ResetUserStoryResult {
  readonly usId: string;
  readonly currentPhase: string;
  readonly status: string;
  readonly deletedPaths: readonly string[];
  readonly preservedPaths: readonly string[];
}

export interface RewindWorkflowResult {
  readonly usId: string;
  readonly currentPhase: string;
  readonly status: string;
  readonly deletedPaths: readonly string[];
  readonly preservedPaths: readonly string[];
}

export interface WorkflowPhaseDetails {
  readonly phaseId: string;
  readonly title: string;
  readonly order: number;
  readonly requiresApproval: boolean;
  readonly expectsHumanIntervention: boolean;
  readonly isApproved: boolean;
  readonly isCurrent: boolean;
  readonly state: string;
  readonly artifactPath: string | null;
  readonly operationLogPath?: string | null;
  readonly executePromptPath: string | null;
  readonly approvePromptPath: string | null;
  readonly executeSystemPromptPath?: string | null;
  readonly approveSystemPromptPath?: string | null;
  readonly executionBoundary?: PhaseExecutionBoundarySummary | null;
  readonly captureRecord?: CaptureExecutionRecord | null;
  readonly executionReadiness?: PhaseExecutionReadiness | null;
  readonly executionPolicy?: PhaseExecutionPolicy | null;
  readonly executionEnvelope?: PhaseExecutionEnvelope | null;
  readonly harnessProfile?: ResolvedHarnessPhaseProfile | null;
  readonly specApprovalPolicy?: SpecPhaseApprovalPolicyDetails | null;
  readonly technicalDesignGateContract?: TechnicalDesignGateContractDetails | null;
  readonly reviewPolicy?: ReviewPhasePolicyDetails | null;
  readonly releaseApprovalPolicy?: ReleaseApprovalPolicyDetails | null;
  readonly prPreparationPolicy?: PrPreparationPolicyDetails | null;
  readonly latestExecutionInspection?: PhaseExecutionInspectionDetails | null;
  readonly runtimeMetrics?: PhaseRuntimeMetrics | null;
}

export interface HarnessProfileDefinition {
  readonly key: string;
  readonly title: string;
  readonly summary: string;
  readonly inheritsFrom?: string | null;
  readonly traits: readonly string[];
}

export interface HarnessProfileGovernance {
  readonly authority: string;
  readonly lockMode: string;
  readonly allowPerUserStoryOverrides: boolean;
  readonly lockedPhaseIds: readonly string[];
}

export interface ResolvedHarnessPhaseProfile {
  readonly phaseId: string;
  readonly selectedProfile: string;
  readonly resolvedProfile: string;
  readonly resolutionSource: string;
  readonly isLocked: boolean;
  readonly overrideAllowedNow: boolean;
  readonly authority: string;
  readonly lockMode: string;
  readonly lockReason?: string | null;
  readonly title: string;
  readonly summary: string;
  readonly traits: readonly string[];
  readonly inheritsFrom?: string | null;
}

export interface WorkflowRuntimeMetrics {
  readonly attemptCount: number;
  readonly retryCount: number;
  readonly leadTimeMs?: number | null;
  readonly waitingUserDurationMs: number;
  readonly blockedDurationMs: number;
  readonly firstEventAtUtc?: string | null;
  readonly lastEventAtUtc?: string | null;
}

export interface PhaseRuntimeMetrics {
  readonly phaseId: string;
  readonly attemptCount: number;
  readonly retryCount: number;
  readonly leadTimeMs?: number | null;
  readonly executionDurationMs: number;
  readonly waitingUserDurationMs: number;
  readonly blockedDurationMs: number;
  readonly firstEventAtUtc?: string | null;
  readonly lastEventAtUtc?: string | null;
}

export interface PhaseExecutionBoundarySummary {
  readonly boundaryKind: string;
  readonly isModelBacked: boolean;
  readonly summary: string;
}

export interface CaptureExecutionRecord {
  readonly actor: string;
  readonly createdAtUtc: string;
  readonly sourceKind: string;
  readonly sourceReference?: string | null;
  readonly materializedArtifacts: readonly string[];
}

export interface PhaseExecutionPromptSource {
  readonly role: string;
  readonly path: string;
  readonly isOverride: boolean;
  readonly contentSha256?: string | null;
  readonly embeddedContentSha256?: string | null;
}

export interface PhaseExecutionEffectivePrompt {
  readonly systemPrompt: string;
  readonly userPrompt: string;
  readonly warnings?: readonly string[] | null;
  readonly sourcePrompts?: readonly PhaseExecutionPromptSource[] | null;
}

export interface PhaseExecutionArtifactInput {
  readonly path: string;
  readonly sha256?: string | null;
  readonly phaseId?: string | null;
}

export interface PhaseExecutionEffectiveContext {
  readonly workspaceRoot: string;
  readonly userStoryPath: string;
  readonly workspaceGitHeadSha?: string | null;
  readonly previousArtifacts: readonly PhaseExecutionArtifactInput[];
  readonly contextFiles: readonly PhaseExecutionArtifactInput[];
  readonly currentArtifact?: PhaseExecutionArtifactInput | null;
  readonly operationPromptSha256?: string | null;
  readonly technicalDesignContextPack?: TechnicalDesignContextPack | null;
}

export interface PhaseExecutionInspectionDetails {
  readonly receiptPath?: string | null;
  readonly autoRefinementAnswerInspection?: AutoRefinementAnswerInspectionDetails | null;
  readonly evidenceRecord?: PhaseExecutionEvidenceRecord | null;
  readonly refinementPolicySnapshot?: RefinementPolicyDetails | null;
  readonly refinementSkillPreselection?: RefinementSkillPreselection | null;
  readonly refinementGraphScopeRequest?: RefinementGraphScopeRequest | null;
  readonly specApprovalPolicySnapshot?: SpecPhaseApprovalPolicyDetails | null;
  readonly technicalDesignGateSnapshot?: TechnicalDesignGateSnapshot | null;
  readonly implementationPolicySnapshot?: ImplementationPhasePolicySnapshot | null;
  readonly reviewPolicySnapshot?: ReviewPhasePolicySnapshot | null;
  readonly releaseApprovalPolicySnapshot?: ReleaseApprovalPhasePolicySnapshot | null;
  readonly implementationStructuredEvidence?: ImplementationStructuredEvidence | null;
  readonly reviewStructuredGateResult?: ReviewStructuredGateResult | null;
  readonly releaseApprovalEvidencePack?: ReleaseApprovalEvidencePack | null;
  readonly prPreparationStructuredEvidence?: PrPreparationStructuredEvidence | null;
  readonly technicalDesignContextPack?: TechnicalDesignContextPack | null;
  readonly effectivePrompt?: PhaseExecutionEffectivePrompt | null;
  readonly effectiveContext?: PhaseExecutionEffectiveContext | null;
}

export interface AutoRefinementAnswerInspectionDetails {
  readonly status: string;
  readonly summary: string;
  readonly reason?: string | null;
  readonly resolvedAnswerCount: number;
  readonly timestampUtc?: string | null;
  readonly receiptPath?: string | null;
  readonly effectivePrompt?: PhaseExecutionEffectivePrompt | null;
  readonly effectiveContext?: PhaseExecutionEffectiveContext | null;
}

export interface ImplementationPhasePolicySnapshot {
  readonly phaseId: string;
  readonly policyKey: string;
  readonly summary: string;
  readonly executionAllowed: boolean;
  readonly executionBlockingReason?: string | null;
  readonly permissions: PhaseExecutionRequirements;
  readonly allowedTools: readonly PhaseExecutionToolPermission[];
  readonly writablePaths: readonly PhaseExecutionPathPolicy[];
  readonly forbiddenPaths: readonly PhaseExecutionPathPolicy[];
  readonly evidenceRequirements: readonly PhaseExecutionEvidenceRequirement[];
  readonly eligibilityRules: readonly PhaseExecutionEligibilityRule[];
}

export interface ImplementationStructuredEvidence {
  readonly generatedAtUtc: string;
  readonly evidenceJsonPath: string;
  readonly evidenceMarkdownPath: string;
  readonly summary: readonly string[];
  readonly touchedFiles: readonly ImplementationTouchedFileEvidence[];
  readonly graphEvidence?: ImplementationGraphEvidence | null;
}

export interface ImplementationTouchedFileEvidence {
  readonly path: string;
  readonly changeKind: string;
  readonly baselineStatusCode?: string | null;
  readonly currentStatusCode: string;
  readonly baselineFingerprint?: string | null;
  readonly currentFingerprint: string;
}

export interface ImplementationGraphEvidence {
  readonly graphScopeRequestAvailable: boolean;
  readonly graphScopeRequestPath?: string | null;
  readonly impactGraphPath?: string | null;
  readonly impactGraphMetadataPath?: string | null;
  readonly impactSummaryPath?: string | null;
  readonly impactGraphState?: string | null;
  readonly operationReferences: readonly ImplementationGraphOperationReference[];
  readonly warnings: readonly string[];
}

export interface ImplementationGraphOperationReference {
  readonly eventId: string;
  readonly timestamp: string;
  readonly eventFamily: string;
  readonly requestedMode: string;
  readonly actualMode: string;
  readonly triggerSurface: string;
  readonly fallbackUsed: boolean;
  readonly latencyMs: number;
  readonly artifactsRead: readonly string[];
  readonly artifactsWritten: readonly string[];
  readonly warnings: readonly string[];
}

export interface ReviewStructuredGateResult {
  readonly verdict: string;
  readonly primaryReason: string;
  readonly hasBlockingFindings: boolean;
  readonly passedValidationItemCount: number;
  readonly failedValidationItemCount: number;
  readonly deferredValidationItemCount: number;
  readonly findingsSummary: readonly string[];
  readonly correctionTargets: readonly ReviewCorrectionTarget[];
  readonly linkedEvidence: readonly ReviewEvidenceLink[];
}

export interface ReviewCorrectionTarget {
  readonly item: string;
  readonly status: string;
  readonly isBlocking: boolean;
  readonly evidence: string;
  readonly suggestedAction: string;
}

export interface ReviewEvidenceLink {
  readonly kind: string;
  readonly path: string;
  readonly summary?: string | null;
}

export interface ReviewPhasePolicyDetails {
  readonly activeEvidencePolicy: string;
  readonly latestGateVerdict?: string | null;
  readonly latestHasBlockingFindings?: boolean | null;
  readonly forceApprovalAvailableNow: boolean;
  readonly forceApprovalRequiresReason: boolean;
  readonly forceApprovalBlockingReason?: string | null;
  readonly evidenceRules: readonly ReviewEvidencePolicyRule[];
  readonly overrideConditions: readonly ReviewPhaseOverrideCondition[];
  readonly lastForceApprovalDecision?: ReviewForceApprovalDecision | null;
}

export interface ReviewEvidencePolicyRule {
  readonly evidenceKind: string;
  readonly isBlocking: boolean;
  readonly currentStatusMessage: string;
}

export interface ReviewPhaseOverrideCondition {
  readonly id: string;
  readonly description: string;
  readonly status: string;
  readonly isCurrentlySatisfied: boolean;
  readonly blockingReason?: string | null;
  readonly currentStatusMessage?: string | null;
}

export interface ReviewForceApprovalDecision {
  readonly actor: string;
  readonly timestampUtc: string;
  readonly targetPhase: string;
  readonly reason: string;
}

export interface ReviewPhasePolicySnapshot {
  readonly phaseId: string;
  readonly policyKey: string;
  readonly summary: string;
  readonly executionAllowed: boolean;
  readonly executionBlockingReason?: string | null;
  readonly permissions: PhaseExecutionRequirements;
  readonly evidenceRequirements: readonly PhaseExecutionEvidenceRequirement[];
  readonly eligibilityRules: readonly PhaseExecutionEligibilityRule[];
  readonly activeEvidencePolicy: string;
  readonly latestGateVerdict?: string | null;
  readonly latestHasBlockingFindings?: boolean | null;
  readonly forceApprovalRequiresReason: boolean;
  readonly evidenceRules: readonly ReviewEvidencePolicyRule[];
  readonly overrideConditions: readonly ReviewPhaseOverrideCondition[];
}

export interface ReleaseApprovalEvidencePack {
  readonly generatedAtUtc: string;
  readonly releaseApprovalArtifactPath: string;
  readonly reviewVerdict?: string | null;
  readonly reviewPrimaryReason?: string | null;
  readonly changedFiles: readonly ReleaseApprovalChangedFile[];
  readonly validationResults: readonly ReleaseApprovalValidationResult[];
  readonly releaseRiskSummary: readonly string[];
  readonly supportingArtifacts: readonly ReleaseApprovalArtifactLink[];
}

export interface ReleaseApprovalPolicyDetails {
  readonly status: string;
  readonly executionEligibleNow: boolean;
  readonly executionBlockingReason?: string | null;
  readonly approvalAvailableNow: boolean;
  readonly approvalBlockingReason?: string | null;
  readonly latestReviewVerdict?: string | null;
  readonly latestReviewWasForceApproved: boolean;
  readonly hasReleaseArtifact: boolean;
  readonly hasReleaseEvidencePack: boolean;
  readonly hasImplementationEvidence: boolean;
  readonly hasReviewGateResult: boolean;
  readonly hasBranchContext: boolean;
  readonly hasTimelineContext: boolean;
  readonly currentWorkspaceHeadSha?: string | null;
  readonly approvedReviewCommitSha?: string | null;
  readonly reviewCommitMatchesWorkspaceHead?: boolean | null;
  readonly evidenceRules: readonly ReleaseApprovalEvidenceRule[];
  readonly executionConditions: readonly ReleaseApprovalPolicyCondition[];
  readonly approvalConditions: readonly ReleaseApprovalPolicyCondition[];
}

export interface ReleaseApprovalEvidenceRule {
  readonly evidenceKind: string;
  readonly isRequired: boolean;
  readonly currentStatusMessage: string;
}

export interface ReleaseApprovalPolicyCondition {
  readonly id: string;
  readonly description: string;
  readonly status: string;
  readonly isCurrentlySatisfied: boolean;
  readonly blockingReason?: string | null;
  readonly currentStatusMessage?: string | null;
}

export interface ReleaseApprovalPhasePolicySnapshot {
  readonly phaseId: string;
  readonly policyKey: string;
  readonly summary: string;
  readonly status: string;
  readonly executionAllowed: boolean;
  readonly executionBlockingReason?: string | null;
  readonly permissions: PhaseExecutionRequirements;
  readonly evidenceRequirements: readonly PhaseExecutionEvidenceRequirement[];
  readonly eligibilityRules: readonly PhaseExecutionEligibilityRule[];
  readonly approvalAvailableNow: boolean;
  readonly approvalBlockingReason?: string | null;
  readonly latestReviewVerdict?: string | null;
  readonly latestReviewWasForceApproved: boolean;
  readonly hasReleaseArtifact: boolean;
  readonly hasReleaseEvidencePack: boolean;
  readonly hasImplementationEvidence: boolean;
  readonly hasReviewGateResult: boolean;
  readonly hasBranchContext: boolean;
  readonly hasTimelineContext: boolean;
  readonly currentWorkspaceHeadSha?: string | null;
  readonly approvedReviewCommitSha?: string | null;
  readonly reviewCommitMatchesWorkspaceHead?: boolean | null;
  readonly evidenceRules: readonly ReleaseApprovalEvidenceRule[];
  readonly executionConditions: readonly ReleaseApprovalPolicyCondition[];
  readonly approvalConditions: readonly ReleaseApprovalPolicyCondition[];
}

export interface PrPreparationStructuredEvidence {
  readonly generatedAtUtc: string;
  readonly prPreparationArtifactPath: string;
  readonly state: string;
  readonly prTitle: string;
  readonly prSummary: string;
  readonly baseBranch: string;
  readonly workBranch: string;
  readonly releaseApprovalArtifactAvailable: boolean;
  readonly releaseApprovalEvidencePackAvailable: boolean;
  readonly basedOn: readonly string[];
  readonly participants: readonly PrPreparationParticipant[];
  readonly validationSummary: readonly string[];
  readonly reviewerChecklist: readonly string[];
  readonly linkedEvidence: readonly PrPreparationEvidenceLink[];
}

export interface PrPreparationParticipant {
  readonly actor: string;
  readonly phases: readonly string[];
}

export interface PrPreparationEvidenceLink {
  readonly kind: string;
  readonly path: string;
  readonly summary?: string | null;
}

export interface PrPreparationPolicyDetails {
  readonly status: string;
  readonly publicationReadyNow: boolean;
  readonly publicationBlockingReason?: string | null;
  readonly publicationMode: string;
  readonly hasPrPreparationArtifact: boolean;
  readonly hasBranchMetadata: boolean;
  readonly hasReleaseApprovalArtifact: boolean;
  readonly hasReleaseApprovalEvidencePack: boolean;
  readonly hasValidationSummary: boolean;
  readonly hasReviewerChecklist: boolean;
  readonly hasPrBody: boolean;
  readonly existingPullRequestReusable: boolean;
  readonly existingPullRequestStatus?: string | null;
  readonly existingPullRequestUrl?: string | null;
  readonly baseBranch?: string | null;
  readonly workBranch?: string | null;
  readonly requirementRules: readonly PrPreparationRequirementRule[];
  readonly publicationConditions: readonly PrPreparationPublicationCondition[];
}

export interface PrPreparationRequirementRule {
  readonly id: string;
  readonly description: string;
  readonly isRequired: boolean;
  readonly currentStatusMessage: string;
}

export interface PrPreparationPublicationCondition {
  readonly id: string;
  readonly description: string;
  readonly status: string;
  readonly isCurrentlySatisfied: boolean;
  readonly blockingReason?: string | null;
  readonly currentStatusMessage?: string | null;
}

export interface ReleaseApprovalChangedFile {
  readonly path: string;
  readonly changeKind: string;
  readonly currentStatusCode: string;
  readonly baselineStatusCode?: string | null;
}

export interface ReleaseApprovalValidationResult {
  readonly status: string;
  readonly item: string;
  readonly evidence: string;
}

export interface ReleaseApprovalArtifactLink {
  readonly kind: string;
  readonly path: string;
  readonly summary?: string | null;
}

export interface TechnicalDesignContextPack {
  readonly selectedSkills: readonly RefinementSkillSelectionItem[];
  readonly graphScopeRequest?: RefinementGraphScopeRequest | null;
  readonly impactGraphState?: string | null;
  readonly impactSummaryPath?: string | null;
  readonly graphEnabled: boolean;
  readonly graphAvailable: boolean;
  readonly fallbackUsed: boolean;
  readonly graphBackedExpansions: readonly TechnicalDesignGraphExpansion[];
  readonly graphQueryEvidence: readonly TechnicalDesignGraphQueryEvidence[];
  readonly warnings: readonly string[];
}

export interface TechnicalDesignGraphExpansion {
  readonly path: string;
  readonly reason: string;
  readonly source: string;
  readonly projectPath?: string | null;
  readonly sha256?: string | null;
}

export interface TechnicalDesignGraphQueryEvidence {
  readonly queryKind: string;
  readonly purpose: string;
  readonly actor: string;
  readonly tooling: string;
  readonly modelProfile?: string | null;
  readonly sourceGraphUsed: string;
  readonly freshnessState: string;
  readonly fallbackUsed: boolean;
  readonly latencyMs: number;
  readonly tokenUsage?: TokenUsage | null;
  readonly includedFiles: readonly string[];
  readonly includedNodes: readonly string[];
  readonly inclusionReasons: readonly string[];
  readonly warnings: readonly string[];
}

export interface TechnicalDesignGateContractDetails {
  readonly status: string;
  readonly gateMode: string;
  readonly approvalRequiredNow: boolean;
  readonly approvalReadyNow: boolean;
  readonly approvalBlockingReason?: string | null;
  readonly hasTechnicalDesignArtifact: boolean;
  readonly hasStructuredTechnicalDesignArtifact: boolean;
  readonly hasValidationStrategy: boolean;
  readonly hasEvidenceRecord: boolean;
  readonly hasContextPack: boolean;
  readonly graphIntentDeclared: boolean;
  readonly gateRules: readonly TechnicalDesignGateRule[];
}

export interface TechnicalDesignGateRule {
  readonly id: string;
  readonly description: string;
  readonly status: string;
  readonly enforcement: string;
  readonly isCurrentlySatisfied: boolean;
  readonly blockingReason?: string | null;
  readonly currentStatusMessage?: string | null;
}

export interface TechnicalDesignGateSnapshot {
  readonly phaseId: string;
  readonly policyKey: string;
  readonly summary: string;
  readonly executionAllowed: boolean;
  readonly executionBlockingReason?: string | null;
  readonly permissions: PhaseExecutionRequirements;
  readonly evidenceRequirements: readonly PhaseExecutionEvidenceRequirement[];
  readonly eligibilityRules: readonly PhaseExecutionEligibilityRule[];
  readonly gateMode: string;
  readonly approvalRequiredNow: boolean;
  readonly approvalReadyNow: boolean;
  readonly approvalBlockingReason?: string | null;
  readonly hasTechnicalDesignArtifact: boolean;
  readonly hasStructuredTechnicalDesignArtifact: boolean;
  readonly hasValidationStrategy: boolean;
  readonly hasEvidenceRecord: boolean;
  readonly hasContextPack: boolean;
  readonly graphIntentDeclared: boolean;
  readonly gateRules: readonly TechnicalDesignGateRule[];
}

export interface PhaseExecutionEvidenceRecord {
  readonly actor: PhaseExecutionEvidenceActor;
  readonly inputs: readonly PhaseExecutionEvidenceReference[];
  readonly outputs: readonly PhaseExecutionEvidenceReference[];
  readonly settings: readonly PhaseExecutionEvidenceSetting[];
  readonly toolsUsed: readonly PhaseExecutionEvidenceTool[];
  readonly blockingReason?: string | null;
  readonly validationSummary: PhaseExecutionValidationSummary;
  readonly evidenceLinks: readonly PhaseExecutionEvidenceLink[];
}

export interface PhaseExecutionEvidenceActor {
  readonly kind: string;
  readonly providerKind?: string | null;
  readonly model?: string | null;
  readonly profileName?: string | null;
  readonly agentName?: string | null;
  readonly agentRole?: string | null;
}

export interface PhaseExecutionEvidenceReference {
  readonly kind: string;
  readonly path: string;
  readonly sha256?: string | null;
  readonly phaseId?: string | null;
}

export interface PhaseExecutionEvidenceSetting {
  readonly name: string;
  readonly value: string;
}

export interface PhaseExecutionEvidenceTool {
  readonly name: string;
  readonly access: string;
  readonly source: string;
}

export interface PhaseExecutionEvidenceLink {
  readonly label: string;
  readonly path: string;
  readonly kind: string;
}

export interface PhaseExecutionValidationSummary {
  readonly status: string;
  readonly summary: string;
  readonly checks: readonly string[];
}

export interface PhaseExecutionRequirements {
  readonly modelExecutionRequired: boolean;
  readonly repositoryAccess: string;
  readonly workspaceWriteAccess: boolean;
}

export interface PhaseExecutionModelSecurity {
  readonly providerKind: string;
  readonly model: string;
  readonly profileName: string | null;
  readonly repositoryAccess: string;
  readonly nativeCliRequired: boolean;
  readonly nativeCliAvailable: boolean;
  readonly agentName?: string | null;
  readonly agentRole?: string | null;
}

export interface PhaseExecutionReadiness {
  readonly phaseId: string;
  readonly canExecute: boolean;
  readonly blockingReason: string | null;
  readonly requiredPermissions?: PhaseExecutionRequirements | null;
  readonly assignedModelSecurity?: PhaseExecutionModelSecurity | null;
  readonly validationMessage?: string | null;
  readonly phaseSubagentsEnabled?: boolean | null;
}

export interface PhaseExecutionPolicy {
  readonly phaseId: string;
  readonly policyKey: string;
  readonly summary: string;
  readonly permissions: PhaseExecutionRequirements;
  readonly allowedTools: readonly PhaseExecutionToolPermission[];
  readonly writablePaths: readonly PhaseExecutionPathPolicy[];
  readonly forbiddenPaths: readonly PhaseExecutionPathPolicy[];
  readonly evidenceRequirements: readonly PhaseExecutionEvidenceRequirement[];
  readonly eligibilityRules: readonly PhaseExecutionEligibilityRule[];
}

export interface PhaseExecutionToolPermission {
  readonly tool: string;
  readonly access: string;
  readonly enforcement: string;
  readonly reason: string;
}

export interface PhaseExecutionPathPolicy {
  readonly path: string;
  readonly access: string;
  readonly actor: string;
  readonly enforcement: string;
  readonly reason: string;
}

export interface PhaseExecutionEvidenceRequirement {
  readonly id: string;
  readonly description: string;
  readonly enforcement: string;
  readonly policyInput?: string | null;
}

export interface PhaseExecutionEligibilityRule {
  readonly id: string;
  readonly description: string;
  readonly enforcement: string;
  readonly blockingReason?: string | null;
  readonly isCurrentlySatisfied?: boolean | null;
  readonly currentStatusMessage?: string | null;
}

export interface PhaseExecutionEnvelope {
  readonly phaseId: string;
  readonly envelopeKey: string;
  readonly executionMode: string;
  readonly sandboxMode: string;
  readonly toolPermissions: readonly PhaseExecutionEnvelopeToolPermission[];
  readonly writeScopes: readonly PhaseExecutionEnvelopeWriteScope[];
  readonly repositoryBoundaries: readonly PhaseExecutionEnvelopeBoundary[];
  readonly budget: PhaseExecutionEnvelopeBudget;
}

export interface PhaseExecutionEnvelopeToolPermission {
  readonly actor: string;
  readonly tool: string;
  readonly access: string;
  readonly enforcement: string;
}

export interface PhaseExecutionEnvelopeWriteScope {
  readonly actor: string;
  readonly path: string;
  readonly access: string;
  readonly enforcement: string;
}

export interface PhaseExecutionEnvelopeBoundary {
  readonly kind: string;
  readonly path: string;
  readonly access: string;
  readonly summary: string;
}

export interface PhaseExecutionEnvelopeBudget {
  readonly computeTier: string;
  readonly tokenBudget: string;
  readonly timeBudget: string;
  readonly mutationBudget: string;
  readonly notes: string;
}

export interface SpecPhaseApprovalPolicyDetails {
  readonly status: string;
  readonly approvalAvailableNow: boolean;
  readonly approvalBlockingReason: string | null;
  readonly hasSpecArtifact: boolean;
  readonly schemaIsValid: boolean;
  readonly hasUnresolvedApprovalQuestions: boolean;
  readonly unresolvedApprovalQuestionCount: number;
  readonly decompositionApprovalPending: boolean;
  readonly approvalRules: readonly SpecPhaseApprovalRule[];
}

export interface SpecPhaseApprovalRule {
  readonly id: string;
  readonly description: string;
  readonly status: string;
  readonly isCurrentlySatisfied: boolean;
  readonly blockingReason?: string | null;
  readonly currentStatusMessage?: string | null;
}

export interface RefinementQuestionAnswerDetails {
  readonly index: number;
  readonly question: string;
  readonly answer: string | null;
}

export interface RefinementSessionDetails {
  readonly status: string;
  readonly tolerance: string;
  readonly reason: string | null;
  readonly items: readonly RefinementQuestionAnswerDetails[];
  readonly policy?: RefinementPolicyDetails | null;
}

export interface RefinementPolicyDetails {
  readonly tolerance: string;
  readonly pendingQuestionCount: number;
  readonly unansweredQuestionCount: number;
  readonly blockingConditions: readonly RefinementBlockingCondition[];
  readonly autoAnswer: RefinementAutoAnswerPolicy;
}

export interface RefinementBlockingCondition {
  readonly id: string;
  readonly description: string;
  readonly status: string;
  readonly isCurrentlyBlocking: boolean;
  readonly blockingReason?: string | null;
}

export interface RefinementAutoAnswerPolicy {
  readonly isEnabled: boolean;
  readonly mode: string;
  readonly summary: string;
  readonly profileName?: string | null;
  readonly agentName?: string | null;
  readonly agentRole?: string | null;
  readonly isCurrentlyEligible: boolean;
  readonly eligibilityStatus: string;
  readonly eligibilityReason?: string | null;
  readonly lastAttempt?: AutoRefinementAnswerInspectionDetails | null;
}

export interface RefinementSkillPreselection {
  readonly requiredSkills: readonly RefinementSkillSelectionItem[];
  readonly candidateSkills: readonly RefinementSkillSelectionItem[];
  readonly rejectedSkills: readonly RefinementSkillSelectionItem[];
  readonly contextGaps: readonly string[];
}

export interface RefinementSkillSelectionItem {
  readonly skillPath: string;
  readonly rationale: string;
}

export interface RefinementGraphScopeRequest {
  readonly depth: number;
  readonly seedNodes: readonly RefinementGraphSeedNode[];
  readonly seedFiles: readonly PhaseExecutionArtifactInput[];
  readonly unresolvedScopeQuestions: readonly string[];
}

export interface RefinementGraphSeedNode {
  readonly id: string;
  readonly label: string;
  readonly reason: string;
}

export interface ApprovalQuestionDetails {
  readonly index: number;
  readonly question: string;
  readonly status: string;
  readonly isResolved: boolean;
  readonly answer: string | null;
  readonly answeredBy: string | null;
  readonly answeredAtUtc: string | null;
}

export interface DecompositionChildDraft {
  readonly title: string;
  readonly objective: string;
  readonly acceptanceCriteria: readonly string[];
  readonly dependencies: readonly string[];
}

export interface DecompositionDetails {
  readonly state: string;
  readonly decision: string;
  readonly complexityScore: number;
  readonly threshold: number;
  readonly tolerance: number;
  readonly rationale: string;
  readonly artifactPath: string | null;
  readonly proposedChildren: readonly DecompositionChildDraft[];
  readonly createdChildUsIds: readonly string[];
}

export interface CurrentPhaseControls {
  readonly canContinue: boolean;
  readonly canApprove: boolean;
  readonly requiresApproval: boolean;
  readonly blockingReason: string | null;
  readonly canRestartFromSource: boolean;
  readonly regressionTargets: readonly string[];
  readonly rewindTargets?: readonly string[];
  readonly executionPhase?: string | null;
  readonly executionReadiness?: PhaseExecutionReadiness | null;
}

export interface TimelineEventDetails {
  readonly timestampUtc: string;
  readonly code: string;
  readonly actor: string | null;
  readonly phase: string | null;
  readonly summary: string | null;
  readonly artifacts: readonly string[];
  readonly usage: TokenUsage | null;
  readonly durationMs: number | null;
  readonly execution?: PhaseExecutionMetadata | null;
}

export interface PhaseIterationDetails {
  readonly iterationKey: string;
  readonly attempt: number;
  readonly phaseId: string;
  readonly timestampUtc: string;
  readonly code: string;
  readonly actor: string | null;
  readonly summary: string | null;
  readonly outputArtifactPath: string;
  readonly inputArtifactPath: string | null;
  readonly contextArtifactPaths: readonly string[];
  readonly operationLogPath: string | null;
  readonly operationPrompt: string | null;
  readonly usage: TokenUsage | null;
  readonly durationMs: number | null;
  readonly execution?: PhaseExecutionMetadata | null;
}

export interface UserStoryFileDetails {
  readonly name: string;
  readonly path: string;
}

export interface PullRequestDetails {
  readonly status: string;
  readonly title: string;
  readonly isDraft: boolean;
  readonly number: number | null;
  readonly url: string | null;
  readonly remoteBranch: string | null;
  readonly publishedAtUtc: string | null;
}

export interface UserStoryWorkflowDetails {
  readonly usId: string;
  readonly title: string;
  readonly kind?: string;
  readonly category: string;
  readonly tags?: readonly string[];
  readonly status: string;
  readonly currentPhase: string;
  readonly directoryPath: string;
  readonly workBranch: string | null;
  readonly mainArtifactPath: string;
  readonly timelinePath: string;
  readonly rawTimeline: string;
  readonly dependencies?: readonly UserStoryDependencySummary[];
  readonly workflowKind?: string;
  readonly parentUsId?: string | null;
  readonly childUserStories?: readonly UserStorySummary[];
  readonly decomposition?: DecompositionDetails | null;
  readonly pullRequest?: PullRequestDetails | null;
  readonly harnessProfileGovernance?: HarnessProfileGovernance | null;
  readonly phases: readonly WorkflowPhaseDetails[];
  readonly controls: CurrentPhaseControls;
  readonly metrics?: WorkflowRuntimeMetrics | null;
  readonly refinement: RefinementSessionDetails | null;
  readonly approvalQuestions?: readonly ApprovalQuestionDetails[];
  readonly events: readonly TimelineEventDetails[];
  readonly phaseIterations?: readonly PhaseIterationDetails[];
  readonly contextFilesDirectoryPath?: string;
  readonly contextFiles?: readonly UserStoryFileDetails[];
  readonly attachmentsDirectoryPath: string;
  readonly attachments: readonly UserStoryFileDetails[];
}

export interface SpecForgeBackendClient {
  listUserStories(visibility?: "active" | "dropped"): Promise<readonly UserStorySummary[]>;
  getUserStorySummary(usId: string): Promise<UserStorySummary>;
  getUserStoryWorkflow(usId: string): Promise<UserStoryWorkflowDetails>;
  getUserStoryRuntimeStatus(usId: string): Promise<UserStoryRuntimeStatus>;
  analyzeUserStoryLineage(usId: string): Promise<WorkflowLineageAnalysisResult>;
  repairUserStoryLineage(usId: string, actor?: string): Promise<WorkflowLineageRepairResult>;
  createUserStory(usId: string, title: string, kind: string, category: string, sourceText: string, actor?: string, tags?: readonly string[]): Promise<CreateOrImportUserStoryResult>;
  importUserStory(usId: string, sourcePath: string, title: string, kind: string, category: string, actor?: string, tags?: readonly string[]): Promise<CreateOrImportUserStoryResult>;
  initializeRepoPrompts(overwrite?: boolean): Promise<InitializeRepoPromptsResult>;
  exportPromptTemplate(promptPath: string, overwrite?: boolean): Promise<InitializeRepoPromptsResult>;
  continuePhase(usId: string, actor?: string): Promise<ContinuePhaseResult>;
  approveReviewAnyway(usId: string, reason: string, actor?: string): Promise<ContinuePhaseResult>;
  approveCurrentPhase(usId: string, baseBranch?: string, workBranch?: string, actor?: string): Promise<UserStorySummary>;
  requestRegression(usId: string, targetPhase: string, reason?: string, actor?: string, destructive?: boolean): Promise<RequestRegressionResult>;
  reopenCompletedWorkflow(usId: string, reasonKind: string, description: string, actor?: string): Promise<RequestRegressionResult>;
  restartUserStoryFromSource(usId: string, reason?: string, actor?: string): Promise<RestartUserStoryResult>;
  rewindWorkflow(usId: string, targetPhase: string, actor?: string, destructive?: boolean): Promise<RewindWorkflowResult>;
  resetUserStoryToCapture(usId: string): Promise<ResetUserStoryResult>;
  submitRefinementAnswers(usId: string, answers: readonly string[], actor?: string): Promise<void>;
  submitApprovalAnswer(usId: string, question: string, answer: string, actor?: string): Promise<SubmitApprovalAnswerResult>;
  suggestApprovalAnswer(usId: string, question: string, actor?: string): Promise<ApprovalAnswerSuggestionResult>;
  operateCurrentPhaseArtifact(
    usId: string,
    prompt: string,
    actor?: string,
    includeReviewArtifactInContext?: boolean
  ): Promise<OperateCurrentPhaseArtifactResult>;
  isBusy(): boolean;
  cancelActiveOperations(): void;
  dispose(): void;
}

export function createMcpBackendClient(
  workspaceRoot: string,
  hostRoot: string,
  settings: SpecForgeSettings
): SpecForgeBackendClient {
  return new StdioMcpBackendClient(workspaceRoot, hostRoot, settings);
}
