namespace RigKit.Domain.Devices;

public enum SensorSource
{
    Unknown,
    Nvml,
    NvidiaSmi,
    Wmi
}

/// <summary>
/// One normalized sensor reading. Missing values stay null so the UI can show "--"
/// instead of a misleading zero.
/// </summary>
public sealed record SensorSample(
    DeviceId DeviceId,
    DateTimeOffset Timestamp,
    double? TemperatureC,
    double? PowerW,
    double? PowerLimitW,
    double? UtilizationPercent,
    int? MemoryUsedMegabytes,
    int? MemoryTotalMegabytes,
    int? CoreClockMhz,
    int? MemoryClockMhz,
    int? FanPercent,
    string? PerformanceState,
    int? PcieGeneration,
    int? PcieWidth,
    SensorSource Source)
{
    public static SensorSample Empty(DeviceId deviceId, SensorSource source = SensorSource.Unknown) =>
        new(
            deviceId,
            DateTimeOffset.Now,
            null, null, null, null, null, null, null, null, null, null, null, null,
            source);
}
