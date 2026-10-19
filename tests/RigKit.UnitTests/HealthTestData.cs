using RigKit.Application.Abstractions;
using RigKit.Application.Health;
using RigKit.Domain.Devices;

namespace RigKit.UnitTests;

internal static class HealthTestData
{
    public static Device CreateDevice(
        string model = "NVIDIA CMP 90HX",
        string architecture = "GA102",
        string pciIds = "10DE:220D",
        string driver = "616.92",
        int memoryMegabytes = 10240,
        DeviceState state = DeviceState.Ok) =>
        new(
            new DeviceId("00000000:01:00.0", "PCI\\VEN_10DE&DEV_220D"),
            model,
            architecture,
            DeviceVendor.Nvidia,
            pciIds,
            driver,
            memoryMegabytes,
            state,
            "nvml");

    public static SensorSample CreateSample(
        DeviceId? deviceId = null,
        int? pcieGeneration = 2,
        int? pcieWidth = 16,
        double? temperature = 75,
        double? power = 298,
        double? powerLimit = 300,
        double? utilization = 100,
        int? fanPercent = 40) =>
        new(
            deviceId ?? new DeviceId("00000000:01:00.0", "test"),
            DateTimeOffset.Now,
            temperature,
            power,
            powerLimit,
            utilization,
            512,
            10240,
            1800,
            9000,
            fanPercent,
            "P0",
            pcieGeneration,
            pcieWidth,
            SensorSource.Nvml);

    public static HealthCheckContext CreateContext(
        Device? device = null,
        SensorSample? sample = null,
        bool throwOnRead = false,
        IVramTester? vramTester = null)
    {
        var actualDevice = device ?? CreateDevice();
        var actualSample = sample ?? CreateSample(actualDevice.Id);
        return new HealthCheckContext(
            actualDevice,
            (_, _) => throwOnRead
                ? throw new InvalidOperationException("sensor read failed")
                : Task.FromResult(actualSample),
            vramTester);
    }
}
