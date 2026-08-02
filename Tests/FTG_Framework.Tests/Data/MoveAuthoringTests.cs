#nullable enable
using System.Text;
using FTG_Framework.Data;
using Xunit;

namespace FTG_Framework.Tests;

public sealed class MoveAuthoringTests
{
    private const string ValidJson = """
    {"schema_version":1,"moves":[{"move_id":"5A","startup":1,"active":2,"recovery":3,"hit_advantage":4,"block_advantage":-1,"damage":10,"chain_repeatable":false,"knockback_profile_id":"light","cancel_windows":[],"collision_frames":[]}]}
    """;

    [Fact]
    public void ParseAndSerialize_CurrentSchema_RoundTripsCanonically()
    {
        var document = MoveDatasetCodec.Parse(ValidJson);
        var bytes = MoveDatasetCodec.Serialize(document);
        var roundTrip = MoveDatasetCodec.Parse(Encoding.UTF8.GetString(bytes));

        Assert.Equal(1, roundTrip.SchemaVersion);
        Assert.Equal("5A", Assert.Single(roundTrip.Moves).MoveId);
        Assert.False(bytes.Take(Encoding.UTF8.GetPreamble().Length).SequenceEqual(Encoding.UTF8.GetPreamble()));
        Assert.Equal(document.Moves, roundTrip.Moves, MoveDefinitionSemanticComparer.Instance);
    }

