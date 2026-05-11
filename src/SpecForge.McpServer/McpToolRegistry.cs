using System.Text.Json.Nodes;

namespace SpecForge.McpServer;

public static class McpToolRegistry
{
    public static JsonObject BuildToolsList()
    {
        return new JsonObject
        {
            ["tools"] = new JsonArray
            {
                Tool("specforge_query", "Compact SpecForge read facade for Codex clients. Use this instead of granular read tools when possible.",
                    Schema(
                        required: ["workspaceRoot", "query"],
                        Props(
                            ("workspaceRoot", Prop("string", "Absolute path to the workspace root.")),
                            ("query",         EnumProp("Read operation.", "list_user_stories", "summary", "workflow", "current_phase", "runtime_status", "lineage", "files")),
                            ("usId",          Prop("string", "User story identifier. Required for all queries except list_user_stories."))))),

                Tool("specforge_action", "Compact SpecForge mutation facade for Codex clients. Use this instead of editing .specs files directly.",
                    Schema(
                        required: ["workspaceRoot", "action"],
                        Props(
                            ("workspaceRoot", Prop("string", "Absolute path to the workspace root.")),
                            ("action",        EnumProp("Mutation operation. Broad goals should be clarified into small MVP slices before create_user_stories_from_goal.", "create_user_story", "create_user_stories_from_goal", "import_user_story", "advance_phase", "approve_phase", "approve_review_anyway", "request_regression", "restart_from_source", "rewind_workflow", "reopen_completed", "reset_to_capture", "submit_refinement_answers", "submit_approval_answer", "suggest_approval_answer", "operate_artifact", "add_files", "set_file_kind", "repair_lineage")),
                            ("usId",          Prop("string", "User story identifier when the action targets an existing user story.")),
                            ("params",        Prop("object", "Action-specific parameters. Keep this small and use the SpecForge skill for the exact shape."))))),

                Tool("specforge_prompts", "Compact SpecForge prompt-template facade.",
                    Schema(
                        required: ["workspaceRoot", "operation"],
                        Props(
                            ("workspaceRoot", Prop("string", "Absolute path to the workspace root.")),
                            ("operation",     EnumProp("Prompt operation.", "initialize_repo_prompts", "export_prompt_template")),
                            ("promptPath",    Prop("string", "Template path for export_prompt_template.")),
                            ("overwrite",     Prop("boolean", "If true, overwrite existing prompt files. Defaults to false."))))),

                Tool("open_workflow_portal", "Open the native SpecForge workflow portal for a user story. Starts the packaged CLI portal when available, then opens the portal URL.",
                    Schema(
                        required: ["workspaceRoot", "usId"],
                        Props(
                            ("workspaceRoot", Prop("string", "Absolute path to the workspace root.")),
                            ("usId",          Prop("string", "User story identifier.")),
                            ("url",           Prop("string", "Optional portal base URL. Defaults to SPECFORGE_WORKFLOW_PORTAL_URL or http://localhost:5128/.")),
                            ("startPortal",   Prop("boolean", "Whether to start the packaged workflow portal process. Defaults to true.")),
                            ("openBrowser",   Prop("boolean", "Whether to open the portal URL in the system browser. Defaults to true."))))),

                Tool("create_us_from_chat", "Create a user story from free text.",
                    Schema(
                        required: ["workspaceRoot", "usId", "title", "kind", "category", "sourceText"],
                        Props(
                            ("workspaceRoot", Prop("string", "Absolute path to the workspace root (folder containing .specs/).")),
                            ("usId",          Prop("string", "User story identifier, e.g. US-001.")),
                            ("title",         Prop("string", "Short descriptive title for the user story.")),
                            ("kind",          EnumProp("User story kind.", "feature", "bug", "hotfix")),
                            ("category",      Prop("string", "Category that groups the user story, e.g. core, ux, api.")),
                            ("sourceText",    Prop("string", "Free-text description of the user story intent. Vague stories are allowed at capture, but refinement will keep asking until the MVP slice is buildable.")),
                            ("actor",         Prop("string", "Actor performing the action. Defaults to 'user'."))))),

                Tool("import_us_from_markdown", "Import a user story from an existing markdown file.",
                    Schema(
                        required: ["workspaceRoot", "usId", "sourcePath", "title", "kind", "category"],
                        Props(
                            ("workspaceRoot", Prop("string", "Absolute path to the workspace root.")),
                            ("usId",          Prop("string", "User story identifier, e.g. US-001.")),
                            ("sourcePath",    Prop("string", "Absolute path to the source markdown file to import.")),
                            ("title",         Prop("string", "Short descriptive title for the user story.")),
                            ("kind",          EnumProp("User story kind.", "feature", "bug", "hotfix")),
                            ("category",      Prop("string", "Category that groups the user story.")),
                            ("actor",         Prop("string", "Actor performing the action. Defaults to 'user'."))))),

                Tool("initialize_repo_prompts", "Export the repo prompt templates into .specs/prompts/ and SpecForge agent instructions into .specs/.",
                    Schema(
                        required: ["workspaceRoot"],
                        Props(
                            ("workspaceRoot", Prop("string", "Absolute path to the workspace root.")),
                            ("overwrite",     Prop("boolean", "If true, overwrite existing prompt files. Defaults to false."))))),

                Tool("export_prompt_template", "Export one embedded prompt template into .specs/prompts/ so it can be customized.",
                    Schema(
                        required: ["workspaceRoot", "promptPath"],
                        Props(
                            ("workspaceRoot", Prop("string", "Absolute path to the workspace root.")),
                            ("promptPath",    Prop("string", "Known prompt template path, absolute or workspace-relative.")),
                            ("overwrite",     Prop("boolean", "If true, overwrite an existing prompt override. Defaults to false."))))),

                Tool("list_user_stories", "List all user stories persisted in the workspace.",
                    Schema(required: ["workspaceRoot"], Props(("workspaceRoot", Prop("string", "Absolute path to the workspace root."))))),
                Tool("get_user_story_summary", "Get the operational summary of a user story.",
                    Schema(required: ["workspaceRoot", "usId"], Props(("workspaceRoot", Prop("string", "Absolute path to the workspace root.")), ("usId", Prop("string", "User story identifier."))))),
                Tool("get_user_story_workflow", "Get workflow phases, controls, refinement session, and audit trail for a user story.",
                    Schema(required: ["workspaceRoot", "usId"], Props(("workspaceRoot", Prop("string", "Absolute path to the workspace root.")), ("usId", Prop("string", "User story identifier."))))),
                Tool("get_current_phase", "Get the current phase and whether it can advance for a user story.",
                    Schema(required: ["workspaceRoot", "usId"], Props(("workspaceRoot", Prop("string", "Absolute path to the workspace root.")), ("usId", Prop("string", "User story identifier."))))),
                Tool("get_user_story_runtime_status", "Get the persisted runtime status, including whether a phase generation is still running.",
                    Schema(required: ["workspaceRoot", "usId"], Props(("workspaceRoot", Prop("string", "Absolute path to the workspace root.")), ("usId", Prop("string", "User story identifier."))))),
                Tool("analyze_user_story_lineage", "Analyze a user story timeline and artifacts for workflow lineage inconsistencies.",
                    Schema(required: ["workspaceRoot", "usId"], Props(("workspaceRoot", Prop("string", "Absolute path to the workspace root.")), ("usId", Prop("string", "User story identifier."))))),
                Tool("repair_user_story_lineage", "Repair lineage inconsistencies by archiving deprecated artifacts and returning the user story to the recommended phase.",
                    Schema(required: ["workspaceRoot", "usId"], Props(("workspaceRoot", Prop("string", "Absolute path to the workspace root.")), ("usId", Prop("string", "User story identifier.")), ("actor", Prop("string", "Actor requesting the repair. Defaults to 'user'."))))),
                Tool("generate_next_phase", "Advance to the next linear phase and generate its artifact via the configured AI provider.",
                    Schema(required: ["workspaceRoot", "usId"], Props(("workspaceRoot", Prop("string", "Absolute path to the workspace root.")), ("usId", Prop("string", "User story identifier.")), ("actor", Prop("string", "Actor requesting the phase execution. Defaults to 'user'."))))),
                Tool("approve_phase", "Approve the current phase. Creates the work branch when reaching the branch-creation phase.",
                    Schema(required: ["workspaceRoot", "usId"], Props(("workspaceRoot", Prop("string", "Absolute path to the workspace root.")), ("usId", Prop("string", "User story identifier.")), ("baseBranch", Prop("string", "Base branch name for the work branch. Optional.")), ("workBranch", Prop("string", "Override for the work branch name. Optional.")), ("actor", Prop("string", "Actor performing the approval. Defaults to 'user'."))))),
                Tool("approve_review_anyway", "Force the workflow to leave review and enter release approval by explicit human decision.",
                    Schema(required: ["workspaceRoot", "usId", "reason"], Props(("workspaceRoot", Prop("string", "Absolute path to the workspace root.")), ("usId", Prop("string", "User story identifier.")), ("reason", Prop("string", "Audit reason for overriding the review gate.")), ("actor", Prop("string", "Actor performing the override. Defaults to 'user'."))))),
                Tool("request_regression", "Regress a user story to an earlier valid phase.",
                    Schema(required: ["workspaceRoot", "usId", "targetPhase"], Props(("workspaceRoot", Prop("string", "Absolute path to the workspace root.")), ("usId", Prop("string", "User story identifier.")), ("targetPhase", PhaseSlugProp("Phase slug to regress to.")), ("reason", Prop("string", "Optional reason for the regression.")), ("destructive", Prop("boolean", "Whether to delete later derived artifacts while regressing. Defaults to false.")), ("actor", Prop("string", "Actor requesting the regression. Defaults to 'user'."))))),
                Tool("restart_user_story_from_source", "Restart the workflow after the source user story file has been modified.",
                    Schema(required: ["workspaceRoot", "usId"], Props(("workspaceRoot", Prop("string", "Absolute path to the workspace root.")), ("usId", Prop("string", "User story identifier.")), ("reason", Prop("string", "Optional reason for the restart.")), ("actor", Prop("string", "Actor requesting the restart. Defaults to 'user'."))))),
                Tool("rewind_workflow", "Rewind a workflow to an earlier executed phase. Destructive cleanup is optional and disabled by default.",
                    Schema(required: ["workspaceRoot", "usId", "targetPhase"], Props(("workspaceRoot", Prop("string", "Absolute path to the workspace root.")), ("usId", Prop("string", "User story identifier.")), ("targetPhase", PhaseSlugProp("Phase slug to rewind to.")), ("destructive", Prop("boolean", "Whether to delete later derived artifacts while rewinding. Defaults to false.")), ("actor", Prop("string", "Actor requesting the rewind. Defaults to 'user'."))))),
                Tool("reopen_completed_workflow", "Reopen a completed workflow into a controlled earlier phase with an explicit typed reason and human note.",
                    Schema(required: ["workspaceRoot", "usId", "reasonKind", "description"], Props(("workspaceRoot", Prop("string", "Absolute path to the workspace root.")), ("usId", Prop("string", "User story identifier.")), ("reasonKind", EnumProp("Typed reopen reason.", "merge-conflict", "defect", "functional-issue", "technical-issue")), ("description", Prop("string", "Human explanation for what failed or what must be incorporated now.")), ("actor", Prop("string", "Actor requesting the reopen. Defaults to 'user'."))))),
                Tool("reset_user_story_to_capture", "Reset a user story to the capture phase and delete all generated derived artifacts.",
                    Schema(required: ["workspaceRoot", "usId"], Props(("workspaceRoot", Prop("string", "Absolute path to the workspace root.")), ("usId", Prop("string", "User story identifier."))))),
                Tool("submit_refinement_answers", "Store refinement answers so the refinement phase can re-run with the new context.",
                    Schema(required: ["workspaceRoot", "usId", "answers"], Props(("workspaceRoot", Prop("string", "Absolute path to the workspace root.")), ("usId", Prop("string", "User story identifier.")), ("answers", ArrayProp("string", "Ordered list of answers matching the refinement questions.")), ("actor", Prop("string", "Actor submitting the answers. Defaults to 'user'."))))),
                Tool("submit_approval_answer", "Persist a human approval answer into the current spec artifact without invoking the model.",
                    Schema(required: ["workspaceRoot", "usId", "question", "answer"], Props(("workspaceRoot", Prop("string", "Absolute path to the workspace root.")), ("usId", Prop("string", "User story identifier.")), ("question", Prop("string", "Approval question being answered.")), ("answer", Prop("string", "Human answer to persist into the spec artifact.")), ("actor", Prop("string", "Actor submitting the answer. Defaults to 'user'."))))),
                Tool("suggest_approval_answer", "Ask the configured spec model to draft an answer for one approval question without applying it.",
                    Schema(required: ["workspaceRoot", "usId", "question"], Props(("workspaceRoot", Prop("string", "Absolute path to the workspace root.")), ("usId", Prop("string", "User story identifier.")), ("question", Prop("string", "Approval question to answer from the current spec context.")), ("actor", Prop("string", "Actor requesting the suggestion. Defaults to 'user'."))))),
                Tool("operate_current_phase_artifact", "Apply a model-assisted operation over the current phase artifact and persist the trace.",
                    Schema(required: ["workspaceRoot", "usId", "prompt"], Props(("workspaceRoot", Prop("string", "Absolute path to the workspace root.")), ("usId", Prop("string", "User story identifier.")), ("prompt", Prop("string", "Instruction describing what to change or verify in the current artifact.")), ("includeReviewArtifactInContext", Prop("boolean", "Whether implementation operations may include the generated review artifact as previous context. Defaults to true.")), ("actor", Prop("string", "Actor requesting the operation. Defaults to 'user'."))))),
                Tool("list_user_story_files", "List context files and user-story info files for a user story.",
                    Schema(required: ["workspaceRoot", "usId"], Props(("workspaceRoot", Prop("string", "Absolute path to the workspace root.")), ("usId", Prop("string", "User story identifier."))))),
                Tool("add_user_story_files", "Copy external files into a user story as context or user-story info.",
                    Schema(required: ["workspaceRoot", "usId", "sourcePaths", "kind"], Props(("workspaceRoot", Prop("string", "Absolute path to the workspace root.")), ("usId", Prop("string", "User story identifier.")), ("sourcePaths", ArrayProp("string", "Absolute paths of the files to copy into the user story.")), ("kind", EnumProp("File kind.", "context", "attachment"))))),
                Tool("set_user_story_file_kind", "Move an existing user-story file between context and user-story info.",
                    Schema(required: ["workspaceRoot", "usId", "filePath", "kind"], Props(("workspaceRoot", Prop("string", "Absolute path to the workspace root.")), ("usId", Prop("string", "User story identifier.")), ("filePath", Prop("string", "Absolute path of the file to reclassify.")), ("kind", EnumProp("Target file kind.", "context", "attachment")))))
            }
        };
    }

