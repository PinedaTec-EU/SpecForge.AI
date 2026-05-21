#!/usr/bin/env node
"use strict";

const fs = require("node:fs");
const { buildWorkflowHtml } = require("../dist/workflowView");
const { buildSidebarHtml } = require("../dist/sidebarViewContent");

const payload = JSON.parse(fs.readFileSync(0, "utf8"));
const workflow = payload.workflow;
const userStories = Array.isArray(payload.userStories) ? payload.userStories : [];
const sidebarUserStories = Array.isArray(payload.sidebarUserStories) ? payload.sidebarUserStories : userStories;
const activeSidebarUserStories = Array.isArray(payload.activeSidebarUserStories) ? payload.activeSidebarUserStories : userStories;
const droppedSidebarUserStories = Array.isArray(payload.droppedSidebarUserStories) ? payload.droppedSidebarUserStories : [];
const showDroppedUserStories = payload.showDroppedUserStories === true;
const showCompletedUserStories = payload.showCompletedUserStories === true;
const showBlockedUserStories = payload.showBlockedUserStories === true;
const droppedUserStoryCount = Number.isFinite(payload.droppedUserStoryCount) ? payload.droppedUserStoryCount : 0;
const configurationPortalUrl = payload.configurationPortalUrl || "http://localhost:5128/configuration";
const configurationProvidersUrl = payload.configurationProvidersUrl || configurationPortalUrl;
const configurationAdvancedUrl = payload.configurationAdvancedUrl || configurationPortalUrl;
const displayRuntimeVersion = formatRuntimeVersion(payload.runtimeVersion ?? workflow.lastRuntimeVersion ?? workflow.createdWithRuntimeVersion ?? null);
const state = {
  selectedPhaseId: payload.selectedPhaseId ?? workflow.currentPhase,
  selectedArtifactContent: payload.selectedArtifactContent ?? null,
  selectedOperationContent: payload.selectedOperationContent ?? null,
  runtimeVersion: null,
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

function formatRuntimeVersion(runtimeVersion) {
  const value = String(runtimeVersion ?? "").trim();
  return value ? value.split("+", 1)[0] : null;
}

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
      if (message?.command === "openWorkflow" && message.usId) {
        const url = new URL(window.location.href);
        url.searchParams.delete("selectedPhaseId");
        url.searchParams.set("usId", message.usId);
        window.location.href = url.toString();
        return;
      }

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

      if (message?.command === "attachFiles") {
        const kind = message.kind === "context" ? "context" : "attachment";
        const input = document.createElement("input");
        input.type = "file";
        input.multiple = true;
        input.onchange = () => {
          const files = Array.from(input.files || []);
          if (files.length === 0) {
            return;
          }

          Promise.all(files.map(async file => {
            const buffer = await file.arrayBuffer();
            const bytes = new Uint8Array(buffer);
            let binary = "";
            const chunkSize = 0x8000;
            for (let index = 0; index < bytes.length; index += chunkSize) {
              const chunk = bytes.subarray(index, index + chunkSize);
              binary += String.fromCharCode(...chunk);
            }
            return {
              name: file.name,
              base64Content: btoa(binary)
            };
          }))
            .then(payloadFiles => fetch("/api/attach-files", {
              method: "POST",
              headers: { "content-type": "application/json" },
              body: JSON.stringify({ kind, files: payloadFiles, actor: "cli-user" })
            }))
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
        };
        input.click();
        return;
      }

      if ((message?.command === "addSuggestedContextFile" && message.path)
        || (message?.command === "addSuggestedContextFiles" && Array.isArray(message.paths) && message.paths.length > 0)) {
        const paths = message.command === "addSuggestedContextFile"
          ? [message.path]
          : message.paths;
        fetch("/api/add-context-files", {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify({ paths, actor: "cli-user" })
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

      if (message?.command === "approveDecomposition" || message?.command === "rejectDecomposition") {
        fetch("/api/decomposition-approval", {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify({
            decision: message.command === "approveDecomposition" ? "approve" : "reject",
            actor: "cli-user"
          })
        })
          .then(response => response.ok ? response.json() : response.text().then(text => Promise.reject(new Error(text))))
          .then((result) => {
            if (message.command === "approveDecomposition" && Array.isArray(result.childUsIds) && result.childUsIds.length > 0) {
              const url = new URL(window.location.href);
              url.searchParams.set("selectedPhaseId", "spec");
              window.location.href = url.toString();
              return;
            }
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

      if (message?.command === "openWorkflowTab" && message.usId) {
        const url = new URL(window.location.href);
        url.searchParams.delete("selectedPhaseId");
        url.searchParams.set("usId", message.usId);
        window.open(url.toString(), "_blank", "noopener");
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
    const renderedWorkflowUsId = ${JSON.stringify(workflow.usId)};
    try {
      const url = new URL(window.location.href);
      if (renderedWorkflowUsId && url.searchParams.get("usId") !== renderedWorkflowUsId) {
        url.searchParams.set("usId", renderedWorkflowUsId);
        window.history.replaceState(window.history.state, "", url.toString());
      }
    } catch {}
    let signature = ${JSON.stringify(payload.signature)};
    async function poll() {
      try {
        const response = await fetch("/api/workflow-signature" + window.location.search, { cache: "no-store" });
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

function buildCliSidebarHtml(items, options) {
  return buildSidebarHtml({
    hasWorkspace: true,
    showCreateForm: false,
    busyMessage: null,
    promptsInitialized: true,
    promptsMessage: null,
    settingsConfigured: true,
    settingsMessage: null,
    starredUserStoryId: null,
    activeWorkflowUsId: workflow.usId,
    runtimeVersion: null,
    viewMode: "category",
    showDroppedUserStories: options.showDroppedUserStories,
    showCompletedUserStories: options.showCompletedUserStories,
    showBlockedUserStories: options.showBlockedUserStories,
    droppedUserStoryCount,
    categories: [...new Set(items.map(item => item.category).filter(Boolean))],
    userStories: items
  }).replace("<script>", `${sidebarApiShim}\n<script>`);
}

function safeScriptJson(value) {
  return JSON.stringify(value).replaceAll("</", "<\\/");
}

const activeSidebarHtml = buildCliSidebarHtml(activeSidebarUserStories, {
  showDroppedUserStories: false,
  showCompletedUserStories: false,
  showBlockedUserStories: false
});
const activeCompletedSidebarHtml = buildCliSidebarHtml(activeSidebarUserStories, {
  showDroppedUserStories: false,
  showCompletedUserStories: true,
  showBlockedUserStories: false
});
const activeBlockedSidebarHtml = buildCliSidebarHtml(activeSidebarUserStories, {
  showDroppedUserStories: false,
  showCompletedUserStories: false,
  showBlockedUserStories: true
});
const activeCompletedBlockedSidebarHtml = buildCliSidebarHtml(activeSidebarUserStories, {
  showDroppedUserStories: false,
  showCompletedUserStories: true,
  showBlockedUserStories: true
});
const droppedSidebarHtml = buildCliSidebarHtml(droppedSidebarUserStories, {
  showDroppedUserStories: true,
  showCompletedUserStories: false,
  showBlockedUserStories: false
});
const sidebarHtml = showDroppedUserStories
  ? droppedSidebarHtml
  : showCompletedUserStories && showBlockedUserStories
    ? activeCompletedBlockedSidebarHtml
    : showCompletedUserStories
    ? activeCompletedSidebarHtml
    : showBlockedUserStories
      ? activeBlockedSidebarHtml
    : activeSidebarHtml;

const sidebarShell = `
<style>
  body.specforge-cli-with-sidebar { display: grid; grid-template-columns: minmax(300px, 360px) minmax(0, 1fr); min-height: 100vh; overflow: hidden; }
  body.specforge-cli-with-sidebar.specforge-cli-sidebar-collapsed { grid-template-columns: 58px minmax(0, 1fr); }
  .specforge-cli-sidebar { position: sticky; top: 0; height: 100vh; min-width: 0; border-right: 1px solid rgba(114, 241, 184, 0.16); background: #080e14; display: grid; grid-template-rows: auto minmax(0, 1fr); z-index: 50; }
  .specforge-cli-sidebar__rail { display: grid; grid-template-columns: minmax(0, 1fr) auto auto; align-items: center; gap: 8px; padding: 10px; border-bottom: 1px solid rgba(114, 241, 184, 0.12); }
  .specforge-cli-sidebar__button { width: 38px; height: 38px; border-radius: 12px; border: 1px solid rgba(114, 241, 184, 0.18); background: rgba(255, 255, 255, 0.04); color: #72f1b8; font: 700 1rem/1 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; cursor: pointer; display: inline-grid; place-items: center; }
  .specforge-cli-sidebar__button:hover { background: rgba(114, 241, 184, 0.12); border-color: rgba(114, 241, 184, 0.34); }
  .specforge-cli-sidebar__button--active { background: rgba(114, 241, 184, 0.14); border-color: rgba(114, 241, 184, 0.36); }
  .specforge-cli-sidebar__brand { min-width: 0; display: flex; align-items: baseline; gap: 8px; white-space: nowrap; overflow: hidden; }
  .specforge-cli-sidebar__title { min-width: 0; color: rgba(255, 255, 255, 0.86); font: 800 0.9rem/1.2 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; overflow: hidden; text-overflow: ellipsis; }
  .specforge-cli-sidebar__version { flex-shrink: 0; color: rgba(176, 180, 176, 0.76); font: 700 0.7rem/1.2 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
  .specforge-cli-sidebar__frame { width: 100%; height: 100%; border: 0; min-width: 0; background: transparent; }
  body.specforge-cli-sidebar-collapsed .specforge-cli-sidebar { grid-template-rows: 1fr; }
  body.specforge-cli-sidebar-collapsed .specforge-cli-sidebar__rail { display: flex; flex-direction: column; align-items: center; justify-content: flex-start; border-bottom: 0; padding-top: 12px; }
  body.specforge-cli-sidebar-collapsed .specforge-cli-sidebar__brand,
  body.specforge-cli-sidebar-collapsed .specforge-cli-sidebar__frame { display: none; }
  body.specforge-cli-with-sidebar > .workflow-page { min-width: 0; height: 100vh; overflow: hidden; }
  .specforge-cli-config-overlay { position: fixed; inset: 0; z-index: 200; display: grid; place-items: center; padding: 28px; background: rgba(3, 8, 12, 0.72); backdrop-filter: blur(8px); }
  .specforge-cli-config-overlay[hidden] { display: none; }
  .specforge-cli-config-dialog { width: min(1100px, 100%); height: min(820px, calc(100vh - 56px)); border: 1px solid rgba(114, 241, 184, 0.18); border-radius: 12px; background: #0f1720; box-shadow: 0 28px 90px rgba(0, 0, 0, 0.52); display: grid; grid-template-rows: auto minmax(0, 1fr); overflow: hidden; }
  .specforge-cli-config-head { display: flex; align-items: center; justify-content: space-between; gap: 16px; padding: 12px 14px; border-bottom: 1px solid rgba(114, 241, 184, 0.14); background: #080e14; color: rgba(255, 255, 255, 0.86); font: 800 0.82rem/1.2 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
  .specforge-cli-config-close { width: 34px; height: 34px; border-radius: 10px; border: 1px solid rgba(114, 241, 184, 0.2); background: rgba(255, 255, 255, 0.05); color: #72f1b8; font: 900 1.1rem/1 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; cursor: pointer; }
  .specforge-cli-config-frame { width: 100%; height: 100%; border: 0; background: #0f1720; }
  @media (max-width: 860px) {
    body.specforge-cli-with-sidebar { grid-template-columns: 58px minmax(0, 1fr); }
    body.specforge-cli-with-sidebar:not(.specforge-cli-sidebar-collapsed) { grid-template-columns: minmax(280px, 86vw) minmax(0, 1fr); }
    .specforge-cli-config-overlay { padding: 10px; }
    .specforge-cli-config-dialog { height: calc(100vh - 20px); }
  }
</style>
<aside class="specforge-cli-sidebar" aria-label="SpecForge user stories">
  <div class="specforge-cli-sidebar__rail">
    <div class="specforge-cli-sidebar__brand">
      <span class="specforge-cli-sidebar__title">SpecForge.AI</span>
      ${displayRuntimeVersion ? `<span class="specforge-cli-sidebar__version">v.${escapeHtmlAttr(displayRuntimeVersion)}</span>` : ""}
    </div>
    <button class="specforge-cli-sidebar__button" type="button" data-cli-sidebar-settings title="Configuration" aria-label="Configuration">⚙</button>
    <button class="specforge-cli-sidebar__button specforge-cli-sidebar__button--active" type="button" data-cli-sidebar-pin title="Unpin sidebar" aria-label="Unpin sidebar" aria-pressed="true">📌</button>
  </div>
  <iframe class="specforge-cli-sidebar__frame" title="User stories" srcdoc="${escapeHtmlAttr(sidebarHtml)}"></iframe>
</aside>
<div class="specforge-cli-config-overlay" data-cli-config-overlay hidden>
  <section class="specforge-cli-config-dialog" role="dialog" aria-modal="true" aria-labelledby="specforge-cli-config-title">
    <div class="specforge-cli-config-head">
      <span id="specforge-cli-config-title">SpecForge Configuration</span>
      <button class="specforge-cli-config-close" type="button" data-cli-config-close aria-label="Close configuration">×</button>
    </div>
    <iframe class="specforge-cli-config-frame" title="SpecForge Configuration" data-cli-config-frame></iframe>
  </section>
</div>
<script>
  (() => {
    document.body.classList.add("specforge-cli-with-sidebar");
    const collapsedKey = "specforge.cli.sidebar.collapsed";
    const starredUserStoryStorageKey = "specforge.cli.sidebar.starredUserStoryId";
    const configOverlay = document.querySelector("[data-cli-config-overlay]");
    const configFrame = document.querySelector("[data-cli-config-frame]");
    const sidebarFrame = document.querySelector('iframe[title="User stories"]');
    const sidebarPin = document.querySelector("[data-cli-sidebar-pin]");
    const sidebarHtmlByMode = {
      active: ${safeScriptJson(activeSidebarHtml)},
      activeCompleted: ${safeScriptJson(activeCompletedSidebarHtml)},
      activeBlocked: ${safeScriptJson(activeBlockedSidebarHtml)},
      activeCompletedBlocked: ${safeScriptJson(activeCompletedBlockedSidebarHtml)},
      dropped: ${safeScriptJson(droppedSidebarHtml)}
    };
    let sidebarShowsDropped = ${showDroppedUserStories ? "true" : "false"};
    let sidebarShowsCompleted = ${showCompletedUserStories ? "true" : "false"};
    let sidebarShowsBlocked = ${showBlockedUserStories ? "true" : "false"};
    const openConfiguration = (url) => {
      if (configFrame) {
        configFrame.setAttribute("src", url);
      }
      configOverlay?.removeAttribute("hidden");
    };
    const closeConfiguration = () => {
      configOverlay?.setAttribute("hidden", "");
    };
    const getStarredUserStoryId = () => {
      try { return localStorage.getItem(starredUserStoryStorageKey) || null; }
      catch { return null; }
    };
    const setStarredUserStoryId = (usId) => {
      try {
        if (usId) localStorage.setItem(starredUserStoryStorageKey, usId);
        else localStorage.removeItem(starredUserStoryStorageKey);
      } catch {}
    };
    const replaceSidebarFrame = () => {
      if (!sidebarFrame) return;
      sidebarFrame.srcdoc = sidebarShowsDropped
        ? sidebarHtmlByMode.dropped
        : sidebarShowsCompleted && sidebarShowsBlocked
          ? sidebarHtmlByMode.activeCompletedBlocked
        : sidebarShowsCompleted
          ? sidebarHtmlByMode.activeCompleted
        : sidebarShowsBlocked
          ? sidebarHtmlByMode.activeBlocked
          : sidebarHtmlByMode.active;
    };
    const replaceSidebarUrlState = () => {
      const url = new URL(window.location.href);
      if (sidebarShowsDropped) {
        url.searchParams.set("sidebarVisibility", "dropped");
        url.searchParams.delete("sidebarCompleted");
        url.searchParams.delete("sidebarBlocked");
      } else {
        url.searchParams.delete("sidebarVisibility");
        if (sidebarShowsCompleted) {
          url.searchParams.set("sidebarCompleted", "true");
        } else {
          url.searchParams.delete("sidebarCompleted");
        }
        if (sidebarShowsBlocked) {
          url.searchParams.set("sidebarBlocked", "true");
        } else {
          url.searchParams.delete("sidebarBlocked");
        }
      }
      window.history.replaceState(window.history.state, "", url.toString());
    };
    const applySidebarStarredUserStory = () => {
      const starredUserStoryId = getStarredUserStoryId();
      const doc = sidebarFrame?.contentDocument;
      if (!doc) return;
      for (const button of doc.querySelectorAll('[data-command="toggleStarredUserStory"][data-us-id]')) {
        const usId = button.getAttribute("data-us-id") || "";
        const active = usId === starredUserStoryId;
        const label = (active ? "Unstar " : "Star ") + usId;
        button.classList.toggle("story-star--active", active);
        button.setAttribute("title", label);
        button.setAttribute("aria-label", label);
        const icon = button.querySelector("[aria-hidden='true']");
        if (icon) icon.textContent = active ? "★" : "☆";
      }
    };
    const applyCollapsed = (collapsed) => {
      document.body.classList.toggle("specforge-cli-sidebar-collapsed", collapsed);
      if (sidebarPin) {
        sidebarPin.classList.toggle("specforge-cli-sidebar__button--active", !collapsed);
        sidebarPin.setAttribute("title", collapsed ? "Pin sidebar" : "Unpin sidebar");
        sidebarPin.setAttribute("aria-label", collapsed ? "Pin sidebar" : "Unpin sidebar");
        sidebarPin.setAttribute("aria-pressed", collapsed ? "false" : "true");
      }
      try { localStorage.setItem(collapsedKey, collapsed ? "true" : "false"); } catch {}
    };
    applyCollapsed(localStorage.getItem(collapsedKey) === "true");
    sidebarPin?.addEventListener("click", () => applyCollapsed(!document.body.classList.contains("specforge-cli-sidebar-collapsed")));
    document.querySelector("[data-cli-sidebar-settings]")?.addEventListener("click", () => {
      openConfiguration(${JSON.stringify(configurationPortalUrl)});
    });
    sidebarFrame?.addEventListener("load", applySidebarStarredUserStory);
    applySidebarStarredUserStory();
    document.querySelector("[data-cli-config-close]")?.addEventListener("click", closeConfiguration);
    configOverlay?.addEventListener("click", event => {
      if (event.target === configOverlay) closeConfiguration();
    });
    window.addEventListener("keydown", event => {
      if (event.key === "Escape" && !configOverlay?.hasAttribute("hidden")) closeConfiguration();
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
      if (message.command === "openMainArtifact" && message.usId) {
        const url = new URL(window.location.href);
        url.searchParams.set("usId", message.usId);
        url.searchParams.set("selectedPhaseId", "capture");
        window.location.href = url.toString();
        return;
      }
      if (message.command === "toggleStarredUserStory" && message.usId) {
        const current = getStarredUserStoryId();
        setStarredUserStoryId(current === message.usId ? null : message.usId);
        applySidebarStarredUserStory();
        return;
      }
      if (message.command === "toggleDroppedUserStories") {
        sidebarShowsDropped = !sidebarShowsDropped;
        sidebarShowsCompleted = false;
        sidebarShowsBlocked = false;
        replaceSidebarUrlState();
        replaceSidebarFrame();
        return;
      }
      if (message.command === "toggleCompletedUserStories") {
        sidebarShowsDropped = false;
        sidebarShowsCompleted = !sidebarShowsCompleted;
        replaceSidebarUrlState();
        replaceSidebarFrame();
        return;
      }
      if (message.command === "toggleBlockedUserStories") {
        sidebarShowsDropped = false;
        sidebarShowsBlocked = !sidebarShowsBlocked;
        replaceSidebarUrlState();
        replaceSidebarFrame();
        return;
      }
      if ((message.command === "dropUserStory" || message.command === "recoverUserStory") && message.usId) {
        if (message.command === "dropUserStory" && !window.confirm("Drop " + message.usId + "? It will be marked as deleted and hidden from the SpecForge panel.")) {
          return;
        }
        const endpoint = message.command === "dropUserStory" ? "/api/drop-user-story" : "/api/recover-user-story";
        fetch(endpoint, {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify({ usId: message.usId })
        })
          .then(response => response.ok ? response.json() : response.text().then(text => Promise.reject(new Error(text))))
          .then(() => {
            const url = new URL(window.location.href);
            if (message.command === "dropUserStory") {
              url.searchParams.delete("sidebarVisibility");
            }
            window.location.href = url.toString();
          })
          .catch(error => {
            window.alert(error instanceof Error ? error.message : String(error));
          });
        return;
      }
      if (message.command === "openExecutionSettings") {
        openConfiguration(${JSON.stringify(configurationProvidersUrl)});
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