    [Theory]
    [InlineData("{\"moves\":[]}", "schema_version")]
    [InlineData("{\"schema_version\":2,\"moves\":[]}", "schema_version")]
    [InlineData("{\"schema_version\":1,\"moves\":null}", "moves")]
    [InlineData("{\"schema_version\":1,\"moves\":[{\"move_id\":\"5A\"}]}", "startup")]
    [InlineData("{\"schema_version\":1,\"moves\":[{\"move_id\":\"5A\",\"startup\":\"1\"}]}", "startup")]
    [InlineData("{\"schema_version\":1,\"schema_version\":1,\"moves\":[]}", "duplicate")]
    public void Parse_InvalidPresenceVersionOrType_ReturnsFieldError(string json, string field)
    {
        var error = Assert.Throws<MoveDatasetFormatException>(() => MoveDatasetCodec.Parse(json));
        Assert.Contains(field, error.FieldPath, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(error.RecoveryAction);
    }

    [Fact]
    public void CandidateEdit_PreservesUntouchedSemanticValuesAndSourceIdentity()
    {
        var loaded = MoveDatasetCodec.Parse(ValidJson);
        var candidate = MoveAuthoringCandidate.FromDocument(loaded).EditMove(
            "5A", move => move with { Damage = 11 });

        Assert.Equal(loaded.ContentIdentity, candidate.SourceIdentity);
        var edited = Assert.Single(candidate.Moves);
        Assert.Equal(11, edited.Damage);
        Assert.Equal(1, edited.Startup);
        Assert.Equal("light", edited.KnockbackProfileId);
        Assert.Equal(10, Assert.Single(loaded.Moves).Damage);
    }

    [Fact]
    public void ViewModel_ValidationFailure_PreservesFormAndExposesFocusRecovery()
    {
        var vm = new MoveAuthoringViewModel(MoveDatasetCodec.Parse(ValidJson));
        vm.Edit("5A", move => move with { Startup = -1 });

        var result = vm.Validate();

        Assert.False(result.Success);
        Assert.Equal("moves[0].startup", result.FirstInvalidField);
        Assert.Equal("-1", Assert.Single(result.Errors).RejectedValue);
        Assert.Contains("non-negative", Assert.Single(result.Errors).RecoveryAction);
        Assert.Equal(-1, vm.CurrentCandidate.Moves[0].Startup);
    }

    [Fact]
    public void ReapplyCommitted_AfterExternalChange_PreservesFormEditOnLatestVersion()
    {
        string root = Path.Combine(Path.GetTempPath(), $"ftg-reapply-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "moves.json");
            File.WriteAllText(path, ValidJson, new UTF8Encoding(false));
            var store = new DataStore(MoveDataLoader.LoadFromJson(ValidJson), knockbackProfiles: new[]
            {
                new KnockbackProfile { ProfileId = "light", Horizontal = 1, Vertical = 1, Gravity = 1, Friction = 1 }
            });
            var persistence = new MoveDatasetPersistence(root, store);
            var vm = new MoveAuthoringViewModel(persistence.Load("moves"), persistence, "moves");
            vm.Edit("5A", move => move with { Damage = 11 });

            string external = ValidJson.Replace("\"startup\":1", "\"startup\":2", StringComparison.Ordinal);
            File.WriteAllText(path, external, new UTF8Encoding(false));

            vm.ReapplyCommitted();

            MoveAuthoringMove move = Assert.Single(vm.CurrentCandidate.Moves);
            Assert.Equal(2, move.Startup);
            Assert.Equal(11, move.Damage);
            Assert.True(vm.Validate().Success);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ReapplyCommitted_AfterMoveRename_PreservesRenameAndOtherEdits()
    {
        var original = MoveDatasetCodec.Parse(ValidJson);
        var vm = new MoveAuthoringViewModel(original);
        vm.Edit("5A", move => move with { MoveId = "5LP", Damage = 25 });
        string external = ValidJson.Replace("\"startup\":1", "\"startup\":2", StringComparison.Ordinal);

        vm.ReapplyAfterConflict(MoveDatasetCodec.Parse(external));

        MoveAuthoringMove move = Assert.Single(vm.CurrentCandidate.Moves);
        Assert.Equal("5LP", move.MoveId);
        Assert.Equal(25, move.Damage);
        Assert.Equal(2, move.Startup);
        Assert.True(vm.LastResult.Success);
    }

    [Fact]
    public void Validate_InMemoryCandidate_RejectsEmptyNestedIdentifiers()
    {
        var vm = new MoveAuthoringViewModel(MoveDatasetCodec.Parse(ValidJson));
        vm.Edit("5A", move => move with
        {
            CancelWindows = [new CancelWindow { StartFrame = 0, EndFrame = 1, TargetCategory = "" }],
            CollisionFrames =
            [
                new CollisionFrameDefinition
                {
                    Frame = 1,
                    Hitboxes = [new CollisionBoxDefinition { BoxId = "", Width = 1, Height = 1 }]
                }
            ]
        });

        MoveValidationResult result = vm.Validate();

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.FieldPath.EndsWith("target_category", StringComparison.Ordinal));

        vm = new MoveAuthoringViewModel(MoveDatasetCodec.Parse(ValidJson));
        vm.Edit("5A", move => move with
        {
            CollisionFrames =
            [
                new CollisionFrameDefinition
                {
                    Frame = 1,
                    Hitboxes = [new CollisionBoxDefinition { BoxId = "", Width = 1, Height = 1 }]
                }
            ]
        });
        result = vm.Validate();
        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.FieldPath.EndsWith("box_id", StringComparison.Ordinal));
    }

    [Fact]
    public void ReapplyCommitted_RenameToExistingId_ReturnsValidationInsteadOfThrowing()
    {
        var document = MoveDatasetCodec.Parse(ValidJson);
        var vm = new MoveAuthoringViewModel(document);
        vm.Add(vm.CurrentCandidate.Moves[0] with { MoveId = "6A" });
        vm.Edit("5A", move => move with { MoveId = "6A" });

        vm.ReapplyAfterConflict(document);

        Assert.False(vm.LastResult.Success);
        Assert.Contains(vm.LastResult.Errors, error => error.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_MultipleIndependentErrors_ReturnsCompleteStableFieldPaths()
    {
        var vm = new MoveAuthoringViewModel(MoveDatasetCodec.Parse(ValidJson));
        vm.Edit("5A", move => move with
        {
            MoveId = "",
            Startup = -1,
            Active = -2,
            Damage = -3,
            KnockbackProfileId = "",
            CancelWindows =
            [
                new CancelWindow { StartFrame = -1, EndFrame = -2, TargetCategory = "" },
                null!,
            ],
            CollisionFrames =
            [
                new CollisionFrameDefinition
                {
                    Frame = 0,
                    Hitboxes =
                    [
                        new CollisionBoxDefinition { BoxId = "", X = float.NaN, Width = -1, Height = -2 },
                        null!,
                    ],
                    Hurtboxes = [],
                },
                null!,
            ],
        });

        MoveValidationResult result = vm.Validate();

        Assert.False(result.Success);
        Assert.Equal("moves[0].move_id", result.FirstInvalidField);
        Assert.Equal(result.Errors.Select(error => error.FieldPath).Distinct().Count(), result.Errors.Count);
        Assert.Contains(result.Errors, error => error.FieldPath == "moves[0].startup");
        Assert.Contains(result.Errors, error => error.FieldPath == "moves[0].active");
        Assert.Contains(result.Errors, error => error.FieldPath == "moves[0].damage");
        Assert.Contains(result.Errors, error => error.FieldPath == "moves[0].knockback_profile_id");
        Assert.Contains(result.Errors, error => error.FieldPath == "moves[0].cancel_windows[0].target_category");
        Assert.Contains(result.Errors, error => error.FieldPath == "moves[0].cancel_windows[0]");
        Assert.Contains(result.Errors, error => error.FieldPath == "moves[0].cancel_windows[1]");
        Assert.Contains(result.Errors, error => error.FieldPath == "moves[0].collision_frames[0].frame");
        Assert.Contains(result.Errors, error => error.FieldPath == "moves[0].collision_frames[0].hitboxes[0].box_id");
        Assert.Contains(result.Errors, error => error.FieldPath == "moves[0].collision_frames[0].hitboxes[0].x");
        Assert.Contains(result.Errors, error => error.FieldPath == "moves[0].collision_frames[0].hitboxes[0].width");
        Assert.Contains(result.Errors, error => error.FieldPath == "moves[0].collision_frames[0].hitboxes[0].height");
        Assert.Contains(result.Errors, error => error.FieldPath == "moves[0].collision_frames[0].hitboxes[1]");
        Assert.Contains(result.Errors, error => error.FieldPath == "moves[0].collision_frames[1]");
        Assert.Same(result, vm.LastResult);
        Assert.Equal(-1, vm.CurrentCandidate.Moves[0].Startup);
    }

    [Fact]
    public void ValidationPresentation_MapsScalarAndNestedErrorsWithoutGodot()
    {
        var errors = new[]
        {
            new MoveValidationError("moves[0].startup", "-1", "must be non-negative", "Enter a non-negative frame count."),
            new MoveValidationError("moves[0].cancel_windows[2].target_category", "", "identifier must be non-empty", "Enter a category."),
            new MoveValidationError("moves[0].collision_frames[1].hitboxes[3].width", "-2", "must be non-negative", "Enter a non-negative width."),
        };

        MoveValidationPresentation presentation = MoveValidationPresentation.From(new MoveValidationResult(errors));

        Assert.Equal("moves[0].startup", presentation.FirstFieldKey);
        Assert.Equal(3, presentation.ErrorCount);
        Assert.Contains("3 validation error(s)", presentation.Summary, StringComparison.Ordinal);
        Assert.Single(presentation.ErrorsByFieldKey["moves[0].startup"]);
        Assert.Single(presentation.ErrorsByFieldKey["moves[0].cancel_windows[2].target_category"]);
        Assert.Single(presentation.ErrorsByFieldKey["moves[0].collision_frames[1].hitboxes[3].width"]);
        Assert.All(presentation.ErrorsByFieldKey.SelectMany(pair => pair.Value), line => Assert.Contains("Recovery:", line, StringComparison.Ordinal));
    }

    [Fact]
    public void ValidationPresentation_PreservesMoveIndexesInsteadOfMergingFields()
    {
        var errors = new[]
        {
            new MoveValidationError("moves[0].startup", "-1", "invalid", "Fix move zero."),
            new MoveValidationError("moves[1].startup", "-2", "invalid", "Fix move one."),
        };

        MoveValidationPresentation presentation = MoveValidationPresentation.From(new MoveValidationResult(errors));

        Assert.Equal(2, presentation.ErrorsByFieldKey.Count);
        Assert.Contains("moves[0].startup", presentation.ErrorsByFieldKey.Keys);
        Assert.Contains("moves[1].startup", presentation.ErrorsByFieldKey.Keys);
    }

    [Fact]
    public void ScalarParser_MultipleMalformedValues_ReturnsCompleteOrderedErrors()
    {
        var fields = new Dictionary<string, string>
        {
            ["startup"] = "abc", ["active"] = "1.5", ["recovery"] = "3",
            ["hit_advantage"] = "4", ["block_advantage"] = "-1", ["damage"] = "lots",
            ["chain_repeatable"] = "sometimes",
        };

        MoveAuthoringScalarParseResult result = MoveAuthoringScalarParser.Parse(fields, 2);

        Assert.Null(result.Values);
        Assert.Collection(result.Validation.Errors,
            error => Assert.Equal("moves[2].startup", error.FieldPath),
            error => Assert.Equal("moves[2].active", error.FieldPath),
            error => Assert.Equal("moves[2].damage", error.FieldPath),
            error => Assert.Equal("moves[2].chain_repeatable", error.FieldPath));
    }
}
