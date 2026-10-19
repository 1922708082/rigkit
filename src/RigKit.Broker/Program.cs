using RigKit.Broker;
using Serilog;

var logDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "RigKit",
    "logs");
Directory.CreateDirectory(logDirectory);
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.WithProperty("app", "RigKit.Broker")
    .WriteTo.File(
        Path.Combine(logDirectory, "broker-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        shared: true)
    .CreateLogger();

using var gate = new Semaphore(1, 1, @"Local\RigKit.Broker.SingleInstance");
if (!gate.WaitOne(0))
{
    Console.Error.WriteLine("RigKit Broker 已在运行。");
    Log.Warning("Broker already running");
    Log.CloseAndFlush();
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

    var devMode = args.Contains("--dev", StringComparer.OrdinalIgnoreCase);
    if (devMode)
    {
        Console.WriteLine("警告：--dev 已开启，跳过调用方镜像校验。仅用于本机联调。");
    }

    var idleTimeout = TimeSpan.FromSeconds(60);
    var idleArgument = args.FirstOrDefault(
        argument => argument.StartsWith("--idle=", StringComparison.OrdinalIgnoreCase));
    if (idleArgument is not null &&
        int.TryParse(idleArgument[7..], out var idleSeconds) &&
        idleSeconds > 0)
    {
        idleTimeout = TimeSpan.FromSeconds(idleSeconds);
    }

    var server = new PipeServer(pipeName, devMode, idleTimeout);
    var once = args.Contains("--once", StringComparer.OrdinalIgnoreCase);
    Log.Information("Broker starting pipe={Pipe} dev={Dev} idle={Idle}s once={Once}", pipeName, devMode, idleTimeout.TotalSeconds, once);
    await server.RunAsync(cancellation.Token, once);
    Log.Information("Broker stopped");
    return 0;
}
catch (OperationCanceledException)
{
    return 0;
}
finally
{
    Log.CloseAndFlush();
    gate.Release();
}
