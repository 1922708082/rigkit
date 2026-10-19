namespace RigKit.Contracts.Ipc;

/// <summary>
/// JSON-RPC method names shared by the UI, Agent and Broker.
/// Keep this list small and explicit; the Broker rejects anything not registered here.
/// </summary>
public static class IpcMethods
{
    public const string PipeName = "RigKit.Broker.v1";

    public const string SystemInfo = "system.info";
    public const string DeviceList = "device.list";
    public const string SensorSnapshot = "sensor.snapshot";
    public const string OperationPlan = "op.plan";
    public const string OperationExecute = "op.execute";
    public const string OperationCancel = "op.cancel";
    public const string SnapshotList = "snapshot.list";
    public const string SnapshotRestore = "snapshot.restore";
    public const string LogTail = "log.tail";
}
