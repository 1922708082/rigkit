using RigKit.Domain.Devices;

namespace RigKit.Application.Abstractions;

public sealed record VramTestResult(
    bool Available,
    bool Passed,
    int Mismatches,
    long BytesTested,
    string Message);

public interface IVramTester
{
    VramTestResult Test(
        DeviceId deviceId,
        int megabytes,
        CancellationToken cancellationToken = default);
}
