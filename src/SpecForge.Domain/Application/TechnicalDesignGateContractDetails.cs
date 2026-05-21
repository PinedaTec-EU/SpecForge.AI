using SpecForge.Domain.Persistence;
using SpecForge.Domain.Workflow;

namespace SpecForge.Domain.Application;

public sealed record TechnicalDesignGateContractDetails(
    string Status,
    string GateMode,
    bool ApprovalRequiredNow,
    bool ApprovalReadyNow,
    string? ApprovalBlockingReason,
    bool HasTechnicalDesignArtifact,
    bool HasStructuredTechnicalDesignArtifact,
    bool HasValidationStrategy,
    bool HasEvidenceRecord,
    bool HasContextPack,
    bool GraphIntentDeclared,
    IReadOnlyCollection<TechnicalDesignGateRule> GateRules);

public sealed record TechnicalDesignGateRule(
    string Id,
    string Description,
    string Status,
    string Enforcement,
    bool IsCurrentlySatisfied,
    string? BlockingReason = null,
    string? CurrentStatusMessage = null);

public sealed record TechnicalDesignGateSnapshot(
    string PhaseId,
    string PolicyKey,
    string Summary,
    bool ExecutionAllowed,
    string? ExecutionBlockingReason,
    PhaseExecutionRequirements Permissions,
    IReadOnlyCollection<PhaseExecutionEvidenceRequirement> EvidenceRequirements,
    IReadOnlyCollection<PhaseExecutionEligibilityRule> EligibilityRules,
    string GateMode,
    bool ApprovalRequiredNow,
    bool ApprovalReadyNow,
    string? ApprovalBlockingReason,
    bool HasTechnicalDesignArtifact,
    bool HasStructuredTechnicalDesignArtifact,
    bool HasValidationStrategy,
    bool HasEvidenceRecord,
    bool HasContextPack,
    bool GraphIntentDeclared,
    IReadOnlyCollection<TechnicalDesignGateRule> GateRules);

public static class TechnicalDesignGateContractBuilder
{
    public static TechnicalDesignGateContractDetails Build(
        UserStoryFilePaths paths,
        PhaseExecutionEvidenceRecord? evidenceRecord,
        TechnicalDesignContextPack? contextPack,
        bool approvalRequiredNow = false)
    {
        var artifactPath = paths.GetLatestExistingPhaseArtifactPath(PhaseId.TechnicalDesign);
        var structuredArtifactPath = paths.GetLatestExistingPhaseArtifactJsonPath(PhaseId.TechnicalDesign);
        var validationStrategy = WorkflowRunner.ReadTechnicalDesignValidationStrategy(paths);
        var hasArtifact = !string.IsNullOrWhiteSpace(artifactPath) && File.Exists(artifactPath);
        var hasStructuredArtifact = !string.IsNullOrWhiteSpace(structuredArtifactPath) && File.Exists(structuredArtifactPath);
        var hasValidationStrategy = validationStrategy.Count > 0;
        var hasEvidenceRecord = evidenceRecord is not null;
        var hasContextPack = contextPack is not null;
        var graphIntentDeclared = contextPack?.GraphScopeRequest is not null
            || contextPack?.GraphBackedExpansions.Count > 0
            || contextPack?.GraphQueryEvidence.Count > 0;

        var gateRules = BuildGateRules(
            hasArtifact,
            hasStructuredArtifact,
            hasValidationStrategy,
            hasEvidenceRecord,
            hasContextPack,
            graphIntentDeclared);
        var blockingRule = gateRules
            .FirstOrDefault(static rule =>
                string.Equals(rule.Enforcement, "enforced", StringComparison.OrdinalIgnoreCase) &&
                !rule.IsCurrentlySatisfied);
        var approvalBlockingReason = blockingRule
            ?.BlockingReason;
        var approvalReadyNow = approvalBlockingReason is null;
        var hasDeclaredAttention = gateRules.Any(static rule =>
            string.Equals(rule.Enforcement, "declared", StringComparison.OrdinalIgnoreCase) &&
            !rule.IsCurrentlySatisfied);
        var status = !approvalReadyNow
            ? "blocking"
            : hasDeclaredAttention
                ? "attention"
                : "ready";

        return new TechnicalDesignGateContractDetails(
            Status: status,
            GateMode: approvalRequiredNow ? "required-pre-implementation-approval" : "reusable-pre-implementation-approval",
            ApprovalRequiredNow: approvalRequiredNow,
            ApprovalReadyNow: approvalReadyNow,
            ApprovalBlockingReason: approvalBlockingReason,
            HasTechnicalDesignArtifact: hasArtifact,
            HasStructuredTechnicalDesignArtifact: hasStructuredArtifact,
            HasValidationStrategy: hasValidationStrategy,
            HasEvidenceRecord: hasEvidenceRecord,
            HasContextPack: hasContextPack,
            GraphIntentDeclared: graphIntentDeclared,
            GateRules: gateRules);
    }

