using System.Text;
using RigKit.Application.Abstractions;
using RigKit.Domain.Devices;

namespace RigKit.Infrastructure.Nvidia;

/// <summary>
/// Primary read-only provider backed by NVML. Gives richer data than nvidia-smi
/// and works without spawning a process on every refresh.
/// </summary>
public sealed class NvmlProvider : IDeviceProvider, IDisposable
{
    private const uint TemperatureGpu = 0;
    private const uint ClockGraphics = 0;
    private const uint ClockMemory = 2;

    private readonly object _gate = new();
    private bool _initialized;
    private bool _disposed;

    public string Name => "nvml";

    public Task<IReadOnlyList<Device>> EnumerateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureInitialized();

        lock (_gate)
        {
            var devices = new List<Device>();
            var countResult = NvmlNative.GetCount(out var count);
            if (countResult != NvmlReturn.Success)
            {
                throw new NvmlException($"nvmlDeviceGetCount 失败：{countResult}");
            }

            var driverVersion = ReadDriverVersion();
            for (uint index = 0; index < count; index++)
            {
                var handleResult = NvmlNative.GetHandleByIndex(index, out var handle);
                if (handleResult != NvmlReturn.Success || handle == IntPtr.Zero)
                {
                    continue;
                }

                var name = ReadName(handle);
                var pciResult = NvmlNative.GetPciInfo(handle, out var pci);
                if (pciResult != NvmlReturn.Success)
                {
                    continue;
                }

                var (pciIds, deviceHex) = NvidiaModelMap.FromPciDeviceId(pci.PciDeviceId);
                var busId = string.IsNullOrWhiteSpace(pci.BusId) ? BuildBusId(pci) : pci.BusId;
                var memoryMegabytes = ReadMemoryMegabytes(handle);

                devices.Add(new Device(
                    new DeviceId(busId, $"PCI\\VEN_10DE&DEV_{deviceHex}"),
                    name,
                    NvidiaModelMap.GuessArchitecture(name),
                    DeviceVendor.Nvidia,
                    pciIds,
                    driverVersion,
                    memoryMegabytes,
                    DeviceState.Ok,
                    "nvml"));
            }

            return Task.FromResult<IReadOnlyList<Device>>(devices);
        }
    }

    public Task<SensorSample> ReadSensorsAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureInitialized();

        lock (_gate)
        {
            var handle = FindHandle(deviceId);
            if (handle == IntPtr.Zero)
            {
                throw new NvmlException($"NVML 中找不到设备 {deviceId.PciLocation}");
            }

            double? temperature = null;
            if (NvmlNative.GetTemperature(handle, TemperatureGpu, out var temperatureRaw) == NvmlReturn.Success)
            {
                temperature = temperatureRaw;
            }

            double? power = null;
            if (NvmlNative.GetPowerUsage(handle, out var powerRaw) == NvmlReturn.Success)
            {
                power = powerRaw / 1000.0;
            }

            double? powerLimit = null;
            if (NvmlNative.GetEnforcedPowerLimit(handle, out var limitRaw) == NvmlReturn.Success)
            {
                powerLimit = limitRaw / 1000.0;
            }

            double? utilization = null;
            if (NvmlNative.GetUtilizationRates(handle, out var utilizationValue) == NvmlReturn.Success)
            {
                utilization = utilizationValue.Gpu;
            }

            var memoryResult = NvmlNative.GetMemoryInfo(handle, out var memory);

            uint? coreClock = null;
            if (NvmlNative.GetClockInfo(handle, ClockGraphics, out var coreRaw) == NvmlReturn.Success)
            {
                coreClock = coreRaw;
            }

            uint? memoryClock = null;
            if (NvmlNative.GetClockInfo(handle, ClockMemory, out var memoryRaw) == NvmlReturn.Success)
            {
                memoryClock = memoryRaw;
            }

            uint? fan = null;
            if (NvmlNative.GetFanSpeed(handle, out var fanRaw) == NvmlReturn.Success)
            {
                fan = fanRaw;
            }

            string? pstate = null;
            if (NvmlNative.GetPerformanceState(handle, out var pstateValue) == NvmlReturn.Success && pstateValue <= 15)
            {
                pstate = $"P{pstateValue}";
            }

            uint? pcieGen = null;
            if (NvmlNative.GetPcieLinkGeneration(handle, out var genRaw) == NvmlReturn.Success)
            {
                pcieGen = genRaw;
            }

            uint? pcieWidth = null;
            if (NvmlNative.GetPcieLinkWidth(handle, out var widthRaw) == NvmlReturn.Success)
            {
                pcieWidth = widthRaw;
            }

            return Task.FromResult(new SensorSample(
                deviceId,
                DateTimeOffset.Now,
                temperature,
                power,
                powerLimit,
                utilization,
                memoryResult == NvmlReturn.Success ? (int?)(memory.Used / 1024 / 1024) : null,
                memoryResult == NvmlReturn.Success ? (int?)(memory.Total / 1024 / 1024) : null,
                coreClock.HasValue ? (int)coreClock.Value : null,
                memoryClock.HasValue ? (int)memoryClock.Value : null,
                fan.HasValue ? (int)fan.Value : null,
                pstate,
                pcieGen.HasValue ? (int)pcieGen.Value : null,
                pcieWidth.HasValue ? (int)pcieWidth.Value : null,
                SensorSource.Nvml));
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            if (_initialized)
            {
                _ = NvmlNative.Shutdown();
                _initialized = false;
            }

            _disposed = true;
        }
    }

    private void EnsureInitialized()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(NvmlProvider));
            }

            if (_initialized)
            {
                return;
            }

            NvmlReturn result;
            try
            {
                result = NvmlNative.Init();
            }
            catch (DllNotFoundException ex)
            {
                throw new NvmlException("找不到 nvml.dll，请确认 NVIDIA 驱动已安装。", ex);
            }
            catch (EntryPointNotFoundException ex)
            {
                throw new NvmlException("nvml.dll 版本过旧，缺少 nvmlInit_v2。", ex);
            }

            if (result != NvmlReturn.Success && result != NvmlReturn.AlreadyInitialized)
            {
                throw new NvmlException($"nvmlInit 失败：{result}");
            }

            _initialized = true;
        }
    }

    private IntPtr FindHandle(DeviceId deviceId)
    {
        if (NvmlNative.GetCount(out var count) != NvmlReturn.Success)
        {
            return IntPtr.Zero;
        }

        for (uint index = 0; index < count; index++)
        {
            if (NvmlNative.GetHandleByIndex(index, out var handle) != NvmlReturn.Success || handle == IntPtr.Zero)
            {
                continue;
            }

            if (NvmlNative.GetPciInfo(handle, out var pci) != NvmlReturn.Success)
            {
                continue;
            }

            var busId = string.IsNullOrWhiteSpace(pci.BusId) ? BuildBusId(pci) : pci.BusId;
            if (string.Equals(busId, deviceId.PciLocation, StringComparison.OrdinalIgnoreCase))
            {
                return handle;
            }
        }

        // Fall back to the first device when PCI ids are unavailable (mining drivers).
        return count > 0 && NvmlNative.GetHandleByIndex(0, out var first) == NvmlReturn.Success ? first : IntPtr.Zero;
    }

    private static string ReadName(IntPtr handle)
    {
        var buffer = new StringBuilder(96);
        var result = NvmlNative.GetName(handle, buffer, (uint)buffer.Capacity);
        return result == NvmlReturn.Success ? buffer.ToString() : "NVIDIA GPU";
    }

    private static string ReadDriverVersion()
    {
        var buffer = new StringBuilder(80);
        var result = NvmlNative.GetDriverVersion(buffer, (uint)buffer.Capacity);
        return result == NvmlReturn.Success ? buffer.ToString() : "unknown";
    }

    private static int ReadMemoryMegabytes(IntPtr handle) =>
        NvmlNative.GetMemoryInfo(handle, out var memory) == NvmlReturn.Success
            ? (int)(memory.Total / 1024 / 1024)
            : 0;

    private static string BuildBusId(NvmlPciInfo pci) =>
        $"{pci.Domain:X8}:{pci.Bus:X2}:{pci.Device:X2}.0";
}
