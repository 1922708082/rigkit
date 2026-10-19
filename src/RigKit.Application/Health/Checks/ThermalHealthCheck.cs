using RigKit.Domain.Health;

namespace RigKit.Application.Health.Checks;

public sealed class ThermalHealthCheck : IHealthCheck
{
    private const int SampleCount = 5;
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(500);

    public string Id => "thermal";

    public string Title => "温度与散热";

    public int Order => 60;

    public TimeSpan Timeout => TimeSpan.FromSeconds(20);

    public async Task<HealthCheckResult> RunAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var temperatures = new List<double>();
        var fans = new List<int>();
        var failures = 0;

        for (var index = 0; index < SampleCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var sample = await context.ReadSensors(context.Device.Id, cancellationToken)
                    .ConfigureAwait(false);
                if (sample.TemperatureC is double temperature)
                {
                    temperatures.Add(temperature);
                }

                if (sample.FanPercent is int fan)
                {
                    fans.Add(fan);
                }
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                failures++;
            }

            if (index < SampleCount - 1)
            {
                await Task.Delay(SampleInterval, cancellationToken).ConfigureAwait(false);
            }
        }

        var details = new List<string>
        {
            $"采样：{SampleCount} 次 / {(SampleCount - 1) * SampleInterval.TotalSeconds:0.#} 秒",
            $"读取失败：{failures} 次"
        };

        if (temperatures.Count == 0)
        {
            return HealthCheckResult.Warning(
                Id,
                Title,
                failures >= 3 ? "连续多次读取温度失败。" : "驱动未报告温度。",
                details,
                "更新驱动后重试；若持续失败，检查传感器是否被矿卡驱动屏蔽。");
        }

        var max = temperatures.Max();
        var min = temperatures.Min();
        details.Add($"温度范围：{min:0.#} ~ {max:0.#} ℃");
        if (fans.Count > 0)
        {
            details.Add($"风扇：{fans.Min()}% ~ {fans.Max()}%");
        }

        if (failures >= 3)
        {
            return HealthCheckResult.Warning(
                Id,
                Title,
                "采样期间多次读取失败，数据可能不完整。",
                details,
                "重试一次；持续失败时检查驱动与供电。");
        }

        if (max >= 85)
        {
            return HealthCheckResult.Failed(
                Id,
                Title,
                $"最高温度 {max:0.#} ℃，已进入高风险区。",
                details,
                "立即降低功耗/负载并检查散热；不要继续调优。");
        }

        if (max >= 80)
        {
            return HealthCheckResult.Warning(
                Id,
                Title,
                $"最高温度 {max:0.#} ℃，偏高。",
                details,
                "清理风扇与硅脂，或降低功耗墙。");
        }

        return HealthCheckResult.Passed(
            Id,
            Title,
            $"温度 {min:0.#} ~ {max:0.#} ℃，短时采样正常。",
            details);
    }
}
