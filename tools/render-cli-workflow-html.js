#!/usr/bin/env node
"use strict";

const fs = require("node:fs");
const { buildWorkflowHtml } = require("../dist/workflowView");
const { buildSidebarHtml } = require("../dist/sidebarViewContent");
const { readWorkflowGraphLayoutConfigAsync } = require("./workflow-graph-layout-portable");

function formatRuntimeVersion(runtimeVersion) {
  const value = String(runtimeVersion ?? "").trim();
  return value ? value.split("+", 1)[0] : null;
}

async function main() {
  const payload = JSON.parse(fs.readFileSync(0, "utf8"));
  const currentActor = String(payload.currentActor || "cli-user").trim() || "cli-user";
  const workflow = payload.workflow;
  const userStories = Array.isArray(payload.userStories) ? payload.userStories : [];
  const sidebarUserStories = Array.isArray(payload.sidebarUserStories) ? payload.sidebarUserStories : userStories;
  const activeSidebarUserStories = Array.isArray(payload.activeSidebarUserStories) ? payload.activeSidebarUserStories : userStories;
  const droppedSidebarUserStories = Array.isArray(payload.droppedSidebarUserStories) ? payload.droppedSidebarUserStories : [];
  const showDroppedUserStories = payload.showDroppedUserStories === true;
  const showCompletedUserStories = payload.showCompletedUserStories === true;
  const showBlockedUserStories = payload.showBlockedUserStories === true;
  const showHiddenUserStories = payload.showHiddenUserStories === true;
  const watchingUserStoryIds = Array.isArray(payload.watchingUserStoryIds) ? payload.watchingUserStoryIds : [];
  const hiddenUserStoryIds = Array.isArray(payload.hiddenUserStoryIds) ? payload.hiddenUserStoryIds : [];
  const droppedUserStoryCount = Number.isFinite(payload.droppedUserStoryCount) ? payload.droppedUserStoryCount : 0;
  const configurationPortalUrl = payload.configurationPortalUrl || "http://localhost:5128/configuration";
  const configurationProvidersUrl = payload.configurationProvidersUrl || configurationPortalUrl;
  const configurationAdvancedUrl = payload.configurationAdvancedUrl || configurationPortalUrl;
  const displayRuntimeVersion = formatRuntimeVersion(payload.runtimeVersion ?? workflow.lastRuntimeVersion ?? workflow.createdWithRuntimeVersion ?? null);
  const workflowGraphLayout = typeof payload.workspaceRoot === "string" && payload.workspaceRoot.length > 0
    ? await readWorkflowGraphLayoutConfigAsync(payload.workspaceRoot)
    : null;
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
    workflowGraphLayout
  };

