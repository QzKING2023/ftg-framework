#nullable enable
using FTG_Framework.Core;
using FTG_Framework.Core.Events;
using FTG_Framework.Data;
using FTG_Framework.UI.Training;
using Godot;

namespace FTG_Framework.Scenes;

public partial class CharacterSelectScene : Node, IScene
{
    public IDataStore? DataStore { get; set; }

    private ISceneManager? _sceneManager;
    private CharacterSelect? _characterSelect;

    public void Enter(ISceneManager manager)
    {
        _sceneManager = manager;

        _characterSelect = new CharacterSelect
        {
            DataStore = DataStore,
            PanelPosition = new Vector2(200, 200)
        };
        AddChild(_characterSelect);

        manager.Subscribe<MatchInitializedEvent>(OnMatchInitialized);

        CallDeferred(nameof(AutoStartMatch));
    }

    private void AutoStartMatch()
    {
        _characterSelect?.AutoConfirmBoth();
    }

    public void Exit()
    {
        // Subscriptions are auto-cleaned by SceneManager.
    }

    private void OnMatchInitialized(MatchInitializedEvent e)
    {
        _sceneManager?.GoTo("training");
    }
}
