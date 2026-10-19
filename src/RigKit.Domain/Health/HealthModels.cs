using RigKit.Domain.Devices;

namespace RigKit.Domain.Health;

public enum HealthStatus
{
    Pass,
    Warning,
    Fail,
    Skipped
}

public sealed record HealthCheckResult(
    string CheckId,
    string Title,
    HealthStatus Status,
    string Summary,
    IReadOnlyList<string> Details,
    TimeSpan Duration,
    string? Remediation = null)
{
    public static HealthCheckResult Passed(
        string checkId,
        string title,
        string summary,
        IReadOnlyList<string>? details = null,
        string? remediation = null) =>
        new(checkId, title, HealthStatus.Pass, summary, details ?? Array.Empty<string>(), TimeSpan.Zero, remediation);

    public static HealthCheckResult Warning(
        string checkId,
        string title,
        string summary,
        IReadOnlyList<string>? details = null,
        string? remediation = null) =>
        new(checkId, title, HealthStatus.Warning, summary, details ?? Array.Empty<string>(), TimeSpan.Zero, remediation);

    public static HealthCheckResult Failed(
        string checkId,
        string title,
        string summary,
        IReadOnlyList<string>? details = null,
        string? remediation = null) =>
        new(checkId, title, HealthStatus.Fail, summary, details ?? Array.Empty<string>(), TimeSpan.Zero, remediation);

    public static HealthCheckResult Skipped(
        string checkId,
        string title,
        string summary,
        IReadOnlyList<string>? details = null) =>
        new(checkId, title, HealthStatus.Skipped, summary, details ?? Array.Empty<string>(), TimeSpan.Zero);
}

public sealed record HealthCheckProgress(
    int Index,
    int Total,
    string CheckId,
    string Title,
    HealthStatus? Status);

public sealed record HealthReport(
    DeviceId DeviceId,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    IReadOnlyList<HealthCheckResult> Results)
{
    public int PassCount => Results.Count(result => result.Status == HealthStatus.Pass);

    public int WarningCount => Results.Count(result => result.Status == HealthStatus.Warning);

    public int FailCount => Results.Count(result => result.Status == HealthStatus.Fail);

    public bool HasFailures => FailCount > 0;

    public HealthStatus OverallStatus =>
        HasFailures
            ? HealthStatus.Fail
            : WarningCount > 0
                ? HealthStatus.Warning
                : HealthStatus.Pass;
}
