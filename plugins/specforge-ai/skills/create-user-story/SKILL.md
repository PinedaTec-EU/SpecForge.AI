---
name: create-user-story
description: Create or import a SpecForge user story through MCP without touching .specs files directly.
---

# Create User Story

Use `specforge_action`.

## Intake Before Creation

If the user request is broad, vague, or describes a product goal instead of a concrete user story, do not create stories yet. Ask targeted clarification questions first.

Reach enough detail to identify:

- actor or user segment;
- business outcome and trigger;
- expected product behavior;
- relevant inputs, outputs, states, data, and integrations;
- scope boundaries and explicit exclusions;
- acceptance criteria, failure cases, and edge cases;
- priority and dependencies between slices.

Use 3-7 practical questions per round. Continue asking only while blocking ambiguity remains. Do not invent business-critical requirements just to proceed.

When the answers are concrete enough, split broad goals into small user stories. Each story should deliver one narrow functional increment, be independently reviewable, preserve traceability to the original goal, and avoid over-engineered scope.

For free text:

```json
{
  "workspaceRoot": "<absolute repo path>",
  "action": "create_user_story",
  "params": {
    "usId": "US-123",
    "title": "Short title",
    "kind": "feature",
    "category": "core",
    "sourceText": "User-story intent",
    "actor": "user"
  }
}
```

For a concrete broad goal that should become multiple small stories, use `action: "create_user_stories_from_goal"`:

```json
{
  "workspaceRoot": "<absolute repo path>",
  "action": "create_user_stories_from_goal",
  "params": {
    "goalText": "<original user goal plus relevant clarified answers>",
    "goalId": "GOAL-123",
    "strategy": "small-user-stories",
    "actor": "model-on-behalf-of-user",
    "stories": [
      {
        "title": "Narrow functional increment",
        "kind": "feature",
        "category": "core",
        "sourceText": "As a <actor>, I want <behavior> so that <outcome>. Include concrete scope, boundaries, and relevant context.",
        "acceptanceCriteria": [
          "Concrete observable criterion."
        ],
        "dependencies": [],
        "clarifiedAnswers": [
          "Relevant answer from the intake conversation."
        ],
        "nonGoals": [
          "Explicitly excluded scope."
        ],
        "mvpOutcome": "The customer-visible MVP outcome this slice enables.",
        "sliceRationale": "Why this is one small independent increment."
      }
    ]
  }
}
```

For an existing Markdown file, use `action: "import_user_story"` with `sourcePath`, `usId`, `title`, `kind`, and `category`.

Never create `.specs/us/**` folders or files by hand.
