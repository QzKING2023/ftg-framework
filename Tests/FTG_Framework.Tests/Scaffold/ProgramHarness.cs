#nullable enable
using FTG_Framework.Scaffold;

namespace FTG_Framework.Tests.Scaffold;

/// <summary>
/// Test harness that invokes the CLI Program.Main in-process,
/// avoiding cross-process test complexity. The invoked code paths
/// still shell out to dotnet (--version probe and restore).
/// </summary>
public static class ProgramHarness
{
    public static int Run(params string[] args)
    {
        return FTG_Framework.Scaffold.Program.Main(args);
    }

    internal static int RunWithHooks(ScaffoldHooks hooks, params string[] args)
    {
        return FTG_Framework.Scaffold.Program.Run(args, hooks);
    }
}
