using RigKit.Application.Abstractions;
using RigKit.Domain.Devices;

namespace RigKit.Application.Health;

public sealed record HealthCheckContext(
    Device Device,
    Func<DeviceId, CancellationToken, Task<SensorSample>> ReadSensors,
    IVramTester? VramTester = null);
