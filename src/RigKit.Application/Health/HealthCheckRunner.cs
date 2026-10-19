using System.Diagnostics;
using RigKit.Domain.Health;

namespace RigKit.Application.Health;

public sealed class HealthCheckRunner
{
    private readonly IReadOnlyList<IHealthCheck> _checks;

    public HealthCheckRunner(IEnumerable<IHealthCheck> checks)
    {
        ArgumentNullException.ThrowIfNull(checks);
        _checks = checks.OrderBy(check => check.Order).ToArray();
        if (_checks.Count == 0)
        {
            throw new ArgumentException("至少需要一个体检项。", nameof(checks));
        }
    }

    public IReadOnlyList<IHealthCheck> Checks => _checks;

    public async Task<HealthReport> RunAsync(
        HealthCheckContext context,
        IProgress<HealthCheckProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var startedAt = DateTimeOffset.Now;
        var results = new List<HealthCheckResult>(_checks.Count);
        var total = _checks.Count;

        for (var index = 0; index < _checks.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var check = _checks[index];
            progress?.Report(new HealthCheckProgress(index + 1, total, check.Id, check.Title, null));

            var stopwatch = Stopwatch.StartNew();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(check.Timeout > TimeSpan.Zero ? check.Timeout : TimeSpan.FromSeconds(10));

            HealthCheckResult result;
            try
            {
                result = await check.RunAsync(context, timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                result = HealthCheckResult.Failed(
                    check.Id,
                    check.Title,
                    $"检查超时（{check.Timeout.TotalSeconds:0} 秒）。",
                    remediation: "确认设备仍在线，然后重试。");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                result = HealthCheckResult.Failed(
                    check.Id,
                    check.Title,
                    "检查执行失败。",
                    new[] { ex.Message },
                    "查看日志后重试；若持续失败请导出诊断包。");
            }

            stopwatch.Stop();
            result = result with { Duration = stopwatch.Elapsed };
            results.Add(result);
            progress?.Report(new HealthCheckProgress(index + 1, total, check.Id, check.Title, result.Status));
        }

        return new HealthReport(context.Device.Id, startedAt, DateTimeOffset.Now, results);
    }
}
