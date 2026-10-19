namespace RigKit.Domain.Operations;

public enum RiskLevel
{
    Safe,
    Low,
    Medium,
    High,
    Critical
}

public enum OperationState
{
    Draft,
    Preflight,
    AwaitingConfirmation,
    Snapshotting,
    Executing,
    Verifying,
    Succeeded,
    Failed,
    RollingBack,
    RolledBack,
    ManualIntervention
}

public sealed record Precondition(
    string Key,
    string Description,
    bool IsSatisfied,
    string? Remediation);

public sealed record OperationStep(
    int Order,
    string Title,
    string CommandId,
    IReadOnlyDictionary<string, string> Arguments,
    bool RequiresReboot);

public sealed record OperationPlan(
    Guid Id,
    string Title,
    RiskLevel Risk,
    IReadOnlyList<Precondition> Preconditions,
    IReadOnlyList<OperationStep> Steps,
    string Rollback,
    string Explanation)
{
    public bool CanExecute => Preconditions.All(p => p.IsSatisfied);
}

public sealed record Snapshot(
    Guid Id,
    DateTimeOffset CreatedAt,
    string Operation,
    IReadOnlyList<string> Items,
    string ManifestHash);
