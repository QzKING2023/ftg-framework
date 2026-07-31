#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Data;
using Xunit;

namespace FTG_Framework.Tests;

public class DataStorePhysicsProfileTests
{
    private static MoveDefinition MakeMove(string moveId) => new()
    {
        MoveId = moveId,
        Startup = 5,
        Active = 3,
        Recovery = 7,
    };

    private static KnockbackProfile MakeKnockbackProfile(string profileId, float h = 8.0f, float v = 3.0f) => new()
    {
        ProfileId = profileId,
        Horizontal = h,
        Vertical = v,
        Gravity = 1.5f,
        Friction = 0.3f,
    };

    private static PhysicsResponseProfile MakeResponseProfile(string profileId, float multiplier = 1.0f) => new()
    {
        ProfileId = profileId,
        KnockbackMultiplier = multiplier,
        GravityScale = 1.0f,
        Friction = 0.5f,
        AirFriction = 0.2f,
    };

    [Fact]
    public void GetKnockbackProfile_KnownId_ReturnsProfile()
    {
        var moves = new[] { MakeMove("5LP") };
        var profiles = new[] { MakeKnockbackProfile("light_hit") };
        var store = new DataStore(moves, knockbackProfiles: profiles);

        var profile = store.GetKnockbackProfile("light_hit");

        Assert.NotNull(profile);
        Assert.Equal("light_hit", profile!.ProfileId);
        Assert.Equal(8.0f, profile.Horizontal);
    }

    [Fact]
    public void GetKnockbackProfile_UnknownId_ReturnsNull()
    {
        var moves = new[] { MakeMove("5LP") };
        var store = new DataStore(moves);

        var profile = store.GetKnockbackProfile("nonexistent");

        Assert.Null(profile);
    }

    [Fact]
    public void GetKnockbackProfile_NullId_ReturnsNull()
    {
        var moves = new[] { MakeMove("5LP") };
        var profiles = new[] { MakeKnockbackProfile("light_hit") };
        var store = new DataStore(moves, knockbackProfiles: profiles);

        var profile = store.GetKnockbackProfile(null!);

        Assert.Null(profile);
    }

    [Fact]
    public void GetKnockbackProfile_SameInstanceReturned()
    {
        var moves = new[] { MakeMove("5LP") };
        var profiles = new[] { MakeKnockbackProfile("light_hit") };
        var store = new DataStore(moves, knockbackProfiles: profiles);

        var p1 = store.GetKnockbackProfile("light_hit");
        var p2 = store.GetKnockbackProfile("light_hit");

        Assert.Same(p1, p2);
    }

    [Fact]
    public void GetPhysicsResponseProfile_KnownId_ReturnsProfile()
    {
        var moves = new[] { MakeMove("5LP") };
        var profiles = new[] { MakeResponseProfile("hitstun_air", 0.3f) };
        var store = new DataStore(moves, physicsResponseProfiles: profiles);

        var profile = store.GetPhysicsResponseProfile("hitstun_air");

        Assert.NotNull(profile);
        Assert.Equal("hitstun_air", profile!.ProfileId);
        Assert.Equal(0.3f, profile.KnockbackMultiplier);
    }

    [Fact]
    public void GetPhysicsResponseProfile_UnknownId_ReturnsNull()
    {
        var moves = new[] { MakeMove("5LP") };
        var store = new DataStore(moves);

        var profile = store.GetPhysicsResponseProfile("nonexistent");

        Assert.Null(profile);
    }

