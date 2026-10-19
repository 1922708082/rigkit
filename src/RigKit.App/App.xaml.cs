using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RigKit.App.Services;
using RigKit.App.ViewModels;
using RigKit.Application.Devices;
using RigKit.Application.Health;
using RigKit.Application.Health.Checks;
using RigKit.Infrastructure.Configuration;
using RigKit.Infrastructure.Nvidia;
using RigKit.Infrastructure.Storage;
using Serilog;

namespace RigKit.App;

public partial class App : System.Windows.Application
{
    private FallbackDeviceProvider? _provider;
    private DeviceChangeWatcher? _deviceWatcher;
    private SingleInstanceGuard? _singleInstance;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ConfigureLogging();
        CrashReporter.Register(this);

        Log.Information("RigKit starting version={Version} args={Args}", typeof(App).Assembly.GetName().Version, e.Args);
        StartupDiagnostics.Write(
            $"start version={typeof(App).Assembly.GetName().Version} os={Environment.OSVersion} args={string.Join(' ', e.Args)}");

        if (e.Args.Contains("--crash-test", StringComparer.OrdinalIgnoreCase))
        {
            var report = CrashReporter.Write(new InvalidOperationException("RigKit crash test"), "crash-test");
            StartupDiagnostics.Write($"crash-test report={report}");
            Shutdown(1);
            return;
        }

        var smokeIndex = Array.FindIndex(
            e.Args,
            argument => string.Equals(argument, "--smoke", StringComparison.OrdinalIgnoreCase));

        if (smokeIndex < 0)
        {
            try
            {
                _singleInstance = new SingleInstanceGuard();
                if (!_singleInstance.IsFirstInstance)
                {
                    StartupDiagnostics.Write("second instance detected, activated existing window");
                    _singleInstance.Dispose();
                    Shutdown(0);
                    return;
                }
            }
            catch (Exception ex)
            {
                StartupDiagnostics.Write($"single-instance guard failed: {ex.Message}");
            }
        }

        _provider = new FallbackDeviceProvider(new NvmlProvider(), new NvidiaSmiProvider());
        var history = new SensorHistoryStore();
        var healthRunner = new HealthCheckRunner(new IHealthCheck[]
        {
            new IdentityHealthCheck(),
            new PcieHealthCheck(),
            new DriverHealthCheck(),
            new PowerHealthCheck()
        });
        var viewModel = new MainViewModel(new DeviceService(_provider), history, healthRunner);
        var config = new JsonConfigStore().LoadOrCreate();

        if (smokeIndex >= 0)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var outputPath = smokeIndex + 1 < e.Args.Length
                ? e.Args[smokeIndex + 1]
                : Path.Combine(Environment.CurrentDirectory, "docs", "screenshots", "home-m0.png");

            try
            {
                var width = ReadIntArgument(e.Args, "--width", 1360);
                var height = ReadIntArgument(e.Args, "--height", 860);
                await RunSmokeAsync(viewModel, outputPath, width, height);
                Shutdown(0);
            }
            catch (Exception ex)
            {
                var logPath = Path.Combine(Path.GetTempPath(), "rigkit-smoke-error.txt");
                await File.WriteAllTextAsync(logPath, ex.ToString());
                Shutdown(1);
            }

            return;
        }

        var window = new MainWindow(viewModel, TimeSpan.FromSeconds(config.PollIntervalSeconds));
        MainWindow = window;
        window.Show();

        if (_singleInstance is not null)
        {
            _singleInstance.ActivationRequested += (_, _) => Dispatcher.Invoke(ActivateMainWindow);
        }

        _deviceWatcher = new DeviceChangeWatcher();
        _deviceWatcher.DevicesChanged += (_, _) =>
            Dispatcher.InvokeAsync(() => viewModel.ReloadDevicesAsync());
        _deviceWatcher.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _deviceWatcher?.Dispose();
        _singleInstance?.Dispose();
        _provider?.Dispose();
        Log.Information("RigKit exiting code={ExitCode}", e.ApplicationExitCode);
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static void ConfigureLogging()
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RigKit",
                "logs");
            Directory.CreateDirectory(directory);
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.WithProperty("app", "RigKit")
                .WriteTo.File(
                    Path.Combine(directory, "rigkit-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    shared: true)
                .CreateLogger();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Logging must not block startup; StartupDiagnostics still works.
        }
    }

    private void ActivateMainWindow()
    {
        if (MainWindow is not { } window)
        {
            return;
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Show();
        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
    }

    private static async Task RunSmokeAsync(
        MainViewModel viewModel,
        string outputPath,
        int width,
        int height)
    {
        await viewModel.InitializeAsync();
        viewModel.SeedHistoryForPreview();

        var window = new MainWindow(viewModel)
        {
            Width = Math.Max(1000, width),
            Height = Math.Max(700, height),
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000,
            Top = 0,
            ShowInTaskbar = false,
            ShowActivated = false
        };

        window.Show();
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        await Task.Delay(3200);
        window.UpdateLayout();

        var pixelWidth = Math.Max(1, (int)window.ActualWidth);
        var pixelHeight = Math.Max(1, (int)window.ActualHeight);
        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);

        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        await using var stream = File.Create(fullPath);
        encoder.Save(stream);

        window.Close();
    }

    private static int ReadIntArgument(string[] args, string name, int fallback)
    {
        var index = Array.FindIndex(
            args,
            argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 &&
               index + 1 < args.Length &&
               int.TryParse(args[index + 1], out var value)
            ? value
            : fallback;
    }
}
