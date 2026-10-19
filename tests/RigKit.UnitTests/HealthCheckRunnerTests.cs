using RigKit.Application.Health;
using RigKit.Domain.Health;

namespace RigKit.UnitTests;

public sealed class HealthCheckRunnerTests
{
    [Fact]
    public async Task RunAsync_ContinuesAfterFailure_AndRecordsTimeout()
    {
        var runner = new HealthCheckRunner(new IHealthCheck[]
        {
            new FakeCheck(
                "pass",
                "Pass",
                1,
                TimeSpan.FromSeconds(1),
                (_, _) => Task.FromResult(HealthCheckResult.Passed("pass", "Pass", "ok"))),
            new FakeCheck(
                "boom",
                "Boom",
                2,
                TimeSpan.FromSeconds(1),
                (_, _) => throw new InvalidOperationException("boom")),
            new FakeCheck(
                "timeout",
                "Timeout",
                3,
                TimeSpan.FromMilliseconds(30),
                async (_, cancellationToken) =>
                {
                    await Task.Delay(500, cancellationToken);
                    return HealthCheckResult.Passed("timeout", "Timeout", "should not reach");
                })
        });

        var report = await runner.RunAsync(HealthTestData.CreateContext());

        Assert.Equal(3, report.Results.Count);
        Assert.Equal(1, report.PassCount);
        Assert.Equal(2, report.FailCount);
        Assert.Equal(HealthStatus.Fail, report.OverallStatus);
        Assert.Contains(report.Results, result => result.CheckId == "boom" && result.Summary == "检查执行失败。");
        Assert.Contains(report.Results, result => result.CheckId == "timeout" && result.Summary.Contains("超时"));
    }

    [Fact]
    public async Task RunAsync_ReportsProgressForEachCheck()
    {
        var runner = new HealthCheckRunner(new IHealthCheck[]
        {
            new FakeCheck(
                "one",
                "One",
                1,
                TimeSpan.FromSeconds(1),
                (_, _) => Task.FromResult(HealthCheckResult.Passed("one", "One", "ok"))),
            new FakeCheck(
                "two",
                "Two",
                2,
                TimeSpan.FromSeconds(1),
                (_, _) => Task.FromResult(HealthCheckResult.Warning("two", "Two", "warn")))
        });
        var progress = new List<HealthCheckProgress>();

        await runner.RunAsync(
            HealthTestData.CreateContext(),
            new InlineProgress<HealthCheckProgress>(progress.Add));

        Assert.Equal(4, progress.Count);
        Assert.Equal(HealthStatus.Pass, progress[1].Status);
        Assert.Equal(HealthStatus.Warning, progress[3].Status);
    }

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _report;

        public InlineProgress(Action<T> report)
        {
            _report = report;
        }

        public void Report(T value) => _report(value);
    }

    private sealed class FakeCheck : IHealthCheck
    {
        private readonly Func<HealthCheckContext, CancellationToken, Task<HealthCheckResult>> _run;

        public FakeCheck(
            string id,
            string title,
            int order,
            TimeSpan timeout,
            Func<HealthCheckContext, CancellationToken, Task<HealthCheckResult>> run)
        {
            Id = id;
            Title = title;
            Order = order;
            Timeout = timeout;
            _run = run;
        }

        public string Id { get; }

        public string Title { get; }

        public int Order { get; }

        public TimeSpan Timeout { get; }

        public Task<HealthCheckResult> RunAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default) =>
            _run(context, cancellationToken);
    }
}
