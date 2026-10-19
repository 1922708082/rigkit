using Microsoft.UI.Xaml;

namespace RigKit.Desktop.Services;

public static class ThemeService
{
    public static ElementTheme Current { get; private set; } = ElementTheme.Default;

    public static ElementTheme FromConfig(string? theme) =>
        string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase)
            ? ElementTheme.Dark
            : ElementTheme.Light;

    public static void Apply(ElementTheme theme, bool persist, Window? window = null)
    {
        Current = theme;
        var app = App.CurrentApp;
        var target = window ?? app.ShellWindow;
        if (target?.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme;
        }

        if (target is MainWindow mainWindow)
        {
            mainWindow.UpdateTitleBarTheme(theme);
        }

        if (persist)
        {
            app.SaveConfig(app.Config with
            {
                Theme = theme == ElementTheme.Dark ? "dark" : "light"
            });
        }
    }
}
