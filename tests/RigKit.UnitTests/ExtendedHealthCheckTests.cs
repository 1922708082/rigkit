using RigKit.Application.Abstractions;
using RigKit.Application.Health.Checks;
using RigKit.Domain.Devices;
using RigKit.Domain.Health;

namespace RigKit.UnitTests;

public sealed class ExtendedHealthCheckTests
{
    [Fact]
    public async Task Vram_Passes_WhenReadWriteIsClean()
    {
        var tester = new FakeVramTester(new VramTestResult(true, true, 0, 64 * 1024 * 1024, "ok"));
        var result = await new VramIntegrityHealthCheck().RunAsync(
            HealthTestData.CreateContext(vramTester: tester));

        Assert.Equal(HealthStatus.Pass, result.Status);
    }

    [Fact]
    public async Task Vram_Fails_WhenMismatchesAreFound()
    {
        var tester = new FakeVramTester(new VramTestResult(true, false, 12, 64 * 1024 * 1024, "bad"));
        var result = await new VramIntegrityHealthCheck().RunAsync(
            HealthTestData.CreateContext(vramTester: tester));

        Assert.Equal(HealthStatus.Fail, result.Status);
        Assert.Contains("12", result.Summary);
    }

    [Fact]
    public async Task Vram_Skips_WhenTesterIsMissing()
    {
        var result = await new VramIntegrityHealthCheck().RunAsync(HealthTestData.CreateContext());
        Assert.Equal(HealthStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task Thermal_Fails_WhenTemperatureIsHigh()
    {
        var device = HealthTestData.CreateDevice();
        var sample = HealthTestData.CreateSample(device.Id, temperature: 90);
        var result = await new ThermalHealthCheck().RunAsync(
            HealthTestData.CreateContext(device, sample));

        Assert.Equal(HealthStatus.Fail, result.Status);
        Assert.Contains("90", result.Summary);
    }

    [Fact]
    public async Task Fan_Skips_ForCmp90HxAtFullLoad()
    {
        var device = HealthTestData.CreateDevice();
        var sample = HealthTestData.CreateSample(device.Id, utilization: 100, fanPercent: 0);
        var result = await new FanHealthCheck().RunAsync(
            HealthTestData.CreateContext(device, sample));
        Assert.Equal(HealthStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task Fan_Warns_ForPascalCardAtFullLoad()
    {
        var device = HealthTestData.CreateDevice(
            model: "NVIDIA P106-100",
            architecture: "GP106",
            pciIds: "10DE:1C07",
            memoryMegabytes: 6144);
        var sample = HealthTestData.CreateSample(device.Id, utilization: 100, fanPercent: 0);
        var result = await new FanHealthCheck().RunAsync(
            HealthTestData.CreateContext(device, sample));

        Assert.Equal(HealthStatus.Warning, result.Status);
    }

    private sealed class FakeVramTester : IVramTester
    {
        private readonly VramTestResult _result;

        public FakeVramTester(VramTestResult result)
        {
            _result = result;
        }

        public VramTestResult Test(
            DeviceId deviceId,
            int megabytes,
            CancellationToken cancellationToken = default) => _result;
    }
}
