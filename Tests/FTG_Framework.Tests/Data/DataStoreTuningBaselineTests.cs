#nullable enable
using FTG_Framework.Data;
using Xunit;

namespace FTG_Framework.Tests.DataLayer;

public sealed class DataStoreTuningBaselineTests
{
    [Fact]
    public void CaptureMoveBaseline_ReturnsValuesVersionAndIdentityFromOneState()
    {
        var move = new MoveDefinition { MoveId = "5A", Damage = 10 };
        var store = new DataStore(new[] { move });
        var identity = new MoveContentIdentity("abc");
        store.ObserveMoveContentIdentity(identity);

        MoveDatasetBaseline baseline = store.CaptureMoveBaseline();

        Assert.Same(move, Assert.Single(baseline.Moves));
        Assert.Equal(0UL, baseline.DatasetVersion);
        Assert.Equal(identity, baseline.ContentIdentity);
    }

    [Fact]
    public void CapturePhysicsBaseline_ReturnsImmutableCopiesUnderOneVersion()
    {
        var knockback = new KnockbackProfile { ProfileId = "light", Horizontal = 1 };
        var response = new PhysicsResponseProfile { ProfileId = "default", GravityScale = 1 };
        var store = new DataStore(System.Array.Empty<MoveDefinition>(),
            knockbackProfiles: new[] { knockback }, physicsResponseProfiles: new[] { response });
        store.ObservePhysicsContentIdentity(PhysicsDocumentKind.Knockback, new DataContentIdentity("kb"));
        store.ObservePhysicsContentIdentity(PhysicsDocumentKind.Response, new DataContentIdentity("response"));

        PhysicsDatasetBaseline baseline = store.CapturePhysicsBaseline();

        Assert.Same(knockback, Assert.Single(baseline.KnockbackProfiles));
        Assert.Same(response, Assert.Single(baseline.ResponseProfiles));
        Assert.Equal(store.PhysicsDatasetVersion, baseline.DatasetVersion);
        Assert.Equal("kb", baseline.KnockbackIdentity.Sha256);
        Assert.Equal("response", baseline.ResponseIdentity.Sha256);
    }
}
