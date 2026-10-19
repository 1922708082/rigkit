using RigKit.Domain.Devices;

namespace RigKit.Application.Abstractions;

public interface ISensorHistoryStore
{
    Task AppendAsync(SensorSample sample, CancellationToken cancellationToken = default);

    IReadOnlyList<SensorSample> GetRecent(DeviceId deviceId, int count);
}
