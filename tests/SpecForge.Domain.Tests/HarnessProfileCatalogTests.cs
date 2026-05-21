using SpecForge.Domain.Application;

namespace SpecForge.Domain.Tests;

public sealed class HarnessProfileCatalogTests
{
    [Fact]
    public void ResolveByPhase_UsesExplicitPhaseAssignmentAndLockGovernance()
    {
        var settings = new HarnessProfileRuntimeSettings(
            DefaultProfile: "balanced",
            PhaseProfiles: new HarnessPhaseProfileAssignments(
                DefaultProfile: "strict",
                CaptureProfile: null,
                RefinementProfile: null,
                SpecProfile: null,
                TechnicalDesignProfile: null,
                ImplementationProfile: null,
                ReviewProfile: "regulated",
                ReleaseApprovalProfile: null,
                PrPreparationProfile: null),
            Governance: new HarnessProfileGovernance(
                Authority: "central",
                LockMode: "phase",
                AllowPerUserStoryOverrides: true,
                LockedPhaseIds: ["review"]));

        var resolved = HarnessProfileCatalog.ResolveByPhase(settings);

        Assert.Equal("strict", resolved["spec"].ResolvedProfile);
        Assert.Equal("phase-default-assignment", resolved["spec"].ResolutionSource);
        Assert.Equal("regulated", resolved["review"].ResolvedProfile);
        Assert.Equal("phase-assignment", resolved["review"].ResolutionSource);
        Assert.True(resolved["review"].IsLocked);
        Assert.False(resolved["review"].OverrideAllowedNow);
        Assert.Equal("central", resolved["review"].Authority);
    }

    [Fact]
    public void FromEnvironment_NormalizesInvalidValuesToBalancedWorkspaceDefaults()
    {
        var settings = HarnessProfileRuntimeSettings.FromEnvironment(key => key switch
        {
            "SPECFORGE_HARNESS_PROFILE_DEFAULT" => "invalid",
            "SPECFORGE_HARNESS_PHASE_PROFILES_JSON" => """{"reviewProfile":"regulated","implementationProfile":"nope"}""",
            "SPECFORGE_HARNESS_PROFILE_AUTHORITY" => "unsupported",
            "SPECFORGE_HARNESS_PROFILE_LOCK_MODE" => "phase",
            "SPECFORGE_HARNESS_LOCKED_PHASE_IDS_JSON" => """["review","unknown","review"]""",
            "SPECFORGE_ALLOW_PER_US_HARNESS_PROFILE_OVERRIDES" => "false",
            _ => null
        });

        Assert.Equal("balanced", settings.DefaultProfile);
        Assert.Equal("regulated", settings.PhaseProfiles.ReviewProfile);
        Assert.Null(settings.PhaseProfiles.ImplementationProfile);
        Assert.Equal("workspace", settings.Governance.Authority);
        Assert.Equal("phase", settings.Governance.LockMode);
        Assert.Equal(["review"], settings.Governance.LockedPhaseIds);
        Assert.False(settings.Governance.AllowPerUserStoryOverrides);
    }
}
