using System.Text.Json;

namespace SpecForge.Domain.Application;

public sealed record HarnessProfileDefinition(
    string Key,
    string Title,
    string Summary,
    string? InheritsFrom,
    IReadOnlyCollection<string> Traits);

public sealed record HarnessPhaseProfileAssignments(
    string? DefaultProfile,
    string? CaptureProfile,
    string? RefinementProfile,
    string? SpecProfile,
    string? TechnicalDesignProfile,
    string? ImplementationProfile,
    string? ReviewProfile,
    string? ReleaseApprovalProfile,
    string? PrPreparationProfile);

public sealed record HarnessProfileGovernance(
    string Authority,
    string LockMode,
    bool AllowPerUserStoryOverrides,
    IReadOnlyCollection<string> LockedPhaseIds)
{
    public static readonly HarnessProfileGovernance Default = new(
        Authority: "workspace",
        LockMode: "none",
        AllowPerUserStoryOverrides: true,
        LockedPhaseIds: []);
}

public sealed record HarnessProfileRuntimeSettings(
    string DefaultProfile,
    HarnessPhaseProfileAssignments PhaseProfiles,
    HarnessProfileGovernance Governance)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static readonly HarnessProfileRuntimeSettings Default = new(
        DefaultProfile: HarnessProfileCatalog.BalancedProfileKey,
        PhaseProfiles: new HarnessPhaseProfileAssignments(
            DefaultProfile: null,
            CaptureProfile: null,
            RefinementProfile: null,
            SpecProfile: null,
            TechnicalDesignProfile: null,
            ImplementationProfile: null,
            ReviewProfile: null,
            ReleaseApprovalProfile: null,
            PrPreparationProfile: null),
        Governance: HarnessProfileGovernance.Default);

    public static HarnessProfileRuntimeSettings FromEnvironment(Func<string, string?>? readSetting = null)
    {
        var read = readSetting ?? Environment.GetEnvironmentVariable;
        var defaultProfile = HarnessProfileCatalog.NormalizeProfileKey(read("SPECFORGE_HARNESS_PROFILE_DEFAULT"));
        var phaseProfiles = ParsePhaseProfiles(read("SPECFORGE_HARNESS_PHASE_PROFILES_JSON"));
        var authority = HarnessProfileCatalog.NormalizeAuthority(read("SPECFORGE_HARNESS_PROFILE_AUTHORITY"));
        var lockMode = HarnessProfileCatalog.NormalizeLockMode(read("SPECFORGE_HARNESS_PROFILE_LOCK_MODE"));
        var allowOverrides = !string.Equals(
            read("SPECFORGE_ALLOW_PER_US_HARNESS_PROFILE_OVERRIDES")?.Trim(),
            "false",
            StringComparison.OrdinalIgnoreCase);
        var lockedPhaseIds = ParseLockedPhaseIds(read("SPECFORGE_HARNESS_LOCKED_PHASE_IDS_JSON"));

        return new HarnessProfileRuntimeSettings(
            DefaultProfile: defaultProfile,
            PhaseProfiles: phaseProfiles,
            Governance: new HarnessProfileGovernance(
                Authority: authority,
                LockMode: lockMode,
                AllowPerUserStoryOverrides: allowOverrides,
                LockedPhaseIds: lockedPhaseIds));
    }

    private static HarnessPhaseProfileAssignments ParsePhaseProfiles(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Default.PhaseProfiles;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<HarnessPhaseProfileAssignments>(json, JsonOptions);
            return parsed is null
                ? Default.PhaseProfiles
                : NormalizeAssignments(parsed);
        }
        catch
        {
            return Default.PhaseProfiles;
        }
    }

    private static IReadOnlyCollection<string> ParseLockedPhaseIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? [];
            return parsed
                .Select(HarnessProfileCatalog.NormalizePhaseSlug)
                .Where(static phaseId => phaseId is not null)
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static HarnessPhaseProfileAssignments NormalizeAssignments(HarnessPhaseProfileAssignments assignments) =>
        assignments with
        {
            DefaultProfile = HarnessProfileCatalog.NormalizeOptionalProfileKey(assignments.DefaultProfile),
            CaptureProfile = HarnessProfileCatalog.NormalizeOptionalProfileKey(assignments.CaptureProfile),
            RefinementProfile = HarnessProfileCatalog.NormalizeOptionalProfileKey(assignments.RefinementProfile),
            SpecProfile = HarnessProfileCatalog.NormalizeOptionalProfileKey(assignments.SpecProfile),
            TechnicalDesignProfile = HarnessProfileCatalog.NormalizeOptionalProfileKey(assignments.TechnicalDesignProfile),
            ImplementationProfile = HarnessProfileCatalog.NormalizeOptionalProfileKey(assignments.ImplementationProfile),
            ReviewProfile = HarnessProfileCatalog.NormalizeOptionalProfileKey(assignments.ReviewProfile),
            ReleaseApprovalProfile = HarnessProfileCatalog.NormalizeOptionalProfileKey(assignments.ReleaseApprovalProfile),
            PrPreparationProfile = HarnessProfileCatalog.NormalizeOptionalProfileKey(assignments.PrPreparationProfile)
        };
}

