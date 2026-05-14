import test from "node:test";
import assert from "node:assert/strict";
import { graphPath } from "../src-vscode/workflow-view/graphLayout";

test("graphPath builds rounded orthogonal links for horizontal flow", () => {
  const path = graphPath(
    "capture",
    "refinement",
    {
      capture: { left: 80, top: 120 },
      refinement: { left: 390, top: 340 }
    },
    240,
    "horizontal",
    { from: "R3", to: "T3" }
  );

  assert.match(path, /^M /);
  assert.match(path, / Q /);
  assert.doesNotMatch(path, / C /);
});

test("graphPath builds rounded orthogonal links for vertical flow", () => {
  const path = graphPath(
    "implementation",
    "review",
    {
      implementation: { left: 390, top: 600 },
      review: { left: 390, top: 780 }
    },
    240,
    "vertical",
    { from: "B3", to: "T3" }
  );

  assert.match(path, /^M /);
  assert.match(path, / L /);
  assert.doesNotMatch(path, / C /);
});
