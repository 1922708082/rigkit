using RigKit.Domain.Health;

namespace RigKit.Application.Health;

public interface IHealthCheck
{
    string Id { get; }

    string Title { get; }

    int Order { get; }

    TimeSpan Timeout { get; }

    Task<HealthCheckResult> RunAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default);
}
