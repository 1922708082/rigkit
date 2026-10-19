using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RigKit.App.ViewModels;
using RigKit.Application.Devices;
using RigKit.Infrastructure.Nvidia;

namespace RigKit.App;

public partial class App : System.Windows.Application
{
    private FallbackDeviceProvider? _provider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _provider = new FallbackDeviceProvider(new NvmlProvider(), new NvidiaSmiProvider());
        var viewModel = new MainViewModel(new DeviceService(_provider));

        var smokeIndex = Array.FindIndex(
            e.Args,
            argument => string.Equals(argument, "--smoke", StringComparison.OrdinalIgnoreCase));

        if (smokeIndex >= 0)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var outputPath = smokeIndex + 1 < e.Args.Length
                ? e.Args[smokeIndex + 1]
                : Path.Combine(Environment.CurrentDirectory, "docs", "screenshots", "home-m0.png");

            try
            {
                await RunSmokeAsync(viewModel, outputPath);
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

        var window = new MainWindow(viewModel);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _provider?.Dispose();
        base.OnExit(e);
    }

    private static async Task RunSmokeAsync(MainViewModel viewModel, string outputPath)
    {
        await viewModel.InitializeAsync();
        viewModel.SeedHistoryForPreview();

        var window = new MainWindow(viewModel)
        {
            Width = 1360,
            Height = 860,
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

        var width = Math.Max(1, (int)window.ActualWidth);
        var height = Math.Max(1, (int)window.ActualHeight);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
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
}
