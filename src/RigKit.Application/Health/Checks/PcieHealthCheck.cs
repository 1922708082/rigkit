using RigKit.Domain.Capabilities;
using RigKit.Domain.Health;

namespace RigKit.Application.Health.Checks;

public sealed class PcieHealthCheck : IHealthCheck
{
    public string Id => "pcie";

    public string Title => "PCIe 链路";

    public int Order => 20;

    public TimeSpan Timeout => TimeSpan.FromSeconds(5);

    public async Task<HealthCheckResult> RunAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var sample = await context.ReadSensors(context.Device.Id, cancellationToken).ConfigureAwait(false);
        var details = new List<string>
        {
            $"当前链路：Gen{sample.PcieGeneration?.ToString() ?? "--"} x{sample.PcieWidth?.ToString() ?? "--"}",
            $"传感器来源：{sample.Source}"
        };

        if (sample.PcieGeneration is null || sample.PcieWidth is null)
        {
            return HealthCheckResult.Warning(
                Id,
                Title,
                "驱动未报告 PCIe 链路信息。",
                details,
                "更新驱动或改用 nvidia-smi 复核。");
        }

        var generation = sample.PcieGeneration.Value;
        var width = sample.PcieWidth.Value;
        var isPascalMiningCard = SupportMatrix.IsPascalMiningCard(context.Device.Model);

        if (isPascalMiningCard && context.Device.Model.Contains("P106-090", StringComparison.OrdinalIgnoreCase))
        {
            return width >= 1
                ? HealthCheckResult.Passed(Id, Title, "P106-090 的 x1 链路符合该型号预期。", details)
                : HealthCheckResult.Warning(Id, Title, "P106-090 未报告有效链路宽度。", details);
        }

        if (isPascalMiningCard)
        {
            return width < 4
                ? HealthCheckResult.Warning(
                    Id,
                    Title,
                    $"P 系列链路为 x{width}，带宽受限。",
                    details,
                    "P 系列不做链路解锁承诺，按实验性使用。")
                : HealthCheckResult.Passed(Id, Title, $"PCIe Gen{generation} x{width}，符合 P 系列预期。", details);
        }

        if (generation <= 1)
        {
            return HealthCheckResult.Warning(
                Id,
                Title,
                "当前链路仍在 Gen1，带宽可能未解锁。",
                details,
                "可在改造中心执行 PCIe Gen2 解锁；执行前先创建快照。");
        }

        if (width < 4)
        {
            return HealthCheckResult.Warning(
                Id,
                Title,
                $"当前链路为 x{width}，带宽偏低。",
                details,
                "检查插槽、转接卡与 BIOS 设置。");
        }

        return HealthCheckResult.Passed(
            Id,
            Title,
            $"PCIe Gen{generation} x{width}，链路正常。",
            details);
    }
}