    private static IReadOnlyCollection<TechnicalDesignGateRule> BuildGateRules(
        bool hasArtifact,
        bool hasStructuredArtifact,
        bool hasValidationStrategy,
        bool hasEvidenceRecord,
        bool hasContextPack,
        bool graphIntentDeclared)
    {
        return
        [
            new(
                "technical_design_artifact_available",
                "A technical-design artifact must exist before a repository can apply an explicit pre-implementation design gate.",
                hasArtifact ? "satisfied" : "blocking",
                "enforced",
                hasArtifact,
                BlockingReason: hasArtifact ? null : "technical_design_artifact_missing",
                CurrentStatusMessage: hasArtifact
                    ? "A technical-design artifact is available."
                    : "No technical-design artifact is available yet."),
            new(
                "technical_design_structured_artifact_available",
                "The gate contract should rely on the structured technical-design artifact so repositories can reason about validation strategy deterministically.",
                hasStructuredArtifact ? "satisfied" : "attention",
                "declared",
                hasStructuredArtifact,
                BlockingReason: hasStructuredArtifact ? null : "technical_design_structured_artifact_missing",
                CurrentStatusMessage: hasStructuredArtifact
                    ? "A structured technical-design artifact is available."
                    : "No structured technical-design artifact is available yet."),
            new(
                "technical_design_validation_strategy_declared",
                "A reusable design gate requires a declared validation strategy that implementation and review can later verify.",
                hasValidationStrategy ? "satisfied" : "blocking",
                "enforced",
                hasValidationStrategy,
                BlockingReason: hasValidationStrategy ? null : "technical_design_validation_strategy_missing",
                CurrentStatusMessage: hasValidationStrategy
                    ? "The technical-design artifact declares at least one validation strategy item."
                    : "The technical-design artifact does not declare validation strategy items yet."),
            new(
                "technical_design_evidence_record_available",
                "A reusable design gate should persist a receipt-linked evidence record for auditability and downstream explanation.",
                hasEvidenceRecord ? "satisfied" : "attention",
                "declared",
                hasEvidenceRecord,
                BlockingReason: hasEvidenceRecord ? null : "technical_design_evidence_record_missing",
                CurrentStatusMessage: hasEvidenceRecord
                    ? "A receipt-linked evidence record was captured for the latest technical-design execution."
                    : "No receipt-linked evidence record is available for the latest technical-design execution."),
            new(
                "technical_design_context_pack_available",
                "A reusable design gate should capture the context pack that shaped the design scope so reviewers can trace why files and graph evidence were included.",
                hasContextPack ? "satisfied" : "attention",
                "declared",
                hasContextPack,
                BlockingReason: hasContextPack ? null : "technical_design_context_pack_missing",
                CurrentStatusMessage: hasContextPack
                    ? "A design context pack was captured for the latest technical-design execution."
                    : "No design context pack was captured for the latest technical-design execution."),
            new(
                "technical_design_graph_intent_declared",
                "When semantic graph support exists, the design gate should declare whether graph scope or bounded graph evidence influenced the design narrowing.",
                graphIntentDeclared ? "satisfied" : "declared",
                "declared",
                graphIntentDeclared,
                BlockingReason: null,
                CurrentStatusMessage: graphIntentDeclared
                    ? "Graph scope or bounded graph evidence influenced the design narrowing."
                    : "No graph intent was recorded; repositories may still use the gate, but graph provenance will be absent.")
        ];
    }
}

public static class TechnicalDesignGateSnapshotBuilder
{
    public static TechnicalDesignGateSnapshot Build(
        PhaseExecutionReadiness readiness,
        PhaseExecutionPolicy policy,
        TechnicalDesignGateContractDetails gateContract) =>
        new(
            PhaseId: policy.PhaseId,
            PolicyKey: policy.PolicyKey,
            Summary: policy.Summary,
            ExecutionAllowed: readiness.CanExecute,
            ExecutionBlockingReason: readiness.BlockingReason,
            Permissions: policy.Permissions,
            EvidenceRequirements: policy.EvidenceRequirements,
            EligibilityRules: policy.EligibilityRules,
            GateMode: gateContract.GateMode,
            ApprovalRequiredNow: gateContract.ApprovalRequiredNow,
            ApprovalReadyNow: gateContract.ApprovalReadyNow,
            ApprovalBlockingReason: gateContract.ApprovalBlockingReason,
            HasTechnicalDesignArtifact: gateContract.HasTechnicalDesignArtifact,
            HasStructuredTechnicalDesignArtifact: gateContract.HasStructuredTechnicalDesignArtifact,
            HasValidationStrategy: gateContract.HasValidationStrategy,
            HasEvidenceRecord: gateContract.HasEvidenceRecord,
            HasContextPack: gateContract.HasContextPack,
            GraphIntentDeclared: gateContract.GraphIntentDeclared,
            GateRules: gateContract.GateRules);
}
