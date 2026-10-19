using RigKit.Domain.Devices;
using RigKit.Domain.Health;

namespace RigKit.Application.Health.Checks;

public sealed class DriverHealthCheck : IHealthCheck
{
    public string Id => "driver";

    public string Title => "驱动与签名";

    public int Order => 30;

    public TimeSpan Timeout => TimeSpan.FromSeconds(3);

    public Task<HealthCheckResult> RunAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var device = context.Device;
        var details = new List<string>
        {
            $"驱动版本：{device.DriverVersion}",
            $"设备状态：{device.State}",
            $"状态说明：{device.StatusDetail}",
            "Code 43 / 14：M1 后续通过 WMI ConfigManagerErrorCode 补充"
        };

        if (string.IsNullOrWhiteSpace(device.DriverVersion) ||
            string.Equals(device.DriverVersion, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(HealthCheckResult.Failed(
                Id,
                Title,
                "未读到驱动版本。",
                details,
                "安装 NVIDIA 驱动后重新扫描；矿卡请确认驱动来源可信。"));
        }

        if (device.State == DeviceState.Error)
        {
            return Task.FromResult(HealthCheckResult.Failed(
                Id,
                Title,
                "设备处于错误状态。",
                details,
                "在设备管理器中查看错误码，必要时重装驱动。"));
        }

        if (!Version.TryParse(device.DriverVersion, out var version))
        {
            return Task.FromResult(HealthCheckResult.Warning(
                Id,
                Title,
                $"驱动版本格式无法解析：{device.DriverVersion}",
                details,
                "核对 nvidia-smi 输出与驱动安装记录。"));
        }

        if (version.Major < 500)
        {
            return Task.FromResult(HealthCheckResult.Warning(
                Id,
                Title,
                $"驱动版本较旧：{device.DriverVersion}",
                details,
                "更新到支持当前卡型的驱动；更新前记录版本以便回滚。"));
        }

        return Task.FromResult(HealthCheckResult.Passed(
            Id,
            Title,
            $"驱动 {device.DriverVersion} 已加载。",
            details));
    }
}
