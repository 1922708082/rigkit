using System.Windows.Media;
using RigKit.Domain.Health;

namespace RigKit.App.ViewModels;

public sealed class HealthCheckItem
{
    public HealthCheckItem(HealthCheckResult result)
    {
        Title = result.Title;
        Summary = result.Summary;
        Detail = result.Details.Count > 0 ? string.Join(Environment.NewLine, result.Details) : result.Summary;
        (StatusText, Accent) = result.Status switch
        {
            HealthStatus.Pass => ("通过", Brush("#12805C")),
            HealthStatus.Warning => ("注意", Brush("#B54708")),
            HealthStatus.Fail => ("失败", Brush("#D92D20")),
            _ => ("跳过", Brush("#98A2B3"))
        };
        Background = result.Status switch
        {
            HealthStatus.Pass => Brush("#E9F7F1"),
            HealthStatus.Warning => Brush("#FEF4E6"),
            HealthStatus.Fail => Brush("#FEF0EE"),
            _ => Brush("#F1F3F6")
        };
    }

    public string Title { get; }

    public string Summary { get; }

    public string Detail { get; }

    public string StatusText { get; }

    public Brush Accent { get; }

    public Brush Background { get; }

    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
