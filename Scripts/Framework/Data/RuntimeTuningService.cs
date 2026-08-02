#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace FTG_Framework.Data;

public enum RuntimeTuningFaultPoint
{
    BeforeIdentityRead,
    BeforeSerialize,
    BeforeStageFlush,
    AfterStage,
    AfterStagedValidation,
    BeforeCommit,
    BeforeReplace,
    DuringPostCommitCleanup
}

internal sealed class RuntimeTuningService : IRuntimeTuningService
{
    private readonly DataStore _store;
    private readonly MoveDatasetPersistence _moves;
    private readonly string _moveRoot;
    private readonly string _knockbackPath;
    private readonly string _responsePath;
    private readonly Func<IReadOnlyCollection<string>> _requiredResponseIds;
    private readonly Action<RuntimeTuningFaultPoint>? _fault;
    private readonly Func<ulong, bool>? _isSessionCurrent;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    internal RuntimeTuningService(string moveRoot, DataStore store, string knockbackPath,
        string responsePath, Func<IReadOnlyCollection<string>> requiredResponseIds,
        Action<RuntimeTuningFaultPoint>? fault = null, Func<ulong, bool>? isSessionCurrent = null,
        Action<MovePersistenceFaultPoint>? moveFault = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _moveRoot = Path.GetFullPath(moveRoot);
        _moves = new MoveDatasetPersistence(_moveRoot, store, moveFault);
        _knockbackPath = Path.GetFullPath(knockbackPath);
        _responsePath = Path.GetFullPath(responsePath);
        _requiredResponseIds = requiredResponseIds ?? throw new ArgumentNullException(nameof(requiredResponseIds));
        _fault = fault;
        _isSessionCurrent = isSessionCurrent;
        ObserveInitialPhysicsIdentity(PhysicsDocumentKind.Knockback, _knockbackPath);
        ObserveInitialPhysicsIdentity(PhysicsDocumentKind.Response, _responsePath);
    }

    public RuntimeTuningBaseline Load(RuntimeTuningSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return selection.Kind switch
        {
            RuntimeTuningSelectionKind.Move => LoadMove(selection),
            RuntimeTuningSelectionKind.KnockbackProfile => LoadKnockback(selection),
            RuntimeTuningSelectionKind.PhysicsResponseProfile => LoadResponse(selection),
            _ => throw new ArgumentOutOfRangeException(nameof(selection))
        };
    }

    public RuntimeTuningValidationResult Validate(RuntimeTuningCommitRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new List<RuntimeTuningValidationError>();
        switch (request.Selection.Kind)
        {
            case RuntimeTuningSelectionKind.Move: ValidateMoveFields(request.Fields, errors); break;
            case RuntimeTuningSelectionKind.KnockbackProfile:
                ValidateFloatFields(request.Fields, errors, "horizontal", "vertical", "gravity", "friction"); break;
            case RuntimeTuningSelectionKind.PhysicsResponseProfile:
                ValidateFloatFields(request.Fields, errors, "knockback_multiplier", "gravity_scale", "friction", "air_friction");
                if (!request.Fields.TryGetValue("participates_in_hitstop", out string? flag) || !bool.TryParse(flag, out _))
                    errors.Add(Error("participates_in_hitstop", flag, "Expected true or false."));
                break;
        }
        return errors.Count == 0 ? RuntimeTuningValidationResult.Valid :
            new RuntimeTuningValidationResult(new ReadOnlyCollection<RuntimeTuningValidationError>(errors));
    }

    public RuntimeTuningCommitResult Commit(RuntimeTuningCommitRequest request)
    {
        RuntimeTuningValidationResult validation = Validate(request);
        if (!validation.Success)
            return new(RuntimeTuningCommitStatus.ValidationFailed, request.ExpectedIdentity,
                Errors: validation.Errors);
        try
        {
            return request.Selection.Kind == RuntimeTuningSelectionKind.Move
                ? CommitMove(request)
                : CommitPhysics(request);
        }
        catch (FormatException ex) { return new(RuntimeTuningCommitStatus.ValidationFailed, request.ExpectedIdentity, Diagnostic: ex.Message); }
        catch (IOException ex) { return new(RuntimeTuningCommitStatus.IoFailure, request.ExpectedIdentity, Diagnostic: ex.Message); }
        catch (UnauthorizedAccessException ex) { return new(RuntimeTuningCommitStatus.IoFailure, request.ExpectedIdentity, Diagnostic: ex.Message); }
    }

