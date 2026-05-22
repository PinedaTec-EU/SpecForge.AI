"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.buildReleaseApprovalPhaseSections = buildReleaseApprovalPhaseSections;
function buildReleaseApprovalPhaseSections(args) {
    const effectivePrompt = args.selectedPhase.latestExecutionInspection?.effectivePrompt ?? null;
    const effectiveContext = args.selectedPhase.latestExecutionInspection?.effectiveContext ?? null;
    const evidenceRecord = args.selectedPhase.latestExecutionInspection?.evidenceRecord ?? null;
    const releaseEvidencePack = args.selectedPhase.latestExecutionInspection?.releaseApprovalEvidencePack ?? null;
    const releaseApprovalPolicy = args.selectedPhase.latestExecutionInspection?.releaseApprovalPolicySnapshot
        ?? args.selectedPhase.releaseApprovalPolicy
        ?? null;
    const releaseApprovalExecutionEligible = releaseApprovalPolicy
        ? ("executionEligibleNow" in releaseApprovalPolicy
            ? releaseApprovalPolicy.executionEligibleNow
            : releaseApprovalPolicy.executionAllowed)
        : false;
    const executionPolicy = args.selectedPhase.executionPolicy ?? null;
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
    const releaseApprovalPolicySection = releaseApprovalPolicy
        ? `
      <section class="detail-card">
        <h3>Release Approval Policy</h3>
        <p class="panel-copy">
          Inspect the explicit run and approval rules that currently govern release approval, including review-entry posture, commit consistency, and required supporting evidence.
        </p>
        <div class="detail-grid">
          <div><strong>Status</strong><div><code>${args.escapeHtml(releaseApprovalPolicy.status ?? "unknown")}</code></div></div>
          <div><strong>Execution Eligible</strong><div><code>${releaseApprovalExecutionEligible ? "true" : "false"}</code></div></div>
          <div><strong>Approval Available</strong><div><code>${releaseApprovalPolicy.approvalAvailableNow ? "true" : "false"}</code></div></div>
          <div><strong>Review Verdict</strong><div><code>${args.escapeHtml(releaseApprovalPolicy.latestReviewVerdict ?? "unknown")}</code></div></div>
          <div><strong>Force Approved</strong><div><code>${releaseApprovalPolicy.latestReviewWasForceApproved ? "true" : "false"}</code></div></div>
          <div><strong>HEAD Match</strong><div><code>${releaseApprovalPolicy.reviewCommitMatchesWorkspaceHead == null ? "n/a" : releaseApprovalPolicy.reviewCommitMatchesWorkspaceHead ? "true" : "false"}</code></div></div>
          <div><strong>Release Artifact</strong><div><code>${releaseApprovalPolicy.hasReleaseArtifact ? "available" : "missing"}</code></div></div>
          <div><strong>Evidence Pack</strong><div><code>${releaseApprovalPolicy.hasReleaseEvidencePack ? "available" : "missing"}</code></div></div>
          <div><strong>Implementation Evidence</strong><div><code>${releaseApprovalPolicy.hasImplementationEvidence ? "available" : "missing"}</code></div></div>
          <div><strong>Review Gate Result</strong><div><code>${releaseApprovalPolicy.hasReviewGateResult ? "available" : "missing"}</code></div></div>
          <div><strong>Branch Context</strong><div><code>${releaseApprovalPolicy.hasBranchContext ? "available" : "missing"}</code></div></div>
          <div><strong>Timeline Context</strong><div><code>${releaseApprovalPolicy.hasTimelineContext ? "available" : "missing"}</code></div></div>
        </div>
        <div class="detail-grid">
          <div><strong>Execution Blocking Reason</strong><div><code>${args.escapeHtml(releaseApprovalPolicy.executionBlockingReason ?? "none")}</code></div></div>
          <div><strong>Approval Blocking Reason</strong><div><code>${args.escapeHtml(releaseApprovalPolicy.approvalBlockingReason ?? "none")}</code></div></div>
          <div><strong>Current HEAD</strong><div><code>${args.escapeHtml(releaseApprovalPolicy.currentWorkspaceHeadSha ?? "unavailable")}</code></div></div>
          <div><strong>Approved Review Commit</strong><div><code>${args.escapeHtml(releaseApprovalPolicy.approvedReviewCommitSha ?? "unavailable")}</code></div></div>
        </div>
        <div class="detail-stack">
          <div>
            <strong>Evidence Requirements</strong>
            <ul class="detail-list">
              ${(releaseApprovalPolicy.evidenceRules ?? []).map(item => `
                <li>
                  <code>${args.escapeHtml(item.evidenceKind)}</code> · ${item.isRequired ? "required" : "optional"}
                  <div class="muted">${args.escapeHtml(item.currentStatusMessage)}</div>
                </li>
              `).join("")}
            </ul>
          </div>
          <div>
            <strong>Execution Conditions</strong>
            ${renderReleaseApprovalConditionList(releaseApprovalPolicy.executionConditions, args.escapeHtml)}
          </div>
          <div>
            <strong>Approval Conditions</strong>
            ${renderReleaseApprovalConditionList(releaseApprovalPolicy.approvalConditions, args.escapeHtml)}
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
        beforeArtifact: [releaseApprovalInspectionSection, releaseApprovalPolicySection, releaseEvidencePackSection].filter(Boolean),
        afterArtifact: []
    };
}
function renderReleaseApprovalConditionList(items, escapeHtml) {
    if (items.length === 0) {
        return "<p class=\"muted\">No release-approval policy conditions were recorded.</p>";
    }
    return `
    <ul class="detail-list">
      ${items.map(item => `
        <li>
          <code>${escapeHtml(item.id)}</code> · ${escapeHtml(item.status)} · ${escapeHtml(item.description)}
          ${item.currentStatusMessage ? `<div class="muted">${escapeHtml(item.currentStatusMessage)}</div>` : ""}
          ${item.blockingReason ? `<div class="muted">Blocking reason: <code>${escapeHtml(item.blockingReason)}</code></div>` : ""}
        </li>
      `).join("")}
    </ul>
  `;
}
//# sourceMappingURL=releaseApprovalPhaseView.js.map