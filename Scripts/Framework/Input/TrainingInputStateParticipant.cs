#nullable enable
using System;
using System.Text.Json;
using FTG_Framework.Core;

namespace FTG_Framework.Input;

/// <summary>
/// Snapshot participant for the training-input component (library, assignment,
/// selection, playback session). Capture serializes the immutable DTO; Prepare
/// decodes and fully validates the payload through the service so Commit's
/// install is a no-fail reference/field swap (AD-20).
/// </summary>
internal sealed class TrainingInputStateParticipant : IStateSnapshotParticipant
{
    private readonly TrainingInputService _service;
    private readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = false };

    internal TrainingInputStateParticipant(TrainingInputService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public string Discriminator => SnapshotParticipantCatalog.TrainingInput;
    public int CodecVersion => 1;

    public SnapshotComponent Capture(int frame, ulong epoch) => new(
        Discriminator, CodecVersion, JsonSerializer.Serialize(_service.CaptureTrainingState(), _options));

    public IPreparedSnapshotComponent Prepare(SnapshotComponent component, SnapshotPrepareContext context)
    {
        TrainingInputRuntimeSnapshot decoded;
        try
        {
            decoded = JsonSerializer.Deserialize<TrainingInputRuntimeSnapshot>(component.Payload, _options)
                ?? throw new SnapshotPrepareException(Discriminator, "Codec returned null.");
        }
        catch (JsonException ex)
        {
            throw new SnapshotPrepareException(Discriminator, "Invalid component payload.", ex);
        }
        TrainingInputService.PreparedTrainingInputState prepared = _service.PrepareTrainingState(decoded, context);
        return PreparedSnapshotComponent.CreateOwnerSwap(Discriminator, prepared, _service.InstallTrainingState);
    }
}
