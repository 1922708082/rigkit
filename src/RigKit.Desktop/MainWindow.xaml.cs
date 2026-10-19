using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RigKit.Desktop.Services;
using RigKit.Desktop.Views;
using Windows.Graphics;

namespace RigKit.Desktop;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SetBackdrop();
        ResizeWindow();

        var theme = ThemeService.FromConfig(App.CurrentApp.Config.Theme);
        ThemeService.Apply(theme, persist: false, window: this);
        NavView.IsPaneOpen = !App.CurrentApp.Config.SidebarCollapsed;

        NavView.SelectedItem = WorkbenchItem;
        if (ContentFrame.CurrentSourcePageType != typeof(WorkbenchPage))
        {
            ContentFrame.Navigate(typeof(WorkbenchPage));
        }
    }

    public void UpdateTitleBarTheme(ElementTheme theme)
    {
        var dark = theme == ElementTheme.Dark;
        var titleBar = AppWindow.TitleBar;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonForegroundColor = dark ? Colors.White : Colors.Black;
        titleBar.ButtonInactiveForegroundColor = dark ? Colors.Gray : Colors.DimGray;
        titleBar.ButtonHoverBackgroundColor = dark
            ? ColorHelper.FromArgb(24, 255, 255, 255)
            : ColorHelper.FromArgb(18, 0, 0, 0);
    }

    public void SetPaneOpen(bool open)
    {
        NavView.IsPaneOpen = open;
    }

    public void OpenSettings()
    {
        NavView.SelectedItem = SettingsItem;
    }

    private void SetBackdrop()
    {
        if (MicaController.IsSupported())
        {
            SystemBackdrop = new MicaBackdrop();
            return;
        }

        try
        {
            SystemBackdrop = new DesktopAcrylicBackdrop();
        }
        catch
        {
            // Windows 10 without acrylic support falls back to the page background.
        }
    }

    private void ResizeWindow()
    {
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var width = Math.Min(1440, area.WorkArea.Width - 80);
        var height = Math.Min(900, area.WorkArea.Height - 80);
        AppWindow.Resize(new SizeInt32(width, height));
        var x = area.WorkArea.X + (area.WorkArea.Width - width) / 2;
        var y = area.WorkArea.Y + (area.WorkArea.Height - height) / 2;
        AppWindow.Move(new PointInt32(x, y));
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is not NavigationViewItem item || item.Tag is not string tag)
        {
            return;
        }

        var pageType = tag switch
        {
            "workbench" => typeof(WorkbenchPage),
            "settings" => typeof(SettingsPage),
            _ => null
        };

        if (pageType is not null && ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }

    private void NavView_PaneOpening(NavigationView sender, object args) => SavePaneState(true);

    private void NavView_PaneClosing(NavigationView sender, NavigationViewPaneClosingEventArgs args) => SavePaneState(false);

    private void SavePaneState(bool open)
    {
        var app = App.CurrentApp;
        app.SaveConfig(app.Config with { SidebarCollapsed = !open });
    }
}
