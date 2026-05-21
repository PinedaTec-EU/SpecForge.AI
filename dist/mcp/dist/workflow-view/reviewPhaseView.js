"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.buildReviewPhaseSections = buildReviewPhaseSections;
const workflowAutomation_1 = require("../workflowAutomation");
function buildReviewPhaseSections(args) {
    const effectivePrompt = args.selectedPhase.latestExecutionInspection?.effectivePrompt ?? null;
    const effectiveContext = args.selectedPhase.latestExecutionInspection?.effectiveContext ?? null;
    const evidenceRecord = args.selectedPhase.latestExecutionInspection?.evidenceRecord ?? null;
    const structuredGateResult = args.selectedPhase.latestExecutionInspection?.reviewStructuredGateResult ?? null;
    const reviewPolicy = args.selectedPhase.reviewPolicy ?? null;
    const receiptPath = args.selectedPhase.latestExecutionInspection?.receiptPath?.trim() || null;
    const implementationAttempts = (0, workflowAutomation_1.countImplementationAttempts)(args.workflow);
    const currentPhaseIsReview = args.selectedPhase.isCurrent && args.selectedPhase.phaseId === "review";
    const includeReviewArtifact = args.state.reviewRegressionIncludeArtifact !== false;
    const reviewInspectionSection = args.selectedPhase.phaseId === "review"
        ? `
      <section class="detail-card">
        <h3>Inspect Last Review Execution</h3>
        <p class="panel-copy">
          Review the latest persisted effective prompt and injected runtime context for review before changing evidence policy, retrying review, or regressing back to implementation.
        </p>
        ${effectivePrompt || effectiveContext
            ? `
            <div class="detail-grid">
              <div><strong>Effective Prompt</strong><div><code>${effectivePrompt ? "available" : "unavailable"}</code></div></div>
              <div><strong>Warnings</strong><div><code>${effectivePrompt?.warnings?.length ?? 0}</code></div></div>
              <div><strong>Previous Artifacts</strong><div><code>${effectiveContext?.previousArtifacts.length ?? 0}</code></div></div>
              <div><strong>Context Files</strong><div><code>${effectiveContext?.contextFiles.length ?? 0}</code></div></div>
              <div><strong>Current Artifact</strong><div><code>${effectiveContext?.currentArtifact ? "available" : "unavailable"}</code></div></div>
              <div><strong>Evidence Links</strong><div><code>${evidenceRecord?.evidenceLinks.length ?? 0}</code></div></div>
            </div>
            <div class="detail-actions">
              ${effectivePrompt
                ? `<button class="workflow-action-button workflow-action-button--document" type="button" data-open-effective-prompt-modal>View Last Review Prompt</button>`
                : ""}
              ${effectiveContext
                ? `<button class="workflow-action-button workflow-action-button--document" type="button" data-open-effective-context-modal>View Last Review Context</button>`
                : ""}
              ${receiptPath
                ? `<button class="workflow-action-button workflow-action-button--document" data-command="openArtifact" data-path="${args.escapeHtmlAttribute(receiptPath)}">Open Receipt</button>`
                : ""}
            </div>
          `
            : `<p class="muted">No persisted review execution inspection is available yet for this user story.</p>`}
      </section>
    `
        : "";
    const reviewGateResultSection = structuredGateResult
        ? `
      <section class="detail-card">
        <h3>Review Gate Result</h3>
        <p class="panel-copy">
          Structured review gate output captured from the persisted receipt, including verdict, correction targets, and linked evidence used by the review decision path.
        </p>
        <div class="detail-grid">
          <div><strong>Verdict</strong><div><code>${args.escapeHtml(structuredGateResult.verdict)}</code></div></div>
          <div><strong>Blocking Findings</strong><div><code>${structuredGateResult.hasBlockingFindings ? "yes" : "no"}</code></div></div>
          <div><strong>Passed Items</strong><div><code>${structuredGateResult.passedValidationItemCount}</code></div></div>
          <div><strong>Failed Items</strong><div><code>${structuredGateResult.failedValidationItemCount}</code></div></div>
          <div><strong>Deferred Items</strong><div><code>${structuredGateResult.deferredValidationItemCount}</code></div></div>
          <div><strong>Linked Evidence</strong><div><code>${structuredGateResult.linkedEvidence.length}</code></div></div>
        </div>
        <div class="detail-stack">
          <div>
            <strong>Primary Reason</strong>
            <p class="panel-copy">${args.escapeHtml(structuredGateResult.primaryReason)}</p>
          </div>
          <div>
            <strong>Findings Summary</strong>
            <ul class="detail-list">
              ${structuredGateResult.findingsSummary.map(item => `<li>${args.escapeHtml(item)}</li>`).join("")}
            </ul>
          </div>
          <div>
            <strong>Correction Targets</strong>
            ${structuredGateResult.correctionTargets.length > 0
            ? `
                <ul class="detail-list">
                  ${structuredGateResult.correctionTargets.map(item => `
                    <li>
                      <strong>${args.escapeHtml(item.status)}</strong>: ${args.escapeHtml(item.item)}
                      <div class="muted">${args.escapeHtml(item.evidence)}</div>
                      <div class="muted">${args.escapeHtml(item.suggestedAction)}</div>
                    </li>
                  `).join("")}
                </ul>
              `
            : `<p class="muted">No corrective targets were recorded for this review execution.</p>`}
          </div>
          <div>
            <strong>Linked Evidence</strong>
            <ul class="detail-list">
              ${structuredGateResult.linkedEvidence.map(item => `
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
    const reviewPolicySection = reviewPolicy
        ? `
      <section class="detail-card">
        <h3>Review Policy</h3>
        <p class="panel-copy">
          Live review policy visibility for evidence classification, override conditions, and force-approval audit semantics.
        </p>
        <div class="detail-grid">
          <div><strong>Evidence Policy</strong><div><code>${args.escapeHtml(reviewPolicy.activeEvidencePolicy)}</code></div></div>
          <div><strong>Latest Verdict</strong><div><code>${args.escapeHtml(reviewPolicy.latestGateVerdict ?? "unknown")}</code></div></div>
          <div><strong>Blocking Findings</strong><div><code>${reviewPolicy.latestHasBlockingFindings === true ? "yes" : reviewPolicy.latestHasBlockingFindings === false ? "no" : "unknown"}</code></div></div>
          <div><strong>Force Approve Now</strong><div><code>${reviewPolicy.forceApprovalAvailableNow ? "available" : "blocked"}</code></div></div>
          <div><strong>Reason Required</strong><div><code>${reviewPolicy.forceApprovalRequiresReason ? "yes" : "no"}</code></div></div>
          <div><strong>Last Override</strong><div><code>${reviewPolicy.lastForceApprovalDecision ? "recorded" : "none"}</code></div></div>
        </div>
        <div class="detail-stack">
          <div>
            <strong>Evidence Rules</strong>
            <ul class="detail-list">
              ${reviewPolicy.evidenceRules.map(item => `
                <li>
                  <code>${args.escapeHtml(item.evidenceKind)}</code>: ${item.isBlocking ? "blocking" : "non-blocking"}
                  <div class="muted">${args.escapeHtml(item.currentStatusMessage)}</div>
                </li>
              `).join("")}
            </ul>
          </div>
          <div>
            <strong>Override Conditions</strong>
            <ul class="detail-list">
              ${reviewPolicy.overrideConditions.map(item => `
                <li>
                  <strong>${args.escapeHtml(item.status)}</strong>: ${args.escapeHtml(item.description)}
                  ${item.currentStatusMessage ? `<div class="muted">${args.escapeHtml(item.currentStatusMessage)}</div>` : ""}
                  ${item.blockingReason ? `<div class="muted"><code>${args.escapeHtml(item.blockingReason)}</code></div>` : ""}
                </li>
              `).join("")}
            </ul>
          </div>
          ${reviewPolicy.lastForceApprovalDecision
            ? `
              <div>
                <strong>Last Force Approval Decision</strong>
                <p class="panel-copy">
                  <code>${args.escapeHtml(reviewPolicy.lastForceApprovalDecision.actor)}</code> forced review into
                  <code>${args.escapeHtml(reviewPolicy.lastForceApprovalDecision.targetPhase)}</code> on
                  <code>${args.escapeHtml(reviewPolicy.lastForceApprovalDecision.timestampUtc)}</code>.
                </p>
                <p class="panel-copy">${args.escapeHtml(reviewPolicy.lastForceApprovalDecision.reason)}</p>
              </div>
            `
            : ""}
        </div>
      </section>
    `
        : "";
    if (!currentPhaseIsReview) {
        return {
            beforeArtifact: [reviewInspectionSection, reviewGateResultSection, reviewPolicySection].filter(Boolean),
            afterArtifact: []
        };
    }
    return {
        beforeArtifact: [
            ...([reviewInspectionSection, reviewGateResultSection, reviewPolicySection].filter(Boolean)),
            `
      <section class="detail-card detail-card--review-regression">
        <div class="review-regression">
          <div class="review-regression__header">
            <div class="review-regression__copy">
              <span class="badge badge--attention">Review feedback loop</span>
              <h3>Send Back To Implementation</h3>
              <p class="panel-copy">
                Use this only when the review found concrete issues that require another implementation pass.
                SpecForge will return the workflow to the <code>implementation</code> correction loop, preserve the current review artifact as context,
                and apply your note directly over the existing implementation artifact instead of starting from scratch.
              </p>
            </div>
            <div class="review-regression__stat" aria-label="Implementation attempts so far">
              <span class="review-regression__stat-label">Attempts so far</span>
              <strong class="review-regression__stat-value">${implementationAttempts}</strong>
            </div>
          </div>
          <div class="review-regression__body">
            <label class="phase-input-shell" for="review-regression-textarea">
              <span class="phase-input-label">Implementation context source</span>
              <label class="review-regression__toggle">
                <input
                  id="review-regression-include-artifact"
                  type="checkbox"
                  ${includeReviewArtifact ? "checked" : ""} />
                <span>Send the generated review artifact to implementation as corrective context</span>
              </label>
              <span class="phase-input-label">Correction context</span>
              <p class="phase-input-copy">
                ${includeReviewArtifact
                ? "Optional. Add only the extra constraints or steering that should accompany the review artifact when implementation runs again."
                : "Required when the review artifact is not sent. Explain what failed, what must change now, and what constraints the next implementation pass must preserve."}
              </p>
              <textarea
                id="review-regression-textarea"
                class="phase-input-textarea phase-input-textarea--review-regression"
                rows="6"
                placeholder="${includeReviewArtifact
                ? "Optional example: preserve the current loading flow and only address the accessibility findings from the review."
                : "Required example: the implementation must rebuild the empty state behavior from the review findings, preserve the current loading flow, and avoid expanding scope."}"></textarea>
            </label>
            <p class="review-regression__audit-note">The user decision, selected context mode, regression reason, and resulting implementation operation are all recorded in the workflow audit trail.</p>
          </div>
        </div>
      </section>
      `
        ],
        afterArtifact: []
    };
}
//# sourceMappingURL=reviewPhaseView.js.map