public sealed record ResolvedHarnessPhaseProfile(
    string PhaseId,
    string SelectedProfile,
    string ResolvedProfile,
    string ResolutionSource,
    bool IsLocked,
    bool OverrideAllowedNow,
    string Authority,
    string LockMode,
    string? LockReason,
    string Title,
    string Summary,
    IReadOnlyCollection<string> Traits,
    string? InheritsFrom = null);

public static class HarnessProfileCatalog
{
    public const string StrictProfileKey = "strict";
    public const string BalancedProfileKey = "balanced";
    public const string RegulatedProfileKey = "regulated";

    private static readonly IReadOnlyDictionary<string, HarnessProfileDefinition> BuiltIns =
        new Dictionary<string, HarnessProfileDefinition>(StringComparer.Ordinal)
        {
            [StrictProfileKey] = new(
                Key: StrictProfileKey,
                Title: "Strict",
                Summary: "Minimizes automation drift, keeps graph mutation conservative, and expects stronger operator scrutiny before progressing.",
                InheritsFrom: null,
                Traits: ["human-gated", "evidence-forward", "low-automation", "graph-read-preferred"]),
            [BalancedProfileKey] = new(
                Key: BalancedProfileKey,
                Title: "Balanced",
                Summary: "Default delivery posture that keeps automation enabled while preserving phase-specific receipts, evidence, and controllable operator gates.",
                InheritsFrom: null,
                Traits: ["default", "automation-friendly", "auditable", "graph-aware"]),
            [RegulatedProfileKey] = new(
                Key: RegulatedProfileKey,
                Title: "Regulated",
                Summary: "Optimized for stronger auditability and centrally-governed release posture, with phase overrides expected to be locked or explicitly justified.",
                InheritsFrom: StrictProfileKey,
                Traits: ["audit-heavy", "compliance-oriented", "central-ready", "override-constrained"])
        };

    public static IReadOnlyCollection<HarnessProfileDefinition> All => BuiltIns.Values.ToArray();

