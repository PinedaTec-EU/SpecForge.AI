#!/usr/bin/env node
"use strict";

const fs = require("node:fs");
const { buildWorkflowHtml } = require("../dist/workflowView");
const { buildSidebarHtml } = require("../dist/sidebarViewContent");

const payload = JSON.parse(fs.readFileSync(0, "utf8"));
const workflow = payload.workflow;
const userStories = Array.isArray(payload.userStories) ? payload.userStories : [];
const configurationPortalUrl = payload.configurationPortalUrl || "http://localhost:5127/";
const configurationProvidersUrl = payload.configurationProvidersUrl || configurationPortalUrl;
const configurationAdvancedUrl = payload.configurationAdvancedUrl || configurationPortalUrl;
const state = {
  selectedPhaseId: payload.selectedPhaseId ?? workflow.currentPhase,
  selectedArtifactContent: payload.selectedArtifactContent ?? null,
  selectedOperationContent: payload.selectedOperationContent ?? null,
  runtimeVersion: payload.runtimeVersion ?? workflow.lastRuntimeVersion ?? workflow.createdWithRuntimeVersion ?? null,
  contextSuggestions: [],
  settingsConfigured: true,
  settingsMessage: null,
  executionSettingsPending: false,
  executionSettingsPendingMessage: null,
  maxImplementationReviewCycles: 5,
  completedUsLockOnCompleted: false,
  visualTimelineEnabled: false,
  debugMode: false,
  requireExplicitApprovalBranchAcceptance: false,
  graphLayoutMode: "vertical",
  graphInitialZoomMode: "fit-width",
  workflowGraphLayout: null
};

const browserShim = `
<script>
  window.__specForgeVsCodeApi = window.__specForgeVsCodeApi || {
    getState() {
      try {
        const state = JSON.parse(sessionStorage.getItem("specforge.workflow.state") || "{}");
        if (sessionStorage.getItem("specforge.workflow.userViewport") !== "true") {
          delete state.graphScrollTop;
          delete state.graphScrollLeft;
          delete state.graphStageOffsetX;
          delete state.graphStageOffsetY;
          delete state.graphInitialZoomMode;
          delete state.graphZoomMode;
          delete state.graphZoomScale;
        }

        return state;
      }
      catch { return {}; }
    },
    setState(value) {
      try { sessionStorage.setItem("specforge.workflow.state", JSON.stringify(value || {})); }
      catch {}
    },
    postMessage(message) {
      if (message?.command === "selectPhase" && message.phaseId) {
        const url = new URL(window.location.href);
        url.searchParams.set("selectedPhaseId", message.phaseId);
        window.location.href = url.toString();
        return;
      }

      if (message?.command === "suggestApprovalAnswer" && message.question) {
        fetch("/api/suggest-approval-answer", {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify({ question: message.question, actor: "cli-user" })
        })
          .then(response => response.ok ? response.json() : response.text().then(text => Promise.reject(new Error(text))))
          .then(result => {
            try {
              const state = JSON.parse(sessionStorage.getItem("specforge.workflow.state") || "{}");
              state.approvalAnswerDrafts = {
                ...(state.approvalAnswerDrafts || {}),
                [String(message.index)]: result.answer || ""
              };
              sessionStorage.setItem("specforge.workflow.state", JSON.stringify(state));
            } catch {}
            window.postMessage({
              command: "approvalAnswerSuggested",
              index: message.index,
              question: result.question,
              answer: result.answer || ""
            }, "*");
          })
          .catch(error => {
            window.postMessage({
              command: "approvalAnswerSuggested",
              index: message.index,
              question: message.question,
              answer: error instanceof Error ? error.message : String(error)
            }, "*");
          });
        return;
      }

      if (message?.command === "continue" || message?.command === "play") {
        fetch("/api/continue", { method: "POST" })
          .then(response => response.ok ? response.json() : response.text().then(text => Promise.reject(new Error(text))))
          .then(() => {
            window.location.reload();
          })
          .catch(error => {
            window.postMessage({
              command: "workflowActionFailed",
              action: message.command,
              detail: error instanceof Error ? error.message : String(error)
            }, "*");
          });
        return;
      }

      if (message?.command === "submitApprovalAnswer" && message.question && message.answer) {
        fetch("/api/approval-answer", {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify({ question: message.question, answer: message.answer, actor: "cli-user" })
        })
          .then(response => response.ok ? response.json() : response.text().then(text => Promise.reject(new Error(text))))
          .then(() => {
            window.location.reload();
          })
          .catch(error => {
            window.postMessage({
              command: "workflowActionFailed",
              action: message.command,
              detail: error instanceof Error ? error.message : String(error)
            }, "*");
          });
        return;
      }

      if (message?.command === "submitRefinementAnswers" && Array.isArray(message.answers)) {
        fetch("/api/refinement-answers", {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify({ answers: message.answers, actor: "cli-user" })
        })
          .then(response => response.ok ? response.json() : response.text().then(text => Promise.reject(new Error(text))))
          .then(() => {
            window.location.reload();
          })
          .catch(error => {
            window.postMessage({
              command: "workflowActionFailed",
              action: message.command,
              detail: error instanceof Error ? error.message : String(error)
            }, "*");
          });
        return;
      }

      if (message?.command === "approve") {
        fetch("/api/approve", {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify({
            baseBranch: message.baseBranch || null,
            workBranch: message.workBranch || null,
            actor: "cli-user"
          })
        })
          .then(response => response.ok ? response.json() : response.text().then(text => Promise.reject(new Error(text))))
          .then(() => {
            window.location.reload();
          })
          .catch(error => {
            window.postMessage({
              command: "workflowActionFailed",
              action: message.command,
              detail: error instanceof Error ? error.message : String(error)
            }, "*");
          });
        return;
      }

      window.dispatchEvent(new CustomEvent("specforge-cli-command", { detail: message }));
    }
  };
  window.addEventListener("pointerdown", event => {
    if (event.target?.closest?.('[data-panel-scroll="graph"], [data-graph-zoom-in], [data-graph-zoom-out], [data-graph-fit-width], [data-graph-auto-fit]')) {
      sessionStorage.setItem("specforge.workflow.userViewport", "true");
    }
  }, true);
  window.addEventListener("wheel", event => {
    if (event.target?.closest?.('[data-panel-scroll="graph"]')) {
      sessionStorage.setItem("specforge.workflow.userViewport", "true");
    }
  }, { capture: true, passive: true });
</script>`;

