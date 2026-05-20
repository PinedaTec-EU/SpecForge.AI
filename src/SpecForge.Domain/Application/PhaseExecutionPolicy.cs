using SpecForge.Domain.Workflow;

namespace SpecForge.Domain.Application;

public sealed record PhaseExecutionPolicy(
    string PhaseId,
    string PolicyKey,
    string Summary,
    PhaseExecutionRequirements Permissions,
    IReadOnlyCollection<PhaseExecutionToolPermission> AllowedTools,
    IReadOnlyCollection<PhaseExecutionPathPolicy> WritablePaths,
    IReadOnlyCollection<PhaseExecutionPathPolicy> ForbiddenPaths,
    IReadOnlyCollection<PhaseExecutionEvidenceRequirement> EvidenceRequirements,
    IReadOnlyCollection<PhaseExecutionEligibilityRule> EligibilityRules);

public sealed record PhaseExecutionToolPermission(
    string Tool,
    string Access,
    string Enforcement,
    string Reason);

public sealed record PhaseExecutionPathPolicy(
    string Path,
    string Access,
    string Actor,
    string Enforcement,
    string Reason);

public sealed record PhaseExecutionEvidenceRequirement(
    string Id,
    string Description,
    string Enforcement,
    string? PolicyInput = null);

public sealed record PhaseExecutionEligibilityRule(
    string Id,
    string Description,
    string Enforcement,
    string? BlockingReason = null,
    bool? IsCurrentlySatisfied = null,
    string? CurrentStatusMessage = null);

public static class PhaseExecutionPolicyCatalog
{
    private const string PolicyKey = "shared-phase-policy/v1";
    private const string EnforcementEnforced = "enforced";
    private const string EnforcementDeclared = "declared";

    public static PhaseExecutionPolicy Describe(
        PhaseId phaseId,
        PhaseExecutionReadiness? readiness = null,
        string reviewEvidencePolicy = "balanced")
    {
        var effectiveReadiness = readiness ?? new PhaseExecutionReadiness(
            phaseId,
            CanExecute: true,
            RequiredPermissions: PhaseExecutionPermissionCatalog.Describe(phaseId));
        var permissions = effectiveReadiness.RequiredPermissions ?? PhaseExecutionPermissionCatalog.Describe(phaseId);

        return new PhaseExecutionPolicy(
            PhaseId: WorkflowPresentation.ToPhaseSlug(phaseId),
            PolicyKey,
            Summary: BuildSummary(phaseId, permissions, reviewEvidencePolicy),
            Permissions: permissions,
            AllowedTools: BuildAllowedTools(permissions, effectiveReadiness),
            WritablePaths: BuildWritablePaths(phaseId, permissions),
            ForbiddenPaths: BuildForbiddenPaths(phaseId, permissions),
            EvidenceRequirements: BuildEvidenceRequirements(phaseId, reviewEvidencePolicy),
            EligibilityRules: BuildEligibilityRules(phaseId, permissions, effectiveReadiness, reviewEvidencePolicy));
    }

    private static string BuildSummary(
        PhaseId phaseId,
        PhaseExecutionRequirements permissions,
        string reviewEvidencePolicy)
    {
        var phaseSlug = WorkflowPresentation.ToPhaseSlug(phaseId);

        if (!permissions.ModelExecutionRequired)
        {
            return $"Phase `{phaseSlug}` is a non-model workflow boundary managed by SpecForge runtime state.";
        }

        if (phaseId == PhaseId.Review)
        {
            return $"Phase `{phaseSlug}` requires `{permissions.RepositoryAccess}` repository access and applies `{ReviewEvidencePolicy.Normalize(reviewEvidencePolicy)}` review evidence policy.";
        }

        return $"Phase `{phaseSlug}` requires `{permissions.RepositoryAccess}` repository access and {(permissions.WorkspaceWriteAccess ? "allows" : "does not allow")} repository writes by the assigned phase agent.";
    }

