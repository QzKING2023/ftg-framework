#nullable enable
using System.Collections.Generic;
using FTG_Framework.Data;
using FTG_Framework.UI.Training.ViewModels;
using Xunit;

namespace FTG_Framework.Tests.UI.Training.ViewModels;

public sealed class RuntimeTuningViewModelTests
{
    [Fact]
    public void Edit_ChangesCandidateOnly_UntilApply()
    {
        var service = new StubRuntimeTuningService(Baseline("10", "v1", 3));
        var sessions = new RuntimeTuningSessionAuthority();
        var vm = new RuntimeTuningViewModel(service, sessions);

        vm.Open(new RuntimeTuningSelection(RuntimeTuningSelectionKind.Move, "moves", "5A"));
        vm.EditField("damage", "11");

        Assert.Equal("10", vm.BaselineFields["damage"]);
        Assert.Equal("11", vm.CandidateFields["damage"]);
        Assert.Empty(service.CommitRequests);
    }

    [Fact]
    public void Apply_ObsoleteSession_IsRejectedBeforeServiceCommit()
    {
        var service = new StubRuntimeTuningService(Baseline("10", "v1", 3));
        var sessions = new RuntimeTuningSessionAuthority();
        var vm = new RuntimeTuningViewModel(service, sessions);
        vm.Open(new RuntimeTuningSelection(RuntimeTuningSelectionKind.Move, "moves", "5A"));
        vm.EditField("damage", "11");
        sessions.Invalidate();

        RuntimeTuningCommitResult result = vm.Apply();

        Assert.Equal(RuntimeTuningCommitStatus.Cancelled, result.Status);
        Assert.Empty(service.CommitRequests);
        Assert.Equal("11", vm.CandidateFields["damage"]);
    }

    [Fact]
    public void Conflict_PreservesCandidate_AndReapplyUsesFreshBaseline()
    {
        var service = new StubRuntimeTuningService(Baseline("10", "v1", 3));
        service.CommitResult = new RuntimeTuningCommitResult(
            RuntimeTuningCommitStatus.Conflict, "v1", "v2", "Concurrent change.");
        var vm = new RuntimeTuningViewModel(service, new RuntimeTuningSessionAuthority());
        var selection = new RuntimeTuningSelection(RuntimeTuningSelectionKind.Move, "moves", "5A");
        vm.Open(selection);
        vm.EditField("damage", "11");

        Assert.Equal(RuntimeTuningCommitStatus.Conflict, vm.Apply().Status);
        Assert.Equal("11", vm.CandidateFields["damage"]);

        service.CurrentBaseline = Baseline("12", "v2", 4);
        vm.Reapply();
        Assert.Equal("12", vm.BaselineFields["damage"]);
        Assert.Equal("11", vm.CandidateFields["damage"]);
        Assert.Equal("v2", vm.ExpectedIdentity);
    }

    private static RuntimeTuningBaseline Baseline(string damage, string identity, ulong version) =>
        new(new RuntimeTuningSelection(RuntimeTuningSelectionKind.Move, "moves", "5A"),
            new Dictionary<string, string> { ["damage"] = damage }, identity, version);

    private sealed class StubRuntimeTuningService : IRuntimeTuningService
    {
        internal StubRuntimeTuningService(RuntimeTuningBaseline baseline) => CurrentBaseline = baseline;
        internal RuntimeTuningBaseline CurrentBaseline { get; set; }
        internal RuntimeTuningCommitResult CommitResult { get; set; } =
            new(RuntimeTuningCommitStatus.Succeeded, CurrentIdentity: "v2");
        internal List<RuntimeTuningCommitRequest> CommitRequests { get; } = new();
        public RuntimeTuningBaseline Load(RuntimeTuningSelection selection) => CurrentBaseline with { Selection = selection };
        public RuntimeTuningValidationResult Validate(RuntimeTuningCommitRequest request) => RuntimeTuningValidationResult.Valid;
        public RuntimeTuningCommitResult Commit(RuntimeTuningCommitRequest request)
        {
            CommitRequests.Add(request);
            return CommitResult;
        }
    }
}
