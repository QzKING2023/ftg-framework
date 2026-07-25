#nullable enable
using System.Collections.Generic;
using FTG_Framework.Core;
using Xunit;

namespace FTG_Framework.Tests;

public class GameLoopShutdownTests
{
    private sealed class RecordingModule : IModule
    {
        private readonly List<string> _log;
        private readonly string _name;

        public RecordingModule(List<string> log, string name)
        {
            _log = log;
            _name = name;
        }

        public void Initialize(IDataStore dataStore) { }

        public void Shutdown() => _log.Add(_name);
    }

    [Fact]
    public void ShutdownModules_CallsAllModules_InReverseRegistrationOrder()
    {
        var log = new List<string>();
        var modules = new List<IModule>
        {
            new RecordingModule(log, "first"),
            new RecordingModule(log, "second"),
            new RecordingModule(log, "third"),
        };

        GameLoop.ShutdownModules(modules);

        Assert.Equal(new[] { "third", "second", "first" }, log);
        Assert.Empty(modules);
    }

    [Fact]
    public void ShutdownModules_EmptyList_NoOp()
    {
        var modules = new List<IModule>();

        GameLoop.ShutdownModules(modules);

        Assert.Empty(modules);
    }

    [Fact]
    public void ShutdownModules_CalledTwice_SecondCallNoOp()
    {
        var log = new List<string>();
        var modules = new List<IModule> { new RecordingModule(log, "only") };

        GameLoop.ShutdownModules(modules);
        GameLoop.ShutdownModules(modules);

        Assert.Equal(new[] { "only" }, log);
    }
}