    [Fact]
    public void GetAllKnockbackProfiles_ReturnsAll()
    {
        var moves = new[] { MakeMove("5LP") };
        var profiles = new[] { MakeKnockbackProfile("light"), MakeKnockbackProfile("heavy") };
        var store = new DataStore(moves, knockbackProfiles: profiles);

        var all = store.GetAllKnockbackProfiles();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void GetAllPhysicsResponseProfiles_ReturnsAll()
    {
        var moves = new[] { MakeMove("5LP") };
        var profiles = new[] { MakeResponseProfile("default"), MakeResponseProfile("armored") };
        var store = new DataStore(moves, physicsResponseProfiles: profiles);

        var all = store.GetAllPhysicsResponseProfiles();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void Constructor_WithoutProfiles_Works()
    {
        var moves = new[] { MakeMove("5LP") };
        var store = new DataStore(moves);

        var allKb = store.GetAllKnockbackProfiles();
        var allPr = store.GetAllPhysicsResponseProfiles();

        Assert.Empty(allKb);
        Assert.Empty(allPr);
    }

    [Fact]
    public void SetKnockbackProfiles_ReplacesAtomically()
    {
        var moves = new[] { MakeMove("5LP") };
        var initial = new[] { MakeKnockbackProfile("old") };
        var store = new DataStore(moves, knockbackProfiles: initial);
        var updated = new[] { MakeKnockbackProfile("new", 10.0f, 5.0f) };

        store.SetKnockbackProfiles(updated);

        Assert.Null(store.GetKnockbackProfile("old"));
        var profile = store.GetKnockbackProfile("new");
        Assert.NotNull(profile);
        Assert.Equal(10.0f, profile!.Horizontal);
    }

    [Fact]
    public void SetKnockbackProfiles_DuplicateId_ThrowsFormatException()
    {
        var moves = new[] { MakeMove("5LP") };
        var store = new DataStore(moves);
        var profiles = new[]
        {
            MakeKnockbackProfile("same"),
            MakeKnockbackProfile("same"),
        };

        var ex = Assert.Throws<FormatException>(() =>
            store.SetKnockbackProfiles(profiles));
        Assert.Contains("[Data]", ex.Message);
    }

    [Fact]
    public void SetKnockbackProfiles_InvalidCandidate_RetainsPreviousSetAndIdentity()
    {
        var previous = MakeKnockbackProfile("previous");
        var store = new DataStore([MakeMove("5LP")], knockbackProfiles: [previous]);

        Assert.Throws<FormatException>(() => store.SetKnockbackProfiles(
            [MakeKnockbackProfile("duplicate"), MakeKnockbackProfile("duplicate")]));

        Assert.Same(previous, store.GetKnockbackProfile("previous"));
        Assert.Null(store.GetKnockbackProfile("duplicate"));
    }

    [Fact]
    public void SetKnockbackProfiles_EmptyProfileId_ThrowsFormatException()
    {
        var moves = new[] { MakeMove("5LP") };
        var store = new DataStore(moves);
        var profiles = new[] { MakeKnockbackProfile("") };

        var ex = Assert.Throws<FormatException>(() =>
            store.SetKnockbackProfiles(profiles));
        Assert.Contains("[Data]", ex.Message);
    }

    [Fact]
    public void SetKnockbackProfiles_NullArray_ThrowsArgumentNullException()
    {
        var moves = new[] { MakeMove("5LP") };
        var store = new DataStore(moves);

        Assert.Throws<ArgumentNullException>(() =>
            store.SetKnockbackProfiles(null!));
    }

    [Fact]
    public void SetKnockbackProfiles_NullEntry_RetainsPreviousSetAndIdentity()
    {
        var previous = MakeKnockbackProfile("previous");
        var store = new DataStore([MakeMove("5LP")], knockbackProfiles: [previous]);

        Assert.Throws<FormatException>(() =>
            store.SetKnockbackProfiles([null!]));

        Assert.Same(previous, store.GetKnockbackProfile("previous"));
    }

    [Fact]
    public void SetPhysicsResponseProfiles_ReplacesAtomically()
    {
        var moves = new[] { MakeMove("5LP") };
        var initial = new[] { MakeResponseProfile("old") };
        var store = new DataStore(moves, physicsResponseProfiles: initial);
        var updated = new[] { MakeResponseProfile("new", 0.5f) };

        store.SetPhysicsResponseProfiles(updated);

        Assert.Null(store.GetPhysicsResponseProfile("old"));
        var profile = store.GetPhysicsResponseProfile("new");
        Assert.NotNull(profile);
        Assert.Equal(0.5f, profile!.KnockbackMultiplier);
    }

    [Fact]
    public void Constructor_DelegatesToSetKnockbackProfiles()
    {
        var moves = new[] { MakeMove("5LP") };
        var profiles = new[] { MakeKnockbackProfile("a"), MakeKnockbackProfile("a") };

        var ex = Assert.Throws<FormatException>(() =>
            new DataStore(moves, knockbackProfiles: profiles));
        Assert.Contains("[Data]", ex.Message);
    }

    [Fact]
    public void GetPhysicsResponseProfile_NullId_ReturnsNull()
    {
        var moves = new[] { MakeMove("5LP") };
        var profiles = new[] { MakeResponseProfile("hitstun") };
        var store = new DataStore(moves, physicsResponseProfiles: profiles);

        var profile = store.GetPhysicsResponseProfile(null!);

        Assert.Null(profile);
    }

    [Fact]
    public void SetPhysicsResponseProfiles_DuplicateId_ThrowsFormatException()
    {
        var moves = new[] { MakeMove("5LP") };
        var store = new DataStore(moves);
        var profiles = new[]
        {
            MakeResponseProfile("same"),
            MakeResponseProfile("same"),
        };

        var ex = Assert.Throws<FormatException>(() =>
            store.SetPhysicsResponseProfiles(profiles));
        Assert.Contains("[Data]", ex.Message);
    }

    [Fact]
    public void SetPhysicsResponseProfiles_InvalidCandidate_RetainsPreviousSetAndIdentity()
    {
        var previous = MakeResponseProfile("previous");
        var store = new DataStore([MakeMove("5LP")], physicsResponseProfiles: [previous]);

        Assert.Throws<FormatException>(() => store.SetPhysicsResponseProfiles(
            [MakeResponseProfile("duplicate"), MakeResponseProfile("duplicate")]));

        Assert.Same(previous, store.GetPhysicsResponseProfile("previous"));
        Assert.Null(store.GetPhysicsResponseProfile("duplicate"));
    }

    [Fact]
    public void SetPhysicsResponseProfiles_EmptyProfileId_ThrowsFormatException()
    {
        var moves = new[] { MakeMove("5LP") };
        var store = new DataStore(moves);
        var profiles = new[] { MakeResponseProfile("") };

        var ex = Assert.Throws<FormatException>(() =>
            store.SetPhysicsResponseProfiles(profiles));
        Assert.Contains("[Data]", ex.Message);
    }

    [Fact]
    public void SetPhysicsResponseProfiles_NullArray_ThrowsArgumentNullException()
    {
        var moves = new[] { MakeMove("5LP") };
        var store = new DataStore(moves);

        Assert.Throws<ArgumentNullException>(() =>
            store.SetPhysicsResponseProfiles(null!));
    }

    [Fact]
    public void SetPhysicsResponseProfiles_NullEntry_RetainsPreviousSetAndIdentity()
    {
        var previous = MakeResponseProfile("previous");
        var store = new DataStore([MakeMove("5LP")], physicsResponseProfiles: [previous]);

        Assert.Throws<FormatException>(() =>
            store.SetPhysicsResponseProfiles([null!]));

        Assert.Same(previous, store.GetPhysicsResponseProfile("previous"));
    }

    [Fact]
    public void Constructor_DelegatesToSetPhysicsResponseProfiles()
    {
        var moves = new[] { MakeMove("5LP") };
        var profiles = new[] { MakeResponseProfile("a"), MakeResponseProfile("a") };

        var ex = Assert.Throws<FormatException>(() =>
            new DataStore(moves, physicsResponseProfiles: profiles));
        Assert.Contains("[Data]", ex.Message);
    }
}