    private static IReadOnlyCollection<PhaseExecutionToolPermission> BuildAllowedTools(
        PhaseExecutionRequirements permissions,
        PhaseExecutionReadiness readiness)
    {
        var tools = new List<PhaseExecutionToolPermission>();

        if (permissions.ModelExecutionRequired)
        {
            tools.Add(new PhaseExecutionToolPermission(
                "model-execution",
                "execute",
                EnforcementEnforced,
                "The phase runs through a model-backed execution provider."));
        }

        if (permissions.RepositoryAccess is "read" or "read-write")
        {
            tools.Add(new PhaseExecutionToolPermission(
                "workspace-read",
                permissions.RepositoryAccess,
                EnforcementEnforced,
                "The assigned phase agent may inspect repository files needed for the phase."));
        }

        if (permissions.WorkspaceWriteAccess)
        {
            tools.Add(new PhaseExecutionToolPermission(
                "workspace-write",
                "write",
                EnforcementEnforced,
                "The phase can modify repository files when the assigned agent profile has write access."));
        }

        if (permissions.ModelExecutionRequired)
        {
            tools.Add(new PhaseExecutionToolPermission(
                "phase-artifact-persist",
                "write",
                EnforcementEnforced,
                "SpecForge persists the resulting phase artifact, receipts, and timeline metadata."));
        }

        if (readiness.AssignedModelSecurity?.NativeCliRequired == true)
        {
            tools.Add(new PhaseExecutionToolPermission(
                "native-cli-runner",
                "execute",
                EnforcementEnforced,
                "The selected phase agent requires an installed native CLI runner."));
        }

        return tools;
    }

    private static IReadOnlyCollection<PhaseExecutionPathPolicy> BuildWritablePaths(
        PhaseId phaseId,
        PhaseExecutionRequirements permissions)
    {
        var paths = new List<PhaseExecutionPathPolicy>
        {
            new(
                "<workspace-root>/.specs/us/**/phases/*",
                "write",
                "specforge-runtime",
                EnforcementEnforced,
                "Phase artifacts are persisted under the user-story phase directory."),
            new(
                "<workspace-root>/.specs/us/**/receipts/*.json",
                "write",
                "specforge-runtime",
                EnforcementEnforced,
                "Execution receipts are persisted for model-backed phase runs."),
            new(
                "<workspace-root>/.specs/us/**/timeline.md",
                "append",
                "specforge-runtime",
                EnforcementEnforced,
                "Timeline events are appended after execution and state transitions.")
        };

        if (!permissions.ModelExecutionRequired)
        {
            paths.Add(new PhaseExecutionPathPolicy(
                "<workspace-root>/.specs/us/**",
                "write",
                "specforge-runtime",
                EnforcementEnforced,
                "Capture materializes the initial workflow state and seed artifacts."));
            return paths;
        }

        if (permissions.WorkspaceWriteAccess)
        {
            paths.Add(new PhaseExecutionPathPolicy(
                "<workspace-root>/**",
                "write",
                "phase-agent",
                EnforcementDeclared,
                "The current phase contract allows repository mutation by the assigned agent profile."));
        }

        return paths;
    }

    private static IReadOnlyCollection<PhaseExecutionPathPolicy> BuildForbiddenPaths(
        PhaseId phaseId,
        PhaseExecutionRequirements permissions)
    {
        var paths = new List<PhaseExecutionPathPolicy>
        {
            new(
                "<workspace-root>/.git/**",
                "write",
                "phase-agent",
                EnforcementDeclared,
                "Phase agents must not mutate Git internals directly."),
            new(
                "<workspace-root>/.specs/us/**/state.yaml",
                "write",
                "phase-agent",
                EnforcementDeclared,
                "Workflow state is owned by SpecForge runtime transitions rather than the phase agent."),
            new(
                "<workspace-root>/.specs/us/**/runtime.yaml",
                "write",
                "phase-agent",
                EnforcementDeclared,
                "Runtime configuration must stay under SpecForge control."),
            new(
                "<workspace-root>/.specs/us/**/branch.yaml",
                "write",
                "phase-agent",
                EnforcementDeclared,
                "Branch and PR state are runtime-managed workflow facts.")
        };

        if (permissions.ModelExecutionRequired && !permissions.WorkspaceWriteAccess)
        {
            paths.Add(new PhaseExecutionPathPolicy(
                "<workspace-root>/**",
                "write",
                "phase-agent",
                EnforcementEnforced,
                $"Phase `{WorkflowPresentation.ToPhaseSlug(phaseId)}` runs with read-only repository access."));
        }

        return paths;
    }

