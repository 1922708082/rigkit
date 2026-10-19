using RigKit.Application.Abstractions;
using RigKit.Domain.Devices;

namespace RigKit.Application.Devices;

public sealed class DeviceService(IDeviceProvider provider)
{
    private readonly IDeviceProvider _provider = provider ?? throw new ArgumentNullException(nameof(provider));

    public string ProviderName => _provider.Name;

    public string LastError => (_provider as FallbackDeviceProvider)?.LastError ?? string.Empty;

    public Task<IReadOnlyList<Device>> GetDevicesAsync(CancellationToken cancellationToken = default) =>
        _provider.EnumerateAsync(cancellationToken);

    public Task<SensorSample> GetSensorsAsync(DeviceId deviceId, CancellationToken cancellationToken = default) =>
        _provider.ReadSensorsAsync(deviceId, cancellationToken);
}
