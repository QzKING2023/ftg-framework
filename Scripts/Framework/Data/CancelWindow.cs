namespace FTG_Framework.Data;

public sealed class CancelWindow
{
    public int StartFrame { get; init; }
    public int EndFrame { get; init; }
    public string TargetCategory { get; init; } = string.Empty;
}