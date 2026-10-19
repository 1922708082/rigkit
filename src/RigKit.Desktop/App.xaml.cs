using Microsoft.UI.Xaml;
using RigKit.Application.Devices;
using RigKit.Infrastructure.Configuration;
using RigKit.Infrastructure.Nvidia;

namespace RigKit.Desktop;

public partial class App : Microsoft.UI.Xaml.Application
{
    private readonly FallbackDeviceProvider _provider;

    public App()
    {
        InitializeComponent();
        _provider = new FallbackDeviceProvider(new NvmlProvider(), new NvidiaSmiProvider());
        Devices = new DeviceService(_provider);
        ConfigStore = new JsonConfigStore();
        Config = ConfigStore.LoadOrCreate();
        UnhandledException += (_, args) =>
        {
            System.Diagnostics.Debug.WriteLine(args.Exception);
        };
    }

    public static App CurrentApp => (App)Current;

    public DeviceService Devices { get; }

    public JsonConfigStore ConfigStore { get; }

    public AppConfig Config { get; private set; }

    public MainWindow? ShellWindow { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        ShellWindow = new MainWindow();
        ShellWindow.Activate();
        if (Environment.GetCommandLineArgs().Any(argument =>
                string.Equals(argument, "--settings", StringComparison.OrdinalIgnoreCase)))
        {
            ShellWindow.OpenSettings();
        }
    }

    public void SaveConfig(AppConfig config)
    {
        Config = config;
        ConfigStore.Save(config);
    }
}
