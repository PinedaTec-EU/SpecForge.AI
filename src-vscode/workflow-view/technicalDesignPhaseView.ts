import type { UserStoryWorkflowDetails, WorkflowPhaseDetails } from "../backendClient";
import type { PhaseSectionFragments } from "./models";

interface TechnicalDesignPhaseViewArgs {
  readonly workflow: UserStoryWorkflowDetails;
  readonly selectedPhase: WorkflowPhaseDetails;
  readonly escapeHtml: (value: string) => string;
  readonly escapeHtmlAttribute: (value: string) => string;
}

export function buildTechnicalDesignPhaseSections(args: TechnicalDesignPhaseViewArgs): PhaseSectionFragments {
  const { workflow, selectedPhase, escapeHtml, escapeHtmlAttribute } = args;
  const effectivePrompt = selectedPhase.latestExecutionInspection?.effectivePrompt ?? null;
  const effectiveContext = selectedPhase.latestExecutionInspection?.effectiveContext ?? null;
  const evidenceRecord = selectedPhase.latestExecutionInspection?.evidenceRecord ?? null;
  const contextPack = selectedPhase.latestExecutionInspection?.technicalDesignContextPack
    ?? selectedPhase.latestExecutionInspection?.effectiveContext?.technicalDesignContextPack
    ?? null;
  const gateSnapshot = selectedPhase.latestExecutionInspection?.technicalDesignGateSnapshot ?? null;
  const receiptPath = selectedPhase.latestExecutionInspection?.receiptPath?.trim() || null;
  const executionReadiness = selectedPhase.executionReadiness ?? null;
  const executionPolicy = selectedPhase.executionPolicy ?? null;
  const technicalDesignGateContract = gateSnapshot ?? selectedPhase.technicalDesignGateContract ?? null;

  const technicalDesignInspectionSection = selectedPhase.phaseId === "technical-design"
    ? `
      <section class="detail-card">
        <h3>Inspect Last Technical Design Execution</h3>
        <p class="panel-copy">
          Review the latest persisted technical-design prompt and injected context before adjusting design prompts, context files, or downstream implementation expectations.
        </p>
        ${effectivePrompt || effectiveContext
          ? `
            <div class="detail-grid">
              <div><strong>Effective Prompt</strong><div><code>${effectivePrompt ? "available" : "unavailable"}</code></div></div>
              <div><strong>Warnings</strong><div><code>${effectivePrompt?.warnings?.length ?? 0}</code></div></div>
              <div><strong>Previous Artifacts</strong><div><code>${effectiveContext?.previousArtifacts.length ?? 0}</code></div></div>
              <div><strong>Context Files</strong><div><code>${effectiveContext?.contextFiles.length ?? 0}</code></div></div>
              <div><strong>Current Artifact</strong><div><code>${effectiveContext?.currentArtifact ? "available" : "unavailable"}</code></div></div>
              <div><strong>Workspace Git HEAD</strong><div><code>${escapeHtml(effectiveContext?.workspaceGitHeadSha ?? "unavailable")}</code></div></div>
            </div>
            <div class="detail-actions">
              ${effectivePrompt
                ? `<button class="workflow-action-button workflow-action-button--document" type="button" data-open-effective-prompt-modal>View Last Technical Design Prompt</button>`
                : ""}
              ${effectiveContext
                ? `<button class="workflow-action-button workflow-action-button--document" type="button" data-open-effective-context-modal>View Last Technical Design Context</button>`
                : ""}
              ${receiptPath
                ? `<button class="workflow-action-button workflow-action-button--document" data-command="openArtifact" data-path="${escapeHtmlAttribute(receiptPath)}">Open Receipt</button>`
                : ""}
            </div>
            <div class="detail-grid">
              <div>
                <strong>User Story</strong>
                <div><code>${escapeHtml(workflow.usId)}</code></div>
              </div>
              <div>
                <strong>User Story Artifact</strong>
                <div><code>${escapeHtml(effectiveContext?.userStoryPath ?? workflow.mainArtifactPath)}</code></div>
              </div>
            </div>
          `
          : `<p class="muted">No persisted technical-design execution inspection is available yet for this user story.</p>`}
      </section>
    `
    : "";

  const technicalDesignEvidenceSection = selectedPhase.phaseId === "technical-design" && evidenceRecord
    ? `
      <section class="detail-card">
        <h3>Design Evidence Record</h3>
        <p class="panel-copy">
          This is the structured evidence summary persisted with the latest technical-design receipt: inputs, outputs, orchestration settings, tools used, and validation signals.
        </p>
        <div class="detail-grid">
          <div><strong>Actor</strong><div><code>${escapeHtml(evidenceRecord.actor.agentName ?? evidenceRecord.actor.kind)}</code></div></div>
          <div><strong>Model</strong><div><code>${escapeHtml(evidenceRecord.actor.model ?? evidenceRecord.actor.providerKind ?? "runtime-managed")}</code></div></div>
          <div><strong>Inputs</strong><div><code>${evidenceRecord.inputs.length}</code></div></div>
          <div><strong>Outputs</strong><div><code>${evidenceRecord.outputs.length}</code></div></div>
          <div><strong>Tools Used</strong><div><code>${evidenceRecord.toolsUsed.length}</code></div></div>
          <div><strong>Validation</strong><div><code>${escapeHtml(evidenceRecord.validationSummary.status)}</code></div></div>
        </div>
        <div class="refinement-suggestions">
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Inputs</strong>
              ${renderEvidenceReferenceList(evidenceRecord.inputs, escapeHtml)}
            </div>
          </div>
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Outputs</strong>
              ${renderEvidenceReferenceList(evidenceRecord.outputs, escapeHtml)}
            </div>
          </div>
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Orchestration Metadata</strong>
              ${renderEvidenceSettings(evidenceRecord.settings, escapeHtml)}
            </div>
          </div>
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Validation Summary</strong>
              <span>${escapeHtml(evidenceRecord.validationSummary.summary)}</span>
              <span>Checks: <code>${escapeHtml(evidenceRecord.validationSummary.checks.join(", ") || "none")}</code></span>
              ${evidenceRecord.blockingReason ? `<span>Blocking reason: <code>${escapeHtml(evidenceRecord.blockingReason)}</code></span>` : ""}
            </div>
          </div>
        </div>
      </section>
    `
    : "";

  const technicalDesignContextPackSection = selectedPhase.phaseId === "technical-design" && contextPack
    ? `
      <section class="detail-card">
        <h3>Design Context Pack</h3>
        <p class="panel-copy">
          This is the graph-aware narrowing pack that fed the latest technical-design execution: selected skills, graph scope, impact summary, and graph-backed file expansions.
        </p>
        <div class="detail-grid">
          <div><strong>Selected Skills</strong><div><code>${contextPack.selectedSkills.length}</code></div></div>
          <div><strong>Graph Enabled</strong><div><code>${contextPack.graphEnabled ? "true" : "false"}</code></div></div>
          <div><strong>Impact Graph State</strong><div><code>${escapeHtml(contextPack.impactGraphState ?? "missing")}</code></div></div>
          <div><strong>Graph Available</strong><div><code>${contextPack.graphAvailable ? "true" : "false"}</code></div></div>
          <div><strong>Fallback Used</strong><div><code>${contextPack.fallbackUsed ? "true" : "false"}</code></div></div>
          <div><strong>Graph Expansions</strong><div><code>${contextPack.graphBackedExpansions.length}</code></div></div>
          <div><strong>Graph Queries</strong><div><code>${contextPack.graphQueryEvidence.length}</code></div></div>
        </div>
        <div class="refinement-suggestions">
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Selected Skills</strong>
              ${contextPack.selectedSkills.length === 0
                ? "<span>No selected skills were recorded for this technical-design execution.</span>"
                : contextPack.selectedSkills.map((skill) => `<span><code>${escapeHtml(skill.skillPath)}</code> · ${escapeHtml(skill.rationale)}</span>`).join("")}
            </div>
          </div>
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Graph Scope</strong>
              ${contextPack.graphScopeRequest
                ? `
                  <span>Depth: <code>${contextPack.graphScopeRequest.depth}</code></span>
                  <span>Seed nodes: <code>${contextPack.graphScopeRequest.seedNodes.length}</code></span>
                  <span>Seed files: <code>${contextPack.graphScopeRequest.seedFiles.length}</code></span>
                  <span>Unresolved questions: <code>${contextPack.graphScopeRequest.unresolvedScopeQuestions.length}</code></span>
                `
                : "<span>No graph scope request was available.</span>"}
            </div>
          </div>
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Graph-Backed Expansions</strong>
              ${contextPack.graphBackedExpansions.length === 0
                ? "<span>No graph-backed expansions were available for this execution.</span>"
                : contextPack.graphBackedExpansions.map((item) => `<span><code>${escapeHtml(item.path)}</code> · <code>${escapeHtml(item.source)}</code> · ${escapeHtml(item.reason)}</span>`).join("")}
              ${contextPack.impactSummaryPath
                ? `<span>Impact summary: <code>${escapeHtml(contextPack.impactSummaryPath)}</code></span>`
                : ""}
            </div>
          </div>
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Graph Query Evidence</strong>
              ${contextPack.graphQueryEvidence.length === 0
                ? "<span>No bounded graph query evidence was recorded for this execution.</span>"
                : contextPack.graphQueryEvidence.map((item) => `<span><code>${escapeHtml(item.queryKind)}</code> via <code>${escapeHtml(item.tooling)}</code> · source <code>${escapeHtml(item.sourceGraphUsed)}</code> · freshness <code>${escapeHtml(item.freshnessState)}</code> · latency <code>${item.latencyMs} ms</code> · ${escapeHtml(item.purpose)}</span>`).join("")}
            </div>
          </div>
        </div>
        ${contextPack.warnings.length > 0
          ? `<div class="callout callout--warning"><strong>Warnings:</strong> ${contextPack.warnings.map((warning) => escapeHtml(warning)).join(" · ")}</div>`
          : ""}
      </section>
    `
    : "";

  const technicalDesignPolicySection = selectedPhase.phaseId === "technical-design" && (executionReadiness || executionPolicy || technicalDesignGateContract)
    ? `
      <section class="detail-card">
        <h3>Technical Design Policy</h3>
        <p class="panel-copy">
          Inspect the explicit repository-access, subagent, and quality-gate rules that currently govern the technical-design phase.
        </p>
        <div class="detail-grid">
          <div><strong>Execution Readiness</strong><div><code>${executionReadiness?.canExecute ? "ready" : "blocked"}</code></div></div>
          <div><strong>Blocking Reason</strong><div><code>${escapeHtml(executionReadiness?.blockingReason ?? "none")}</code></div></div>
          <div><strong>Repository Access</strong><div><code>${escapeHtml(executionPolicy?.permissions.repositoryAccess ?? executionReadiness?.requiredPermissions?.repositoryAccess ?? "unknown")}</code></div></div>
          <div><strong>Workspace Writes</strong><div><code>${(executionPolicy?.permissions.workspaceWriteAccess ?? executionReadiness?.requiredPermissions?.workspaceWriteAccess) ? "allowed" : "not allowed"}</code></div></div>
          <div><strong>Subagents</strong><div><code>${executionReadiness?.phaseSubagentsEnabled == null ? "not-declared" : executionReadiness.phaseSubagentsEnabled ? "enabled" : "disabled"}</code></div></div>
          <div><strong>Quality Gate</strong><div><code>${escapeHtml(technicalDesignGateContract?.gateMode ?? "review-driven")}</code></div></div>
        </div>
        ${technicalDesignGateContract
          ? `
            <div class="detail-grid">
              <div><strong>Approval Required</strong><div><code>${technicalDesignGateContract.approvalRequiredNow ? "true" : "false"}</code></div></div>
              <div><strong>Approval Ready</strong><div><code>${technicalDesignGateContract.approvalReadyNow ? "true" : "false"}</code></div></div>
              <div><strong>Blocking Reason</strong><div><code>${escapeHtml(technicalDesignGateContract.approvalBlockingReason ?? "none")}</code></div></div>
              <div><strong>Snapshot</strong><div><code>${gateSnapshot ? "persisted" : "live"}</code></div></div>
              <div><strong>Structured Artifact</strong><div><code>${technicalDesignGateContract.hasStructuredTechnicalDesignArtifact ? "available" : "missing"}</code></div></div>
              <div><strong>Validation Strategy</strong><div><code>${technicalDesignGateContract.hasValidationStrategy ? "declared" : "missing"}</code></div></div>
              <div><strong>Evidence Record</strong><div><code>${technicalDesignGateContract.hasEvidenceRecord ? "available" : "missing"}</code></div></div>
              <div><strong>Context Pack</strong><div><code>${technicalDesignGateContract.hasContextPack ? "available" : "missing"}</code></div></div>
              <div><strong>Graph Intent</strong><div><code>${technicalDesignGateContract.graphIntentDeclared ? "declared" : "not-declared"}</code></div></div>
            </div>
            <div class="refinement-suggestions">
              <div class="refinement-suggestion refinement-suggestion--static">
                <div class="refinement-suggestion__body">
                  <strong>Design Gate Rules</strong>
                  ${renderPolicyRuleList(technicalDesignGateContract.gateRules, escapeHtml)}
                </div>
              </div>
            </div>
          `
          : ""}
        ${executionPolicy
          ? `
            <div class="refinement-suggestions">
              <div class="refinement-suggestion refinement-suggestion--static">
                <div class="refinement-suggestion__body">
                  <strong>Eligibility Rules</strong>
                  ${renderPolicyRuleList(executionPolicy.eligibilityRules, escapeHtml)}
                </div>
              </div>
              <div class="refinement-suggestion refinement-suggestion--static">
                <div class="refinement-suggestion__body">
                  <strong>Evidence Requirements</strong>
                  ${renderEvidenceRequirementList(executionPolicy.evidenceRequirements, escapeHtml)}
                </div>
              </div>
            </div>
          `
          : ""}
      </section>
    `
    : "";

  return {
    beforeArtifact: [
      ...(technicalDesignInspectionSection ? [technicalDesignInspectionSection] : []),
      ...(technicalDesignContextPackSection ? [technicalDesignContextPackSection] : []),
      ...(technicalDesignPolicySection ? [technicalDesignPolicySection] : []),
      ...(technicalDesignEvidenceSection ? [technicalDesignEvidenceSection] : [])
    ],
    afterArtifact: []
  };
}

