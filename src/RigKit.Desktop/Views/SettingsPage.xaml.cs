using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RigKit.Desktop.Services;

namespace RigKit.Desktop.Views;

public sealed partial class SettingsPage : Page
{
    private bool _initializing;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _initializing = true;
        DarkModeToggle.IsOn = ThemeService.Current == ElementTheme.Dark;
        SidebarToggle.IsOn = App.CurrentApp.Config.SidebarCollapsed;
        VersionText.Text = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
        _initializing = false;
    }

    private void DarkModeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        ThemeService.Apply(
            DarkModeToggle.IsOn ? ElementTheme.Dark : ElementTheme.Light,
            persist: true);
    }

    private void SidebarToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        App.CurrentApp.ShellWindow?.SetPaneOpen(!SidebarToggle.IsOn);
    }
}
