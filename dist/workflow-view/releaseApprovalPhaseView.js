"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.buildReleaseApprovalPhaseSections = buildReleaseApprovalPhaseSections;
function buildReleaseApprovalPhaseSections(args) {
    const effectivePrompt = args.selectedPhase.latestExecutionInspection?.effectivePrompt ?? null;
    const effectiveContext = args.selectedPhase.latestExecutionInspection?.effectiveContext ?? null;
    const evidenceRecord = args.selectedPhase.latestExecutionInspection?.evidenceRecord ?? null;
    const releaseEvidencePack = args.selectedPhase.latestExecutionInspection?.releaseApprovalEvidencePack ?? null;
    const receiptPath = args.selectedPhase.latestExecutionInspection?.receiptPath?.trim() || null;
    const releaseApprovalInspectionSection = args.selectedPhase.phaseId === "release-approval"
        ? `
      <section class="detail-card">
        <h3>Inspect Last Release Approval Execution</h3>
        <p class="panel-copy">
          Review the latest persisted effective prompt and injected runtime context for release approval before changing approval posture or advancing toward PR preparation.
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
              <div><strong>Evidence Links</strong><div><code>${evidenceRecord?.evidenceLinks.length ?? 0}</code></div></div>
            </div>
            <div class="detail-actions">
              ${effectivePrompt
                ? `<button class="workflow-action-button workflow-action-button--document" type="button" data-open-effective-prompt-modal>View Last Release Approval Prompt</button>`
                : ""}
              ${effectiveContext
                ? `<button class="workflow-action-button workflow-action-button--document" type="button" data-open-effective-context-modal>View Last Release Approval Context</button>`
                : ""}
              ${receiptPath
                ? `<button class="workflow-action-button workflow-action-button--document" data-command="openArtifact" data-path="${args.escapeHtmlAttribute(receiptPath)}">Open Receipt</button>`
                : ""}
            </div>
          `
            : `<p class="muted">No persisted release-approval execution inspection is available yet for this user story.</p>`}
      </section>
    `
        : "";
    const releaseEvidencePackSection = releaseEvidencePack
        ? `
      <section class="detail-card">
        <h3>Release Evidence Pack</h3>
        <p class="panel-copy">
          Structured release evidence captured from the release-approval receipt, bundling review outcome, changed files, validation results, residual risks, and supporting artifacts.
        </p>
        <div class="detail-grid">
          <div><strong>Review Verdict</strong><div><code>${args.escapeHtml(releaseEvidencePack.reviewVerdict ?? "unknown")}</code></div></div>
          <div><strong>Changed Files</strong><div><code>${releaseEvidencePack.changedFiles.length}</code></div></div>
          <div><strong>Validation Results</strong><div><code>${releaseEvidencePack.validationResults.length}</code></div></div>
          <div><strong>Residual Risks</strong><div><code>${releaseEvidencePack.releaseRiskSummary.length}</code></div></div>
          <div><strong>Supporting Artifacts</strong><div><code>${releaseEvidencePack.supportingArtifacts.length}</code></div></div>
          <div><strong>Generated At</strong><div><code>${args.escapeHtml(releaseEvidencePack.generatedAtUtc)}</code></div></div>
        </div>
        <div class="detail-stack">
          ${releaseEvidencePack.reviewPrimaryReason
            ? `
              <div>
                <strong>Review Primary Reason</strong>
                <p class="panel-copy">${args.escapeHtml(releaseEvidencePack.reviewPrimaryReason)}</p>
              </div>
            `
            : ""}
          <div>
            <strong>Changed Files</strong>
            ${releaseEvidencePack.changedFiles.length > 0
            ? `
                <ul class="detail-list">
                  ${releaseEvidencePack.changedFiles.map(item => `
                    <li>
                      <code>${args.escapeHtml(item.path)}</code> <strong>${args.escapeHtml(item.changeKind)}</strong>
                      <div class="muted">${args.escapeHtml(item.currentStatusCode)}${item.baselineStatusCode ? ` · baseline ${args.escapeHtml(item.baselineStatusCode)}` : ""}</div>
                    </li>
                  `).join("")}
                </ul>
              `
            : `<p class="muted">No changed files were captured in the supporting implementation evidence.</p>`}
          </div>
          <div>
            <strong>Validation Results</strong>
            <ul class="detail-list">
              ${releaseEvidencePack.validationResults.map(item => `
                <li>
                  <strong>${args.escapeHtml(item.status)}</strong>: ${args.escapeHtml(item.item)}
                  <div class="muted">${args.escapeHtml(item.evidence)}</div>
                </li>
              `).join("")}
            </ul>
          </div>
          <div>
            <strong>Residual Risks</strong>
            <ul class="detail-list">
              ${releaseEvidencePack.releaseRiskSummary.map(item => `<li>${args.escapeHtml(item)}</li>`).join("")}
            </ul>
          </div>
          <div>
            <strong>Supporting Artifacts</strong>
            <ul class="detail-list">
              ${releaseEvidencePack.supportingArtifacts.map(item => `
                <li>
                  <code>${args.escapeHtml(item.kind)}</code>: ${args.escapeHtml(item.path)}
                  ${item.summary ? `<div class="muted">${args.escapeHtml(item.summary)}</div>` : ""}
                </li>
              `).join("")}
            </ul>
          </div>
        </div>
      </section>
    `
        : "";
    return {
        beforeArtifact: [releaseApprovalInspectionSection, releaseEvidencePackSection].filter(Boolean),
        afterArtifact: []
    };
}
//# sourceMappingURL=releaseApprovalPhaseView.js.map