namespace RigKit.Domain.Devices;

public enum DeviceVendor
{
    Unknown,
    Nvidia,
    Amd,
    Intel
}

public enum DeviceState
{
    Unknown,
    Ok,
    Warning,
    Error,
    Missing
}

public sealed record Device(
    DeviceId Id,
    string Model,
    string Architecture,
    DeviceVendor Vendor,
    string PciIds,
    string DriverVersion,
    int MemoryMegabytes,
    DeviceState State,
    string StatusDetail)
{
    public string DisplayMemory =>
        MemoryMegabytes > 0 ? $"{MemoryMegabytes / 1024.0:0.#} GB" : "--";
}
