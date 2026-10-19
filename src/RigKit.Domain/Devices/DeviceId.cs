namespace RigKit.Domain.Devices;

/// <summary>
/// Stable identity of a GPU. PCI location is the primary key; indexes are never used
/// because they change between boots and multi-GPU configurations.
/// </summary>
public sealed record DeviceId(string PciLocation, string PnpInstanceId)
{
    public static DeviceId Unknown { get; } = new("unknown", "unknown");

    public override string ToString() => PciLocation;
}
