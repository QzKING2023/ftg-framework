#nullable enable
using System;
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.UI.Training.ViewModels;

namespace FTG_Framework.UI.Training;

public sealed class ComboDisplayController : IDisposable
{
    private readonly ComboDisplayViewModel _viewModel;
    private Action<HitConnectedEvent>? _hitHandler;
    private Action<MoveBlockedEvent>? _blockHandler;
    private Action<ComboEndedEvent>? _comboEndedHandler;
    private Action<MatchInitializedEvent>? _matchHandler;
    private Action<StateRestoredEvent>? _restoreHandler;
    private Action<ReplayStartedEvent>? _replayStartedHandler;
    private Action<ReplayEndedEvent>? _replayEndedHandler;
    private bool _started;
    private ulong _generation;

    public event Action? StateChanged;

    public ComboDisplayController(ComboDisplayViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    public void Start()
    {
        if (_started) return;
        _generation = checked(_generation + 1UL);
        _started = true;
        BindHandlers(_generation);
    }

    public void Shutdown()
    {
        if (!_started)
        {
            _viewModel.ResetAll();
            return;
        }

        _generation = checked(_generation + 1UL);
        _started = false;
        UnbindHandlers();
        _viewModel.ResetAll();
        StateChanged?.Invoke();
    }

    public void Dispose() => Shutdown();

    private void BindHandlers(ulong token)
    {
        _hitHandler = hit => HandleHit(token, hit);
        _blockHandler = blocked => HandleBlock(token, blocked);
        _comboEndedHandler = ended => HandleComboEnded(token, ended);
        _matchHandler = _ => ResetLifecycle(token);
        _restoreHandler = _ => ResetLifecycle(token);
        _replayStartedHandler = _ => ResetLifecycle(token);
        _replayEndedHandler = _ => ResetLifecycle(token);
        EventBus.Instance.Subscribe(_hitHandler);
        EventBus.Instance.Subscribe(_blockHandler);
        EventBus.Instance.Subscribe(_comboEndedHandler);
        EventBus.Instance.Subscribe(_matchHandler);
        EventBus.Instance.Subscribe(_restoreHandler);
        EventBus.Instance.Subscribe(_replayStartedHandler);
        EventBus.Instance.Subscribe(_replayEndedHandler);
    }

    private void UnbindHandlers()
    {
        if (_hitHandler is not null) EventBus.Instance.Unsubscribe(_hitHandler);
        if (_blockHandler is not null) EventBus.Instance.Unsubscribe(_blockHandler);
        if (_comboEndedHandler is not null) EventBus.Instance.Unsubscribe(_comboEndedHandler);
        if (_matchHandler is not null) EventBus.Instance.Unsubscribe(_matchHandler);
        if (_restoreHandler is not null) EventBus.Instance.Unsubscribe(_restoreHandler);
        if (_replayStartedHandler is not null) EventBus.Instance.Unsubscribe(_replayStartedHandler);
        if (_replayEndedHandler is not null) EventBus.Instance.Unsubscribe(_replayEndedHandler);
        _hitHandler = null;
        _blockHandler = null;
        _comboEndedHandler = null;
        _matchHandler = null;
        _restoreHandler = null;
        _replayStartedHandler = null;
        _replayEndedHandler = null;
    }

    private void HandleHit(ulong token, HitConnectedEvent hit)
    {
        if (!_started || token != _generation) return;
        if (_viewModel.HandleHit(hit)) StateChanged?.Invoke();
    }

    private void HandleBlock(ulong token, MoveBlockedEvent blocked)
    {
        if (!_started || token != _generation) return;
        if (_viewModel.HandleBlock(blocked)) StateChanged?.Invoke();
    }

    private void HandleComboEnded(ulong token, ComboEndedEvent ended)
    {
        if (!_started || token != _generation) return;
        if (_viewModel.HandleComboEnded(ended)) StateChanged?.Invoke();
    }

    private void ResetLifecycle(ulong token)
    {
        if (!_started || token != _generation) return;
        _generation = checked(_generation + 1UL);
        UnbindHandlers();
        _viewModel.ResetAll();
        StateChanged?.Invoke();
        BindHandlers(_generation);
    }
}
