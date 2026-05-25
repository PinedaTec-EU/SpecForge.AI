# Reference Docs

This directory keeps durable reference material that is still relevant to the product, but should not live in the root `doc/` navigation.

Use it for:

- canonical workflow semantics
- MCP and artifact contracts
- artifact schemas
- baseline templates that explain persisted workflow files

Current structure:

- `workflow-canonical.md`: baseline workflow semantics
- `mcp-contract.md`: baseline MCP contract
- `spec-schema.md`: required structure for `01-spec.md`
- `artifacts/`: persisted artifact formats such as `branch.yaml` and `timeline.md`
- `templates/`: baseline templates for workflow artifacts

The `baseline` wording preserves historical context from the original phase-1 design without keeping the old `*-fase-1` filenames in the main documentation root.
