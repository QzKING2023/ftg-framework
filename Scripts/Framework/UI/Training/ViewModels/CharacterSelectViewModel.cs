#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;

namespace FTG_Framework.UI.Training.ViewModels;

public sealed class CharacterSelectViewModel
{
    private readonly IDataStore _dataStore;

    public CharacterSelectViewModel(IDataStore dataStore)
    {
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        Roster = _dataStore.GetAllCharacters();

        if (Roster.Count == 0)
            FrameworkLog.Info?.Invoke("[Data] Character roster is empty — character select will be inert.");

        // Default: select first character in roster for both players
        if (Roster.Count > 0)
        {
            P1SelectedIndex = 0;
            P2SelectedIndex = 0;
        }
    }

    public IReadOnlyList<CharacterDefinition> Roster { get; }

    public int P1SelectedIndex { get; private set; } = -1;
    public int P2SelectedIndex { get; private set; } = -1;

    public string? P1CharacterName =>
        P1SelectedIndex >= 0 && P1SelectedIndex < Roster.Count
            ? Roster[P1SelectedIndex].DisplayName
            : null;

    public string? P2CharacterName =>
        P2SelectedIndex >= 0 && P2SelectedIndex < Roster.Count
            ? Roster[P2SelectedIndex].DisplayName
            : null;

    public string? P1ConfirmedCharacterId { get; private set; }
    public string? P2ConfirmedCharacterId { get; private set; }

    public bool P1Locked => P1ConfirmedCharacterId is not null;
    public bool P2Locked => P2ConfirmedCharacterId is not null;
    public bool BothReady => P1Locked && P2Locked;

    public void BrowseP1Next()
    {
        if (Roster.Count == 0) return;
        P1SelectedIndex = (P1SelectedIndex + 1) % Roster.Count;
    }

    public void BrowseP1Previous()
    {
        if (Roster.Count == 0) return;
        P1SelectedIndex = (P1SelectedIndex - 1 + Roster.Count) % Roster.Count;
    }

    public void BrowseP2Next()
    {
        if (Roster.Count == 0) return;
        P2SelectedIndex = (P2SelectedIndex + 1) % Roster.Count;
    }

    public void BrowseP2Previous()
    {
        if (Roster.Count == 0) return;
        P2SelectedIndex = (P2SelectedIndex - 1 + Roster.Count) % Roster.Count;
    }

    public bool ConfirmP1()
    {
        if (Roster.Count == 0) return false;
        if (P1SelectedIndex < 0 || P1SelectedIndex >= Roster.Count) return false;
        if (P1Locked) return false;

        P1ConfirmedCharacterId = Roster[P1SelectedIndex].CharacterId;
        EventBus.Instance.Publish(new CharacterSelectedEvent(1, P1ConfirmedCharacterId));
        TryFinalize();
        return true;
    }

    public bool ConfirmP2()
    {
        if (Roster.Count == 0) return false;
        if (P2SelectedIndex < 0 || P2SelectedIndex >= Roster.Count) return false;
        if (P2Locked) return false;

        P2ConfirmedCharacterId = Roster[P2SelectedIndex].CharacterId;
        EventBus.Instance.Publish(new CharacterSelectedEvent(2, P2ConfirmedCharacterId));
        TryFinalize();
        return true;
    }

    public void CancelP1()
    {
        P1ConfirmedCharacterId = null;
    }

    public void CancelP2()
    {
        P2ConfirmedCharacterId = null;
    }

    public bool TryFinalize()
    {
        if (!BothReady) return false;
        EventBus.Instance.Publish(new MatchInitializedEvent(
            P1ConfirmedCharacterId!,
            P2ConfirmedCharacterId!));
        return true;
    }
}
