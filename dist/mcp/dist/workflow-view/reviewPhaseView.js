"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.buildReviewPhaseSections = buildReviewPhaseSections;
const workflowAutomation_1 = require("../workflowAutomation");
function buildReviewPhaseSections(args) {
    const effectivePrompt = args.selectedPhase.latestExecutionInspection?.effectivePrompt ?? null;
    const effectiveContext = args.selectedPhase.latestExecutionInspection?.effectiveContext ?? null;
    const evidenceRecord = args.selectedPhase.latestExecutionInspection?.evidenceRecord ?? null;
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
    if (!currentPhaseIsReview) {
        return { beforeArtifact: reviewInspectionSection ? [reviewInspectionSection] : [], afterArtifact: [] };
    }
    return {
        beforeArtifact: [
            ...(reviewInspectionSection ? [reviewInspectionSection] : []),
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