using System.Globalization;
using System.Text;

namespace RigKit.Infrastructure.Nvidia;

internal static class Csv
{
    public static string[] Split(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        foreach (var ch in line)
        {
            if (ch == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (ch == ',' && !quoted)
            {
                fields.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        fields.Add(current.ToString().Trim());
        return fields.ToArray();
    }

    public static int? ToInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains("N/A", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    public static double? ToDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains("N/A", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }
}
