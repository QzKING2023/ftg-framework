#nullable enable
using System;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using FTG_Framework.UI.Training.ViewModels;
using Xunit;

namespace FTG_Framework.Tests.UI;

[Collection(EventBusTestCollection.Name)]
public class CharacterSelectViewModelTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new(EventBusResidualState.Queues);
    // -- Roster empty --

    [Fact]
    public void EmptyRoster_BothReadyIsFalse()
    {
        var dataStore = new StubDataStore();
        var vm = new CharacterSelectViewModel(dataStore);
        Assert.False(vm.BothReady);
    }

    [Fact]
    public void EmptyRoster_ConfirmReturnsFalse()
    {
        var dataStore = new StubDataStore();
        var vm = new CharacterSelectViewModel(dataStore);
        Assert.False(vm.ConfirmP1());
        Assert.False(vm.ConfirmP2());
    }

    [Fact]
    public void EmptyRoster_NoEventPublished()
    {
        var dataStore = new StubDataStore();
        var events = EventBusTestHelper.Collect<CharacterSelectedEvent>(() =>
        {
            var vm = new CharacterSelectViewModel(dataStore);
            vm.ConfirmP1();
            vm.ConfirmP2();
        });
        Assert.Empty(events);
    }

    // -- Single character --

    [Fact]
    public void SingleCharacter_BothPlayersDefaultToFirst()
    {
        var dataStore = new StubDataStore();
        dataStore.RegisterCharacter(new CharacterDefinition { CharacterId = "ryu", DisplayName = "Ryu" });
        var vm = new CharacterSelectViewModel(dataStore);

        Assert.Equal(0, vm.P1SelectedIndex);
        Assert.Equal(0, vm.P2SelectedIndex);
        Assert.Equal("Ryu", vm.P1CharacterName);
        Assert.Equal("Ryu", vm.P2CharacterName);
    }

    [Fact]
    public void SingleCharacter_P1ConfirmsAndEventPublished()
    {
        var dataStore = new StubDataStore();
        dataStore.RegisterCharacter(new CharacterDefinition { CharacterId = "ryu", DisplayName = "Ryu" });

        var events = EventBusTestHelper.Collect<CharacterSelectedEvent>(() =>
        {
            var vm = new CharacterSelectViewModel(dataStore);
            vm.ConfirmP1();
        });

        Assert.Single(events);
        Assert.Equal(1, events[0].PlayerSlot);
        Assert.Equal("ryu", events[0].CharacterId);
    }

    [Fact]
    public void SingleCharacter_P2ConfirmsAndEventPublished()
    {
        var dataStore = new StubDataStore();
        dataStore.RegisterCharacter(new CharacterDefinition { CharacterId = "ryu", DisplayName = "Ryu" });

        var events = EventBusTestHelper.Collect<CharacterSelectedEvent>(() =>
        {
            var vm = new CharacterSelectViewModel(dataStore);
            vm.ConfirmP2();
        });

        Assert.Single(events);
        Assert.Equal(2, events[0].PlayerSlot);
        Assert.Equal("ryu", events[0].CharacterId);
    }

    [Fact]
    public void SingleCharacter_BothConfirm_MatchInitializedEventPublished()
    {
        var dataStore = new StubDataStore();
        dataStore.RegisterCharacter(new CharacterDefinition { CharacterId = "ryu", DisplayName = "Ryu" });

        var (charEvents, matchEvents) = EventBusTestHelper.Collect<CharacterSelectedEvent, MatchInitializedEvent>(() =>
        {
            var vm = new CharacterSelectViewModel(dataStore);
            vm.ConfirmP1();
            vm.ConfirmP2();
        });

        Assert.Equal(2, charEvents.Count);
        Assert.Single(matchEvents);
        Assert.Equal("ryu", matchEvents[0].P1CharacterId);
        Assert.Equal("ryu", matchEvents[0].P2CharacterId);
    }

    // -- Browse wrapping --

    [Fact]
    public void BrowseNext_WrapsAround()
    {
        var dataStore = new StubDataStore();
        dataStore.RegisterCharacter(new CharacterDefinition { CharacterId = "a", DisplayName = "A" });
        dataStore.RegisterCharacter(new CharacterDefinition { CharacterId = "b", DisplayName = "B" });
        var vm = new CharacterSelectViewModel(dataStore);

        Assert.Equal(0, vm.P1SelectedIndex);
        vm.BrowseP1Next();
        Assert.Equal(1, vm.P1SelectedIndex);
        vm.BrowseP1Next();
        Assert.Equal(0, vm.P1SelectedIndex); // wrap
    }

    [Fact]
    public void BrowsePrevious_WrapsAround()
    {
        var dataStore = new StubDataStore();
        dataStore.RegisterCharacter(new CharacterDefinition { CharacterId = "a", DisplayName = "A" });
        dataStore.RegisterCharacter(new CharacterDefinition { CharacterId = "b", DisplayName = "B" });
        var vm = new CharacterSelectViewModel(dataStore);

        Assert.Equal(0, vm.P1SelectedIndex);
        vm.BrowseP1Previous();
        Assert.Equal(1, vm.P1SelectedIndex); // wrap
        vm.BrowseP1Previous();
        Assert.Equal(0, vm.P1SelectedIndex);
    }

    [Fact]
    public void BrowseOnEmptyRoster_DoesNotCrash()
    {
        var dataStore = new StubDataStore();
        var vm = new CharacterSelectViewModel(dataStore);

        vm.BrowseP1Next();
        vm.BrowseP1Previous();
        vm.BrowseP2Next();
        vm.BrowseP2Previous();

        Assert.Equal(-1, vm.P1SelectedIndex);
        Assert.Equal(-1, vm.P2SelectedIndex);
    }

    // -- Cancel --

    [Fact]
    public void CancelP1_AfterConfirmation_Unlocks()
    {
        var dataStore = new StubDataStore();
        dataStore.RegisterCharacter(new CharacterDefinition { CharacterId = "ryu", DisplayName = "Ryu" });
        var vm = new CharacterSelectViewModel(dataStore);

        Assert.True(vm.ConfirmP1());
        Assert.True(vm.P1Locked);
        Assert.NotNull(vm.P1ConfirmedCharacterId);

        vm.CancelP1();
        Assert.False(vm.P1Locked);
        Assert.Null(vm.P1ConfirmedCharacterId);
    }

    [Fact]
    public void CancelP2_AfterConfirmation_Unlocks()
    {
        var dataStore = new StubDataStore();
        dataStore.RegisterCharacter(new CharacterDefinition { CharacterId = "ryu", DisplayName = "Ryu" });
        var vm = new CharacterSelectViewModel(dataStore);

        Assert.True(vm.ConfirmP2());
        Assert.True(vm.P2Locked);

        vm.CancelP2();
        Assert.False(vm.P2Locked);
    }

    // -- BothReady toggle --

    [Fact]
    public void BothReady_TrueWhenBothLocked()
    {
        var dataStore = new StubDataStore();
        dataStore.RegisterCharacter(new CharacterDefinition { CharacterId = "ryu", DisplayName = "Ryu" });
        var vm = new CharacterSelectViewModel(dataStore);

        Assert.False(vm.BothReady);
        vm.ConfirmP1();
        Assert.False(vm.BothReady);
        vm.ConfirmP2();
        Assert.True(vm.BothReady);
    }

    [Fact]
    public void BothReady_FalseAfterCancel()
    {
        var dataStore = new StubDataStore();
        dataStore.RegisterCharacter(new CharacterDefinition { CharacterId = "ryu", DisplayName = "Ryu" });
        var vm = new CharacterSelectViewModel(dataStore);

        vm.ConfirmP1();
        vm.ConfirmP2();
        Assert.True(vm.BothReady);

        vm.CancelP1();
        Assert.False(vm.BothReady);
    }

    [Fact]
    public void BothReady_P2RemainsLockedWhenP1Cancels()
    {
        var dataStore = new StubDataStore();
        dataStore.RegisterCharacter(new CharacterDefinition { CharacterId = "ryu", DisplayName = "Ryu" });
        var vm = new CharacterSelectViewModel(dataStore);

        vm.ConfirmP1();
        vm.ConfirmP2();
        vm.CancelP1();

        Assert.False(vm.P1Locked);
        Assert.True(vm.P2Locked);
        Assert.False(vm.BothReady);
    }

    // -- DataStore RegisterCharacter --

    [Fact]
    public void DataStore_RegisterCharacter_ValidRegistration()
    {
        var dataStore = new DataStore(Array.Empty<MoveDefinition>());
        dataStore.RegisterCharacter(new CharacterDefinition { CharacterId = "ryu", DisplayName = "Ryu" });

        var all = dataStore.GetAllCharacters();
        Assert.Single(all);
        Assert.Equal("ryu", all[0].CharacterId);
    }

    [Fact]
    public void DataStore_RegisterCharacter_DuplicateThrows()
    {
        var dataStore = new DataStore(Array.Empty<MoveDefinition>());
        dataStore.RegisterCharacter(new CharacterDefinition { CharacterId = "ryu", DisplayName = "Ryu" });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            dataStore.RegisterCharacter(new CharacterDefinition { CharacterId = "ryu", DisplayName = "Ryu V2" }));
        Assert.Contains("[Data]", ex.Message);
    }

    [Fact]
    public void DataStore_RegisterCharacter_NullThrows()
    {
        var dataStore = new DataStore(Array.Empty<MoveDefinition>());
        Assert.Throws<ArgumentNullException>(() => dataStore.RegisterCharacter(null!));
    }

    [Fact]
    public void DataStore_RegisterCharacter_EmptyIdThrows()
    {
        var dataStore = new DataStore(Array.Empty<MoveDefinition>());
        var ex = Assert.Throws<ArgumentException>(() =>
            dataStore.RegisterCharacter(new CharacterDefinition { CharacterId = "", DisplayName = "X" }));
        Assert.Contains("[Data]", ex.Message);
    }

    [Fact]
    public void DataStore_RegisterCharacter_EmptyDisplayNameThrows()
    {
        var dataStore = new DataStore(Array.Empty<MoveDefinition>());
        var ex = Assert.Throws<ArgumentException>(() =>
            dataStore.RegisterCharacter(new CharacterDefinition { CharacterId = "x", DisplayName = "" }));
        Assert.Contains("[Data]", ex.Message);
    }

    // -- DataStore GetCharacter --

    [Fact]
    public void DataStore_GetCharacter_ReturnsCorrectCharacter()
    {
        var dataStore = new DataStore(Array.Empty<MoveDefinition>());
        dataStore.RegisterCharacter(new CharacterDefinition { CharacterId = "ryu", DisplayName = "Ryu" });

        var c = dataStore.GetCharacter("ryu");
        Assert.NotNull(c);
        Assert.Equal("ryu", c.CharacterId);
        Assert.Equal("Ryu", c.DisplayName);
    }

    [Fact]
    public void DataStore_GetCharacter_UnknownIdReturnsNull()
    {
        var dataStore = new DataStore(Array.Empty<MoveDefinition>());
        Assert.Null(dataStore.GetCharacter("nonexistent"));
    }

    [Fact]
    public void DataStore_GetCharacter_NullReturnsNull()
    {
        var dataStore = new DataStore(Array.Empty<MoveDefinition>());
        Assert.Null(dataStore.GetCharacter(null!));
    }

    // -- DataStore GetAllCharacters --

    [Fact]
    public void DataStore_GetAllCharacters_ReturnsAllRegistered()
    {
        var dataStore = new DataStore(Array.Empty<MoveDefinition>());
        dataStore.RegisterCharacter(new CharacterDefinition { CharacterId = "a", DisplayName = "A" });
        dataStore.RegisterCharacter(new CharacterDefinition { CharacterId = "b", DisplayName = "B" });

        var all = dataStore.GetAllCharacters();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void DataStore_GetAllCharacters_EmptyWhenNoneRegistered()
    {
        var dataStore = new DataStore(Array.Empty<MoveDefinition>());
        Assert.Empty(dataStore.GetAllCharacters());
    }
    public void Dispose() => _eventBusScope.Dispose();
}
