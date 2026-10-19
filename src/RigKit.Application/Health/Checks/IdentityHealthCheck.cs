using RigKit.Domain.Capabilities;
using RigKit.Domain.Health;

namespace RigKit.Application.Health.Checks;

public sealed class IdentityHealthCheck : IHealthCheck
{
    public string Id => "identity";

    public string Title => "识别与真伪";

    public int Order => 10;

    public TimeSpan Timeout => TimeSpan.FromSeconds(5);

    public Task<HealthCheckResult> RunAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var device = context.Device;
        var details = new List<string>
        {
            $"型号：{device.Model}",
            $"架构：{device.Architecture}",
            $"显存：{device.DisplayMemory}",
            $"PCI ID：{device.PciIds}",
            $"驱动：{device.DriverVersion}",
            "vBIOS 哈希：M1 后续通过 nvflash / 注册表接入"
        };

        if (string.IsNullOrWhiteSpace(device.Model))
        {
            return Task.FromResult(HealthCheckResult.Failed(
                Id,
                Title,
                "设备型号为空，无法确认识别结果。",
                details,
                "重新枚举设备；若仍为空，检查驱动是否正常加载。"));
        }

        if (!IsValidNvidiaPciId(device.PciIds))
        {
            return Task.FromResult(HealthCheckResult.Failed(
                Id,
                Title,
                $"PCI ID 格式异常：{device.PciIds}",
                details,
                "核对设备是否通过 PCI 直通/转接；异常 ID 可能来自虚拟化或改装。"));
        }

        var deviceHex = device.PciIds[5..];
        if (VerifiedCardCatalog.TryGetExpectedDeviceId(device.Model, out var expected))
        {
            if (!string.Equals(deviceHex, expected, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(HealthCheckResult.Failed(
                    Id,
                    Title,
                    $"PCI ID 与已验证记录不一致：期望 {expected}，实际 {deviceHex}。",
                    details,
                    "核对卡型与 PCI ID；改装卡请以实测为准并保留证据。"));
            }

            return Task.FromResult(HealthCheckResult.Passed(
                Id,
                Title,
                $"识别为 {device.Model}，PCI ID 与已验证记录一致。",
                details));
        }

        if (string.Equals(device.Architecture, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(HealthCheckResult.Warning(
                Id,
                Title,
                "型号已识别，但架构未知，未纳入已验证表。",
                details,
                "补充 vBIOS 与子系统信息后再判定真伪。"));
        }

        return Task.FromResult(HealthCheckResult.Warning(
            Id,
            Title,
            $"识别为 {device.Model}，该型号尚未纳入已验证表。",
            details,
            "首版仅对 CMP 90HX 做了真伪基线；P 系列按实验性处理。"));
    }

    private static bool IsValidNvidiaPciId(string? value)
    {
        if (value is null ||
            value.Length != 9 ||
            !value.StartsWith("10DE:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return value[5..].All(Uri.IsHexDigit);
    }
}