function renderEvidenceReferenceList(
  items: readonly {
    readonly kind: string;
    readonly path: string;
    readonly sha256?: string | null;
    readonly phaseId?: string | null;
  }[],
  escapeHtml: (value: string) => string
): string {
  if (items.length === 0) {
    return "<span>No evidence references recorded.</span>";
  }

  return items
    .map((item) =>
      `<span><code>${escapeHtml(item.kind)}</code>${item.phaseId ? ` · <code>${escapeHtml(item.phaseId)}</code>` : ""} · ${escapeHtml(item.path)}${item.sha256 ? ` · sha256 <code>${escapeHtml(item.sha256)}</code>` : ""}</span>`)
    .join("");
}

function renderEvidenceSettings(
  items: readonly {
    readonly name: string;
    readonly value: string;
  }[],
  escapeHtml: (value: string) => string
): string {
  if (items.length === 0) {
    return "<span>No orchestration settings recorded.</span>";
  }

  return items
    .map((item) => `<span><code>${escapeHtml(item.name)}</code>: ${escapeHtml(item.value)}</span>`)
    .join("");
}

function renderPolicyRuleList(
  items: readonly {
    readonly id: string;
    readonly description: string;
    readonly enforcement: string;
    readonly blockingReason?: string | null;
    readonly isCurrentlySatisfied?: boolean | null;
    readonly currentStatusMessage?: string | null;
  }[],
  escapeHtml: (value: string) => string
): string {
  if (items.length === 0) {
    return "<span>No eligibility rules declared.</span>";
  }

  return items
    .map((item) => `<span><code>${escapeHtml(item.id)}</code> · ${escapeHtml(item.description)} · status <code>${item.isCurrentlySatisfied === false ? "blocked" : "ready"}</code>${item.currentStatusMessage ? ` · ${escapeHtml(item.currentStatusMessage)}` : ""}${item.blockingReason ? ` · reason <code>${escapeHtml(item.blockingReason)}</code>` : ""}</span>`)
    .join("");
}

function renderEvidenceRequirementList(
  items: readonly {
    readonly id: string;
    readonly description: string;
    readonly enforcement: string;
    readonly policyInput?: string | null;
  }[],
  escapeHtml: (value: string) => string
): string {
  if (items.length === 0) {
    return "<span>No evidence requirements declared.</span>";
  }

  return items
    .map((item) => `<span><code>${escapeHtml(item.id)}</code> · ${escapeHtml(item.description)} · enforcement <code>${escapeHtml(item.enforcement)}</code>${item.policyInput ? ` · input <code>${escapeHtml(item.policyInput)}</code>` : ""}</span>`)
    .join("");
}
