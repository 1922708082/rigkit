using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using RigKit.App.Services;
using RigKit.App.ViewModels;
using Serilog;

namespace RigKit.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _timer;
    private bool _initialized;
    private bool _refreshing;

    public MainWindow(MainViewModel viewModel, TimeSpan? pollInterval = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _timer = new DispatcherTimer { Interval = pollInterval ?? TimeSpan.FromSeconds(1) };
        _timer.Tick += Timer_Tick;
        Loaded += MainWindow_Loaded;
        Closed += (_, _) => _timer.Stop();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await _viewModel.InitializeAsync();
        StartupDiagnostics.Write(
            $"ui initialized device={_viewModel.DeviceName} provider={_viewModel.ProviderText} error={_viewModel.ErrorText}");
        Log.Information(
            "UI initialized device={Device} provider={Provider} error={Error}",
            _viewModel.DeviceName,
            _viewModel.ProviderText,
            _viewModel.ErrorText);
        _timer.Start();
    }

    private async void Timer_Tick(object? sender, EventArgs e)
    {
        await RefreshSafeAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshSafeAsync();
    }

    private async void Rescan_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.ReloadDevicesAsync();
    }

    private async Task RefreshSafeAsync()
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        try
        {
            await _viewModel.RefreshAsync();
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The mouse button was released before DragMove started.
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }
}
