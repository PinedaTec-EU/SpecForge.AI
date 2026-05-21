using System.Text.Json;
using SpecForge.Domain.Application;
using SpecForge.Domain.Workflow;

namespace SpecForge.Domain.Tests;

public sealed class PhaseExecutionInspectionTests : IDisposable
{
    private readonly string workspaceRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task BuildEffectiveContext_CapturesNormalizedPathsAndHashes()
    {
        Directory.CreateDirectory(workspaceRoot);
        var userStoryPath = Path.Combine(workspaceRoot, ".specs", "us", "US-0001", "us.md");
        var previousArtifactPath = Path.Combine(workspaceRoot, ".specs", "us", "US-0001", "phases", "01-spec.md");
        var contextFilePath = Path.Combine(workspaceRoot, "context", "architecture.md");
        var currentArtifactPath = Path.Combine(workspaceRoot, ".specs", "us", "US-0001", "phases", "02-technical-design.md");

        Directory.CreateDirectory(Path.GetDirectoryName(userStoryPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(previousArtifactPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(contextFilePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(currentArtifactPath)!);
        await File.WriteAllTextAsync(userStoryPath, "# US");
        await File.WriteAllTextAsync(previousArtifactPath, "# Spec");
        await File.WriteAllTextAsync(contextFilePath, "# Context");
        await File.WriteAllTextAsync(currentArtifactPath, "# Technical Design");

        var context = new PhaseExecutionContext(
            WorkspaceRoot: workspaceRoot,
            UsId: "US-0001",
            PhaseId: PhaseId.TechnicalDesign,
            UserStoryPath: userStoryPath,
            PreviousArtifactPaths: new Dictionary<PhaseId, string>
            {
                [PhaseId.Spec] = previousArtifactPath
            },
            ContextFilePaths: [contextFilePath],
            CurrentArtifactPath: currentArtifactPath,
            OperationPrompt: "Tighten the design constraints.");

        var effectiveContext = PhaseExecutionInspectionBuilder.BuildEffectiveContext(workspaceRoot, context);

        Assert.Equal(PhaseExecutionReceiptStore.NormalizePath(workspaceRoot), effectiveContext.WorkspaceRoot);
        Assert.Equal(PhaseExecutionReceiptStore.NormalizePath(userStoryPath), effectiveContext.UserStoryPath);
        Assert.Equal(
            PhaseExecutionReceiptStore.ComputeSha256("Tighten the design constraints."),
            effectiveContext.OperationPromptSha256);

        var previousArtifact = Assert.Single(effectiveContext.PreviousArtifacts);
        Assert.Equal("spec", previousArtifact.PhaseId);
        Assert.Equal(PhaseExecutionReceiptStore.NormalizePath(previousArtifactPath), previousArtifact.Path);
        Assert.NotNull(previousArtifact.Sha256);

        var contextFile = Assert.Single(effectiveContext.ContextFiles);
        Assert.Equal(PhaseExecutionReceiptStore.NormalizePath(contextFilePath), contextFile.Path);
        Assert.NotNull(contextFile.Sha256);

        Assert.NotNull(effectiveContext.CurrentArtifact);
        Assert.Equal("technical-design", effectiveContext.CurrentArtifact!.PhaseId);
        Assert.Equal(PhaseExecutionReceiptStore.NormalizePath(currentArtifactPath), effectiveContext.CurrentArtifact.Path);
    }

    [Fact]
    public void EffectivePrompt_SerializesSourcePromptsAsPartOfSharedContract()
    {
        var prompt = new PhaseExecutionEffectivePrompt(
            SystemPrompt: "system",
            UserPrompt: "user",
            Warnings: ["override drift"],
            SourcePrompts:
            [
                new PhaseExecutionPromptSource(
                    "phase-task",
                    "/repo/.specs/prompts/phases/spec.execute.md",
                    IsOverride: true,
                    ContentSha256: "abc",
                    EmbeddedContentSha256: "def")
            ]);

        var json = JsonSerializer.Serialize(prompt, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"systemPrompt\":\"system\"", json);
        Assert.Contains("\"userPrompt\":\"user\"", json);
        Assert.Contains("\"warnings\":[\"override drift\"]", json);
        Assert.Contains("\"sourcePrompts\":[", json);
        Assert.Contains("\"role\":\"phase-task\"", json);
        Assert.Contains("\"isOverride\":true", json);
        Assert.Contains("\"contentSha256\":\"abc\"", json);
        Assert.Contains("\"embeddedContentSha256\":\"def\"", json);
    }

    [Fact]
    public void Receipt_SerializesEffectivePromptAndEffectiveContext_WhenPresent()
    {
        var receipt = new PhaseExecutionReceipt(
            ExecutionId: "execution-1",
            UsId: "US-0001",
            PhaseId: "spec",
            StartedAtUtc: "2026-05-19T10:00:00.0000000+00:00",
            CompletedAtUtc: "2026-05-19T10:00:01.0000000+00:00",
            InputManifest: new PhaseExecutionInputManifest(
                "manifest-hash",
                "/repo",
                "/repo/.specs/us/US-0001/us.md",
                "us-hash",
                "git-head",
                [],
                [],
                null,
                null),
            OutputManifest: new PhaseExecutionOutputManifest(
                "/repo/.specs/us/US-0001/phases/01-spec.md",
                "artifact-hash",
                []),
            Usage: new TokenUsage(10, 20, 30),
            Execution: new PhaseExecutionMetadata("openai-compatible", "test-model"),
            EvidenceRecord: new PhaseExecutionEvidenceRecord(
                new PhaseExecutionEvidenceActor("phase-agent", ProviderKind: "openai-compatible", Model: "test-model"),
                [new PhaseExecutionEvidenceReference("user-story", "/repo/.specs/us/US-0001/us.md", "us-hash")],
                [new PhaseExecutionEvidenceReference("result-artifact", "/repo/.specs/us/US-0001/phases/01-spec.md", "artifact-hash")],
                [new PhaseExecutionEvidenceSetting("policy-key", "shared-phase-policy/v1")],
                [new PhaseExecutionEvidenceTool("model:openai-compatible", "execute", "execution-metadata")],
                null,
                new PhaseExecutionValidationSummary("captured", "Execution persisted evidence links.", ["result-artifact-generated"]),
                [new PhaseExecutionEvidenceLink("receipt", "/repo/.specs/us/US-0001/execution-receipts/execution-1.json", "receipt")]),
            ExecutionEnvelope: new PhaseExecutionEnvelope(
                "spec",
                "shared-execution-envelope/v1",
                "managed-provider",
                "provider-managed",
                [new PhaseExecutionEnvelopeToolPermission("phase-agent", "model-execution", "execute", "enforced")],
                [new PhaseExecutionEnvelopeWriteScope("specforge-runtime", "<workspace-root>/.specs/us/**/phases/*", "write", "enforced")],
                [new PhaseExecutionEnvelopeBoundary("workspace-root", "<workspace-root>", "scoped", "Execution is scoped to the current workspace root.")],
                new PhaseExecutionEnvelopeBudget("standard", "medium", "standard", "artifact-only", "Declared budget only.")),
            AutoRefinementAnswerAttempt: new AutoRefinementAnswerAttemptRecord(
                "answered",
                "Automatic refinement answering recorded 2 answer(s) before retrying spec readiness.",
                "Grounded retry succeeded.",
                2),
            RefinementPolicySnapshot: new RefinementPolicyDetails(
                "strict",
                2,
                2,
                [new RefinementBlockingCondition("unanswered_questions_require_resolution", "Questions remain unanswered.", "blocking", true, "refinement_pending_answers")],
                new RefinementAutoAnswerPolicy(
                    true,
                    "model",
                    "Auto-answer will retry once.",
                    "resolver",
                    "resolver",
                    "resolver",
                    true,
                    "eligible",
                    "Context is sufficient for one retry.",
                    new AutoRefinementAnswerInspectionDetails(
                        "answered",
                        "Automatic refinement answering recorded 2 answer(s) before retrying spec readiness.",
                        "Grounded retry succeeded.",
                        2,
                        "2026-05-19T10:00:00.5000000+00:00",
                        "/repo/.specs/us/US-0001/execution-receipts/execution-1-auto-answer.json",
                        new PhaseExecutionEffectivePrompt("auto-system", "auto-user"),
                        new PhaseExecutionEffectiveContext("/repo", "/repo/.specs/us/US-0001/us.md", "git-head", [], [], null, null)))),
            RefinementSkillPreselection: new RefinementSkillPreselection(
                [new RefinementSkillSelectionItem(".codex/skills/sdd-phase-agents/SKILL.md", "Required by local SDD workflow.")],
                [new RefinementSkillSelectionItem("../ai-skills-shared/.shared-skills/skills/dotnet/SKILL.md", "Candidate for .NET repository scope.")],
                [new RefinementSkillSelectionItem(".codex/skills/functional-commit-version-bump/SKILL.md", "Not part of refinement.")],
                ["No repository context files are attached yet for this refinement run."]),
            RefinementGraphScopeRequest: new RefinementGraphScopeRequest(
                2,
                [new RefinementGraphSeedNode("user-story-intent", "User Story Intent", "Primary intent source.")],
                [new PhaseExecutionArtifactInput("/repo/.specs/us/US-0001/us.md", "us-hash", "capture")],
                ["Which actor executes the workflow?"]),
            SpecApprovalPolicySnapshot: new SpecPhaseApprovalPolicyDetails(
                "blocked",
                false,
                "spec_approval_questions_unresolved",
                HasSpecArtifact: true,
                SchemaIsValid: true,
                HasUnresolvedApprovalQuestions: true,
                UnresolvedApprovalQuestionCount: 2,
                DecompositionApprovalPending: false,
                ApprovalRules:
                [
                    new SpecPhaseApprovalRule(
                        "human_approval_questions_resolved",
                        "All human approval questions must be answered before the spec baseline can be approved.",
                        "blocked",
                        false,
                        "spec_approval_questions_unresolved",
                        "2 unresolved approval question(s) remain.")
                ]),
            ImplementationPolicySnapshot: new ImplementationPhasePolicySnapshot(
                "implementation",
                "shared-phase-policy/v1",
                "Implementation may modify the workspace and must persist evidence for downstream review.",
                ExecutionAllowed: true,
                ExecutionBlockingReason: null,
                new PhaseExecutionRequirements(true, "read-write", true),
                [new PhaseExecutionToolPermission("workspace-write", "write", "enforced", "Implementation edits repository files inside the declared writable scope.")],
                [new PhaseExecutionPathPolicy("<workspace-root>/**", "write", "phase-agent", "declared", "Implementation can update repository files inside the active workspace.")],
                [new PhaseExecutionPathPolicy("<workspace-root>/.git/**", "write", "phase-agent", "enforced", "Git metadata must never be mutated by phase execution.")],
                [new PhaseExecutionEvidenceRequirement("implementation_evidence_record", "Implementation must persist evidence markdown/json describing touched files and validation performed.", "enforced")],
                [new PhaseExecutionEligibilityRule("implementation_write_scope_declared", "Implementation must expose writable scope and forbidden mutation zones so repository edits stay auditable.", "declared", null, true, "Writable scope and forbidden mutation zones are declared through the shared implementation policy contract.")]),
            ReviewPolicySnapshot: new ReviewPhasePolicySnapshot(
                "review",
                "shared-phase-policy/v1",
                "Phase `review` requires `read` repository access and applies `release` review evidence policy.",
                ExecutionAllowed: true,
                ExecutionBlockingReason: null,
                new PhaseExecutionRequirements(true, "read", false),
                [new PhaseExecutionEvidenceRequirement("validation_strategy_evidence", "Review must classify validation strategy items according to the active review evidence policy.", "enforced", "release")],
                [new PhaseExecutionEligibilityRule("review_evidence_policy_selected", "Review execution must declare the active evidence policy so operators can interpret blocking evidence gaps.", "enforced", null, true, "Active review evidence policy: `release`.")],
                "release",
                "fail",
                true,
                true,
                [new ReviewEvidencePolicyRule("automated", true, "Automated validation items are treated as blocking when they fail under the active policy.")],
                [new ReviewPhaseOverrideCondition("force_approval_reason_required", "Operators must provide an explicit rationale before overriding review.", "required", true, null, "Approve Anyway always requires a human reason that is recorded in the workflow audit trail.")]),
            ReleaseApprovalPolicySnapshot: new ReleaseApprovalPhasePolicySnapshot(
                "release-approval",
                "shared-phase-policy/v1",
                "Phase `release-approval` requires `read` repository access and does not allow repository writes by the assigned phase agent.",
                "ready",
                ExecutionAllowed: true,
                ExecutionBlockingReason: null,
                new PhaseExecutionRequirements(true, "read", false),
                [
                    new PhaseExecutionEvidenceRequirement("release_evidence_bundle", "Release approval must persist a structured release evidence pack bundling review outcome, changed files, validation results, residual risks, and supporting artifact links.", "enforced")
                ],
                [
                    new PhaseExecutionEligibilityRule("release_approval_review_entry_visible", "Release approval must surface whether it was reached through a passing review or an explicit review force-approval decision.", "declared", null, true, "Release-approval policy details record both the latest review verdict and any force-approval transition.")
                ],
                ApprovalAvailableNow: true,
                ApprovalBlockingReason: null,
                LatestReviewVerdict: "fail",
                LatestReviewWasForceApproved: true,
                HasReleaseArtifact: true,
                HasReleaseEvidencePack: true,
                HasImplementationEvidence: true,
                HasReviewGateResult: true,
                HasBranchContext: true,
                HasTimelineContext: true,
                CurrentWorkspaceHeadSha: "abc1234",
                ApprovedReviewCommitSha: "def5678",
                ReviewCommitMatchesWorkspaceHead: null,
                [
                    new ReleaseApprovalEvidenceRule("release-evidence-pack", true, "The latest release-approval receipt contains a structured release evidence pack.")
                ],
                [
                    new ReleaseApprovalPolicyCondition("release_approval_requires_review_outcome", "Release approval can only start after review passes or a human explicitly force-approves review.", "satisfied", true, null, "Release approval may run because a human force-approval decision moved the workflow out of review.")
                ],
                [
                    new ReleaseApprovalPolicyCondition("release_approval_evidence_pack_present", "The latest release-approval receipt must persist a structured release evidence pack.", "satisfied", true, null, "The structured release evidence pack is available for operator inspection.")
                ]),
            TechnicalDesignGateSnapshot: new TechnicalDesignGateSnapshot(
                "technical-design",
                "shared-phase-policy/v1",
                "Phase `technical-design` requires `read` repository access and does not allow repository writes by the assigned phase agent.",
                ExecutionAllowed: true,
                ExecutionBlockingReason: null,
                new PhaseExecutionRequirements(true, "read", false),
                [
                    new PhaseExecutionEvidenceRequirement("technical_design_design_record", "Technical design should persist receipt-linked evidence and validation strategy context before repositories enforce an explicit pre-implementation approval gate.", "declared")
                ],
                [
                    new PhaseExecutionEligibilityRule("technical_design_quality_gate_visible", "Technical design must expose whether an explicit design gate is required or reusable.", "declared", null, true, "A reusable design gate contract is visible.")
                ],
                GateMode: "reusable-pre-implementation-approval",
                ApprovalRequiredNow: false,
                ApprovalReadyNow: true,
                ApprovalBlockingReason: null,
                HasTechnicalDesignArtifact: true,
                HasStructuredTechnicalDesignArtifact: true,
                HasValidationStrategy: true,
                HasEvidenceRecord: true,
                HasContextPack: true,
                GraphIntentDeclared: true,
                [
                    new TechnicalDesignGateRule("technical_design_validation_strategy_declared", "A reusable design gate requires validation strategy.", "satisfied", "enforced", true, null, "Validation strategy items are declared.")
                ]),
            ImplementationStructuredEvidence: new ImplementationStructuredEvidence(
                "2026-05-19T10:00:01.0000000+00:00",
                "/repo/.specs/us/US-0001/phases/03-implementation.evidence.json",
                "/repo/.specs/us/US-0001/phases/03-implementation.evidence.md",
                [
                    "Phase-scoped repository evidence was computed from git workspace snapshots captured immediately before and after implementation execution.",
                    "Meaningful touched repository files detected: `1`."
                ],
                [
                    new ImplementationTouchedFileEvidence(
                        "src/App/Service.cs",
                        "content_changed",
                        " M",
                        "M ",
                        "baseline-hash",
                        "current-hash")
                ],
                new ImplementationGraphEvidence(
                    GraphScopeRequestAvailable: true,
                    GraphScopeRequestPath: "/repo/.specs/us/US-0001/context/graph-scope-request.json",
                    ImpactGraphPath: "/repo/.specs/us/US-0001/context/impact-graph.json",
                    ImpactGraphMetadataPath: "/repo/.specs/us/US-0001/context/impact-graph.meta.json",
                    ImpactSummaryPath: "/repo/.specs/us/US-0001/context/impact-summary.md",
                    ImpactGraphState: "fresh",
                    OperationReferences:
                    [
                        new ImplementationGraphOperationReference(
                            "event-1",
                            "2026-05-19T09:59:00.0000000+00:00",
                            "graph.impact.executed",
                            "derive-impact-graph",
                            "materialize-impact-graph",
                            "workflow-runtime",
                            false,
                            42,
                            ["/repo/.specs/cache/graphs/global-graph.meta.json"],
                            ["/repo/.specs/us/US-0001/context/impact-graph.meta.json"],
                            [])
                    ],
                    Warnings: [])),
            ReviewStructuredGateResult: new ReviewStructuredGateResult(
                "fail",
                "Review failed because at least one validation strategy item was not validated successfully.",
                HasBlockingFindings: true,
                PassedValidationItemCount: 1,
                FailedValidationItemCount: 1,
                DeferredValidationItemCount: 0,
                ["Review failed 1 validation strategy item(s)."],
                [
                    new ReviewCorrectionTarget(
                        "Review must compare implementation back to the approved spec before final release approval.",
                        "fail",
                        true,
                        "The review artifact did not validate this Technical Design validation strategy item.",
                        "Fix the failed validation item and rerun the review phase.")
                ],
                [
                    new ReviewEvidenceLink(
                        "implementation-evidence-json",
                        "/repo/.specs/us/US-0001/phases/03-implementation.evidence.json",
                        "Machine-readable implementation evidence consumed by review.")
                ]),
            ReleaseApprovalEvidencePack: new ReleaseApprovalEvidencePack(
                "2026-05-19T10:00:02.0000000+00:00",
                "/repo/.specs/us/US-0001/phases/05-release-approval.md",
                "fail",
                "Review failed because at least one validation strategy item was not validated successfully.",
                [
                    new ReleaseApprovalChangedFile(
                        "src/App/Service.cs",
                        "content_changed",
                        "M ",
                        " M")
                ],
                [
                    new ReleaseApprovalValidationResult(
                        "fail",
                        "Review must compare implementation back to the approved spec before final release approval.",
                        "The review artifact did not validate this Technical Design validation strategy item.")
                ],
                [
                    "Deterministic release approval does not inspect live repository diffs beyond the recorded workflow artifacts."
                ],
                [
                    new ReleaseApprovalArtifactLink(
                        "branch-context",
                        "/repo/.specs/us/US-0001/branch.yaml",
                        "Branch metadata injected into release approval.")
                ]),
            PrPreparationStructuredEvidence: new PrPreparationStructuredEvidence(
                "2026-05-19T10:00:03.0000000+00:00",
                "/repo/.specs/us/US-0001/phases/06-pr-preparation.md",
                "ready_to_publish",
                "US-0001: deliver approved workflow scope",
                "This PR packages the approved workflow scope into a draft pull request ready for reviewer validation.",
                "main",
                "feature/us-0001",
                ReleaseApprovalArtifactAvailable: true,
                ReleaseApprovalEvidencePackAvailable: true,
                ["release-approval", "04-review.md", "03-implementation.md"],
                [new PrPreparationParticipant("alice", ["capture", "spec", "review"])],
                ["Validated through review artifact `04-review.md`."],
                ["Verify claimed validation evidence"],
                [new PrPreparationEvidenceLink("branch-context", "/repo/.specs/us/US-0001/branch.yaml", "Workflow branch metadata used for PR publication.")]),
            TechnicalDesignContextPack: new TechnicalDesignContextPack(
                [new RefinementSkillSelectionItem("../ai-skills-shared/.shared-skills/skills/dotnet/SKILL.md", "Selected for .NET repository scope.")],
                new RefinementGraphScopeRequest(
                    2,
                    [new RefinementGraphSeedNode("user-story-intent", "User Story Intent", "Primary intent source.")],
                    [new PhaseExecutionArtifactInput("/repo/.specs/us/US-0001/us.md", "us-hash", "capture")],
                    []),
                "fresh",
                "/repo/.specs/us/US-0001/context/impact-summary.md",
                GraphEnabled: true,
                GraphAvailable: true,
                FallbackUsed: false,
                GraphBackedExpansions:
                [
                    new TechnicalDesignGraphExpansion(
                        "/repo/src/App/Service.cs",
                        "Seed file from graph scope request.",
                        "graph-scope-request",
                        "/repo/src/App/App.csproj",
                        "service-hash")
                ],
                GraphQueryEvidence:
                [
                    new TechnicalDesignGraphQueryEvidence(
                        "status",
                        "Inspect graph readiness before technical-design narrowing begins.",
                        "workflow-runtime",
                        "semantic-graph",
                        null,
                        "impact-graph",
                        "fresh",
                        false,
                        8,
                        null,
                        [],
                        [],
                        ["Impact graph metadata matches the current graph-scope request and parent graph fingerprint."],
                        [])
                ],
                Warnings: []),
            EffectivePrompt: new PhaseExecutionEffectivePrompt("system", "user"),
            EffectiveContext: new PhaseExecutionEffectiveContext(
                "/repo",
                "/repo/.specs/us/US-0001/us.md",
                "git-head",
                [],
                [],
                null,
                null,
                new TechnicalDesignContextPack(
                    [new RefinementSkillSelectionItem("../ai-skills-shared/.shared-skills/skills/dotnet/SKILL.md", "Selected for .NET repository scope.")],
                    null,
                    "fresh",
                    "/repo/.specs/us/US-0001/context/impact-summary.md",
                    GraphEnabled: true,
                    GraphAvailable: true,
                    FallbackUsed: false,
                    GraphBackedExpansions: [],
                    GraphQueryEvidence: [],
                    Warnings: [])));

        var json = JsonSerializer.Serialize(receipt, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"effectivePrompt\":{", json);
        Assert.Contains("\"effectiveContext\":{", json);
        Assert.Contains("\"refinementPolicySnapshot\":{", json);
        Assert.Contains("\"refinementSkillPreselection\":{", json);
        Assert.Contains("\"refinementGraphScopeRequest\":{", json);
        Assert.Contains("\"specApprovalPolicySnapshot\":{", json);
        Assert.Contains("\"technicalDesignGateSnapshot\":{", json);
        Assert.Contains("\"implementationPolicySnapshot\":{", json);
        Assert.Contains("\"reviewPolicySnapshot\":{", json);
        Assert.Contains("\"releaseApprovalPolicySnapshot\":{", json);
        Assert.Contains("\"activeEvidencePolicy\":\"release\"", json);
        Assert.Contains("\"implementationStructuredEvidence\":{", json);
        Assert.Contains("\"reviewStructuredGateResult\":{", json);
        Assert.Contains("\"releaseApprovalEvidencePack\":{", json);
        Assert.Contains("\"prPreparationStructuredEvidence\":{", json);
        Assert.Contains("\"reviewVerdict\":\"fail\"", json);
        Assert.Contains("\"verdict\":\"fail\"", json);
        Assert.Contains("\"correctionTargets\":[", json);
        Assert.Contains("\"linkedEvidence\":[", json);
        Assert.Contains("\"technicalDesignContextPack\":{", json);
        Assert.Contains("\"evidenceRecord\":{", json);
        Assert.Contains("\"executionEnvelope\":{", json);
        Assert.Contains("\"autoRefinementAnswerAttempt\":{", json);
        Assert.Contains("\"systemPrompt\":\"system\"", json);
        Assert.Contains("\"userPrompt\":\"user\"", json);
        Assert.Contains("\"eligibilityStatus\":\"eligible\"", json);
        Assert.Contains("\"resolvedAnswerCount\":2", json);
        Assert.Contains("\"requiredSkills\":[", json);
        Assert.Contains("\"seedNodes\":[", json);
        Assert.Contains("\"approvalBlockingReason\":\"spec_approval_questions_unresolved\"", json);
        Assert.Contains("\"executionAllowed\":true", json);
        Assert.Contains("\"evidenceJsonPath\":\"/repo/.specs/us/US-0001/phases/03-implementation.evidence.json\"", json);
        Assert.Contains("\"graphScopeRequestAvailable\":true", json);
        Assert.Contains("\"workspaceRoot\":\"/repo\"", json);
        Assert.Contains("\"impactGraphState\":\"fresh\"", json);
        Assert.Contains("\"graphQueryEvidence\":[", json);
        Assert.Contains("\"validationSummary\":{", json);
        Assert.Contains("\"sandboxMode\":\"provider-managed\"", json);
    }

    [Fact]
    public void PhaseExecutionPolicyCatalog_DescribesImplementationReviewReleaseApprovalAndPrPreparationPolicies()
    {
        var implementationPolicy = PhaseExecutionPolicyCatalog.Describe(
            PhaseId.Implementation,
            new PhaseExecutionReadiness(
                PhaseId.Implementation,
                CanExecute: true,
                RequiredPermissions: PhaseExecutionPermissionCatalog.Describe(PhaseId.Implementation)));
        var reviewPolicy = PhaseExecutionPolicyCatalog.Describe(
            PhaseId.Review,
            new PhaseExecutionReadiness(
                PhaseId.Review,
                CanExecute: true,
                RequiredPermissions: PhaseExecutionPermissionCatalog.Describe(PhaseId.Review)),
            reviewEvidencePolicy: "release");
        var releaseApprovalPolicy = PhaseExecutionPolicyCatalog.Describe(
            PhaseId.ReleaseApproval,
            new PhaseExecutionReadiness(
                PhaseId.ReleaseApproval,
                CanExecute: true,
                RequiredPermissions: PhaseExecutionPermissionCatalog.Describe(PhaseId.ReleaseApproval)));
        var prPreparationPolicy = PhaseExecutionPolicyCatalog.Describe(
            PhaseId.PrPreparation,
            new PhaseExecutionReadiness(
                PhaseId.PrPreparation,
                CanExecute: true,
                RequiredPermissions: PhaseExecutionPermissionCatalog.Describe(PhaseId.PrPreparation)));

        Assert.Equal("implementation", implementationPolicy.PhaseId);
        Assert.Equal("shared-phase-policy/v1", implementationPolicy.PolicyKey);
        Assert.Contains(implementationPolicy.AllowedTools, tool => tool.Tool == "workspace-write" && tool.Enforcement == "enforced");
        Assert.Contains(implementationPolicy.WritablePaths, path => path.Path == "<workspace-root>/**" && path.Actor == "phase-agent");
        Assert.Contains(implementationPolicy.ForbiddenPaths, path => path.Path == "<workspace-root>/.git/**");
        Assert.Contains(implementationPolicy.EvidenceRequirements, item => item.Id == "implementation_evidence_record");
        Assert.Contains(implementationPolicy.EvidenceRequirements, item => item.Id == "graph_guided_scope_evidence" && item.Enforcement == "declared");
        Assert.Contains(implementationPolicy.EligibilityRules, rule => rule.Id == "implementation_write_scope_declared");
        Assert.Contains(implementationPolicy.EligibilityRules, rule => rule.Id == "implementation_review_loop_visible");

        Assert.Equal("review", reviewPolicy.PhaseId);
        Assert.Contains("`release`", reviewPolicy.Summary);
        Assert.Contains(reviewPolicy.EvidenceRequirements, item => item.Id == "validation_strategy_evidence" && item.PolicyInput == "release");
        Assert.Contains(reviewPolicy.EligibilityRules, rule => rule.Id == "review_evidence_policy_selected");
        Assert.Contains(releaseApprovalPolicy.EvidenceRequirements, item => item.Id == "release_evidence_bundle" && item.Enforcement == "enforced");
        Assert.Contains(releaseApprovalPolicy.EvidenceRequirements, item => item.Id == "branch_and_timeline_context");
        Assert.Contains(releaseApprovalPolicy.EligibilityRules, rule => rule.Id == "release_approval_review_entry_visible");
        Assert.Contains(prPreparationPolicy.EvidenceRequirements, item => item.Id == "pr_preparation_structured_evidence");
        Assert.Contains(prPreparationPolicy.EvidenceRequirements, item => item.Id == "branch_publication_context");
        Assert.Contains(prPreparationPolicy.EligibilityRules, rule => rule.Id == "pr_preparation_publication_mode_visible");
    }

    [Fact]
    public void PhaseExecutionPolicyCatalog_DescribesTechnicalDesignPolicyVisibility()
    {
        var readiness = new PhaseExecutionReadiness(
            PhaseId.TechnicalDesign,
            CanExecute: true,
            RequiredPermissions: PhaseExecutionPermissionCatalog.Describe(PhaseId.TechnicalDesign),
            AssignedModelSecurity: new PhaseExecutionModelSecurity(
                "openai-compatible",
                "gpt-5",
                "technical-design",
                "read",
                NativeCliRequired: false,
                NativeCliAvailable: false,
                AgentName: "designer",
                AgentRole: "technical-design"),
            ValidationMessage: "Phase permission precheck passed for the assigned agent profile.",
            PhaseSubagentsEnabled: true);
        var policy = PhaseExecutionPolicyCatalog.Describe(PhaseId.TechnicalDesign, readiness);

        Assert.Equal("technical-design", policy.PhaseId);
        Assert.Contains(policy.EvidenceRequirements, item => item.Id == "design_receipt_evidence");
        Assert.Contains(policy.EvidenceRequirements, item => item.Id == "refinement_graph_handoff");
        Assert.Contains(policy.EligibilityRules, rule => rule.Id == "technical_design_subagent_mode_declared");
        Assert.Contains(policy.EligibilityRules, rule => rule.Id == "technical_design_quality_gate_visible");
    }

    [Fact]
    public void PhaseExecutionEvidenceBuilder_CapturesSharedEvidenceShape()
    {
        var inputManifest = new PhaseExecutionInputManifest(
            "manifest-hash",
            "/repo",
            "/repo/.specs/us/US-0001/us.md",
            "us-hash",
            "git-head",
            [new PhaseExecutionArtifactInput("/repo/.specs/us/US-0001/phases/01-spec.md", "spec-hash", "spec")],
            [new PhaseExecutionArtifactInput("/repo/context/architecture.md", "ctx-hash")],
            new PhaseExecutionArtifactInput("/repo/.specs/us/US-0001/phases/02-technical-design.md", "td-hash", "technical-design"),
            "op-hash");
        var outputManifest = new PhaseExecutionOutputManifest(
            "/repo/.specs/us/US-0001/phases/03-implementation.md",
            "impl-hash",
            [
                new PhaseExecutionArtifactInput("/repo/.specs/us/US-0001/phases/03-implementation.evidence.md", "emd-hash"),
                new PhaseExecutionArtifactInput("/repo/.specs/us/US-0001/phases/03-implementation.evidence.json", "ejson-hash")
            ]);
        var policy = PhaseExecutionPolicyCatalog.Describe(
            PhaseId.Implementation,
            new PhaseExecutionReadiness(
                PhaseId.Implementation,
                CanExecute: true,
                RequiredPermissions: PhaseExecutionPermissionCatalog.Describe(PhaseId.Implementation)));
        var execution = new PhaseExecutionMetadata(
            "openai-compatible",
            "test-model",
            "impl-profile",
            RuntimeVersion: "0.1.5.554",
            AgentName: "implementer",
            AgentRole: "implementation",
            UsedSkills: [".codex/skills/sdd-phase-agents/SKILL.md"]);

        var record = PhaseExecutionEvidenceBuilder.Build(
            PhaseId.Implementation,
            inputManifest,
            outputManifest,
            execution,
            policy,
            "/repo/.specs/us/US-0001/execution-receipts/20260520-implementation.json");

        Assert.Equal("phase-agent", record.Actor.Kind);
        Assert.Contains(record.Inputs, item => item.Kind == "current-artifact");
        Assert.Contains(record.Outputs, item => item.Kind == "phase-evidence");
        Assert.Contains(record.Settings, item => item.Name == "policy-key" && item.Value == "shared-phase-policy/v1");
        Assert.Contains(record.ToolsUsed, item => item.Name == "workspace-write");
        Assert.Equal("captured", record.ValidationSummary.Status);
        Assert.Contains("phase-evidence-generated", record.ValidationSummary.Checks);
        Assert.Contains(record.EvidenceLinks, item => item.Kind == "receipt");
    }

    [Fact]
    public void PhaseExecutionEnvelopeCatalog_DescribesNativeCliAndManagedProviderBoundaries()
    {
        var implementationReadiness = new PhaseExecutionReadiness(
            PhaseId.Implementation,
            CanExecute: true,
            RequiredPermissions: PhaseExecutionPermissionCatalog.Describe(PhaseId.Implementation),
            AssignedModelSecurity: new PhaseExecutionModelSecurity(
                ProviderKind: "codex",
                Model: "gpt-5-codex",
                ProfileName: "implementation",
                RepositoryAccess: "read-write",
                NativeCliRequired: true,
                NativeCliAvailable: true,
                AgentName: "implementer",
                AgentRole: "implementation"));
        var implementationPolicy = PhaseExecutionPolicyCatalog.Describe(PhaseId.Implementation, implementationReadiness);
        var implementationEnvelope = PhaseExecutionEnvelopeCatalog.Describe(PhaseId.Implementation, implementationPolicy, implementationReadiness);

        var specReadiness = new PhaseExecutionReadiness(
            PhaseId.Spec,
            CanExecute: true,
            RequiredPermissions: PhaseExecutionPermissionCatalog.Describe(PhaseId.Spec),
            AssignedModelSecurity: new PhaseExecutionModelSecurity(
                ProviderKind: "openai-compatible",
                Model: "gpt-5",
                ProfileName: "spec",
                RepositoryAccess: "read",
                NativeCliRequired: false,
                NativeCliAvailable: false));
        var specPolicy = PhaseExecutionPolicyCatalog.Describe(PhaseId.Spec, specReadiness);
        var specEnvelope = PhaseExecutionEnvelopeCatalog.Describe(PhaseId.Spec, specPolicy, specReadiness);

        Assert.Equal("native-cli", implementationEnvelope.ExecutionMode);
        Assert.Equal("workspace-write", implementationEnvelope.SandboxMode);
        Assert.Contains(implementationEnvelope.ToolPermissions, item => item.Tool == "workspace-write" && item.Actor == "phase-agent");
        Assert.Contains(implementationEnvelope.WriteScopes, item => item.Actor == "phase-agent");
        Assert.Contains(implementationEnvelope.RepositoryBoundaries, item => item.Kind == "forbidden-path" && item.Path == "<workspace-root>/.git/**");
        Assert.Equal("extended", implementationEnvelope.Budget.ComputeTier);
        Assert.Equal("phase-scoped-repository-mutation", implementationEnvelope.Budget.MutationBudget);

        Assert.Equal("managed-provider", specEnvelope.ExecutionMode);
        Assert.Equal("provider-managed", specEnvelope.SandboxMode);
        Assert.DoesNotContain(specEnvelope.WriteScopes, item => item.Actor == "phase-agent");
        Assert.Contains(specEnvelope.ToolPermissions, item => item.Tool == "context-materialization");
    }

    public void Dispose()
    {
        if (Directory.Exists(workspaceRoot))
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }
}
