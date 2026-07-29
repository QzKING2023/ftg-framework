#nullable enable
using System;
using System.Collections.Generic;
using FTG_Framework.Core.Events;
using Godot;

namespace FTG_Framework.Core;

internal sealed class SceneManager : ISceneManager
{
    private readonly Node? _sceneRoot;
    private readonly Dictionary<string, SceneSource> _registeredScenes = new();
    private readonly List<ISubscription> _sceneSubscriptions = new();
    private Node? _currentSceneRoot;
    private string? _currentSceneId;

    public string? CurrentSceneId => _currentSceneId;

    public SceneManager(Node? sceneRoot)
    {
        _sceneRoot = sceneRoot;
    }

    public void RegisterScene(string sceneId, string scenePath)
    {
        _registeredScenes[sceneId] = new PathSceneSource(scenePath);
    }

    public void RegisterScene(string sceneId, Func<Node> factory)
    {
        _registeredScenes[sceneId] = new FactorySceneSource(factory);
    }

    public void GoTo(string sceneId)
    {
        if (!_registeredScenes.TryGetValue(sceneId, out var source))
        {
            FrameworkLog.Error?.Invoke($"[SceneManager] Scene '{sceneId}' is not registered.");
            return;
        }

        var oldSceneId = _currentSceneId ?? string.Empty;
        EventBus.Instance.PublishImmediate(new SceneChangingEvent(oldSceneId, sceneId));

        ExitCurrentScene();

        _currentSceneRoot = source.Instantiate(_sceneRoot);
        _currentSceneId = sceneId;

        if (_sceneRoot is not null && _currentSceneRoot is not null)
            _sceneRoot.AddChild(_currentSceneRoot);

        if (_currentSceneRoot is IScene scene)
            scene.Enter(this);

        EventBus.Instance.PublishImmediate(new SceneChangedEvent(sceneId));
    }

    public void Subscribe<T>(Action<T> handler)
    {
        EventBus.Instance.Subscribe(handler);
        _sceneSubscriptions.Add(new TypedSubscription<T>(handler));
    }

    public void Unsubscribe<T>(Action<T> handler)
    {
        EventBus.Instance.Unsubscribe(handler);
        _sceneSubscriptions.RemoveAll(s => s is TypedSubscription<T> ts && ts.Handler.Equals(handler));
    }

    private void ExitCurrentScene()
    {
        if (_currentSceneRoot is IScene scene)
            scene.Exit();

        foreach (var sub in _sceneSubscriptions)
            sub.Unsubscribe();
        _sceneSubscriptions.Clear();

        if (_currentSceneRoot is not null)
        {
            _currentSceneRoot.QueueFree();
            _currentSceneRoot = null;
        }

        _currentSceneId = null;
    }

    // ── Scene source abstraction ──

    private abstract class SceneSource
    {
        public abstract Node? Instantiate(Node? root);
    }

    private sealed class PathSceneSource : SceneSource
    {
        private readonly string _path;

        public PathSceneSource(string path) => _path = path;

        public override Node? Instantiate(Node? root)
        {
            if (root is null) return null;
            var packedScene = ResourceLoader.Load<PackedScene>(_path);
            return packedScene.Instantiate();
        }
    }

    private sealed class FactorySceneSource : SceneSource
    {
        private readonly Func<Node> _factory;

        public FactorySceneSource(Func<Node> factory) => _factory = factory;

        public override Node? Instantiate(Node? root)
        {
            return root is not null ? _factory() : null;
        }
    }

    // ── Subscription tracking ──

    private interface ISubscription
    {
        void Unsubscribe();
    }

    private sealed class TypedSubscription<T> : ISubscription
    {
        public readonly Action<T> Handler;

        public TypedSubscription(Action<T> handler) => Handler = handler;

        public void Unsubscribe() => EventBus.Instance.Unsubscribe(Handler);
    }
}
