#nullable enable
namespace FTG_Framework.Core;

public interface IScene
{
    void Enter(ISceneManager manager);
    void Exit();
}