    private static IReadOnlyCollection<PhaseExecutionEvidenceRequirement> BuildEvidenceRequirements(
        PhaseId phaseId,
        string reviewEvidencePolicy)
    {
        return phaseId switch
        {
            PhaseId.Capture =>
            [
                new PhaseExecutionEvidenceRequirement(
                    "capture_materialized",
                    "Initial user-story source, state, and timeline artifacts must be materialized.",
                    EnforcementEnforced)
            ],
            PhaseId.Implementation =>
            [
                new PhaseExecutionEvidenceRequirement(
                    "implementation_evidence_record",
                    "Implementation must persist evidence markdown/json describing touched files and validation performed.",
                    EnforcementEnforced),
                new PhaseExecutionEvidenceRequirement(
                    "graph_guided_scope_evidence",
                    "When semantic graph narrowing influenced the editable scope, implementation evidence should reference the resulting graph-backed file selection or fallback rationale.",
                    EnforcementDeclared),
                new PhaseExecutionEvidenceRequirement(
                    "artifact_iteration_log",
                    "Implementation iterations must preserve the operated artifact and operation log chain.",
                    EnforcementEnforced)
            ],
            PhaseId.TechnicalDesign =>
            [
                new PhaseExecutionEvidenceRequirement(
                    "design_receipt_evidence",
                    "Technical design must persist a receipt-backed evidence record that captures the design input chain, orchestration settings, and generated artifact.",
                    EnforcementEnforced),
                new PhaseExecutionEvidenceRequirement(
                    "refinement_graph_handoff",
                    "Technical design should consume the refinement graph-scope handoff when it exists so design exploration starts from declared scope anchors.",
                    EnforcementDeclared)
            ],
            PhaseId.Review =>
            [
                new PhaseExecutionEvidenceRequirement(
                    "implementation_evidence_input",
                    "Review consumes implementation evidence as a first-class input alongside the implementation artifact.",
                    EnforcementEnforced),
                new PhaseExecutionEvidenceRequirement(
                    "validation_strategy_evidence",
                    "Review must classify validation strategy items according to the active review evidence policy.",
                    EnforcementEnforced,
                    PolicyInput: ReviewEvidencePolicy.Normalize(reviewEvidencePolicy))
            ],
            PhaseId.ReleaseApproval =>
            [
                new PhaseExecutionEvidenceRequirement(
                    "release_evidence_bundle",
                    "Release approval must inspect review output, branch metadata, and workflow timeline context.",
                    EnforcementDeclared)
            ],
            _ =>
            [
                new PhaseExecutionEvidenceRequirement(
                    "phase_artifact_and_receipt",
                    "Model-backed phases must leave an artifact and an execution receipt for operator inspection.",
                    EnforcementEnforced)
            ]
        };
    }

