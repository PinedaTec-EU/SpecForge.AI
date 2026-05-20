import type { UserStoryWorkflowDetails, WorkflowPhaseDetails } from "../backendClient";
import type { WorkflowViewState, PhaseSectionFragments } from "./models";

interface RefinementPhaseViewArgs {
  readonly workflow: UserStoryWorkflowDetails;
  readonly selectedPhase: WorkflowPhaseDetails;
  readonly state: WorkflowViewState;
  readonly heroTokenClass: (value: string) => string;
  readonly escapeHtml: (value: string) => string;
  readonly escapeHtmlAttribute: (value: string) => string;
}

export function buildRefinementPhaseSections(args: RefinementPhaseViewArgs): PhaseSectionFragments {
  const { workflow, selectedPhase, state, heroTokenClass, escapeHtml, escapeHtmlAttribute } = args;
  const effectiveContext = selectedPhase.latestExecutionInspection?.effectiveContext ?? null;
  const refinementPolicy = selectedPhase.latestExecutionInspection?.refinementPolicySnapshot ?? workflow.refinement?.policy ?? null;
  const skillPreselection = selectedPhase.latestExecutionInspection?.refinementSkillPreselection ?? null;
  const graphScopeRequest = selectedPhase.latestExecutionInspection?.refinementGraphScopeRequest ?? null;
  const graphScopeSection = graphScopeRequest
    ? `
      <div class="refinement-context">
        <div class="refinement-context__copy">
          <h4>Technical Design Graph Handoff</h4>
          <p>
            This persisted handoff tells the future technical-design phase where to start exploring: graph depth, seed nodes, seed files, and unresolved scope questions.
          </p>
        </div>
        <div class="detail-grid">
          <div><strong>Depth</strong><div><code>${graphScopeRequest.depth}</code></div></div>
          <div><strong>Seed Nodes</strong><div><code>${graphScopeRequest.seedNodes.length}</code></div></div>
          <div><strong>Seed Files</strong><div><code>${graphScopeRequest.seedFiles.length}</code></div></div>
          <div><strong>Unresolved Questions</strong><div><code>${graphScopeRequest.unresolvedScopeQuestions.length}</code></div></div>
        </div>
        <div class="refinement-suggestions">
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Seed Nodes</strong>
              ${renderGraphSeedNodes(graphScopeRequest.seedNodes, escapeHtml)}
            </div>
          </div>
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Seed Files</strong>
              ${renderInjectedArtifactList(graphScopeRequest.seedFiles, escapeHtml)}
            </div>
          </div>
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Unresolved Scope Questions</strong>
              ${renderContextGapList(graphScopeRequest.unresolvedScopeQuestions, escapeHtml)}
            </div>
          </div>
        </div>
      </div>
    `
    : "";
  const skillPreselectionSection = skillPreselection
    ? `
      <div class="refinement-context">
        <div class="refinement-context__copy">
          <h4>Skill Preselection</h4>
          <p>
            This persisted refinement snapshot records which skills should be mandatory, which ones may help, which ones were ruled out, and which context gaps still block confident scope handoff.
          </p>
        </div>
        <div class="detail-grid">
          <div><strong>Required</strong><div><code>${skillPreselection.requiredSkills.length}</code></div></div>
          <div><strong>Candidate</strong><div><code>${skillPreselection.candidateSkills.length}</code></div></div>
          <div><strong>Rejected</strong><div><code>${skillPreselection.rejectedSkills.length}</code></div></div>
          <div><strong>Context Gaps</strong><div><code>${skillPreselection.contextGaps.length}</code></div></div>
        </div>
        <div class="refinement-suggestions">
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Required Skills</strong>
              ${renderSkillSelectionList(skillPreselection.requiredSkills, escapeHtml)}
            </div>
          </div>
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Candidate Skills</strong>
              ${renderSkillSelectionList(skillPreselection.candidateSkills, escapeHtml)}
            </div>
          </div>
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Rejected Skills</strong>
              ${renderSkillSelectionList(skillPreselection.rejectedSkills, escapeHtml)}
            </div>
          </div>
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Context Gaps</strong>
              ${renderContextGapList(skillPreselection.contextGaps, escapeHtml)}
            </div>
          </div>
        </div>
      </div>
    `
    : "";
  const refinementPolicySection = refinementPolicy
    ? `
      <div class="refinement-context">
        <div class="refinement-context__copy">
          <h4>Refinement Policy Inputs</h4>
          <p>
            This is the active policy snapshot that governs how refinement blocks spec progression and whether auto-answer can retry from repository context.
          </p>
        </div>
        <div class="detail-grid">
          <div><strong>Active Tolerance</strong><div><code>${escapeHtml(refinementPolicy.tolerance)}</code></div></div>
          <div><strong>Pending Questions</strong><div><code>${refinementPolicy.pendingQuestionCount}</code></div></div>
          <div><strong>Unanswered Questions</strong><div><code>${refinementPolicy.unansweredQuestionCount}</code></div></div>
          <div><strong>Auto-Answer Eligibility</strong><div><code>${escapeHtml(refinementPolicy.autoAnswer.eligibilityStatus)}</code></div></div>
        </div>
        <div class="refinement-suggestions">
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Auto-Answer Policy</strong>
              <span>${escapeHtml(refinementPolicy.autoAnswer.summary)}</span>
              <span>Status: <code>${escapeHtml(refinementPolicy.autoAnswer.isEnabled ? "enabled" : "disabled")}</code> · Eligibility: <code>${escapeHtml(refinementPolicy.autoAnswer.eligibilityStatus)}</code></span>
              ${refinementPolicy.autoAnswer.agentName ? `<span>Agent: <code>${escapeHtml(refinementPolicy.autoAnswer.agentName)}</code>${refinementPolicy.autoAnswer.agentRole ? ` · Role: <code>${escapeHtml(refinementPolicy.autoAnswer.agentRole)}</code>` : ""}</span>` : ""}
              ${refinementPolicy.autoAnswer.profileName ? `<span>Model profile: <code>${escapeHtml(refinementPolicy.autoAnswer.profileName)}</code></span>` : ""}
              ${refinementPolicy.autoAnswer.eligibilityReason ? `<span>${escapeHtml(refinementPolicy.autoAnswer.eligibilityReason)}</span>` : ""}
            </div>
          </div>
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Blocking Conditions</strong>
              ${renderRefinementBlockingConditions(refinementPolicy.blockingConditions, escapeHtml)}
            </div>
          </div>
        </div>
      </div>
    `
    : "";
  const refinementContextSummarySection = effectiveContext
    ? `
      <div class="refinement-context">
        <div class="refinement-context__copy">
          <h4>Injected Runtime Context</h4>
          <p>
            These are the exact prior artifacts and context files that were injected into the latest persisted refinement run.
          </p>
        </div>
        <div class="detail-grid">
          <div><strong>Workspace Git HEAD</strong><div><code>${escapeHtml(effectiveContext.workspaceGitHeadSha ?? "unavailable")}</code></div></div>
          <div><strong>Operation Prompt SHA256</strong><div><code>${escapeHtml(effectiveContext.operationPromptSha256 ?? "none")}</code></div></div>
          <div><strong>Previous Artifacts</strong><div><code>${effectiveContext.previousArtifacts.length}</code></div></div>
          <div><strong>Context Files</strong><div><code>${effectiveContext.contextFiles.length}</code></div></div>
        </div>
        <div class="refinement-suggestions">
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Previous Artifacts</strong>
              ${renderInjectedArtifactList(effectiveContext.previousArtifacts, escapeHtml)}
            </div>
          </div>
          <div class="refinement-suggestion refinement-suggestion--static">
            <div class="refinement-suggestion__body">
              <strong>Context Files</strong>
              ${renderInjectedArtifactList(effectiveContext.contextFiles, escapeHtml)}
            </div>
          </div>
        </div>
      </div>
    `
    : "";
  const refinementInspectionSection = selectedPhase.latestExecutionInspection?.effectivePrompt || selectedPhase.latestExecutionInspection?.effectiveContext
    ? `
      <div class="refinement-context">
        <div class="refinement-context__copy">
          <h4>Inspect the last refinement execution</h4>
          <p>
            Review the exact effective prompt and injected runtime context that SpecForge sent in the latest persisted refinement run.
            Use this before changing prompt templates or adding more context files.
          </p>
        </div>
        <div class="detail-actions detail-actions--files detail-actions--refinement">
          ${selectedPhase.latestExecutionInspection?.effectivePrompt
            ? `<button class="workflow-action-button workflow-action-button--document" type="button" data-open-effective-prompt-modal>View Last Refinement Prompt</button>`
            : ""}
          ${selectedPhase.latestExecutionInspection?.effectiveContext
            ? `<button class="workflow-action-button workflow-action-button--document" type="button" data-open-effective-context-modal>View Last Refinement Context</button>`
            : ""}
          ${selectedPhase.latestExecutionInspection?.receiptPath
            ? `<button class="workflow-action-button workflow-action-button--document" data-command="openArtifact" data-path="${escapeHtmlAttribute(selectedPhase.latestExecutionInspection.receiptPath)}">Open Receipt</button>`
            : ""}
        </div>
      </div>
    `
    : `
      <div class="refinement-context">
        <div class="refinement-context__copy">
          <h4>Inspect the last refinement execution</h4>
          <p>No persisted refinement execution inspection is available yet for this user story.</p>
        </div>
      </div>
    `;
  const refinementSuggestionsSection = `
    <div class="refinement-context">
      <div class="refinement-context__copy">
        <h4>Need more repo context?</h4>
        <p>
          If the model is blocked by missing repository knowledge, add code, tests, configs, or docs as
          <strong> Context</strong>. Those files are injected into execution. <strong>US Info</strong> stays attached
          to the story, but is not sent to the model by default.
        </p>
      </div>
      <div class="detail-actions detail-actions--files detail-actions--refinement">
        <button class="workflow-action-button workflow-action-button--document" data-command="attachFiles" data-kind="context">Add Context Files</button>
        ${state.contextSuggestions.length > 1
          ? `<button class="workflow-action-button workflow-action-button--document" data-add-suggested-context-files='${escapeHtmlAttribute(JSON.stringify(state.contextSuggestions.map((item) => item.path)))}'>Add All Suggested</button>`
          : ""}
      </div>
      ${state.contextSuggestions.length > 0
        ? `
          <div class="refinement-suggestions">
            ${state.contextSuggestions.map((suggestion) => `
              <div class="refinement-suggestion">
                <div class="refinement-suggestion__body">
                  <strong>${escapeHtml(suggestion.relativePath)}</strong>
                  <span>${escapeHtml(suggestion.reason)}</span>
                </div>
                <button class="workflow-action-button workflow-action-button--document workflow-action-button--compact" data-command="addSuggestedContextFile" data-path="${escapeHtmlAttribute(suggestion.path)}">Add to Context</button>
              </div>
            `).join("")}
          </div>
        `
        : `<p class="muted">No local context suggestions matched this refinement yet. You can still add files manually.</p>`}
    </div>
  `;

  const refinementSection = selectedPhase.phaseId === "refinement" && workflow.refinement
    ? `
      <div class="refinement-shell">
        <div class="refinement-meta">
          <span class="badge${heroTokenClass(workflow.refinement.status)}">${escapeHtml(workflow.refinement.status)}</span>
          <span class="badge">${escapeHtml(workflow.refinement.tolerance)}</span>
        </div>
        ${workflow.refinement.reason ? `<p class="refinement-reason">${escapeHtml(workflow.refinement.reason)}</p>` : ""}
        ${refinementPolicySection}
        ${skillPreselectionSection}
        ${graphScopeSection}
        ${refinementInspectionSection}
        ${refinementContextSummarySection}
        ${workflow.refinement.items.length > 0
      ? `
            <div class="refinement-list">
              ${workflow.refinement.items.map((item) => `
                <label class="refinement-item">
                  <span class="refinement-question-row">
                    <span class="refinement-question">${item.index}. ${escapeHtml(item.question)}</span>
                    <button
                      type="button"
                      class="copy-question-button"
                      data-copy-text="${escapeHtmlAttribute(item.question)}"
                      aria-label="Copy question ${item.index}">${renderCopyQuestionIcon()}</button>
                  </span>
                  <textarea
                    class="refinement-answer"
                    data-refinement-answer
                    data-index="${item.index}"
                    rows="3"
                    placeholder="Write the answer that should remain persisted in us.md">${escapeHtml(item.answer ?? "")}</textarea>
                </label>
              `).join("")}
            </div>
            <div class="detail-actions">
              <button id="submit-refinement-answers" class="workflow-action-button workflow-action-button--progress" ${selectedPhase.isCurrent ? "" : "disabled"}>
                Submit Answers
              </button>
            </div>
          `
      : "<p class=\"muted\">No refinement questions are currently registered for this user story.</p>"}
        ${refinementSuggestionsSection}
      </div>
    `
    : "";

  return {
    beforeArtifact: [],
    afterArtifact: refinementSection
      ? [
          `
            <section class="detail-card">
              <h3>Refinement</h3>
              ${refinementSection}
            </section>
          `
        ]
      : []
  };
}

