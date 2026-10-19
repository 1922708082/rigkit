using RigKit.Domain.Devices;

namespace RigKit.Application.Abstractions;

/// <summary>
/// Read-only device data source. Implementations must never require elevation.
/// </summary>
public interface IDeviceProvider
{
    string Name { get; }

    Task<IReadOnlyList<Device>> EnumerateAsync(CancellationToken cancellationToken = default);

    Task<SensorSample> ReadSensorsAsync(DeviceId deviceId, CancellationToken cancellationToken = default);
}