    private static IReadOnlyCollection<PhaseExecutionEligibilityRule> BuildEligibilityRules(
        PhaseId phaseId,
        PhaseExecutionRequirements permissions,
        PhaseExecutionReadiness readiness,
        string reviewEvidencePolicy)
    {
        var rules = new List<PhaseExecutionEligibilityRule>();

        if (!permissions.ModelExecutionRequired)
        {
            rules.Add(new PhaseExecutionEligibilityRule(
                "entry_phase_no_model_required",
                "Capture is entered by workflow creation and does not require model execution.",
                EnforcementEnforced,
                IsCurrentlySatisfied: true,
                CurrentStatusMessage: "Capture is workflow-entry only."));
            return rules;
        }

        rules.Add(new PhaseExecutionEligibilityRule(
            "repository_access_matches_phase",
            $"Assigned agent repository access must satisfy `{permissions.RepositoryAccess}` for this phase.",
            EnforcementEnforced,
            BlockingReason: PhaseExecutionPermissionCatalog.ResolveRepositoryAccessBlockingReason(phaseId),
            IsCurrentlySatisfied: readiness.CanExecute || readiness.BlockingReason != PhaseExecutionPermissionCatalog.ResolveRepositoryAccessBlockingReason(phaseId),
            CurrentStatusMessage: readiness.ValidationMessage));

        if (readiness.AssignedModelSecurity?.NativeCliRequired == true)
        {
            rules.Add(new PhaseExecutionEligibilityRule(
                "native_cli_available",
                "The selected native CLI runner must be installed and available before execution starts.",
                EnforcementEnforced,
                BlockingReason: readiness.BlockingReason,
                IsCurrentlySatisfied: readiness.AssignedModelSecurity.NativeCliAvailable,
                CurrentStatusMessage: readiness.ValidationMessage));
        }

        if (phaseId == PhaseId.Review)
        {
            rules.Add(new PhaseExecutionEligibilityRule(
                "review_evidence_policy_selected",
                "Review execution must declare the active evidence policy so operators can interpret blocking evidence gaps.",
                EnforcementEnforced,
                IsCurrentlySatisfied: true,
                CurrentStatusMessage: $"Active review evidence policy: `{ReviewEvidencePolicy.Normalize(reviewEvidencePolicy)}`."));
        }

        if (phaseId == PhaseId.TechnicalDesign)
        {
            rules.Add(new PhaseExecutionEligibilityRule(
                "technical_design_subagent_mode_declared",
                "Technical design must expose whether specialist subagents are enabled so operators can interpret design synthesis depth and receipts consistently.",
                EnforcementDeclared,
                IsCurrentlySatisfied: readiness.PhaseSubagentsEnabled is not null,
                CurrentStatusMessage: readiness.PhaseSubagentsEnabled is null
                    ? "The active provider does not declare technical-design subagent mode."
                    : readiness.PhaseSubagentsEnabled.Value
                        ? "Technical-design subagents are enabled for the assigned provider profile."
                        : "Technical-design subagents are disabled for the assigned provider profile."));
            rules.Add(new PhaseExecutionEligibilityRule(
                "technical_design_quality_gate_visible",
                "Technical design must expose whether an explicit design gate is required or whether downstream review remains the active quality gate.",
                EnforcementDeclared,
                IsCurrentlySatisfied: true,
                CurrentStatusMessage: "No explicit technical-design approval gate is enforced yet; downstream review remains the active quality gate unless repository-specific gating is introduced."));
        }

        if (phaseId == PhaseId.Implementation)
        {
            rules.Add(new PhaseExecutionEligibilityRule(
                "implementation_write_scope_declared",
                "Implementation must expose writable scope and forbidden mutation zones so repository edits stay auditable.",
                EnforcementDeclared,
                IsCurrentlySatisfied: true,
                CurrentStatusMessage: "Writable scope and forbidden mutation zones are declared through the shared implementation policy contract."));
            rules.Add(new PhaseExecutionEligibilityRule(
                "implementation_review_loop_visible",
                "Implementation should declare that downstream review remains the active quality gate for correctness and validation evidence.",
                EnforcementDeclared,
                IsCurrentlySatisfied: true,
                CurrentStatusMessage: "Implementation remains review-gated; review is the authoritative downstream quality decision."));
        }

        return rules;
    }
}
