using System.Windows.Media;

namespace RigKit.App.ViewModels;

public enum RiskSeverity
{
    Info,
    Warning,
    Danger
}

public sealed class RiskItem
{
    public RiskItem(string title, string detail, RiskSeverity severity)
    {
        Title = title;
        Detail = detail;
        Severity = severity;
        (Accent, Background) = severity switch
        {
            RiskSeverity.Danger => (Brush("#D92D20"), Brush("#FEF0EE")),
            RiskSeverity.Warning => (Brush("#B54708"), Brush("#FEF4E6")),
            _ => (Brush("#2563EB"), Brush("#EFF4FF"))
        };
    }

    public string Title { get; }

    public string Detail { get; }

    public RiskSeverity Severity { get; }

    public Brush Accent { get; }

    public Brush Background { get; }

    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
