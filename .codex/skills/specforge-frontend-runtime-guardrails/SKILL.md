---
name: specforge-frontend-runtime-guardrails
description: SpecForge-local guardrails for workflow views, CLI portal rendering, browser shims, and runtime entrypoints. Use when modifying src-vscode, workflow renderers, portal HTML generation, CLI browser bridges, or SpecForge runtime entrypoints.
---

# SpecForge Frontend Runtime Guardrails

This skill is local to SpecForge and exists to stop the specific structural debt patterns already visible in the product.

## Trigger surfaces

Apply this skill when touching:

- `src-vscode/**`
- `tools/render-cli-workflow-html.js`
- `src/SpecForge.Runner.Cli/**`
- browser workflow portal rendering
- webview HTML/CSS/JS generation
- graph layout and workflow visualization code

## SpecForge-specific rules

- Do not append more behavior to `workflowView.ts` without extracting a coherent section first when the change touches graph rendering, audit rendering, phase sections, overlay behavior, or inline script/style blocks.
- Keep page-shell composition separate from section rendering, client-side interaction code, and large CSS blocks.
- Keep the VS Code webview renderer and CLI portal renderer on a shared source contract. Do not create parallel behavior branches unless the surface difference is real and isolated.
- The CLI browser shim must stay a thin adapter. It may translate browser actions to HTTP calls, but it must not become a second product logic layer.
- `Program.cs` entrypoints in `SpecForge.Runner.Cli` must dispatch to focused handlers. Do not grow one file with more command parsing, HTTP routing, payload parsing, and HTML assembly in the same scope.
- Workflow graph layout rules, edge routing, and node rendering must not be reimplemented in multiple places.
- If a rendering change affects both VS Code and CLI portal surfaces, first ask whether the logic belongs in a shared renderer/helper instead of being duplicated in shell-specific files.

## Required extraction moves

Prefer these extraction targets before adding new behavior:

- phase-specific renderers under `src-vscode/workflow-view/`
- shared graph helpers
- portal route handlers
- payload readers/writers
- browser shim command handlers
- focused HTML section builders
- focused CSS/script resources when inline blocks become large

## Anti-patterns to block

- giant template strings that mix markup, styling, state derivation, and event behavior
- route switches that keep expanding inside one entrypoint file
- importing compiled `dist/` output as if it were the design boundary rather than a packaging artifact
- duplicated workflow behavior between webview and CLI portal
- feature work that increases hotspot size without reducing coupling

## Close-out check

Before closing a SpecForge UI/runtime change, confirm:

1. Did this change reduce or at least not worsen hotspot concentration?
2. Is browser-bridge logic still thin and surface-specific?
3. Is shared workflow behavior implemented once?
4. Did I avoid adding new product logic to `Program.cs` or `workflowView.ts` without extraction?
