using RigKit.Broker;

using var gate = new Semaphore(1, 1, @"Local\RigKit.Broker.SingleInstance");
if (!gate.WaitOne(0))
{
    Console.Error.WriteLine("RigKit Broker 已在运行。");
    return 1;
}

try
{
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };

    var pipeName = args
        .FirstOrDefault(argument => argument.StartsWith("--pipe=", StringComparison.OrdinalIgnoreCase))
        ?[7..];

    var server = new PipeServer(pipeName);
    var once = args.Contains("--once", StringComparer.OrdinalIgnoreCase);
    await server.RunAsync(cancellation.Token, once);
    return 0;
}
catch (OperationCanceledException)
{
    return 0;
}
finally
{
    gate.Release();
}
