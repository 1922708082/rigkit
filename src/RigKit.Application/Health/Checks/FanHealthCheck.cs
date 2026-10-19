using RigKit.Domain.Health;

namespace RigKit.Application.Health.Checks;

public sealed class FanHealthCheck : IHealthCheck
{
    public string Id => "fan";

    public string Title => "风扇与散热";

    public int Order => 70;

    public TimeSpan Timeout => TimeSpan.FromSeconds(5);

    public async Task<HealthCheckResult> RunAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var sample = await context.ReadSensors(context.Device.Id, cancellationToken).ConfigureAwait(false);
        var details = new List<string>
        {
            $"风扇：{sample.FanPercent?.ToString() ?? "--"}%",
            $"GPU 利用率：{sample.UtilizationPercent?.ToString("0.#") ?? "--"}%",
            $"温度：{sample.TemperatureC?.ToString("0.#") ?? "--"} ℃"
        };

        if (sample.FanPercent is null)
        {
            return HealthCheckResult.Skipped(
                Id,
                Title,
                "该卡未报告风扇转速。",
                details);
        }

        var fan = sample.FanPercent.Value;
        var utilization = sample.UtilizationPercent ?? 0;

        if (fan == 0 && utilization >= 50)
        {
            if (context.Device.Model.Contains("CMP 90HX", StringComparison.OrdinalIgnoreCase))
            {
                return HealthCheckResult.Skipped(
                    Id,
                    Title,
                    "CMP 90HX 可能无风扇传感器或为被动散热，需人工确认。",
                    details);
            }

            return HealthCheckResult.Warning(
                Id,
                Title,
                "负载下风扇仍为 0 RPM。",
                details,
                "检查风扇供电与曲线；若为被动散热，请确认风道。");
        }

        if (fan > 0 && utilization >= 50 && fan < 10)
        {
            return HealthCheckResult.Warning(
                Id,
                Title,
                $"负载下风扇仅 {fan}%。",
                details,
                "检查风扇曲线与轴承状态。");
        }

        return HealthCheckResult.Passed(
            Id,
            Title,
            fan == 0 ? "空载下风扇停转，符合零转速策略。" : $"风扇 {fan}%，响应正常。",
            details);
    }
}
