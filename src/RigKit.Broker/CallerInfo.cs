namespace RigKit.Broker;

public sealed record CallerInfo(int ProcessId, string ProcessName, string ImagePath, bool IsAllowed)
{
    public static CallerInfo Unknown { get; } = new(0, "unknown", string.Empty, false);
}
