#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FTG_Framework.Core;
using FTG_Framework.UI.Training.ViewModels;
using Xunit;

namespace FTG_Framework.Tests.UI.Training.ViewModels;

[Collection(EventBusTestCollection.Name)]
public sealed class TrainingSaveLoadViewModelTests : IDisposable
{
    private readonly EventBusTestScope _eventBusScope = new(EventBusResidualState.Queues);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ftg-save-vm-{Guid.NewGuid():N}");

    public TrainingSaveLoadViewModelTests() => Directory.CreateDirectory(_directory);
    public void Dispose()
    {
        _eventBusScope.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    private (TrainingSaveLoadViewModel ViewModel, string Dir) Runtime(int currentFrame = 20)
    {
        var participants = SnapshotParticipantCatalog.Required
            .Select(id => (IStateSnapshotParticipant)new FakeParticipant(id, $"live-{id}"))
            .ToArray();
        var coordinator = StateSnapshotCoordinator.CreateRuntime(EventBus.Instance, participants);
        var service = new TrainingStateService(coordinator, () => currentFrame, () => EventBus.Instance.LifecycleEpoch);
        string dir = Path.Combine(_directory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var viewModel = new TrainingSaveLoadViewModel(
            service,
            name => Path.Combine(dir, name.Replace(" ", "", StringComparison.Ordinal) + ".json"),
            () => Directory.Exists(dir)
                ? Directory.GetFiles(dir, "*.json").Select(Path.GetFileNameWithoutExtension).ToArray()
                : Array.Empty<string>(),
            name => name.Replace(" ", "", StringComparison.Ordinal));
        return (viewModel, dir);
    }

    [Fact]
    public void Refresh_ListsExistingSavesSorted()
    {
        (TrainingSaveLoadViewModel viewModel, string dir) = Runtime();
        File.WriteAllText(Path.Combine(dir, "b.json"), "{}");
        File.WriteAllText(Path.Combine(dir, "a.json"), "{}");

        viewModel.Refresh();

        Assert.Equal(new[] { "a", "b" }, viewModel.Saves);
    }

    [Fact]
    public void Select_ExistingSave_InspectsMetadata()
    {
        (TrainingSaveLoadViewModel viewModel, _) = Runtime();
        Assert.True(viewModel.RequestSave("slot"), viewModel.StatusText);

        Assert.True(viewModel.Select("slot"));
        Assert.Equal("slot", viewModel.SelectedSave);
        Assert.NotNull(viewModel.SelectedMetadata);
        Assert.Equal(19, viewModel.SelectedMetadata!.Frame);
        Assert.Equal(5, viewModel.SelectedMetadata.ComponentCount);
        Assert.Contains("Inspected", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_MissingSave_FailsWithActionableError()
    {
        (TrainingSaveLoadViewModel viewModel, _) = Runtime();
        Assert.False(viewModel.Select("nope"));
        Assert.Contains("not found", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RequestSave_NewName_SavesAndRefreshesList()
    {
        (TrainingSaveLoadViewModel viewModel, string dir) = Runtime();
        Assert.False(viewModel.IsBusy);

        Assert.True(viewModel.RequestSave("setup-a"), viewModel.StatusText);

        Assert.Contains("Saved", viewModel.StatusText, StringComparison.Ordinal);
        Assert.Contains(viewModel.Saves, name => name == "setup-a");
        Assert.Single(Directory.GetFiles(dir, "*.json"));
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void RequestSave_ExistingName_RequiresExplicitOverwriteConfirmation()
    {
        (TrainingSaveLoadViewModel viewModel, _) = Runtime();
        Assert.True(viewModel.RequestSave("slot"));
        Assert.False(viewModel.RequestSave("slot"));
        Assert.True(viewModel.HasPendingSave);
        Assert.Equal("slot", viewModel.PendingSaveName);
        Assert.Contains("Overwrite", viewModel.StatusText, StringComparison.Ordinal);

        Assert.True(viewModel.ConfirmPendingSave(), viewModel.StatusText);
        Assert.False(viewModel.HasPendingSave);
    }

    [Fact]
    public void RequestLoad_RequiresConfirmation_ThenRestoresAndResumesFrame()
    {
        (TrainingSaveLoadViewModel viewModel, _) = Runtime(20);
        Assert.True(viewModel.RequestSave("slot"));
        int frameBefore = EventBus.Instance.CurrentFrame;

        Assert.False(viewModel.RequestLoad("slot"));
        Assert.True(viewModel.HasPendingLoad);
        Assert.Equal("slot", viewModel.PendingLoadName);
        Assert.True(viewModel.ConfirmPendingLoad(), viewModel.StatusText);

        Assert.Contains("Restored", viewModel.StatusText, StringComparison.Ordinal);
        Assert.Equal(20, EventBus.Instance.CurrentFrame);
        Assert.False(viewModel.HasPendingLoad);
    }

    [Fact]
    public void ConfirmPendingLoad_WithoutPending_ReportsError()
    {
        (TrainingSaveLoadViewModel viewModel, _) = Runtime();
        Assert.False(viewModel.ConfirmPendingLoad());
        Assert.Contains("no save load is pending", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_CorruptedSave_ExposesActionableValidationError()
    {
        (TrainingSaveLoadViewModel viewModel, string dir) = Runtime();
        Assert.True(viewModel.RequestSave("slot"));
        string path = Path.Combine(dir, "slot.json");
        File.WriteAllText(path, "broken");

        Assert.False(viewModel.RequestLoad("slot"));
        Assert.True(viewModel.HasPendingLoad);
        Assert.False(viewModel.ConfirmPendingLoad());
        Assert.Contains("JSON", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void RequestSave_CanonicalizedName_DetectsExistingSaveAndRequiresConfirmation()
    {
        (TrainingSaveLoadViewModel viewModel, _) = Runtime();
        Assert.True(viewModel.RequestSave("my save"), viewModel.StatusText);
        Assert.False(viewModel.RequestSave("my save"));

        Assert.True(viewModel.HasPendingSave);
        Assert.Equal("mysave", viewModel.PendingSaveName);
        Assert.Contains("Overwrite", viewModel.StatusText, StringComparison.Ordinal);
        Assert.True(viewModel.ConfirmPendingSave(), viewModel.StatusText);
    }

    [Fact]
    public void RequestSave_NewName_ClearsStalePendingOverwrite()
    {
        (TrainingSaveLoadViewModel viewModel, _) = Runtime();
        Assert.True(viewModel.RequestSave("slot"), viewModel.StatusText);
        Assert.False(viewModel.RequestSave("slot"));
        Assert.True(viewModel.HasPendingSave);

        Assert.True(viewModel.RequestSave("other"), viewModel.StatusText);
        Assert.False(viewModel.HasPendingSave);
        Assert.False(viewModel.ConfirmPendingSave());
        Assert.Contains("no save overwrite is pending", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CancelPending_ClearsBothPendingActions()
    {
        (TrainingSaveLoadViewModel viewModel, _) = Runtime();
        Assert.True(viewModel.RequestSave("slot"));
        viewModel.CancelPending();
        Assert.False(viewModel.HasPendingSave);
        Assert.False(viewModel.HasPendingLoad);
        Assert.Contains("canceled", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ViewModel_WithoutService_ReportsUnavailableOnSave()
    {
        var viewModel = new TrainingSaveLoadViewModel(null);
        Assert.False(viewModel.RequestSave("x"));
        Assert.Contains("unavailable", viewModel.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeParticipant : IStateSnapshotParticipant
    {
        private readonly SnapshotReference<FakeDto> _slot;
        public FakeParticipant(string discriminator, string value)
        {
            Discriminator = discriminator;
            _slot = new SnapshotReference<FakeDto>(new FakeDto(value));
        }

        public string Discriminator { get; }
        public int CodecVersion => 1;
        public string LiveValue { get => _slot.Value.Value; set => _slot.Value = new FakeDto(value); }
        public SnapshotComponent Capture(int frame, ulong epoch) => new(Discriminator, CodecVersion,
            System.Text.Json.JsonSerializer.Serialize(_slot.Value));
        public IPreparedSnapshotComponent Prepare(SnapshotComponent component, SnapshotPrepareContext context)
        {
            FakeDto decoded = System.Text.Json.JsonSerializer.Deserialize<FakeDto>(component.Payload)
                ?? throw new SnapshotPrepareException(Discriminator, "null");
            return PreparedSnapshotComponent.Create(Discriminator, _slot, decoded);
        }

        private sealed record FakeDto(string Value);
    }
}