const browserShim = `
<script>
  const specForgeCliCurrentActor = ${JSON.stringify(currentActor)};
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
          body: JSON.stringify({ question: message.question, actor: specForgeCliCurrentActor })
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
          body: JSON.stringify({ question: message.question, answer: message.answer, actor: specForgeCliCurrentActor })
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
          body: JSON.stringify({ answers: message.answers, actor: specForgeCliCurrentActor })
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

      if (message?.command === "saveWorkflowGraphLayout") {
        fetch("/api/workflow-graph-layout", {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify(message)
        })
          .then(response => response.ok ? response.json() : response.text().then(text => Promise.reject(new Error(text))))
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
              body: JSON.stringify({ kind, files: payloadFiles, actor: specForgeCliCurrentActor })
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
          body: JSON.stringify({ paths, actor: specForgeCliCurrentActor })
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
            actor: specForgeCliCurrentActor
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
            actor: specForgeCliCurrentActor
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
  const currentActor = String(options.currentActor || "cli-user").trim() || "cli-user";
  const normalizedCurrentActor = currentActor.toLowerCase();
  const watchingIds = new Set(options.watchingUserStoryIds.map(normalizeUserStoryId));
  const hiddenIds = new Set(options.hiddenUserStoryIds.map(normalizeUserStoryId));
  const scopedItems = options.includeOtherOwners
    ? items.filter(item => options.showHiddenUserStories || !hiddenIds.has(normalizeUserStoryId(item.usId)))
    : items.filter(item => {
      const normalizedUsId = normalizeUserStoryId(item.usId);
      if (!options.showHiddenUserStories && hiddenIds.has(normalizedUsId)) {
        return false;
      }

      const normalizedOwner = String(item.owner || "").trim().toLowerCase();
      return watchingIds.has(normalizedUsId) || normalizedOwner === normalizedCurrentActor;
    });

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
    showHiddenUserStories: options.showHiddenUserStories,
    searchIncludesOtherOwners: options.includeOtherOwners,
    currentActor,
    watchingUserStoryIds: options.watchingUserStoryIds,
    hiddenUserStoryIds: options.hiddenUserStoryIds,
    maxVisibleUserStories: null,
    totalUserStoryCount: scopedItems.length,
    droppedUserStoryCount,
    categories: [...new Set(scopedItems.map(item => item.category).filter(Boolean))],
    userStories: scopedItems
  }).replace("<script>", `${sidebarApiShim}\n<script>`);
}

function safeScriptJson(value) {
  return JSON.stringify(value).replaceAll("</", "<\\/");
}

function normalizeUserStoryId(value) {
  return String(value || "").trim().toUpperCase();
}

function buildSidebarHtmlModes(includeOtherOwners, showHiddenUserStories, watchingUserStoryIds, hiddenUserStoryIds) {
  return {
    active: buildCliSidebarHtml(activeSidebarUserStories, {
      showDroppedUserStories: false,
      showCompletedUserStories: false,
      showBlockedUserStories: false,
      showHiddenUserStories,
      includeOtherOwners,
      watchingUserStoryIds,
      hiddenUserStoryIds,
      currentActor
    }),
    activeCompleted: buildCliSidebarHtml(activeSidebarUserStories, {
      showDroppedUserStories: false,
      showCompletedUserStories: true,
      showBlockedUserStories: false,
      showHiddenUserStories,
      includeOtherOwners,
      watchingUserStoryIds,
      hiddenUserStoryIds,
      currentActor
    }),
    activeBlocked: buildCliSidebarHtml(activeSidebarUserStories, {
      showDroppedUserStories: false,
      showCompletedUserStories: false,
      showBlockedUserStories: true,
      showHiddenUserStories,
      includeOtherOwners,
      watchingUserStoryIds,
      hiddenUserStoryIds,
      currentActor
    }),
    activeCompletedBlocked: buildCliSidebarHtml(activeSidebarUserStories, {
      showDroppedUserStories: false,
      showCompletedUserStories: true,
      showBlockedUserStories: true,
      showHiddenUserStories,
      includeOtherOwners,
      watchingUserStoryIds,
      hiddenUserStoryIds,
      currentActor
    }),
    dropped: buildCliSidebarHtml(droppedSidebarUserStories, {
      showDroppedUserStories: true,
      showCompletedUserStories: false,
      showBlockedUserStories: false,
      showHiddenUserStories,
      includeOtherOwners,
      watchingUserStoryIds,
      hiddenUserStoryIds,
      currentActor
    })
  };
}

const sidebarHtmlByScope = {
  mine: {
    visible: buildSidebarHtmlModes(false, false, watchingUserStoryIds, hiddenUserStoryIds),
    hidden: buildSidebarHtmlModes(false, true, watchingUserStoryIds, hiddenUserStoryIds)
  },
  all: {
    visible: buildSidebarHtmlModes(true, false, watchingUserStoryIds, hiddenUserStoryIds),
    hidden: buildSidebarHtmlModes(true, true, watchingUserStoryIds, hiddenUserStoryIds)
  }
};

const initialSidebarModes = showHiddenUserStories ? sidebarHtmlByScope.mine.hidden : sidebarHtmlByScope.mine.visible;
const sidebarHtml = showDroppedUserStories
  ? initialSidebarModes.dropped
  : showCompletedUserStories && showBlockedUserStories
    ? initialSidebarModes.activeCompletedBlocked
    : showCompletedUserStories
    ? initialSidebarModes.activeCompleted
    : showBlockedUserStories
      ? initialSidebarModes.activeBlocked
    : initialSidebarModes.active;

const sidebarShell = `
<style>
  body.specforge-cli-with-sidebar { display: grid; grid-template-columns: minmax(300px, 360px) minmax(0, 1fr); min-height: 100vh; overflow: hidden; }
  body.specforge-cli-with-sidebar.specforge-cli-sidebar-collapsed { grid-template-columns: 58px minmax(0, 1fr); }
  .specforge-cli-sidebar { position: sticky; top: 0; height: 100vh; min-width: 0; border-right: 1px solid rgba(114, 241, 184, 0.16); background: #080e14; display: grid; grid-template-rows: auto minmax(0, 1fr); z-index: 50; }
  .specforge-cli-sidebar__rail { display: grid; grid-template-columns: minmax(0, 1fr) auto auto; align-items: center; gap: 8px; padding: 10px; border-bottom: 1px solid rgba(114, 241, 184, 0.12); }
  .specforge-cli-sidebar__button { width: 38px; height: 38px; border-radius: 12px; border: 1px solid rgba(114, 241, 184, 0.18); background: rgba(255, 255, 255, 0.04); color: #72f1b8; font: 700 1rem/1 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; cursor: pointer; display: inline-grid; place-items: center; }
  .specforge-cli-sidebar__button:hover { background: rgba(114, 241, 184, 0.12); border-color: rgba(114, 241, 184, 0.34); }
  .specforge-cli-sidebar__button--active { background: rgba(114, 241, 184, 0.14); border-color: rgba(114, 241, 184, 0.36); }
  .specforge-cli-sidebar__menu { position: relative; }
  .specforge-cli-sidebar__menu-panel { position: absolute; right: 0; top: calc(100% + 8px); z-index: 90; min-width: 220px; padding: 8px; border-radius: 14px; border: 1px solid rgba(114, 241, 184, 0.18); background: rgba(8, 14, 20, 0.96); box-shadow: 0 18px 56px rgba(0, 0, 0, 0.42); display: grid; gap: 4px; }
  .specforge-cli-sidebar__menu-panel[hidden] { display: none; }
  .specforge-cli-sidebar__menu-item { display: grid; grid-template-columns: 18px minmax(0, 1fr); align-items: center; gap: 10px; width: 100%; padding: 10px 12px; border-radius: 10px; border: 0; background: transparent; color: rgba(255, 255, 255, 0.86); font: 700 0.83rem/1.2 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; text-align: left; cursor: pointer; }
  .specforge-cli-sidebar__menu-item:hover { background: rgba(114, 241, 184, 0.10); }
  .specforge-cli-sidebar__menu-item--active { color: #dfffee; background: rgba(114, 241, 184, 0.12); }
  .specforge-cli-sidebar__menu-check { color: #72f1b8; font-weight: 900; }
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
  .specforge-cli-edit-overlay { position: fixed; inset: 0; z-index: 220; display: grid; place-items: center; padding: 28px; background: rgba(3, 8, 12, 0.72); backdrop-filter: blur(8px); }
  .specforge-cli-edit-overlay[hidden] { display: none; }
  .specforge-cli-edit-dialog { width: min(560px, 100%); border: 1px solid rgba(114, 241, 184, 0.18); border-radius: 18px; background: #0f1720; box-shadow: 0 28px 90px rgba(0, 0, 0, 0.52); overflow: hidden; }
  .specforge-cli-edit-form { display: grid; gap: 14px; padding: 18px; }
  .specforge-cli-edit-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 16px; }
  .specforge-cli-edit-kicker { display: block; color: rgba(114, 241, 184, 0.86); font: 800 0.72rem/1.2 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; letter-spacing: 0.14em; text-transform: uppercase; }
  .specforge-cli-edit-head h2 { margin: 6px 0 0; color: rgba(255, 255, 255, 0.94); font: 800 1.05rem/1.2 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
  .specforge-cli-edit-close, .specforge-cli-edit-secondary, .specforge-cli-edit-primary { border-radius: 10px; border: 1px solid rgba(114, 241, 184, 0.2); cursor: pointer; font: 700 0.9rem/1 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
  .specforge-cli-edit-close { width: 34px; height: 34px; background: rgba(255, 255, 255, 0.05); color: #72f1b8; font-size: 1.1rem; }
  .specforge-cli-edit-field { display: grid; gap: 6px; }
  .specforge-cli-edit-field span { color: rgba(255, 255, 255, 0.78); font: 700 0.78rem/1.2 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; text-transform: uppercase; letter-spacing: 0.08em; }
  .specforge-cli-edit-field input { width: 100%; border-radius: 12px; border: 1px solid rgba(255, 255, 255, 0.08); background: rgba(255, 255, 255, 0.04); color: rgba(255, 255, 255, 0.94); padding: 12px 14px; font: 500 0.94rem/1.4 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
  .specforge-cli-edit-owner-row { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: 10px; align-items: end; }
  .specforge-cli-edit-owner-assign { border-radius: 12px; border: 1px solid rgba(114, 241, 184, 0.22); background: rgba(20, 53, 40, 0.9); color: #dfffee; padding: 12px 14px; font: 700 0.84rem/1 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; cursor: pointer; white-space: nowrap; }
  .specforge-cli-edit-owner-assign[hidden] { display: none; }
  .specforge-cli-edit-error { margin: 0; padding: 10px 12px; border-radius: 12px; border: 1px solid rgba(255, 139, 139, 0.2); background: rgba(120, 29, 29, 0.18); color: #ffb8b8; font: 600 0.85rem/1.4 ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
  .specforge-cli-edit-actions { display: flex; justify-content: flex-end; gap: 10px; }
  .specforge-cli-edit-secondary { background: rgba(255, 255, 255, 0.05); color: rgba(255, 255, 255, 0.86); padding: 11px 14px; }
  .specforge-cli-edit-primary { background: linear-gradient(180deg, rgba(114, 241, 184, 0.24), rgba(16, 36, 28, 0.96)); color: #f3fff9; padding: 11px 16px; }
  .specforge-cli-source-focus { outline: 2px solid rgba(114, 241, 184, 0.78); outline-offset: 6px; border-radius: 16px; transition: outline-color 180ms ease; }
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
    <div class="specforge-cli-sidebar__menu" data-cli-sidebar-view-menu>
      <button class="specforge-cli-sidebar__button" type="button" data-cli-sidebar-view-options title="Sidebar view options" aria-label="Sidebar view options" aria-haspopup="menu" aria-expanded="false">☷</button>
      <div class="specforge-cli-sidebar__menu-panel" data-cli-sidebar-view-panel role="menu" hidden>
        <button class="specforge-cli-sidebar__menu-item" type="button" data-cli-parent-command="toggleDroppedUserStories" role="menuitemcheckbox" aria-checked="false"><span class="specforge-cli-sidebar__menu-check" aria-hidden="true"></span><span>Show dropped</span></button>
        <button class="specforge-cli-sidebar__menu-item" type="button" data-cli-parent-command="toggleCompletedUserStories" role="menuitemcheckbox" aria-checked="false"><span class="specforge-cli-sidebar__menu-check" aria-hidden="true"></span><span>Show completed</span></button>
        <button class="specforge-cli-sidebar__menu-item" type="button" data-cli-parent-command="toggleBlockedUserStories" role="menuitemcheckbox" aria-checked="false"><span class="specforge-cli-sidebar__menu-check" aria-hidden="true"></span><span>Show blocked</span></button>
        <button class="specforge-cli-sidebar__menu-item" type="button" data-cli-parent-command="toggleShowHiddenUserStories" role="menuitemcheckbox" aria-checked="false"><span class="specforge-cli-sidebar__menu-check" aria-hidden="true"></span><span>Show hidden</span></button>
        <button class="specforge-cli-sidebar__menu-item" type="button" data-cli-parent-command="toggleSearchIncludesOtherOwners" role="menuitemcheckbox" aria-checked="false"><span class="specforge-cli-sidebar__menu-check" aria-hidden="true"></span><span>Include other owners</span></button>
      </div>
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
<div class="specforge-cli-edit-overlay" data-cli-edit-overlay hidden>
  <section class="specforge-cli-edit-dialog" role="dialog" aria-modal="true" aria-labelledby="specforge-cli-edit-title">
    <form class="specforge-cli-edit-form" data-cli-edit-form>
      <div class="specforge-cli-edit-head">
        <div>
          <span class="specforge-cli-edit-kicker">User Story Metadata</span>
          <h2 id="specforge-cli-edit-title">Edit US info</h2>
        </div>
        <button class="specforge-cli-edit-close" type="button" data-cli-edit-close aria-label="Close edit form">×</button>
      </div>
      <input type="hidden" name="usId" />
      <label class="specforge-cli-edit-field">
        <span>Title</span>
        <input name="title" type="text" required />
      </label>
      <label class="specforge-cli-edit-field">
        <span>Owner</span>
        <div class="specforge-cli-edit-owner-row">
          <input name="owner" type="text" required />
          <button class="specforge-cli-edit-owner-assign" type="button" data-cli-edit-assign-to-me hidden>Assign to me</button>
        </div>
      </label>
      <label class="specforge-cli-edit-field">
        <span>Category</span>
        <input name="category" type="text" required />
      </label>
      <label class="specforge-cli-edit-field">
        <span>Tags</span>
        <input name="tags" type="text" />
      </label>
      <p class="specforge-cli-edit-error" data-cli-edit-error hidden></p>
      <div class="specforge-cli-edit-actions">
        <button class="specforge-cli-edit-secondary" type="button" data-cli-edit-cancel>Cancel</button>
        <button class="specforge-cli-edit-primary" type="submit" data-cli-edit-submit>Save</button>
      </div>
    </form>
  </section>
</div>
<script>
  (() => {
    document.body.classList.add("specforge-cli-with-sidebar");
    const collapsedKey = "specforge.cli.sidebar.collapsed";
    const starredUserStoryStorageKey = "specforge.cli.sidebar.starredUserStoryId";
    const watchingUserStoryIdsStorageKey = "specforge.cli.sidebar.watchingUserStoryIds";
    const hiddenUserStoryIdsStorageKey = "specforge.cli.sidebar.hiddenUserStoryIds";
    const showHiddenStorageKey = "specforge.cli.sidebar.showHiddenUserStories";
    const configOverlay = document.querySelector("[data-cli-config-overlay]");
    const configFrame = document.querySelector("[data-cli-config-frame]");
    const editOverlay = document.querySelector("[data-cli-edit-overlay]");
    const editForm = document.querySelector("[data-cli-edit-form]");
    const editError = document.querySelector("[data-cli-edit-error]");
    const editAssignToMe = document.querySelector("[data-cli-edit-assign-to-me]");
    const sidebarFrame = document.querySelector('iframe[title="User stories"]');
    const sidebarPin = document.querySelector("[data-cli-sidebar-pin]");
    const sidebarViewOptionsToggle = document.querySelector("[data-cli-sidebar-view-options]");
    const sidebarViewOptionsPanel = document.querySelector("[data-cli-sidebar-view-panel]");
    const sidebarHtmlByScope = {
      mine: ${safeScriptJson(sidebarHtmlByScope.mine)},
      all: ${safeScriptJson(sidebarHtmlByScope.all)}
    };
    let sidebarShowsDropped = ${showDroppedUserStories ? "true" : "false"};
    let sidebarShowsCompleted = ${showCompletedUserStories ? "true" : "false"};
    let sidebarShowsBlocked = ${showBlockedUserStories ? "true" : "false"};
    let sidebarShowsHidden = ${showHiddenUserStories ? "true" : "false"};
    let sidebarShowsOtherOwners = false;
    let sidebarWatchingUserStoryIds = ${safeScriptJson(watchingUserStoryIds)};
    let sidebarHiddenUserStoryIds = ${safeScriptJson(hiddenUserStoryIds)};
    const normalizedCurrentActor = currentActor.toLowerCase();
    const openConfiguration = (url) => {
      if (configFrame) {
        configFrame.setAttribute("src", url);
      }
      configOverlay?.removeAttribute("hidden");
    };
    const closeConfiguration = () => {
      configOverlay?.setAttribute("hidden", "");
    };
    const setEditError = (message) => {
      if (!(editError instanceof HTMLElement)) {
        return;
      }
      const normalized = String(message || "").trim();
      editError.textContent = normalized;
      editError.hidden = normalized.length === 0;
    };
    const closeEditUserStoryForm = () => {
      editOverlay?.setAttribute("hidden", "");
      setEditError("");
      if (editForm instanceof HTMLFormElement) {
        editForm.reset();
        editForm.dataset.busy = "false";
      }
    };
    const updateAssignToMeVisibility = () => {
      if (!(editForm instanceof HTMLFormElement) || !(editAssignToMe instanceof HTMLElement)) {
        return;
      }
      const ownerInput = editForm.elements.namedItem("owner");
      const normalizedOwner = String(ownerInput?.value || "").trim().toLowerCase();
      editAssignToMe.hidden = normalizedOwner === normalizedCurrentActor;
    };
    const openEditUserStoryForm = (message) => {
      if (!(editForm instanceof HTMLFormElement)) {
        return;
      }
      editForm.dataset.busy = "false";
      editForm.elements.namedItem("usId").value = String(message.usId || "");
      editForm.elements.namedItem("title").value = String(message.title || "");
      editForm.elements.namedItem("owner").value = String(message.owner || currentActor);
      editForm.elements.namedItem("category").value = String(message.category || "");
      editForm.elements.namedItem("tags").value = String(message.tags || "");
      setEditError("");
      updateAssignToMeVisibility();
      editOverlay?.removeAttribute("hidden");
      editForm.elements.namedItem("title")?.focus();
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
    const normalizeUserStoryId = (value) => String(value || "").trim().toUpperCase();
    const closeSidebarViewOptions = () => {
      sidebarViewOptionsPanel?.setAttribute("hidden", "");
      sidebarViewOptionsToggle?.setAttribute("aria-expanded", "false");
    };
    const toggleSidebarViewOptions = () => {
      if (!(sidebarViewOptionsPanel instanceof HTMLElement)) {
        return;
      }
      const willOpen = sidebarViewOptionsPanel.hasAttribute("hidden");
      if (willOpen) {
        sidebarViewOptionsPanel.removeAttribute("hidden");
        sidebarViewOptionsToggle?.setAttribute("aria-expanded", "true");
      } else {
        closeSidebarViewOptions();
      }
    };
    const parseUserStoryIdList = (value) => Array.from(new Set(String(value || "")
      .split(",")
      .map(normalizeUserStoryId)
      .filter(Boolean)));
    const readUserStoryIdList = (storageKey) => {
      try {
        return parseUserStoryIdList(localStorage.getItem(storageKey) || "");
      } catch {
        return [];
      }
    };
    const writeUserStoryIdList = (storageKey, usIds) => {
      try {
        if (Array.isArray(usIds) && usIds.length > 0) {
          localStorage.setItem(storageKey, usIds.join(","));
        } else {
          localStorage.removeItem(storageKey);
        }
      } catch {}
    };
    const replaceSidebarUrlState = (targetUrl) => {
      const url = targetUrl || new URL(window.location.href);
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
      if (sidebarShowsHidden) {
        url.searchParams.set("sidebarHiddenVisible", "true");
      } else {
        url.searchParams.delete("sidebarHiddenVisible");
      }
      if (sidebarShowsOtherOwners) {
        url.searchParams.set("sidebarOtherOwners", "true");
      } else {
        url.searchParams.delete("sidebarOtherOwners");
      }
      if (sidebarWatchingUserStoryIds.length > 0) {
        url.searchParams.set("sidebarWatching", sidebarWatchingUserStoryIds.join(","));
      } else {
        url.searchParams.delete("sidebarWatching");
      }
      if (sidebarHiddenUserStoryIds.length > 0) {
        url.searchParams.set("sidebarHidden", sidebarHiddenUserStoryIds.join(","));
      } else {
        url.searchParams.delete("sidebarHidden");
      }
      window.history.replaceState(window.history.state, "", url.toString());
      return url;
    };
    const persistSidebarLists = () => {
      writeUserStoryIdList(watchingUserStoryIdsStorageKey, sidebarWatchingUserStoryIds);
      writeUserStoryIdList(hiddenUserStoryIdsStorageKey, sidebarHiddenUserStoryIds);
      try {
        localStorage.setItem(showHiddenStorageKey, sidebarShowsHidden ? "true" : "false");
      } catch {}
    };
    const hydrateSidebarListsFromStorage = () => {
      const url = new URL(window.location.href);
      const hasWatchingInUrl = url.searchParams.has("sidebarWatching");
      const hasHiddenInUrl = url.searchParams.has("sidebarHidden");
      const hasShowHiddenInUrl = url.searchParams.has("sidebarHiddenVisible");
      let shouldReload = false;

      sidebarWatchingUserStoryIds = parseUserStoryIdList(url.searchParams.get("sidebarWatching") || "");
      sidebarHiddenUserStoryIds = parseUserStoryIdList(url.searchParams.get("sidebarHidden") || "");
      sidebarShowsHidden = url.searchParams.get("sidebarHiddenVisible") === "true";

      if (!hasWatchingInUrl) {
        const storedWatching = readUserStoryIdList(watchingUserStoryIdsStorageKey);
        if (storedWatching.length > 0) {
          sidebarWatchingUserStoryIds = storedWatching;
          shouldReload = true;
        }
      }

      if (!hasHiddenInUrl) {
        const storedHidden = readUserStoryIdList(hiddenUserStoryIdsStorageKey);
        if (storedHidden.length > 0) {
          sidebarHiddenUserStoryIds = storedHidden;
          shouldReload = true;
        }
      }

      if (!hasShowHiddenInUrl) {
        try {
          if (localStorage.getItem(showHiddenStorageKey) === "true") {
            sidebarShowsHidden = true;
            shouldReload = true;
          }
        } catch {}
      }

      persistSidebarLists();
      if (!shouldReload) {
        return false;
      }

      const nextUrl = replaceSidebarUrlState(url);
      window.location.replace(nextUrl.toString());
      return true;
    };
    const replaceSidebarFrame = () => {
      if (!sidebarFrame) return;
      const scope = sidebarShowsOtherOwners ? sidebarHtmlByScope.all : sidebarHtmlByScope.mine;
      const visibilityScope = sidebarShowsHidden ? scope.hidden : scope.visible;
      sidebarFrame.srcdoc = sidebarShowsDropped
        ? visibilityScope.dropped
        : sidebarShowsCompleted && sidebarShowsBlocked
          ? visibilityScope.activeCompletedBlocked
        : sidebarShowsCompleted
          ? visibilityScope.activeCompleted
        : sidebarShowsBlocked
          ? visibilityScope.activeBlocked
          : visibilityScope.active;
    };
    const updateSidebarViewOptionsUi = () => {
      const activeMap = {
        toggleDroppedUserStories: sidebarShowsDropped,
        toggleCompletedUserStories: sidebarShowsCompleted,
        toggleBlockedUserStories: sidebarShowsBlocked,
        toggleShowHiddenUserStories: sidebarShowsHidden,
        toggleSearchIncludesOtherOwners: sidebarShowsOtherOwners
      };
      let hasActiveFilter = false;
      for (const button of document.querySelectorAll("[data-cli-parent-command]")) {
        if (!(button instanceof HTMLButtonElement)) {
          continue;
        }
        const command = String(button.dataset.cliParentCommand || "");
        const active = activeMap[command] === true;
        hasActiveFilter = hasActiveFilter || active;
        button.classList.toggle("specforge-cli-sidebar__menu-item--active", active);
        button.setAttribute("aria-checked", active ? "true" : "false");
        const check = button.querySelector(".specforge-cli-sidebar__menu-check");
        if (check) {
          check.textContent = active ? "✓" : "";
        }
      }
      sidebarViewOptionsToggle?.classList.toggle("specforge-cli-sidebar__button--active", hasActiveFilter);
    };
    const applySidebarScopeCommand = (command) => {
      switch (command) {
        case "toggleDroppedUserStories":
          sidebarShowsDropped = !sidebarShowsDropped;
          sidebarShowsCompleted = false;
          sidebarShowsBlocked = false;
          break;
        case "toggleCompletedUserStories":
          sidebarShowsDropped = false;
          sidebarShowsCompleted = !sidebarShowsCompleted;
          break;
        case "toggleBlockedUserStories":
          sidebarShowsDropped = false;
          sidebarShowsBlocked = !sidebarShowsBlocked;
          break;
        case "toggleShowHiddenUserStories":
          sidebarShowsHidden = !sidebarShowsHidden;
          persistSidebarLists();
          break;
        case "toggleSearchIncludesOtherOwners":
          sidebarShowsDropped = false;
          sidebarShowsOtherOwners = !sidebarShowsOtherOwners;
          break;
        default:
          return false;
      }
      replaceSidebarUrlState();
      replaceSidebarFrame();
      updateSidebarViewOptionsUi();
      return true;
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
    const bridgeSidebarScopeControls = () => {
      const doc = sidebarFrame?.contentDocument;
      if (!doc) return;
      const commandButtons = [
        "toggleDroppedUserStories",
        "toggleCompletedUserStories",
        "toggleBlockedUserStories",
        "toggleShowHiddenUserStories",
        "toggleSearchIncludesOtherOwners"
      ];
      for (const command of commandButtons) {
        for (const button of doc.querySelectorAll('[data-command="' + command + '"]')) {
          if (!(button instanceof HTMLButtonElement) || button.dataset.portalBound === "true") {
            continue;
          }
          button.dataset.portalBound = "true";
          button.addEventListener("click", (event) => {
            event.preventDefault();
            event.stopPropagation();
            applySidebarScopeCommand(command);
          });
        }
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
    sidebarShowsOtherOwners = new URL(window.location.href).searchParams.get("sidebarOtherOwners") === "true";
    if (hydrateSidebarListsFromStorage()) {
      return;
    }
    replaceSidebarFrame();
    updateSidebarViewOptionsUi();
    sidebarPin?.addEventListener("click", () => applyCollapsed(!document.body.classList.contains("specforge-cli-sidebar-collapsed")));
    sidebarViewOptionsToggle?.addEventListener("click", toggleSidebarViewOptions);
    sidebarViewOptionsPanel?.addEventListener("click", event => {
      const button = event.target instanceof Element
        ? event.target.closest("[data-cli-parent-command]")
        : null;
      if (!(button instanceof HTMLButtonElement)) {
        return;
      }
      event.preventDefault();
      const command = String(button.dataset.cliParentCommand || "");
      if (applySidebarScopeCommand(command)) {
        closeSidebarViewOptions();
      }
    });
    document.addEventListener("pointerdown", event => {
      if (!(event.target instanceof Node)) {
        return;
      }
      if (sidebarViewOptionsPanel?.contains(event.target) || sidebarViewOptionsToggle?.contains(event.target)) {
        return;
      }
      closeSidebarViewOptions();
    });
    document.querySelector("[data-cli-sidebar-settings]")?.addEventListener("click", () => {
      openConfiguration(${JSON.stringify(configurationPortalUrl)});
    });
    sidebarFrame?.addEventListener("load", () => {
      applySidebarStarredUserStory();
      bridgeSidebarScopeControls();
    });
    applySidebarStarredUserStory();
    bridgeSidebarScopeControls();
    document.querySelector("[data-cli-config-close]")?.addEventListener("click", closeConfiguration);
    configOverlay?.addEventListener("click", event => {
      if (event.target === configOverlay) closeConfiguration();
    });
    document.querySelector("[data-cli-edit-close]")?.addEventListener("click", closeEditUserStoryForm);
    document.querySelector("[data-cli-edit-cancel]")?.addEventListener("click", closeEditUserStoryForm);
    editOverlay?.addEventListener("click", event => {
      if (event.target === editOverlay) closeEditUserStoryForm();
    });
    editForm?.addEventListener("submit", event => {
      event.preventDefault();
      if (!(editForm instanceof HTMLFormElement) || editForm.dataset.busy === "true") {
        return;
      }
      const usId = String(editForm.elements.namedItem("usId")?.value || "").trim();
      const title = String(editForm.elements.namedItem("title")?.value || "").trim();
      const owner = String(editForm.elements.namedItem("owner")?.value || "").trim();
      const category = String(editForm.elements.namedItem("category")?.value || "").trim();
      const tags = String(editForm.elements.namedItem("tags")?.value || "")
        .split(",")
        .map(item => item.trim().toLowerCase())
        .filter(Boolean);
      if (!usId || !title || !owner || !category) {
        setEditError("Title, owner, and category are required.");
        return;
      }
      editForm.dataset.busy = "true";
      setEditError("");
      requestJson("/api/update-user-story-info", { usId, title, owner, category, tags, actor: currentActor })
        .then(() => {
          closeEditUserStoryForm();
          window.location.reload();
        })
        .catch(error => {
          editForm.dataset.busy = "false";
          setEditError(error instanceof Error ? error.message : String(error));
        });
    });
    editForm?.elements?.namedItem("owner")?.addEventListener?.("input", updateAssignToMeVisibility);
    editAssignToMe?.addEventListener("click", () => {
      if (!(editForm instanceof HTMLFormElement)) {
        return;
      }
      const ownerInput = editForm.elements.namedItem("owner");
      if (!(ownerInput instanceof HTMLInputElement)) {
        return;
      }
      ownerInput.value = currentActor;
      updateAssignToMeVisibility();
      ownerInput.focus();
      ownerInput.select();
    });
    window.addEventListener("keydown", event => {
      if (event.key === "Escape" && !configOverlay?.hasAttribute("hidden")) closeConfiguration();
      if (event.key === "Escape" && !editOverlay?.hasAttribute("hidden")) closeEditUserStoryForm();
    });
    const requestJson = (endpoint, body) => fetch(endpoint, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(body)
    }).then(response => response.ok ? response.json() : response.text().then(text => Promise.reject(new Error(text))));
    const focusUserStorySourceSection = () => {
      const section = document.querySelector("[data-user-story-source-section]");
      if (!(section instanceof HTMLElement)) {
        return false;
      }
      section.scrollIntoView({ behavior: "smooth", block: "start" });
      section.classList.add("specforge-cli-source-focus");
      window.setTimeout(() => section.classList.remove("specforge-cli-source-focus"), 1600);
      return true;
    };
    const applyArtifactFocusFromUrl = () => {
      const url = new URL(window.location.href);
      if (url.searchParams.get("artifactFocus") !== "source") {
        return;
      }
      window.requestAnimationFrame(() => {
        focusUserStorySourceSection();
      });
    };
    const reloadWithSidebarState = () => {
      persistSidebarLists();
      const url = replaceSidebarUrlState(new URL(window.location.href));
      window.location.href = url.toString();
    };
    window.addEventListener("message", event => {
      if (event.data?.source !== "specforge-cli-sidebar") return;
      const message = event.data.message || {};
      if (message.command === "openWorkflow" && message.usId) {
        const url = new URL(window.location.href);
        url.searchParams.delete("selectedPhaseId");
        url.searchParams.delete("artifactFocus");
        url.searchParams.set("usId", message.usId);
        window.location.href = url.toString();
        return;
      }
      if (message.command === "openMainArtifact" && message.usId) {
        const url = new URL(window.location.href);
        if (url.searchParams.get("usId") === message.usId && url.searchParams.get("selectedPhaseId") === "capture") {
          if (focusUserStorySourceSection()) {
            return;
          }
        }
        url.searchParams.set("usId", message.usId);
        url.searchParams.set("selectedPhaseId", "capture");
        url.searchParams.set("artifactFocus", "source");
        window.location.href = url.toString();
        return;
      }
      if (message.command === "showEditUserStoryForm" && message.usId) {
        openEditUserStoryForm(message);
        return;
      }
      if (message.command === "toggleStarredUserStory" && message.usId) {
        const current = getStarredUserStoryId();
        setStarredUserStoryId(current === message.usId ? null : message.usId);
        applySidebarStarredUserStory();
        return;
      }
      if (message.command === "toggleDroppedUserStories") {
        applySidebarScopeCommand("toggleDroppedUserStories");
        return;
      }
      if (message.command === "toggleCompletedUserStories") {
        applySidebarScopeCommand("toggleCompletedUserStories");
        return;
      }
      if (message.command === "toggleBlockedUserStories") {
        applySidebarScopeCommand("toggleBlockedUserStories");
        return;
      }
      if (message.command === "toggleShowHiddenUserStories") {
        applySidebarScopeCommand("toggleShowHiddenUserStories");
        return;
      }
      if (message.command === "toggleSearchIncludesOtherOwners") {
        applySidebarScopeCommand("toggleSearchIncludesOtherOwners");
        return;
      }
      if (message.command === "toggleSidebarVisibilityUserStory" && message.usId) {
        const normalizedUsId = normalizeUserStoryId(message.usId);
        const normalizedOwner = String(message.owner || "").trim().toLowerCase();
        const isOwnedByCurrentActor = normalizedOwner === normalizedCurrentActor;
        const isHidden = sidebarHiddenUserStoryIds.includes(normalizedUsId);
        const isWatched = sidebarWatchingUserStoryIds.includes(normalizedUsId);
        const isVisibleInSidebar = !isHidden && (sidebarShowsOtherOwners || isWatched || isOwnedByCurrentActor);
        if (isVisibleInSidebar) {
          sidebarHiddenUserStoryIds = sidebarHiddenUserStoryIds.includes(normalizedUsId)
            ? sidebarHiddenUserStoryIds
            : [...sidebarHiddenUserStoryIds, normalizedUsId];
          sidebarWatchingUserStoryIds = sidebarWatchingUserStoryIds.filter(usId => usId !== normalizedUsId);
        } else {
          sidebarHiddenUserStoryIds = sidebarHiddenUserStoryIds.filter(usId => usId !== normalizedUsId);
          if (!isOwnedByCurrentActor && !sidebarWatchingUserStoryIds.includes(normalizedUsId)) {
            sidebarWatchingUserStoryIds = [...sidebarWatchingUserStoryIds, normalizedUsId];
          }
        }
        reloadWithSidebarState();
        return;
      }
      if (message.command === "resetUserStoryToCapture" && message.usId) {
        if (!window.confirm("Reset " + message.usId + " to capture and delete all derived artifacts after the source?")) {
          return;
        }
        requestJson("/api/reset-user-story-to-capture", { usId: message.usId, actor: currentActor })
          .then(() => {
            window.location.reload();
          })
          .catch(error => {
            window.alert(error instanceof Error ? error.message : String(error));
          });
        return;
      }
      if (message.command === "analyzeRepairUserStory" && message.usId) {
        requestJson("/api/analyze-user-story-lineage", { usId: message.usId, actor: currentActor })
          .then((analysis) => {
            if (analysis.status === "clean") {
              window.alert(message.usId + " lineage is clean.");
              return null;
            }
            if (analysis.status !== "inconsistent" || !Array.isArray(analysis.deprecatedCandidatePaths) || analysis.deprecatedCandidatePaths.length === 0 || !analysis.recommendedTargetPhase) {
              const firstFinding = Array.isArray(analysis.findings) ? analysis.findings[0] : null;
              throw new Error(firstFinding?.summary || (message.usId + " lineage needs manual review."));
            }
            const confirmation = window.confirm(
              message.usId + " lineage is inconsistent. Repair will archive "
              + analysis.deprecatedCandidatePaths.length
              + " artifact(s) and return the workflow to "
              + analysis.recommendedTargetPhase
              + "."
            );
            if (!confirmation) {
              return null;
            }
            return requestJson("/api/repair-user-story-lineage", { usId: message.usId, actor: currentActor });
          })
          .then((result) => {
            if (!result) {
              return;
            }
            window.location.reload();
          })
          .catch(error => {
            window.alert(error instanceof Error ? error.message : String(error));
          });
        return;
      }
      if ((message.command === "dropUserStory" || message.command === "recoverUserStory") && message.usId) {
        if (message.command === "dropUserStory" && !window.confirm("Drop " + message.usId + "? It will be marked as deleted and hidden from the SpecForge panel.")) {
          return;
        }
        const endpoint = message.command === "dropUserStory" ? "/api/drop-user-story" : "/api/recover-user-story";
        requestJson(endpoint, { usId: message.usId })
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
    applyArtifactFocusFromUrl();
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
}

main().catch((error) => {
  process.stderr.write(error instanceof Error ? (error.stack || error.message) : String(error));
  process.exit(1);
});
