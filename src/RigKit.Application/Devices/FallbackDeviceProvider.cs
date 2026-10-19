using RigKit.Application.Abstractions;
using RigKit.Domain.Devices;

namespace RigKit.Application.Devices;

/// <summary>
/// Tries providers in order. NVML is preferred; nvidia-smi is kept as a fallback
/// for mining drivers that do not expose a complete NVML surface.
/// </summary>
public sealed class FallbackDeviceProvider : IDeviceProvider, IDisposable
{
    private readonly IReadOnlyList<IDeviceProvider> _providers;
    private volatile IDeviceProvider? _active;
    private volatile string _lastError = string.Empty;

    public FallbackDeviceProvider(params IDeviceProvider[] providers)
    {
        if (providers is null || providers.Length == 0)
        {
            throw new ArgumentException("至少需要一个设备数据源。", nameof(providers));
        }

        _providers = providers;
    }

    public string Name => string.Join(" -> ", _providers.Select(provider => provider.Name));

    public string LastError => _lastError;

    public async Task<IReadOnlyList<Device>> EnumerateAsync(CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        foreach (var provider in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var devices = await provider.EnumerateAsync(cancellationToken).ConfigureAwait(false);
                if (devices.Count > 0)
                {
                    _active = provider;
                    _lastError = string.Empty;
                    return devices;
                }

                errors.Add($"{provider.Name}: 未发现设备");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add($"{provider.Name}: {ex.Message}");
            }
        }

        _lastError = string.Join(" | ", errors);
        return Array.Empty<Device>();
    }

    public async Task<SensorSample> ReadSensorsAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
    {
        var ordered = new List<IDeviceProvider>();
        if (_active is not null)
        {
            ordered.Add(_active);
        }

        ordered.AddRange(_providers.Where(provider => !ReferenceEquals(provider, _active)));

        var errors = new List<string>();
        foreach (var provider in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var sample = await provider.ReadSensorsAsync(deviceId, cancellationToken).ConfigureAwait(false);
                if (sample.Source != SensorSource.Unknown)
                {
                    _active = provider;
                    _lastError = string.Empty;
                    return sample;
                }

                errors.Add($"{provider.Name}: 无有效数据");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add($"{provider.Name}: {ex.Message}");
            }
        }

        _lastError = string.Join(" | ", errors);
        return SensorSample.Empty(deviceId);
    }

    public void Dispose()
    {
        foreach (var provider in _providers)
        {
            if (provider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
