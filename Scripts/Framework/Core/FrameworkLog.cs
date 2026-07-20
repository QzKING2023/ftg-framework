#nullable enable
using System;

namespace FTG_Framework.Core;

// GD.* calls crash outside the engine process (uninitialized native interop),
// so framework logging routes through these delegates. GameLoop wires them to
// GD.Print/GD.PrintErr at startup; unit tests keep the console defaults.
internal static class FrameworkLog
{
    public static Action<string> Info = Console.WriteLine;
    public static Action<string> Error = message => Console.Error.WriteLine(message);
}
