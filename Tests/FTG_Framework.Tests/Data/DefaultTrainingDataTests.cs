#nullable enable
using System;
using System.IO;
using System.Linq;
using FTG_Framework.Data;
using Xunit;

namespace FTG_Framework.Tests;

public sealed class DefaultTrainingDataTests
{
    [Fact]
    public void FiveLp_DefaultsSupportRepeatedRealHitsWithoutLaunch()
    {
        string dataDirectory = Path.Combine(FindRepoRoot(), "Scripts", "Framework", "Data");
        var move = MoveDataLoader.LoadFromJson(
            File.ReadAllText(Path.Combine(dataDirectory, "example_moves.json")))
            .Single(item => item.MoveId == "5LP");
        var profile = PhysicsDataLoader.LoadKnockbackProfilesFromJson(
            File.ReadAllText(Path.Combine(dataDirectory, "example_knockback_profiles.json")))
            .Single(item => item.ProfileId == move.KnockbackProfileId);

        Assert.Equal(2, move.Recovery);
        Assert.InRange(profile.Horizontal, 0.001f, 0.5f);
        Assert.Equal(0, profile.Vertical);
    }

    private static string FindRepoRoot()
    {
        string? current = Directory.GetCurrentDirectory();
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "project.godot")) &&
                Directory.Exists(Path.Combine(current, "Scripts", "Framework", "Data")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new InvalidOperationException("Could not locate repository root.");
    }
}