const refreshShim = `
<script>
  (() => {
    let signature = ${JSON.stringify(payload.signature)};
    async function poll() {
      try {
        const response = await fetch("/api/workflow-signature", { cache: "no-store" });
        if (!response.ok) return;
        const next = await response.text();
        if (signature && next && next !== signature) {
          window.location.reload();
        }
      } catch {}
    }
    window.addEventListener("specforge-cli-command", event => {
      const command = event.detail?.command;
      if (command === "webviewReady" || command === "webviewDispatch" || command === "webviewClientError") {
        return;
      }
      if (command === "continue" || command === "approve" || command === "play" || command === "pause" || command === "stop") {
        poll();
      }
    });
    window.setInterval(poll, 1000);
  })();
</script>`;

const sidebarApiShim = `
<script>
  window.acquireVsCodeApi = window.acquireVsCodeApi || (() => ({
    getState() {
      try { return JSON.parse(sessionStorage.getItem("specforge.cli.sidebar.state") || "{}"); }
      catch { return {}; }
    },
    setState(value) {
      try { sessionStorage.setItem("specforge.cli.sidebar.state", JSON.stringify(value || {})); }
      catch {}
    },
    postMessage(message) {
      window.parent.postMessage({ source: "specforge-cli-sidebar", message }, "*");
    }
  }));
</script>`;

const sidebarHtml = buildSidebarHtml({
  hasWorkspace: true,
  showCreateForm: false,
  busyMessage: null,
  promptsInitialized: true,
  promptsMessage: null,
  settingsConfigured: true,
  settingsMessage: null,
  starredUserStoryId: null,
  activeWorkflowUsId: workflow.usId,
  runtimeVersion: state.runtimeVersion,
  viewMode: "phase",
  categories: [...new Set(userStories.map(item => item.category).filter(Boolean))],
  userStories
}).replace("<script>", `${sidebarApiShim}\n<script>`);

