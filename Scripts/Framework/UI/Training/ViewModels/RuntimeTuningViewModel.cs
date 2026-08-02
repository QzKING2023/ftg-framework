#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FTG_Framework.Data;

namespace FTG_Framework.UI.Training.ViewModels;

public sealed class RuntimeTuningViewModel : IDisposable
{
    private readonly IRuntimeTuningService _service;
    private readonly RuntimeTuningSessionAuthority _sessions;
    private Dictionary<string, string> _baseline = new(StringComparer.Ordinal);
    private Dictionary<string, string> _candidate = new(StringComparer.Ordinal);
    private ulong _sessionToken;
    private bool _disposed;

    public RuntimeTuningViewModel(IRuntimeTuningService service, RuntimeTuningSessionAuthority sessions)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
    }

    public RuntimeTuningSelection? Selection { get; private set; }
    public IReadOnlyDictionary<string, string> BaselineFields =>
        new ReadOnlyDictionary<string, string>(_baseline);
    public IReadOnlyDictionary<string, string> CandidateFields =>
        new ReadOnlyDictionary<string, string>(_candidate);
    public string ExpectedIdentity { get; private set; } = string.Empty;
    public ulong ExpectedDatasetVersion { get; private set; }
    public RuntimeTuningCommitResult? LastResult { get; private set; }
    public RuntimeTuningValidationResult Validation { get; private set; } = RuntimeTuningValidationResult.Valid;
    public bool CanApply => !_disposed && Selection is not null && _sessions.IsCurrent(_sessionToken);

    public void Open(RuntimeTuningSelection selection)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _sessionToken = _sessions.Capture();
        InstallBaseline(_service.Load(selection), preserveCandidate: false);
    }

    public void EditField(string field, string value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        _candidate[field] = value ?? string.Empty;
        Validation = BuildRequest() is { } request ? _service.Validate(request) : RuntimeTuningValidationResult.Valid;
    }

    public RuntimeTuningCommitResult Apply()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_sessions.IsCurrent(_sessionToken))
            return LastResult = new RuntimeTuningCommitResult(RuntimeTuningCommitStatus.Cancelled,
                ExpectedIdentity, Diagnostic: "Tuning session is no longer current.");
        RuntimeTuningCommitRequest request = BuildRequest()
            ?? throw new InvalidOperationException("[UI] Runtime tuning selection has not been opened.");
        Validation = _service.Validate(request);
        if (!Validation.Success)
            return LastResult = new RuntimeTuningCommitResult(RuntimeTuningCommitStatus.ValidationFailed,
                ExpectedIdentity, Errors: Validation.Errors);
        if (!_sessions.IsCurrent(_sessionToken))
            return LastResult = new RuntimeTuningCommitResult(RuntimeTuningCommitStatus.Cancelled,
                ExpectedIdentity, Diagnostic: "Tuning session became obsolete before commit.");
        RuntimeTuningCommitResult result = _service.Commit(request);
        LastResult = result;
        if (result.Committed && Selection is not null)
            InstallBaseline(_service.Load(Selection), preserveCandidate: false);
        return result;
    }

    public void Reload()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Selection is null) return;
        InstallBaseline(_service.Load(Selection), preserveCandidate: false);
    }

    public void Reapply()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Selection is null) return;
        InstallBaseline(_service.Load(Selection), preserveCandidate: true);
    }

    public void Invalidate()
    {
        if (_disposed) return;
        _sessions.Invalidate();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _sessions.Invalidate();
        _disposed = true;
    }

    private RuntimeTuningCommitRequest? BuildRequest() => Selection is null ? null :
        new RuntimeTuningCommitRequest(Selection,
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(_candidate, StringComparer.Ordinal)),
            ExpectedIdentity, ExpectedDatasetVersion, _sessionToken);

    private void InstallBaseline(RuntimeTuningBaseline baseline, bool preserveCandidate)
    {
        Dictionary<string, string>? prior = preserveCandidate
            ? new Dictionary<string, string>(_candidate, StringComparer.Ordinal) : null;
        Selection = baseline.Selection;
        _baseline = new Dictionary<string, string>(baseline.Fields, StringComparer.Ordinal);
        _candidate = new Dictionary<string, string>(_baseline, StringComparer.Ordinal);
        if (prior is not null)
            foreach ((string key, string value) in prior) _candidate[key] = value;
        ExpectedIdentity = baseline.ContentIdentity;
        ExpectedDatasetVersion = baseline.DatasetVersion;
        Validation = RuntimeTuningValidationResult.Valid;
        LastResult = null;
    }
}