    private RuntimeTuningBaseline LoadMove(RuntimeTuningSelection selection)
    {
        string path = Path.Combine(_moveRoot, selection.DocumentIdentifier + ".json");
        MoveDatasetBaseline baseline = RefreshMoveBaseline(selection.DocumentIdentifier, path);
        MoveDefinition move = baseline.Moves.Single(item => string.Equals(item.MoveId, selection.ItemId, StringComparison.Ordinal));
        return new(selection, MoveFields(move), baseline.ContentIdentity.Sha256, baseline.DatasetVersion);
    }

    private MoveDatasetBaseline RefreshMoveBaseline(string documentIdentifier, string path)
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            MoveDatasetBaseline baseline = _store.CaptureMoveBaseline();
            MoveDatasetDocument document = _moves.Load(documentIdentifier);
            if (string.IsNullOrEmpty(baseline.ContentIdentity.Sha256))
            {
                _store.ObserveMoveContentIdentity(document.ContentIdentity);
                return _store.CaptureMoveBaseline();
            }
            if (baseline.ContentIdentity == document.ContentIdentity)
                return baseline;

            ValidateMoveReferences(document.Moves);
            bool published = _store.TryCommitMoveDataset(document.Moves.ToArray(), baseline.DatasetVersion, () =>
                CanonicalDestinationCoordinator.Execute(path, () =>
                    _moves.Load(documentIdentifier).ContentIdentity == document.ContentIdentity),
                document.ContentIdentity);
            if (published)
                return _store.CaptureMoveBaseline();
        }
        throw new IOException("[Data] Move dataset changed repeatedly during reload.");
    }

    private void ValidateMoveReferences(IReadOnlyList<MoveDefinition> moves)
    {
        for (int index = 0; index < moves.Count; index++)
        {
            string profileId = moves[index].KnockbackProfileId ?? string.Empty;
            if (_store.GetKnockbackProfile(profileId) is not null) continue;
            throw new MoveDatasetFormatException($"moves[{index}].knockback_profile_id", profileId,
                "Referenced KnockbackProfile does not exist.", "Select an existing knockback profile.");
        }
    }

    private RuntimeTuningBaseline LoadKnockback(RuntimeTuningSelection selection)
    {
        PhysicsDatasetBaseline baseline = _store.CapturePhysicsBaseline();
        KnockbackProfile profile = baseline.KnockbackProfiles.Single(item => item.ProfileId == selection.ItemId);
        return new(selection, KnockbackFields(profile), baseline.KnockbackIdentity.Sha256, baseline.DatasetVersion);
    }

    private RuntimeTuningBaseline LoadResponse(RuntimeTuningSelection selection)
    {
        PhysicsDatasetBaseline baseline = _store.CapturePhysicsBaseline();
        PhysicsResponseProfile profile = baseline.ResponseProfiles.Single(item => item.ProfileId == selection.ItemId);
        return new(selection, ResponseFields(profile), baseline.ResponseIdentity.Sha256, baseline.DatasetVersion);
    }

    private RuntimeTuningCommitResult CommitMove(RuntimeTuningCommitRequest request)
    {
        MoveDatasetDocument current = _moves.Load(request.Selection.DocumentIdentifier);
        if (!string.Equals(current.ContentIdentity.Sha256, request.ExpectedIdentity, StringComparison.Ordinal))
            return Conflict(request.ExpectedIdentity, current.ContentIdentity.Sha256);
        MoveAuthoringCandidate candidate = MoveAuthoringCandidate.FromDocument(current)
            .EditMove(request.Selection.ItemId, move => ApplyMove(move, request.Fields));
        MoveSaveResult result = _moves.Save(request.Selection.DocumentIdentifier, candidate,
            finalCommitGuard: () => IsSessionCurrent(request.SessionToken));
        return result.Status switch
        {
            MoveSaveStatus.Succeeded => new(result.Diagnostic is null ? RuntimeTuningCommitStatus.Succeeded : RuntimeTuningCommitStatus.CommittedWithDiagnostic,
                request.ExpectedIdentity, result.CurrentIdentity?.Sha256, result.Diagnostic),
            MoveSaveStatus.Conflict => Conflict(request.ExpectedIdentity, result.CurrentIdentity?.Sha256),
            MoveSaveStatus.ValidationFailed => new(RuntimeTuningCommitStatus.ValidationFailed, request.ExpectedIdentity,
                Errors: result.Errors?.Select(e => new RuntimeTuningValidationError(e.FieldPath, e.RejectedValue, e.Message, e.RecoveryAction)).ToArray()),
            MoveSaveStatus.Cancelled => new(RuntimeTuningCommitStatus.Cancelled, request.ExpectedIdentity,
                result.CurrentIdentity?.Sha256, result.Diagnostic),
            _ => new(RuntimeTuningCommitStatus.IoFailure, request.ExpectedIdentity, Diagnostic: result.Diagnostic)
        };
    }

    private RuntimeTuningCommitResult CommitPhysics(RuntimeTuningCommitRequest request)
    {
        bool knockback = request.Selection.Kind == RuntimeTuningSelectionKind.KnockbackProfile;
        string path = knockback ? _knockbackPath : _responsePath;
        _fault?.Invoke(RuntimeTuningFaultPoint.BeforeIdentityRead);
        byte[] currentBytes = File.ReadAllBytes(path);
        string currentHash = DataContentIdentity.FromBytes(currentBytes).Sha256;
        if (!string.Equals(currentHash, request.ExpectedIdentity, StringComparison.Ordinal))
            return Conflict(request.ExpectedIdentity, currentHash);
        PhysicsDatasetBaseline baseline = _store.CapturePhysicsBaseline();
        if (baseline.DatasetVersion != request.ExpectedDatasetVersion)
            return Conflict(request.ExpectedIdentity, currentHash);
        KnockbackProfile[] knockbacks = baseline.KnockbackProfiles.ToArray();
        PhysicsResponseProfile[] responses = baseline.ResponseProfiles.ToArray();
        if (knockback)
        {
            knockbacks = knockbacks.Select(item => item.ProfileId == request.Selection.ItemId
                ? ApplyKnockback(item, request.Fields) : item).ToArray();
            PhysicsProfileReferenceValidator.ValidateKnockbackProfiles(_store, knockbacks);
        }
        else
        {
            responses = responses.Select(item => item.ProfileId == request.Selection.ItemId
                ? ApplyResponse(item, request.Fields) : item).ToArray();
            PhysicsProfileReferenceValidator.ValidateResponseProfiles(responses, _requiredResponseIds());
        }
        _fault?.Invoke(RuntimeTuningFaultPoint.BeforeSerialize);
        byte[] bytes = SerializePhysics(knockback, knockbacks, responses);
        string staged = PhysicsDataPersistence.StageSameDirectory(path, bytes,
            () => _fault?.Invoke(RuntimeTuningFaultPoint.BeforeStageFlush));
        bool replaced = false;
        try
        {
            _fault?.Invoke(RuntimeTuningFaultPoint.AfterStage);
            byte[] stagedBytes = File.ReadAllBytes(staged);
            if (!stagedBytes.AsSpan().SequenceEqual(bytes)) throw new IOException("[Data] Staged physics bytes changed.");
            if (knockback) PhysicsDataLoader.LoadKnockbackProfilesFromJson(Encoding.UTF8.GetString(stagedBytes));
            else PhysicsDataLoader.LoadPhysicsResponseProfilesFromJson(Encoding.UTF8.GetString(stagedBytes));
            _fault?.Invoke(RuntimeTuningFaultPoint.AfterStagedValidation);
            var identity = DataContentIdentity.FromBytes(bytes);
            _fault?.Invoke(RuntimeTuningFaultPoint.BeforeCommit);
            bool won = _store.TryCommitPhysicsDataset(knockbacks, responses, request.ExpectedDatasetVersion, () =>
                CanonicalDestinationCoordinator.Execute(path, () =>
                {
                    _fault?.Invoke(RuntimeTuningFaultPoint.BeforeReplace);

                    // No injectable/user code may run after these final boundary checks.
                    byte[] finalStagedBytes = File.ReadAllBytes(staged);
                    if (!finalStagedBytes.AsSpan().SequenceEqual(bytes))
                        throw new IOException("[Data] Staged physics bytes changed after validation.");
                    string observed = DataContentIdentity.FromBytes(File.ReadAllBytes(path)).Sha256;
                    if (!string.Equals(observed, request.ExpectedIdentity, StringComparison.Ordinal)) return false;
                    if (!IsSessionCurrent(request.SessionToken)) return false;
                    PhysicsDataPersistence.ReplaceStaged(staged, path);
                    replaced = true;
                    return true;
                }), knockback ? PhysicsDocumentKind.Knockback : PhysicsDocumentKind.Response, identity);
            if (!won)
            {
                if (!IsSessionCurrent(request.SessionToken))
                    return new(RuntimeTuningCommitStatus.Cancelled, request.ExpectedIdentity,
                        Diagnostic: "Tuning session became obsolete before commit.");
                return Conflict(request.ExpectedIdentity, DataContentIdentity.FromBytes(File.ReadAllBytes(path)).Sha256);
            }
            CommittedDocumentRegistry.Observe(path, identity);
            try
            {
                _fault?.Invoke(RuntimeTuningFaultPoint.DuringPostCommitCleanup);
                return new(RuntimeTuningCommitStatus.Succeeded, request.ExpectedIdentity, identity.Sha256);
            }
            catch (Exception ex)
            {
                return new(RuntimeTuningCommitStatus.CommittedWithDiagnostic, request.ExpectedIdentity,
                    identity.Sha256, $"[Data] Commit succeeded; cleanup diagnostic: {ex.Message}");
            }
        }
        finally
        {
            if (!replaced) PhysicsDataPersistence.CleanupOwnedStaging(staged);
        }
    }

    private static MoveAuthoringMove ApplyMove(MoveAuthoringMove move, IReadOnlyDictionary<string, string> f) => move with
    {
        Startup = Int(f, "startup"), Active = Int(f, "active"), Recovery = Int(f, "recovery"),
        HitAdvantage = Int(f, "hit_advantage"), BlockAdvantage = Int(f, "block_advantage"), Damage = Int(f, "damage"),
        ChainRepeatable = bool.Parse(f["chain_repeatable"]), KnockbackProfileId = f["knockback_profile_id"],
        MoveName = f.TryGetValue("move_name", out string? name) ? name : null
    };
    private static KnockbackProfile ApplyKnockback(KnockbackProfile p, IReadOnlyDictionary<string, string> f) => new()
    { ProfileId = p.ProfileId, Horizontal = Float(f,"horizontal"), Vertical = Float(f,"vertical"), Gravity = Float(f,"gravity"), Friction = Float(f,"friction") };
    private static PhysicsResponseProfile ApplyResponse(PhysicsResponseProfile p, IReadOnlyDictionary<string, string> f) => new()
    { ProfileId = p.ProfileId, KnockbackMultiplier = Float(f,"knockback_multiplier"), GravityScale = Float(f,"gravity_scale"), Friction = Float(f,"friction"), AirFriction = Float(f,"air_friction"), ParticipatesInHitstop = bool.Parse(f["participates_in_hitstop"]) };

    private static IReadOnlyDictionary<string,string> MoveFields(MoveDefinition m) => RuntimeTuningFields.Freeze(new Dictionary<string,string>
    { ["startup"]=S(m.Startup), ["active"]=S(m.Active), ["recovery"]=S(m.Recovery), ["hit_advantage"]=S(m.HitAdvantage), ["block_advantage"]=S(m.BlockAdvantage), ["damage"]=S(m.Damage), ["chain_repeatable"]=m.ChainRepeatable.ToString(), ["knockback_profile_id"]=m.KnockbackProfileId??string.Empty, ["move_name"]=m.MoveName??string.Empty });
    private static IReadOnlyDictionary<string,string> KnockbackFields(KnockbackProfile p) => RuntimeTuningFields.Freeze(new Dictionary<string,string>
    { ["horizontal"]=S(p.Horizontal), ["vertical"]=S(p.Vertical), ["gravity"]=S(p.Gravity), ["friction"]=S(p.Friction) });
    private static IReadOnlyDictionary<string,string> ResponseFields(PhysicsResponseProfile p) => RuntimeTuningFields.Freeze(new Dictionary<string,string>
    { ["knockback_multiplier"]=S(p.KnockbackMultiplier), ["gravity_scale"]=S(p.GravityScale), ["friction"]=S(p.Friction), ["air_friction"]=S(p.AirFriction), ["participates_in_hitstop"]=p.ParticipatesInHitstop.ToString() });
    private static byte[] SerializePhysics(bool knockback, KnockbackProfile[] k, PhysicsResponseProfile[] r)
    {
        object document = knockback
            ? new Dictionary<string,object> { ["schema_version"] = 1, ["knockback_profiles"] = k }
            : new Dictionary<string,object> { ["schema_version"] = 1, ["physics_response_profiles"] = r };
        return new UTF8Encoding(false).GetBytes(JsonSerializer.Serialize(document, JsonOptions));
    }
    private static void ValidateMoveFields(IReadOnlyDictionary<string,string> f, List<RuntimeTuningValidationError> e)
    { foreach(string key in new[]{"startup","active","recovery","hit_advantage","block_advantage","damage"}) if(!f.TryGetValue(key,out string? v)||!int.TryParse(v,NumberStyles.Integer,CultureInfo.InvariantCulture,out _)) e.Add(Error(key,v,"Expected an Int32 value.")); if(!f.TryGetValue("chain_repeatable",out string? b)||!bool.TryParse(b,out _))e.Add(Error("chain_repeatable",b,"Expected true or false.")); if(!f.TryGetValue("knockback_profile_id",out string? id)||string.IsNullOrWhiteSpace(id))e.Add(Error("knockback_profile_id",id,"Profile ID is required.")); }
    private static void ValidateFloatFields(IReadOnlyDictionary<string,string> f,List<RuntimeTuningValidationError> e,params string[] keys)
    { foreach(string key in keys) if(!f.TryGetValue(key,out string? v)||!float.TryParse(v,NumberStyles.Float,CultureInfo.InvariantCulture,out float n)||!float.IsFinite(n)||n<0)e.Add(Error(key,v,"Expected a finite non-negative number.")); }
    private static RuntimeTuningValidationError Error(string key,string? value,string message)=>new(key,value??string.Empty,message,"Enter a valid current-schema value.");
    private static int Int(IReadOnlyDictionary<string,string> f,string key)=>int.Parse(f[key],CultureInfo.InvariantCulture);
    private static float Float(IReadOnlyDictionary<string,string> f,string key)=>float.Parse(f[key],CultureInfo.InvariantCulture);
    private static string S<T>(T value) where T:IFormattable=>value.ToString(null,CultureInfo.InvariantCulture);
    private static RuntimeTuningCommitResult Conflict(string expected,string? current)=>new(RuntimeTuningCommitStatus.Conflict,expected,current,"The canonical document changed; reload or explicitly reapply.");
    private bool IsSessionCurrent(ulong token) => _isSessionCurrent?.Invoke(token) ?? true;
    private void ObserveInitialPhysicsIdentity(PhysicsDocumentKind kind, string path)
    {
        if (File.Exists(path)) _store.ObservePhysicsContentIdentity(kind, DataContentIdentity.FromBytes(File.ReadAllBytes(path)));
    }
}
