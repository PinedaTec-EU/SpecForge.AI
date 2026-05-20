import type { WorkflowPhaseDetails } from "../backendClient";
import type { PhaseSectionFragments } from "./models";

interface ImplementationPhaseViewArgs {
  readonly selectedPhase: WorkflowPhaseDetails;
  readonly escapeHtml: (value: string) => string;
}

export function buildImplementationPhaseSections(args: ImplementationPhaseViewArgs): PhaseSectionFragments {
  const { selectedPhase, escapeHtml } = args;
  const executionReadiness = selectedPhase.executionReadiness ?? null;
  const executionPolicy = selectedPhase.executionPolicy ?? null;
  const evidenceRecord = selectedPhase.latestExecutionInspection?.evidenceRecord ?? null;

  const implementationPolicySection = selectedPhase.phaseId === "implementation" && (executionReadiness || executionPolicy)
    ? `
      <section class="detail-card">
        <h3>Implementation Policy</h3>
        <p class="panel-copy">
          Inspect the explicit repository-access semantics, writable scope rules, forbidden mutation zones, and evidence requirements that govern implementation runs.
        </p>
        <div class="detail-grid">
          <div><strong>Execution Readiness</strong><div><code>${executionReadiness?.canExecute ? "ready" : "blocked"}</code></div></div>
          <div><strong>Blocking Reason</strong><div><code>${escapeHtml(executionReadiness?.blockingReason ?? "none")}</code></div></div>
          <div><strong>Repository Access</strong><div><code>${escapeHtml(executionPolicy?.permissions.repositoryAccess ?? executionReadiness?.requiredPermissions?.repositoryAccess ?? "unknown")}</code></div></div>
          <div><strong>Workspace Writes</strong><div><code>${(executionPolicy?.permissions.workspaceWriteAccess ?? executionReadiness?.requiredPermissions?.workspaceWriteAccess) ? "allowed" : "not allowed"}</code></div></div>
          <div><strong>Writable Paths</strong><div><code>${executionPolicy?.writablePaths.length ?? 0}</code></div></div>
          <div><strong>Forbidden Paths</strong><div><code>${executionPolicy?.forbiddenPaths.length ?? 0}</code></div></div>
          <div><strong>Evidence Requirements</strong><div><code>${executionPolicy?.evidenceRequirements.length ?? 0}</code></div></div>
          <div><strong>Eligibility Rules</strong><div><code>${executionPolicy?.eligibilityRules.length ?? 0}</code></div></div>
        </div>
        ${executionPolicy
          ? `
            <div class="refinement-suggestions">
              <div class="refinement-suggestion refinement-suggestion--static">
                <div class="refinement-suggestion__body">
                  <strong>Writable Scope</strong>
                  ${renderPathPolicyList(executionPolicy.writablePaths, escapeHtml, "No writable scope rules declared.")}
                </div>
              </div>
              <div class="refinement-suggestion refinement-suggestion--static">
                <div class="refinement-suggestion__body">
                  <strong>Forbidden Mutation Zones</strong>
                  ${renderPathPolicyList(executionPolicy.forbiddenPaths, escapeHtml, "No forbidden mutation zones declared.")}
                </div>
              </div>
              <div class="refinement-suggestion refinement-suggestion--static">
                <div class="refinement-suggestion__body">
                  <strong>Evidence Requirements</strong>
                  ${renderEvidenceRequirementList(executionPolicy.evidenceRequirements, escapeHtml)}
                </div>
              </div>
              <div class="refinement-suggestion refinement-suggestion--static">
                <div class="refinement-suggestion__body">
                  <strong>Eligibility Rules</strong>
                  ${renderEligibilityRuleList(executionPolicy.eligibilityRules, escapeHtml)}
                </div>
              </div>
            </div>
          `
          : ""}
      </section>
    `
    : "";

  const implementationEvidenceSummarySection = selectedPhase.phaseId === "implementation" && evidenceRecord
    ? `
      <section class="detail-card">
        <h3>Implementation Evidence Summary</h3>
        <p class="panel-copy">
          The latest execution receipt persists implementation evidence separately; this summary shows whether the run produced the expected evidence substrate for downstream review.
        </p>
        <div class="detail-grid">
          <div><strong>Actor</strong><div><code>${escapeHtml(evidenceRecord.actor.agentName ?? evidenceRecord.actor.kind)}</code></div></div>
          <div><strong>Inputs</strong><div><code>${evidenceRecord.inputs.length}</code></div></div>
          <div><strong>Outputs</strong><div><code>${evidenceRecord.outputs.length}</code></div></div>
          <div><strong>Tools Used</strong><div><code>${evidenceRecord.toolsUsed.length}</code></div></div>
          <div><strong>Validation</strong><div><code>${escapeHtml(evidenceRecord.validationSummary.status)}</code></div></div>
          <div><strong>Evidence Links</strong><div><code>${evidenceRecord.evidenceLinks.length}</code></div></div>
        </div>
      </section>
    `
    : "";

  return {
    beforeArtifact: [
      ...(implementationPolicySection ? [implementationPolicySection] : []),
      ...(implementationEvidenceSummarySection ? [implementationEvidenceSummarySection] : [])
    ],
    afterArtifact: []
  };
}

function renderPathPolicyList(
  items: readonly {
    readonly path: string;
    readonly access: string;
    readonly actor: string;
    readonly enforcement: string;
    readonly reason: string;
  }[],
  escapeHtml: (value: string) => string,
  emptyMessage: string
): string {
  if (items.length === 0) {
    return `<span>${escapeHtml(emptyMessage)}</span>`;
  }

  return items
    .map((item) => `<span><code>${escapeHtml(item.path)}</code> · <code>${escapeHtml(item.access)}</code> · actor <code>${escapeHtml(item.actor)}</code> · ${escapeHtml(item.reason)}</span>`)
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

function renderEligibilityRuleList(
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
