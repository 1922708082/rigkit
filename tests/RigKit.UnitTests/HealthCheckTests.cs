using RigKit.Application.Health.Checks;
using RigKit.Domain.Devices;
using RigKit.Domain.Health;

namespace RigKit.UnitTests;

public sealed class HealthCheckTests
{
    [Fact]
    public async Task Identity_Passes_ForVerifiedCmp90Hx()
    {
        var result = await new IdentityHealthCheck().RunAsync(HealthTestData.CreateContext());
        Assert.Equal(HealthStatus.Pass, result.Status);
    }

    [Fact]
    public async Task Identity_Fails_WhenVerifiedPciIdMismatches()
    {
        var device = HealthTestData.CreateDevice(pciIds: "10DE:1234");
        var result = await new IdentityHealthCheck().RunAsync(HealthTestData.CreateContext(device));
        Assert.Equal(HealthStatus.Fail, result.Status);
    }

    [Fact]
    public async Task Identity_Warns_ForUnverifiedPascalCard()
    {
        var device = HealthTestData.CreateDevice(
            model: "NVIDIA P106-100",
            architecture: "GP106",
            pciIds: "10DE:1C07",
            memoryMegabytes: 6144);
        var result = await new IdentityHealthCheck().RunAsync(HealthTestData.CreateContext(device));
        Assert.Equal(HealthStatus.Warning, result.Status);
    }

    [Fact]
    public async Task Pcie_Passes_ForGen2X16()
    {
        var result = await new PcieHealthCheck().RunAsync(HealthTestData.CreateContext());
        Assert.Equal(HealthStatus.Pass, result.Status);
    }

    [Fact]
    public async Task Pcie_Warns_ForGen1()
    {
        var device = HealthTestData.CreateDevice();
        var sample = HealthTestData.CreateSample(device.Id, pcieGeneration: 1, pcieWidth: 16);
        var result = await new PcieHealthCheck().RunAsync(
            HealthTestData.CreateContext(device, sample));
        Assert.Equal(HealthStatus.Warning, result.Status);
    }

    [Fact]
    public async Task Pcie_Passes_ForP106_090X1()
    {
        var device = HealthTestData.CreateDevice(
            model: "NVIDIA P106-090",
            architecture: "GP106",
            pciIds: "10DE:1C06",
            memoryMegabytes: 3072);
        var sample = HealthTestData.CreateSample(device.Id, pcieGeneration: 2, pcieWidth: 1);
        var result = await new PcieHealthCheck().RunAsync(
            HealthTestData.CreateContext(device, sample));
        Assert.Equal(HealthStatus.Pass, result.Status);
    }

    [Fact]
    public async Task Driver_Fails_WhenVersionIsUnknown()
    {
        var device = HealthTestData.CreateDevice(driver: "unknown");
        var result = await new DriverHealthCheck().RunAsync(HealthTestData.CreateContext(device));
        Assert.Equal(HealthStatus.Fail, result.Status);
    }

    [Fact]
    public async Task Driver_Warns_ForOldVersion()
    {
        var device = HealthTestData.CreateDevice(driver: "472.12");
        var result = await new DriverHealthCheck().RunAsync(HealthTestData.CreateContext(device));
        Assert.Equal(HealthStatus.Warning, result.Status);
    }

    [Fact]
    public async Task Power_Passes_WithinLimit()
    {
        var result = await new PowerHealthCheck().RunAsync(HealthTestData.CreateContext());
        Assert.Equal(HealthStatus.Pass, result.Status);
    }

    [Fact]
    public async Task Power_Warns_WhenAboveLimit()
    {
        var device = HealthTestData.CreateDevice();
        var sample = HealthTestData.CreateSample(device.Id, power: 350, powerLimit: 300);
        var result = await new PowerHealthCheck().RunAsync(
            HealthTestData.CreateContext(device, sample));
        Assert.Equal(HealthStatus.Warning, result.Status);
    }

    [Fact]
    public async Task Power_Warns_WhenSensorsAreMissing()
    {
        var device = HealthTestData.CreateDevice();
        var sample = HealthTestData.CreateSample(device.Id, power: null, powerLimit: null);
        var result = await new PowerHealthCheck().RunAsync(
            HealthTestData.CreateContext(device, sample));
        Assert.Equal(HealthStatus.Warning, result.Status);
    }
}
