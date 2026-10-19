using RigKit.Domain.Devices;

namespace RigKit.UnitTests;

public sealed class SensorRingBufferTests
{
    [Fact]
    public void Add_DropsOldestSample_WhenCapacityIsReached()
    {
        var buffer = new SensorRingBuffer(3);
        var id = new DeviceId("test", "test");

        for (var index = 0; index < 5; index++)
        {
            buffer.Add(CreateSample(id, index));
        }

        var recent = buffer.GetRecent(10);

        Assert.Equal(3, recent.Count);
        Assert.Equal(2, recent[0].PowerW);
        Assert.Equal(4, recent[^1].PowerW);
    }

    [Fact]
    public void GetRecent_ReturnsTail_InChronologicalOrder()
    {
        var buffer = new SensorRingBuffer(10);
        var id = new DeviceId("test", "test");
        for (var index = 0; index < 6; index++)
        {
            buffer.Add(CreateSample(id, index));
        }

        var recent = buffer.GetRecent(3);

        Assert.Equal(new double?[] { 3, 4, 5 }, recent.Select(sample => sample.PowerW));
    }

    private static SensorSample CreateSample(DeviceId id, int index) =>
        new(
            id,
            DateTimeOffset.Now.AddSeconds(index),
            60 + index,
            index,
            300,
            50,
            100,
            1024,
            1000,
            8000,
            40,
            "P0",
            2,
            16,
            SensorSource.Nvml);
}
