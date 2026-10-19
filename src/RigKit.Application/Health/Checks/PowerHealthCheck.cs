using RigKit.Domain.Health;

namespace RigKit.Application.Health.Checks;

public sealed class PowerHealthCheck : IHealthCheck
{
    public string Id => "power";

    public string Title => "功耗与墙值";

    public int Order => 40;

    public TimeSpan Timeout => TimeSpan.FromSeconds(5);

    public async Task<HealthCheckResult> RunAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var sample = await context.ReadSensors(context.Device.Id, cancellationToken).ConfigureAwait(false);
        var details = new List<string>
        {
            $"实时功耗：{sample.PowerW?.ToString("0.#") ?? "--"} W",
            $"功耗墙：{sample.PowerLimitW?.ToString("0.#") ?? "--"} W",
            $"GPU 利用率：{sample.UtilizationPercent?.ToString("0.#") ?? "--"}%"
        };

        if (sample.PowerW is null || sample.PowerLimitW is null)
        {
            return HealthCheckResult.Warning(
                Id,
                Title,
                "驱动未报告功耗或功耗墙。",
                details,
                "部分矿卡驱动不暴露功耗墙，改用 nvidia-smi 复核。");
        }

        var power = sample.PowerW.Value;
        var limit = sample.PowerLimitW.Value;
        if (limit <= 0)
        {
            return HealthCheckResult.Warning(
                Id,
                Title,
                "功耗墙为 0 或无效。",
                details,
                "检查 vBIOS 与驱动；异常墙值下不要执行调优。");
        }

        var ratio = power / limit;
        if (ratio > 1.05)
        {
            return HealthCheckResult.Warning(
                Id,
                Title,
                $"功耗 {power:0.#} W 超过墙值 {limit:0.#} W。",
                details,
                "检查功耗墙设置与散热；必要时降低功耗。");
        }

        if (sample.UtilizationPercent is > 95 && ratio < 0.5)
        {
            return HealthCheckResult.Warning(
                Id,
                Title,
                "高负载下功耗明显偏低，可能被限制。",
                details,
                "检查 PSU、供电线与驱动功耗策略。");
        }

        return HealthCheckResult.Passed(
            Id,
            Title,
            $"功耗 {power:0.#} W / {limit:0.#} W，处于正常范围。",
            details);
    }
}
