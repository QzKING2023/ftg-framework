#nullable enable
namespace FTG_Framework.Core.Events;

public readonly record struct SceneChangingEvent(string FromSceneId, string ToSceneId);