function renderSkillSelectionList(
  items: readonly { readonly skillPath: string; readonly rationale: string }[],
  escapeHtml: (value: string) => string
): string {
  if (items.length === 0) {
    return `<p class="muted">None.</p>`;
  }

  return `
    <ul class="detail-list">
      ${items.map((item) => `
        <li>
          <strong>${escapeHtml(fileNameFromPath(item.skillPath))}</strong><br>
          <code>${escapeHtml(item.skillPath)}</code><br>
          <span class="muted">${escapeHtml(item.rationale)}</span>
        </li>
      `).join("")}
    </ul>
  `;
}

function renderContextGapList(
  items: readonly string[],
  escapeHtml: (value: string) => string
): string {
  if (items.length === 0) {
    return `<p class="muted">No unresolved context gaps remain in this persisted refinement run.</p>`;
  }

  return `
    <ul class="detail-list">
      ${items.map((item) => `<li>${escapeHtml(item)}</li>`).join("")}
    </ul>
  `;
}

function renderGraphSeedNodes(
  items: readonly { readonly id: string; readonly label: string; readonly reason: string }[],
  escapeHtml: (value: string) => string
): string {
  if (items.length === 0) {
    return `<p class="muted">None.</p>`;
  }

  return `
    <ul class="detail-list">
      ${items.map((item) => `
        <li>
          <strong>${escapeHtml(item.label)}</strong><br>
          <span class="muted">id: <code>${escapeHtml(item.id)}</code></span><br>
          <span class="muted">${escapeHtml(item.reason)}</span>
        </li>
      `).join("")}
    </ul>
  `;
}

