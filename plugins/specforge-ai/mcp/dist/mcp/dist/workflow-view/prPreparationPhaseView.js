"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.buildPrPreparationPhaseSections = buildPrPreparationPhaseSections;
function buildPrPreparationPhaseSections(args) {
    const effectivePrompt = args.selectedPhase.latestExecutionInspection?.effectivePrompt ?? null;
    const effectiveContext = args.selectedPhase.latestExecutionInspection?.effectiveContext ?? null;
    const evidenceRecord = args.selectedPhase.latestExecutionInspection?.evidenceRecord ?? null;
    const prPreparationEvidence = args.selectedPhase.latestExecutionInspection?.prPreparationStructuredEvidence ?? null;
    const prPreparationPolicy = args.selectedPhase.prPreparationPolicy ?? null;
    const executionPolicy = args.selectedPhase.executionPolicy ?? null;
    const receiptPath = args.selectedPhase.latestExecutionInspection?.receiptPath?.trim() || null;
    const inspectionSection = args.selectedPhase.phaseId === "pr-preparation"
        ? `
      <section class="detail-card">
        <h3>Inspect Last PR Preparation Execution</h3>
        <p class="panel-copy">
          Review the latest persisted PR-preparation prompt and runtime context before reworking the PR body, publication narrative, or draft publication posture.
        </p>
        ${effectivePrompt || effectiveContext
            ? `
            <div class="detail-grid">
              <div><strong>Effective Prompt</strong><div><code>${effectivePrompt ? "available" : "unavailable"}</code></div></div>
              <div><strong>Warnings</strong><div><code>${effectivePrompt?.warnings?.length ?? 0}</code></div></div>
              <div><strong>Previous Artifacts</strong><div><code>${effectiveContext?.previousArtifacts.length ?? 0}</code></div></div>
              <div><strong>Context Files</strong><div><code>${effectiveContext?.contextFiles.length ?? 0}</code></div></div>
              <div><strong>Branch Context</strong><div><code>${effectiveContext?.contextFiles.some(item => item.path.endsWith("/branch.yaml")) ? "available" : "unavailable"}</code></div></div>
              <div><strong>Timeline Context</strong><div><code>${effectiveContext?.contextFiles.some(item => item.path.endsWith("/timeline.md")) ? "available" : "unavailable"}</code></div></div>
              <div><strong>Workspace Git HEAD</strong><div><code>${args.escapeHtml(effectiveContext?.workspaceGitHeadSha ?? "unavailable")}</code></div></div>
              <div><strong>Evidence Links</strong><div><code>${evidenceRecord?.evidenceLinks.length ?? 0}</code></div></div>
            </div>
            <div class="detail-actions">
              ${effectivePrompt
                ? `<button class="workflow-action-button workflow-action-button--document" type="button" data-open-effective-prompt-modal>View Last PR Preparation Prompt</button>`
                : ""}
              ${effectiveContext
                ? `<button class="workflow-action-button workflow-action-button--document" type="button" data-open-effective-context-modal>View Last PR Preparation Context</button>`
                : ""}
              ${receiptPath
                ? `<button class="workflow-action-button workflow-action-button--document" data-command="openArtifact" data-path="${args.escapeHtmlAttribute(receiptPath)}">Open Receipt</button>`
                : ""}
            </div>
          `
            : `<p class="muted">No persisted pr-preparation execution inspection is available yet for this user story.</p>`}
      </section>
    `
        : "";
    const policySection = prPreparationPolicy
        ? `
      <section class="detail-card">
        <h3>PR Preparation Policy</h3>
        <p class="panel-copy">
          Inspect the publication-readiness rules that govern draft PR generation or reuse, including branch metadata, release lineage, and artifact completeness.
        </p>
        <div class="detail-grid">
          <div><strong>Status</strong><div><code>${args.escapeHtml(prPreparationPolicy.status)}</code></div></div>
          <div><strong>Publication Ready</strong><div><code>${prPreparationPolicy.publicationReadyNow ? "true" : "false"}</code></div></div>
          <div><strong>Publication Mode</strong><div><code>${args.escapeHtml(prPreparationPolicy.publicationMode)}</code></div></div>
          <div><strong>Blocking Reason</strong><div><code>${args.escapeHtml(prPreparationPolicy.publicationBlockingReason ?? "none")}</code></div></div>
          <div><strong>Base Branch</strong><div><code>${args.escapeHtml(prPreparationPolicy.baseBranch ?? "unavailable")}</code></div></div>
          <div><strong>Work Branch</strong><div><code>${args.escapeHtml(prPreparationPolicy.workBranch ?? "unavailable")}</code></div></div>
          <div><strong>Release Artifact</strong><div><code>${prPreparationPolicy.hasReleaseApprovalArtifact ? "available" : "missing"}</code></div></div>
          <div><strong>Release Evidence Pack</strong><div><code>${prPreparationPolicy.hasReleaseApprovalEvidencePack ? "available" : "missing"}</code></div></div>
          <div><strong>PR Body</strong><div><code>${prPreparationPolicy.hasPrBody ? "ready" : "missing"}</code></div></div>
          <div><strong>Validation Summary</strong><div><code>${prPreparationPolicy.hasValidationSummary ? "ready" : "missing"}</code></div></div>
          <div><strong>Reviewer Checklist</strong><div><code>${prPreparationPolicy.hasReviewerChecklist ? "ready" : "missing"}</code></div></div>
          <div><strong>Reusable Existing PR</strong><div><code>${prPreparationPolicy.existingPullRequestReusable ? "true" : "false"}</code></div></div>
        </div>
        <div class="detail-stack">
          ${prPreparationPolicy.existingPullRequestUrl
            ? `<div><strong>Existing Pull Request</strong><p class="panel-copy">${args.escapeHtml(prPreparationPolicy.existingPullRequestStatus ?? "unknown")} · ${args.escapeHtml(prPreparationPolicy.existingPullRequestUrl)}</p></div>`
            : ""}
          <div>
            <strong>Requirement Rules</strong>
            <ul class="detail-list">
              ${prPreparationPolicy.requirementRules.map(item => `
                <li>
                  <code>${args.escapeHtml(item.id)}</code> · ${item.isRequired ? "required" : "optional"}
                  <div class="muted">${args.escapeHtml(item.currentStatusMessage)}</div>
                </li>
              `).join("")}
            </ul>
          </div>
          <div>
            <strong>Publication Conditions</strong>
            <ul class="detail-list">
              ${prPreparationPolicy.publicationConditions.map(item => `
                <li>
                  <code>${args.escapeHtml(item.id)}</code> · ${args.escapeHtml(item.status)} · ${args.escapeHtml(item.description)}
                  ${item.currentStatusMessage ? `<div class="muted">${args.escapeHtml(item.currentStatusMessage)}</div>` : ""}
                  ${item.blockingReason ? `<div class="muted">Blocking reason: <code>${args.escapeHtml(item.blockingReason)}</code></div>` : ""}
                </li>
              `).join("")}
            </ul>
          </div>
          ${executionPolicy
            ? `
              <div>
                <strong>Shared Eligibility Rules</strong>
                <ul class="detail-list">
                  ${executionPolicy.eligibilityRules.map(item => `
                    <li>
                      <code>${args.escapeHtml(item.id)}</code> · ${args.escapeHtml(item.description)}
                      ${item.currentStatusMessage ? `<div class="muted">${args.escapeHtml(item.currentStatusMessage)}</div>` : ""}
                    </li>
                  `).join("")}
                </ul>
              </div>
            `
            : ""}
        </div>
      </section>
    `
        : "";
    const structuredEvidenceSection = prPreparationEvidence
        ? `
      <section class="detail-card">
        <h3>PR Preparation Evidence</h3>
        <p class="panel-copy">
          Structured evidence persisted with the latest PR-preparation receipt, ready to support PR description generation, publication audit, and downstream governance.
        </p>
        <div class="detail-grid">
          <div><strong>State</strong><div><code>${args.escapeHtml(prPreparationEvidence.state)}</code></div></div>
          <div><strong>PR Title</strong><div><code>${args.escapeHtml(prPreparationEvidence.prTitle)}</code></div></div>
          <div><strong>Base Branch</strong><div><code>${args.escapeHtml(prPreparationEvidence.baseBranch)}</code></div></div>
          <div><strong>Work Branch</strong><div><code>${args.escapeHtml(prPreparationEvidence.workBranch)}</code></div></div>
          <div><strong>Participants</strong><div><code>${prPreparationEvidence.participants.length}</code></div></div>
          <div><strong>Linked Evidence</strong><div><code>${prPreparationEvidence.linkedEvidence.length}</code></div></div>
          <div><strong>Validation Items</strong><div><code>${prPreparationEvidence.validationSummary.length}</code></div></div>
          <div><strong>Reviewer Checks</strong><div><code>${prPreparationEvidence.reviewerChecklist.length}</code></div></div>
        </div>
        <div class="detail-stack">
          <div>
            <strong>PR Summary</strong>
            <p class="panel-copy">${args.escapeHtml(prPreparationEvidence.prSummary)}</p>
          </div>
          <div>
            <strong>Based On</strong>
            <ul class="detail-list">
              ${prPreparationEvidence.basedOn.map(item => `<li><code>${args.escapeHtml(item)}</code></li>`).join("")}
            </ul>
          </div>
          <div>
            <strong>Participants</strong>
            <ul class="detail-list">
              ${prPreparationEvidence.participants.map(item => `
                <li>
                  <code>${args.escapeHtml(item.actor)}</code>
                  <div class="muted">${args.escapeHtml(item.phases.join(", "))}</div>
                </li>
              `).join("")}
            </ul>
          </div>
          <div>
            <strong>Validation Summary</strong>
            <ul class="detail-list">
              ${prPreparationEvidence.validationSummary.map(item => `<li>${args.escapeHtml(item)}</li>`).join("")}
            </ul>
          </div>
          <div>
            <strong>Reviewer Checklist</strong>
            <ul class="detail-list">
              ${prPreparationEvidence.reviewerChecklist.map(item => `<li>${args.escapeHtml(item)}</li>`).join("")}
            </ul>
          </div>
          <div>
            <strong>Linked Evidence</strong>
            <ul class="detail-list">
              ${prPreparationEvidence.linkedEvidence.map(item => `
                <li>
                  <code>${args.escapeHtml(item.kind)}</code>: ${args.escapeHtml(item.path)}
                  ${item.summary ? `<div class="muted">${args.escapeHtml(item.summary)}</div>` : ""}
                </li>
              `).join("")}
            </ul>
          </div>
          ${args.workflow.pullRequest?.url
            ? `<div><strong>Published Pull Request</strong><p class="panel-copy">${args.escapeHtml(args.workflow.pullRequest.status)} · ${args.escapeHtml(args.workflow.pullRequest.url)}</p></div>`
            : ""}
        </div>
      </section>
    `
        : "";
    return {
        beforeArtifact: [inspectionSection, policySection, structuredEvidenceSection].filter(Boolean),
        afterArtifact: []
    };
}
//# sourceMappingURL=prPreparationPhaseView.js.map