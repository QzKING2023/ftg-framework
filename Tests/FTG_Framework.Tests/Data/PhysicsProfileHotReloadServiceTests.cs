#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using Xunit;

namespace FTG_Framework.Tests;

[Collection(EventBusTestCollection.Name)]
public sealed class PhysicsProfileHotReloadServiceTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new();
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"ftg-hot-reload-{Guid.NewGuid():N}");
    private PhysicsProfileHotReloadService? _service;

    public PhysicsProfileHotReloadServiceTests()
    {
        Directory.CreateDirectory(_directory);
        EventBusTestHelper.Drain();
    }

    public void Dispose()
    {
        _service?.Shutdown();
        FrameworkLog.Info = Console.WriteLine;
        FrameworkLog.Error = message => Console.Error.WriteLine(message);
        Directory.Delete(_directory, recursive: true);
        _eventBusScope.Dispose();
    }

    [Fact]
    public void KnockbackReload_ValidCandidate_CommitsBeforeFrameAdvanced()
    {
        var initial = Knockback("light", horizontal: 8);
        var store = Store(knockback: [initial]);
        string path = Write("knockback.json", KnockbackJson("light", 10.5f));
        Initialize(store, path, Write("response.json", ResponseJson("default", 1)));
        float observedAtFrame = -1;
        Action<FrameAdvancedEvent> frame = _ =>
            observedAtFrame = store.GetKnockbackProfile("light")!.Horizontal;
        EventBus.Instance.Subscribe(frame);
        try
        {
            EventBus.Instance.Publish(new DataReloadedEvent(path));
            EventBus.Instance.ProcessFrame();
            Assert.Equal(10.5f, observedAtFrame);
        }
        finally { EventBus.Instance.Unsubscribe(frame); }
    }

    [Fact]
    public void KnockbackReload_MalformedThenValid_RetainsThenCommits()
    {
        var initial = Knockback("light", horizontal: 8);
        var store = Store(knockback: [initial]);
        string path = Write("knockback.json", "{");
        Initialize(store, path, Write("response.json", ResponseJson("default", 1)));
        var errors = new List<string>();
        FrameworkLog.Error = errors.Add;

        Reload(path);
        Assert.Same(initial, store.GetKnockbackProfile("light"));
        Assert.Contains(errors, message => message.StartsWith("[Data]", StringComparison.Ordinal));

        File.WriteAllText(path, KnockbackJson("light", 11));
        Reload(path);
        Assert.Equal(11, store.GetKnockbackProfile("light")!.Horizontal);
    }

    [Fact]
    public void DeletedTarget_LogsWarningAndRetainsLastKnownGood()
    {
        var initial = Knockback("light", horizontal: 8);
        var store = Store(knockback: [initial]);
        string path = Write("knockback.json", KnockbackJson("light", 8));
        Initialize(store, path, Write("response.json", ResponseJson("default", 1)));
        var messages = new List<string>();
        FrameworkLog.Info = messages.Add;
        File.Delete(path);

        Reload(path);

        Assert.Same(initial, store.GetKnockbackProfile("light"));
        Assert.Contains(messages, message =>
            message.StartsWith("[Data] Warning:", StringComparison.Ordinal));
    }

    [Fact]
    public void UnrelatedPathAndSameBasenameElsewhere_AreIgnored()
    {
        var initial = Knockback("light", horizontal: 8);
        var store = Store(knockback: [initial]);
        string path = Write("knockback.json", KnockbackJson("light", 8));
        Initialize(store, path, Write("response.json", ResponseJson("default", 1)));
        string otherDirectory = Path.Combine(_directory, "other");
        Directory.CreateDirectory(otherDirectory);
        string unrelated = Path.Combine(otherDirectory, "knockback.json");
        File.WriteAllText(unrelated, KnockbackJson("light", 99));

        Reload(unrelated);

        Assert.Same(initial, store.GetKnockbackProfile("light"));
    }

    [Fact]
    public void KnockbackReload_MissingMoveReference_RejectsWholeCandidate()
    {
        var initial = Knockback("light", horizontal: 8);
        var move = HitMove("5A", "light");
        var store = Store(moves: [move], knockback: [initial]);
        string path = Write("knockback.json", KnockbackJson("other", 99));
        Initialize(store, path, Write("response.json", ResponseJson("default", 1)));

        Reload(path);

        Assert.Same(initial, store.GetKnockbackProfile("light"));
        Assert.Null(store.GetKnockbackProfile("other"));
    }

    [Fact]
    public void ResponseReload_MissingRequiredProfile_RejectsWholeCandidate()
    {
        var initialDefault = Response("default", 1);
        var initialHitstun = Response("hitstun", 0.5f);
        var store = Store(response: [initialDefault, initialHitstun]);
        string knockback = Write("knockback.json", """{"schema_version":1,"knockback_profiles":[]}""");
        string response = Write("response.json", ResponseJson("default", 2));
        Initialize(store, knockback, response, requiredResponseIds: () => ["default", "hitstun"]);

        Reload(response);

        Assert.Same(initialDefault, store.GetPhysicsResponseProfile("default"));
        Assert.Same(initialHitstun, store.GetPhysicsResponseProfile("hitstun"));
    }

    [Fact]
    public void Shutdown_UnsubscribesFromReloadEvents()
    {
        var initial = Knockback("light", horizontal: 8);
        var store = Store(knockback: [initial]);
        string path = Write("knockback.json", KnockbackJson("light", 12));
        Initialize(store, path, Write("response.json", ResponseJson("default", 1)));
        _service!.Shutdown();

        Reload(path);

        Assert.Same(initial, store.GetKnockbackProfile("light"));
    }

    [Fact]
    public void DuplicateNotificationsForSameContent_CreateOneLogicalTransition()
    {
        var initial = Knockback("light", horizontal: 8);
        var store = Store(knockback: [initial]);
        string path = Write("knockback.json", KnockbackJson("light", 12));
        Initialize(store, path, Write("response.json", ResponseJson("default", 1)));
        ulong before = store.PhysicsDatasetVersion;

        EventBus.Instance.Publish(new DataReloadedEvent(path));
        EventBus.Instance.Publish(new DataReloadedEvent(path));
        EventBus.Instance.ProcessFrame();

        Assert.Equal(before + 1, store.PhysicsDatasetVersion);
        Assert.Equal(12, store.GetKnockbackProfile("light")!.Horizontal);
    }

    [Fact]
    public void Reload_PublishesProfilesVersionAndIdentityAsOneBaseline()
    {
        var store = Store(knockback: [Knockback("light", horizontal: 8)]);
        string path = Write("knockback.json", KnockbackJson("light", 12));
        Initialize(store, path, Write("response.json", ResponseJson("default", 1)));
        DataContentIdentity expected = DataContentIdentity.FromBytes(File.ReadAllBytes(path));

        Reload(path);

        PhysicsDatasetBaseline baseline = store.CapturePhysicsBaseline();
        Assert.Equal(12, Assert.Single(baseline.KnockbackProfiles).Horizontal);
        Assert.Equal(expected, baseline.KnockbackIdentity);
        Assert.Equal(store.PhysicsDatasetVersion, baseline.DatasetVersion);
    }

    [Fact]
    public void AuthoredIdentityNotification_DoesNotCommitSecondVersion()
    {
        var initial = Knockback("light", horizontal: 8);
        var store = Store(knockback: [initial]);
        string path = Write("knockback.json", KnockbackJson("light", 12));
        string response = Write("response.json", ResponseJson("default", 1));
        Initialize(store, path, response);
        byte[] bytes = File.ReadAllBytes(path);
        CommittedDocumentRegistry.Observe(path, DataContentIdentity.FromBytes(bytes));
        ulong before = store.PhysicsDatasetVersion;

        Reload(path);

        Assert.Equal(before, store.PhysicsDatasetVersion);
        Assert.Same(initial, store.GetKnockbackProfile("light"));
    }

    private void Initialize(
        DataStore store,
        string knockbackPath,
        string responsePath,
        Func<IReadOnlyCollection<string>>? requiredResponseIds = null)
    {
        _service = new PhysicsProfileHotReloadService(
            knockbackPath, responsePath, requiredResponseIds ?? (() => ["default"]));
        _service.Initialize(store);
    }

    private static void Reload(string path)
    {
        EventBus.Instance.Publish(new DataReloadedEvent(path));
        EventBus.Instance.ProcessFrame();
    }

    private string Write(string name, string content)
    {
        string path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static DataStore Store(
        MoveDefinition[]? moves = null,
        KnockbackProfile[]? knockback = null,
        PhysicsResponseProfile[]? response = null) =>
        new(moves ?? [], knockbackProfiles: knockback, physicsResponseProfiles: response);

    private static MoveDefinition HitMove(string id, string profileId) => new()
    {
        MoveId = id,
        KnockbackProfileId = profileId,
        CollisionFrames =
        [
            new CollisionFrameDefinition
            {
                Frame = 1,
                Hitboxes = [new CollisionBoxDefinition { BoxId = "hit", Width = 1, Height = 1 }]
            }
        ]
    };

    private static KnockbackProfile Knockback(string id, float horizontal) => new()
    {
        ProfileId = id,
        Horizontal = horizontal,
        Vertical = 3,
        Gravity = 1,
        Friction = 0.2f
    };

    private static PhysicsResponseProfile Response(string id, float multiplier) => new()
    {
        ProfileId = id,
        KnockbackMultiplier = multiplier,
        GravityScale = 1,
        Friction = 0.5f,
        AirFriction = 0.2f
    };

    private static string KnockbackJson(string id, float horizontal) =>
        $$"""{"schema_version":1,"knockback_profiles":[{"profile_id":"{{id}}","horizontal":{{horizontal}},"vertical":3,"gravity":1,"friction":0.2}]}""";

    private static string ResponseJson(string id, float multiplier) =>
        $$"""{"schema_version":1,"physics_response_profiles":[{"profile_id":"{{id}}","knockback_multiplier":{{multiplier}},"gravity_scale":1,"friction":0.5,"air_friction":0.2,"participates_in_hitstop":true}]}""";
}