function renderRefinementBlockingConditions(
  items: readonly { readonly description: string; readonly status: string; readonly isCurrentlyBlocking: boolean; readonly blockingReason?: string | null }[],
  escapeHtml: (value: string) => string
): string {
  if (items.length === 0) {
    return `<p class="muted">None.</p>`;
  }

  return `
    <ul class="detail-list">
      ${items.map((item) => `
        <li>
          <strong>${escapeHtml(item.description)}</strong><br>
          <span class="muted">status: <code>${escapeHtml(item.status)}</code>${item.isCurrentlyBlocking ? " · blocking now" : " · not blocking now"}</span>
          ${item.blockingReason ? `<br><span class="muted">reason: <code>${escapeHtml(item.blockingReason)}</code></span>` : ""}
        </li>
      `).join("")}
    </ul>
  `;
}

function renderInjectedArtifactList(
  items: readonly { readonly path: string; readonly sha256?: string | null; readonly phaseId?: string | null }[],
  escapeHtml: (value: string) => string
): string {
  if (items.length === 0) {
    return `<p class="muted">None.</p>`;
  }

  return `
    <ul class="detail-list">
      ${items.map((item) => `
        <li>
          <strong>${escapeHtml(item.phaseId ?? fileNameFromPath(item.path))}</strong><br>
          <code>${escapeHtml(item.path)}</code><br>
          <span class="muted">sha256: <code>${escapeHtml(item.sha256 ?? "no hash")}</code></span>
        </li>
      `).join("")}
    </ul>
  `;
}

