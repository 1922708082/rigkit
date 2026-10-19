using RigKit.Domain.Health;

namespace RigKit.Application.Health.Checks;

public sealed class VramIntegrityHealthCheck : IHealthCheck
{
    public string Id => "vram";

    public string Title => "显存完整性";

    public int Order => 50;

    public TimeSpan Timeout => TimeSpan.FromSeconds(30);

    public Task<HealthCheckResult> RunAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.VramTester is null)
        {
            return Task.FromResult(HealthCheckResult.Skipped(
                Id,
                Title,
                "未配置显存测试器。",
                new[] { "CUDA 测试器未注入，跳过显存读写。" }));
        }

        var result = context.VramTester.Test(context.Device.Id, 64, cancellationToken);
        var details = new List<string>
        {
            $"测试容量：64 MB",
            $"校验字节：{result.BytesTested / 1024 / 1024} MB",
            $"不一致数量：{result.Mismatches}"
        };

        if (!result.Available)
        {
            return Task.FromResult(HealthCheckResult.Warning(
                Id,
                Title,
                "CUDA 驱动不可用，无法执行显存读写校验。",
                details,
                "安装完整 NVIDIA 驱动后重试；矿卡请确认驱动支持 CUDA。"));
        }

        if (!result.Passed)
        {
            return Task.FromResult(HealthCheckResult.Failed(
                Id,
                Title,
                result.Mismatches > 0
                    ? $"显存校验发现 {result.Mismatches} 个不一致。"
                    : "显存读写校验失败。",
                details,
                "重复测试确认；持续失败时降低显存频率并检查散热。"));
        }

        return Task.FromResult(HealthCheckResult.Passed(
            Id,
            Title,
            "64 MB 双模式读写校验通过。",
            details));
    }
}