const sidebarShell = `
<style>
  body.specforge-cli-with-sidebar { display: grid; grid-template-columns: minmax(300px, 360px) minmax(0, 1fr); min-height: 100vh; overflow: hidden; }
  body.specforge-cli-with-sidebar.specforge-cli-sidebar-collapsed { grid-template-columns: 58px minmax(0, 1fr); }
  .specforge-cli-sidebar { position: sticky; top: 0; height: 100vh; min-width: 0; border-right: 1px solid rgba(114, 241, 184, 0.16); background: #080e14; display: grid; grid-template-rows: auto minmax(0, 1fr); z-index: 50; }
  .specforge-cli-sidebar__rail { display: flex; align-items: center; gap: 8px; padding: 10px; border-bottom: 1px solid rgba(114, 241, 184, 0.12); }
  .specforge-cli-sidebar__button { width: 38px; height: 38px; border-radius: 12px; border: 1px solid rgba(114, 241, 184, 0.18); background: rgba(255, 255, 255, 0.04); color: #72f1b8; font: 700 1rem/1 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; cursor: pointer; display: inline-grid; place-items: center; }
  .specforge-cli-sidebar__button:hover { background: rgba(114, 241, 184, 0.12); border-color: rgba(114, 241, 184, 0.34); }
  .specforge-cli-sidebar__title { min-width: 0; color: rgba(255, 255, 255, 0.78); font: 700 0.82rem/1.2 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  .specforge-cli-sidebar__frame { width: 100%; height: 100%; border: 0; min-width: 0; background: transparent; }
  body.specforge-cli-sidebar-collapsed .specforge-cli-sidebar { grid-template-rows: 1fr; }
  body.specforge-cli-sidebar-collapsed .specforge-cli-sidebar__rail { flex-direction: column; align-items: center; justify-content: flex-start; border-bottom: 0; padding-top: 12px; }
  body.specforge-cli-sidebar-collapsed .specforge-cli-sidebar__title,
  body.specforge-cli-sidebar-collapsed .specforge-cli-sidebar__frame,
  body.specforge-cli-sidebar-collapsed [data-cli-sidebar-collapse] { display: none; }
  body.specforge-cli-with-sidebar > .workflow-page { min-width: 0; height: 100vh; overflow: hidden; }
  @media (max-width: 860px) {
    body.specforge-cli-with-sidebar { grid-template-columns: 58px minmax(0, 1fr); }
    body.specforge-cli-with-sidebar:not(.specforge-cli-sidebar-collapsed) { grid-template-columns: minmax(280px, 86vw) minmax(0, 1fr); }
  }
</style>
<aside class="specforge-cli-sidebar" aria-label="SpecForge user stories">
  <div class="specforge-cli-sidebar__rail">
    <button class="specforge-cli-sidebar__button" type="button" data-cli-sidebar-stories title="User stories" aria-label="User stories">☷</button>
    <button class="specforge-cli-sidebar__button" type="button" data-cli-sidebar-settings title="Advanced configuration" aria-label="Advanced configuration">⚙</button>
    <span class="specforge-cli-sidebar__title">User Stories</span>
    <button class="specforge-cli-sidebar__button" type="button" data-cli-sidebar-collapse title="Collapse sidebar" aria-label="Collapse sidebar">‹</button>
  </div>
  <iframe class="specforge-cli-sidebar__frame" title="User stories" srcdoc="${escapeHtmlAttr(sidebarHtml)}"></iframe>
</aside>
<script>
  (() => {
    document.body.classList.add("specforge-cli-with-sidebar");
    const collapsedKey = "specforge.cli.sidebar.collapsed";
    const applyCollapsed = (collapsed) => {
      document.body.classList.toggle("specforge-cli-sidebar-collapsed", collapsed);
      try { localStorage.setItem(collapsedKey, collapsed ? "true" : "false"); } catch {}
    };
    applyCollapsed(localStorage.getItem(collapsedKey) === "true");
    document.querySelector("[data-cli-sidebar-collapse]")?.addEventListener("click", () => applyCollapsed(true));
    document.querySelector("[data-cli-sidebar-stories]")?.addEventListener("click", () => applyCollapsed(false));
    document.querySelector("[data-cli-sidebar-settings]")?.addEventListener("click", () => {
      window.open(${JSON.stringify(configurationAdvancedUrl)}, "_blank", "noopener");
    });
    window.addEventListener("message", event => {
      if (event.data?.source !== "specforge-cli-sidebar") return;
      const message = event.data.message || {};
      if (message.command === "openWorkflow" && message.usId) {
        const url = new URL(window.location.href);
        url.searchParams.delete("selectedPhaseId");
        url.searchParams.set("usId", message.usId);
        window.location.href = url.toString();
        return;
      }
      if (message.command === "openExecutionSettings") {
        window.open(${JSON.stringify(configurationProvidersUrl)}, "_blank", "noopener");
      }
    });
  })();
</script>`;

function escapeHtmlAttr(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

const html = buildWorkflowHtml(workflow, state, "idle", "", "")
  .replace("<script", `${browserShim}\n<script`)
  .replace("<body>", `<body>${sidebarShell}`)
  .replace("</body>", `${refreshShim}\n</body>`);

process.stdout.write(html);
