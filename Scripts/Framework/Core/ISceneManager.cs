#nullable enable
using System;
using Godot;

namespace FTG_Framework.Core;

public interface ISceneManager
{
    string? CurrentSceneId { get; }

    void RegisterScene(string sceneId, string scenePath);

    void RegisterScene(string sceneId, Func<Node> factory);

    void GoTo(string sceneId);

    void Subscribe<T>(Action<T> handler);

    void Unsubscribe<T>(Action<T> handler);
}
