using RigKit.Application.Abstractions;
using RigKit.Application.Devices;
using RigKit.Domain.Devices;

namespace RigKit.UnitTests;

public sealed class FallbackDeviceProviderTests
{
    private static readonly Device SampleDevice = new(
        new DeviceId("00000000:01:00.0", "PCI\\VEN_10DE&DEV_220D"),
        "NVIDIA CMP 90HX",
        "GA102",
        DeviceVendor.Nvidia,
        "10DE:220D",
        "616.92",
        10240,
        DeviceState.Ok,
        "fake");

    [Fact]
    public async Task EnumerateAsync_FallsBack_WhenFirstProviderThrows()
    {
        var first = new FakeProvider("nvml", throwOnEnumerate: true);
        var second = new FakeProvider("nvidia-smi") { Devices = new[] { SampleDevice } };
        using var provider = new FallbackDeviceProvider(first, second);

        var devices = await provider.EnumerateAsync();

        Assert.Single(devices);
        Assert.Equal(1, first.EnumerateCalls);
        Assert.Equal(1, second.EnumerateCalls);
    }

    [Fact]
    public async Task EnumerateAsync_FallsBack_WhenFirstProviderReturnsEmpty()
    {
        var first = new FakeProvider("nvml");
        var second = new FakeProvider("nvidia-smi") { Devices = new[] { SampleDevice } };
        using var provider = new FallbackDeviceProvider(first, second);

        var devices = await provider.EnumerateAsync();

        Assert.Single(devices);
        Assert.Equal(1, first.EnumerateCalls);
        Assert.Equal(1, second.EnumerateCalls);
    }

    [Fact]
    public async Task EnumerateAsync_ReturnsEmpty_WhenAllProvidersFail()
    {
        var first = new FakeProvider("nvml", throwOnEnumerate: true);
        var second = new FakeProvider("nvidia-smi", throwOnEnumerate: true);
        using var provider = new FallbackDeviceProvider(first, second);

        var devices = await provider.EnumerateAsync();

        Assert.Empty(devices);
        Assert.Contains("nvml", provider.LastError);
        Assert.Contains("nvidia-smi", provider.LastError);
    }

    [Fact]
    public async Task ReadSensorsAsync_FallsBack_WhenActiveProviderThrows()
    {
        var first = new FakeProvider("nvml", throwOnEnumerate: false)
        {
            Devices = new[] { SampleDevice },
            ThrowOnSensors = true
        };
        var expected = new SensorSample(
            SampleDevice.Id,
            DateTimeOffset.Now,
            70,
            250,
            300,
            100,
            512,
            10240,
            1800,
            9500,
            0,
            "P0",
            2,
            16,
            SensorSource.Wmi);
        var second = new FakeProvider("nvidia-smi") { Sample = expected };
        using var provider = new FallbackDeviceProvider(first, second);

        await provider.EnumerateAsync();
        var sample = await provider.ReadSensorsAsync(SampleDevice.Id);

        Assert.Equal(SensorSource.Wmi, sample.Source);
        Assert.Equal(1, second.SensorCalls);
    }

    private sealed class FakeProvider : IDeviceProvider
    {
        private readonly bool _throwOnEnumerate;

        public FakeProvider(string name, bool throwOnEnumerate = false)
        {
            Name = name;
            _throwOnEnumerate = throwOnEnumerate;
        }

        public string Name { get; }

        public IReadOnlyList<Device> Devices { get; init; } = Array.Empty<Device>();

        public SensorSample Sample { get; init; } = SensorSample.Empty(DeviceId.Unknown);

        public bool ThrowOnSensors { get; init; }

        public int EnumerateCalls { get; private set; }

        public int SensorCalls { get; private set; }

        public Task<IReadOnlyList<Device>> EnumerateAsync(CancellationToken cancellationToken = default)
        {
            EnumerateCalls++;
            if (_throwOnEnumerate)
            {
                throw new InvalidOperationException($"{Name} failed");
            }

            return Task.FromResult(Devices);
        }

        public Task<SensorSample> ReadSensorsAsync(
            DeviceId deviceId,
            CancellationToken cancellationToken = default)
        {
            SensorCalls++;
            if (ThrowOnSensors)
            {
                throw new InvalidOperationException($"{Name} failed");
            }

            return Task.FromResult(Sample);
        }
    }
}