    public static IReadOnlyDictionary<string, ResolvedHarnessPhaseProfile> ResolveByPhase(HarnessProfileRuntimeSettings? settings)
    {
        var runtime = settings ?? HarnessProfileRuntimeSettings.Default;
        var governance = runtime.Governance ?? HarnessProfileGovernance.Default;
        var lockedPhaseIds = governance.LockedPhaseIds
            .Select(NormalizePhaseSlug)
            .Where(static phaseId => phaseId is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

        var phases = new[]
        {
            "capture",
            "refinement",
            "spec",
            "technical-design",
            "implementation",
            "review",
            "release-approval",
            "pr-preparation"
        };

        var resolved = new Dictionary<string, ResolvedHarnessPhaseProfile>(StringComparer.Ordinal);
        foreach (var phaseId in phases)
        {
            var selectedProfile = ResolveSelectedProfile(runtime, phaseId, out var resolutionSource);
            var definition = ResolveDefinition(selectedProfile);
            var isLocked = IsPhaseLocked(governance, phaseId, lockedPhaseIds);
            var lockReason = isLocked
                ? governance.LockMode == "all"
                    ? "All phase profile assignments are locked by harness governance."
                    : $"Phase '{phaseId}' profile assignment is locked by harness governance."
                : null;

            resolved[phaseId] = new ResolvedHarnessPhaseProfile(
                PhaseId: phaseId,
                SelectedProfile: selectedProfile,
                ResolvedProfile: definition.Key,
                ResolutionSource: resolutionSource,
                IsLocked: isLocked,
                OverrideAllowedNow: governance.AllowPerUserStoryOverrides && !isLocked,
                Authority: NormalizeAuthority(governance.Authority),
                LockMode: NormalizeLockMode(governance.LockMode),
                LockReason: lockReason,
                Title: definition.Title,
                Summary: definition.Summary,
                Traits: definition.Traits,
                InheritsFrom: definition.InheritsFrom);
        }

        return resolved;
    }

    public static HarnessProfileDefinition ResolveDefinition(string? key)
    {
        var normalized = NormalizeProfileKey(key);
        return BuiltIns.TryGetValue(normalized, out var definition)
            ? definition
            : BuiltIns[BalancedProfileKey];
    }

    public static string NormalizeProfileKey(string? key)
    {
        var normalized = key?.Trim().ToLowerInvariant();
        return normalized is StrictProfileKey or BalancedProfileKey or RegulatedProfileKey
            ? normalized
            : BalancedProfileKey;
    }

    public static string? NormalizeOptionalProfileKey(string? key)
    {
        var normalized = key?.Trim().ToLowerInvariant();
        return normalized is StrictProfileKey or BalancedProfileKey or RegulatedProfileKey
            ? normalized
            : null;
    }

    public static string NormalizeAuthority(string? authority)
    {
        var normalized = authority?.Trim().ToLowerInvariant();
        return normalized is "workspace" or "central"
            ? normalized
            : "workspace";
    }

    public static string NormalizeLockMode(string? lockMode)
    {
        var normalized = lockMode?.Trim().ToLowerInvariant();
        return normalized is "none" or "phase" or "all"
            ? normalized
            : "none";
    }

    public static string? NormalizePhaseSlug(string? phaseId)
    {
        var normalized = phaseId?.Trim().ToLowerInvariant();
        return normalized is "capture"
            or "refinement"
            or "spec"
            or "technical-design"
            or "implementation"
            or "review"
            or "release-approval"
            or "pr-preparation"
            ? normalized
            : null;
    }

    private static string ResolveSelectedProfile(
        HarnessProfileRuntimeSettings runtime,
        string phaseId,
        out string resolutionSource)
    {
        string? configured = phaseId switch
        {
            "capture" => runtime.PhaseProfiles.CaptureProfile,
            "refinement" => runtime.PhaseProfiles.RefinementProfile,
            "spec" => runtime.PhaseProfiles.SpecProfile,
            "technical-design" => runtime.PhaseProfiles.TechnicalDesignProfile,
            "implementation" => runtime.PhaseProfiles.ImplementationProfile,
            "review" => runtime.PhaseProfiles.ReviewProfile,
            "release-approval" => runtime.PhaseProfiles.ReleaseApprovalProfile,
            "pr-preparation" => runtime.PhaseProfiles.PrPreparationProfile,
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(configured))
        {
            resolutionSource = "phase-assignment";
            return NormalizeProfileKey(configured);
        }

        if (!string.IsNullOrWhiteSpace(runtime.PhaseProfiles.DefaultProfile))
        {
            resolutionSource = "phase-default-assignment";
            return NormalizeProfileKey(runtime.PhaseProfiles.DefaultProfile);
        }

        if (!string.IsNullOrWhiteSpace(runtime.DefaultProfile))
        {
            resolutionSource = "workflow-default";
            return NormalizeProfileKey(runtime.DefaultProfile);
        }

        resolutionSource = "built-in-default";
        return BalancedProfileKey;
    }

    private static bool IsPhaseLocked(
        HarnessProfileGovernance governance,
        string phaseId,
        IReadOnlySet<string> lockedPhaseIds)
    {
        var lockMode = NormalizeLockMode(governance.LockMode);
        return lockMode == "all"
            || (lockMode == "phase" && lockedPhaseIds.Contains(phaseId));
    }
}