function fileNameFromPath(path: string): string {
  const normalizedPath = path.replace(/\\/g, "/");
  const segments = normalizedPath.split("/");
  return segments[segments.length - 1] || normalizedPath;
}

function renderCopyQuestionIcon(): string {
  return `
    <span class="copy-question-button__icon copy-question-button__icon--copy" aria-hidden="true">
      <svg viewBox="0 0 24 24" focusable="false">
        <path d="M9 9.75A2.75 2.75 0 0 1 11.75 7h6.5A2.75 2.75 0 0 1 21 9.75v8.5A2.75 2.75 0 0 1 18.25 21h-6.5A2.75 2.75 0 0 1 9 18.25v-8.5Zm2.75-1.25c-.69 0-1.25.56-1.25 1.25v8.5c0 .69.56 1.25 1.25 1.25h6.5c.69 0 1.25-.56 1.25-1.25v-8.5c0-.69-.56-1.25-1.25-1.25h-6.5ZM5.75 3h6.5A2.75 2.75 0 0 1 15 5.75V6.5h-1.5v-.75c0-.69-.56-1.25-1.25-1.25h-6.5c-.69 0-1.25.56-1.25 1.25v8.5c0 .69.56 1.25 1.25 1.25h.75V17h-.75A2.75 2.75 0 0 1 3 14.25v-8.5A2.75 2.75 0 0 1 5.75 3Z"></path>
      </svg>
    </span>
    <span class="copy-question-button__icon copy-question-button__icon--done" aria-hidden="true">
      <svg viewBox="0 0 24 24" focusable="false">
        <path d="M9.55 16.6 5.7 12.75l1.06-1.06 2.8 2.8 7.68-7.68 1.06 1.06-8.74 8.73Z"></path>
      </svg>
    </span>
  `;
}
