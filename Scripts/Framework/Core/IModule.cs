namespace FTG_Framework.Core;

public interface IModule
{
    void Initialize(IDataStore dataStore);
    void Shutdown();
}