    private static JsonObject Tool(string name, string description, JsonObject inputSchema) =>
        new()
        {
            ["name"] = name,
            ["description"] = description,
            ["inputSchema"] = inputSchema
        };

    private static JsonObject Schema(string[] required, JsonObject properties)
    {
        var req = new JsonArray();
        foreach (var r in required)
        {
            req.Add((JsonNode)r);
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = req
        };
    }

    private static JsonObject Props(params (string name, JsonObject schema)[] entries)
    {
        var obj = new JsonObject();
        foreach (var (name, schema) in entries)
        {
            obj[name] = schema;
        }

        return obj;
    }

    private static JsonObject Prop(string type, string description) =>
        new() { ["type"] = type, ["description"] = description };

    private static JsonObject EnumProp(string description, params string[] values)
    {
        var enumValues = new JsonArray();
        foreach (var value in values)
        {
            enumValues.Add((JsonNode)value);
        }

        return new JsonObject
        {
            ["type"] = "string",
            ["description"] = description,
            ["enum"] = enumValues
        };
    }

    private static JsonObject PhaseSlugProp(string description) =>
        EnumProp(
            description,
            "capture",
            "refinement",
            "spec",
            "technical-design",
            "implementation",
            "review",
            "release-approval",
            "pr-preparation");

    private static JsonObject ArrayProp(string itemType, string description) =>
        new()
        {
            ["type"] = "array",
            ["items"] = new JsonObject { ["type"] = itemType },
            ["description"] = description
        };
}
