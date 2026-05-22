import test from "node:test";
import assert from "node:assert/strict";
import * as fs from "node:fs";
import * as os from "node:os";
import * as path from "node:path";
import {
  getUserWorkspacePreferencesPath,
  readUserWorkspacePreferences,
  setHiddenUserStoryIds,
  setMaxVisibleUserStories,
  setPausedWorkflowPhaseIds,
  setSearchIncludesOtherOwners,
  setStarredUserStory
} from "../src-vscode/userWorkspacePreferences";

test("user workspace preferences persist a starred user story per local user", async () => {
  const workspaceRoot = await fs.promises.mkdtemp(path.join(os.tmpdir(), "specforge-prefs-"));

  await setStarredUserStory(workspaceRoot, "US-0042");
  const preferences = await readUserWorkspacePreferences(workspaceRoot);

  assert.equal(preferences.starredUserStoryId, "US-0042");
  assert.match(getUserWorkspacePreferencesPath(workspaceRoot), /\.specs\/users\/.+\/vscode-preferences\.json$/);
});

test("user workspace preferences clear the starred user story when unset", async () => {
  const workspaceRoot = await fs.promises.mkdtemp(path.join(os.tmpdir(), "specforge-prefs-"));

  await setStarredUserStory(workspaceRoot, "US-0007");
  await setStarredUserStory(workspaceRoot, null);
  const preferences = await readUserWorkspacePreferences(workspaceRoot);

  assert.equal(preferences.starredUserStoryId, null);
});

test("user workspace preferences persist paused workflow phase ids per user story", async () => {
  const workspaceRoot = await fs.promises.mkdtemp(path.join(os.tmpdir(), "specforge-prefs-"));

  await setPausedWorkflowPhaseIds(workspaceRoot, "US-0042", ["implementation", "review", "implementation"]);
  const preferences = await readUserWorkspacePreferences(workspaceRoot);

  assert.deepEqual(preferences.pausedWorkflowPhaseIdsByUsId["US-0042"], ["implementation", "review"]);
});

test("user workspace preferences persist hidden stories, search scope, and max visible stories", async () => {
  const workspaceRoot = await fs.promises.mkdtemp(path.join(os.tmpdir(), "specforge-prefs-"));

  await setHiddenUserStoryIds(workspaceRoot, ["us-0001", "US-0002", "us-0001"]);
  await setSearchIncludesOtherOwners(workspaceRoot, true);
  await setMaxVisibleUserStories(workspaceRoot, 25);
  const preferences = await readUserWorkspacePreferences(workspaceRoot);

  assert.deepEqual(preferences.hiddenUserStoryIds, ["US-0001", "US-0002"]);
  assert.equal(preferences.searchIncludesOtherOwners, true);
  assert.equal(preferences.maxVisibleUserStories, 25);
});

test("user workspace preferences clear invalid max visible values", async () => {
  const workspaceRoot = await fs.promises.mkdtemp(path.join(os.tmpdir(), "specforge-prefs-"));

  await setMaxVisibleUserStories(workspaceRoot, 0);
  const preferences = await readUserWorkspacePreferences(workspaceRoot);

  assert.equal(preferences.maxVisibleUserStories, null);
});
