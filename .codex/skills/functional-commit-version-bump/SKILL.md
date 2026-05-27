---
name: functional-commit-version-bump
description: Use in this repository after completing any new functionality or functional subtask. Enforces a functional commit for the delivered change, followed by a separate version bump using the repository's versionbumper tool and its own independent commit.
---

# Functional Commit And Version Bump

This repository requires every completed functional change to close with traceable commits and local plugin publication. Do not consider a functional change complete until the functional commit, version bump commit, and required marketplace sync have been handled:

1. A functional commit for the delivered change.
2. A separate version bump commit created after the functional commit.
3. Local plugin marketplace sync after the version bump, with a separate artifact/sync commit when it changes files.

## Functional Commit

- Before committing, run the relevant validation for the touched area.
- Stage only files that belong to the delivered change.
- Commit with a message that includes `done`, clearly maps to the delivered outcome, and includes the GitHub issue short reference in the commit subject as `#<number>`.
- Do not assume the issue code in the task title such as `SFF-034` is enough. The subject must contain the native GitHub reference too, for example `done #123 implement <outcome>`.
- Do not include version bump files in the functional commit.

## Version Bump Commit

After the functional commit succeeds:

1. Run the repository version bump tool from the repo root:

```bash
dotnet tool restore
dotnet tool run versionbumper
```

2. Review the changed version files and verify that the version bump moved in the repository artifacts owned directly by `versionbumper`:
   - `version.nfo`
   - `version_definition.json`
   - versioned project metadata such as `src/SpecForge.Domain/SpecForge.Domain.csproj`
3. Treat `plugins/specforge-ai/.codex-plugin/plugin.json` as an explicit exception: it may remain unchanged after `dotnet tool run versionbumper` because that manifest is aligned later by the plugin marketplace sync.
4. Stage only files changed by the version bump.
5. Commit them separately with a message that includes `done` and the same GitHub issue short reference in the subject, for example:

```bash
git commit -m "done #123 bump version after <outcome>"
```

## Local Plugin Marketplace Sync

After the version bump commit succeeds:

1. If the change touched runtime, MCP, CLI, VS Code/webview, packaged plugin, skills, scripts, prompts, docs that ship inside the plugin, or versioned assemblies, rebuild the package artifacts before syncing:

```bash
npm run compile
rsync -a --delete --exclude='*.map' dist/ plugins/specforge-ai/mcp/dist/
rsync -a --delete --exclude='*.map' dist/mcp/ plugins/specforge-ai/mcp/
mkdir -p plugins/specforge-ai/mcp/dist/mcp
rsync -a --delete --exclude='*.map' dist/mcp/ plugins/specforge-ai/mcp/dist/mcp/
```

2. Always run the local marketplace sync script from the repo root after any functional task or subtask:

```bash
tools/sync-local-plugin-marketplace.sh
```

3. Verify that at least one consumer repo resolves `.agents/plugins/specforge-ai` to the central plugin and that the MCP still lists the expected tools when MCP behavior changed.
4. Verify that the sync aligned `plugins/specforge-ai/.codex-plugin/plugin.json` to the new runtime version when that manifest ships with the plugin.
5. Stage only generated package artifacts and plugin marketplace changes that belong to this task.
6. If package artifacts or repository files changed, commit them separately with a message that includes `done` and the same GitHub issue short reference in the subject, for example:

```bash
git commit -m "done #123 refresh plugin marketplace after <outcome>"
```

## Guardrails

- Do not run the version bump before the functional commit.
- Do not stop after only the functional commit when the task delivered a functional change.
- Do not create any commit in this flow if the GitHub issue number is still unknown.
- Do not leave the issue reference only in the body, PR title, branch name, or task description. It must be in every commit subject created for the task.
- Do not mix functional code/docs changes with version bump changes.
- Do not mix functional code/docs changes, version bump changes, and generated marketplace/package sync changes in the same commit.
- If the functional task is intentionally not committed, do not run the version bump.
- If `dotnet tool run versionbumper` fails, stop and report the failure instead of hand-editing version files.
- If the tool succeeds but `version.nfo` or `version_definition.json` do not reflect the new runtime version, stop and report the mismatch instead of pretending the bump is complete.
- If the later plugin sync does not align `plugins/specforge-ai/.codex-plugin/plugin.json` to the new runtime version, stop and report the mismatch instead of pretending the overall versioning flow is complete.
- If `tools/sync-local-plugin-marketplace.sh` fails, stop and report the failure instead of manually copying plugin files.
