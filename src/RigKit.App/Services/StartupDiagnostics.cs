using System.IO;

namespace RigKit.App.Services;

public static class StartupDiagnostics
{
    private static readonly object Gate = new();

    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RigKit",
        "logs",
        "startup.log");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                var directory = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(
                    LogPath,
                    $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
        }
        catch (IOException)
        {
            // Diagnostics must never prevent startup.
        }
        catch (UnauthorizedAccessException)
        {
            // Diagnostics must never prevent startup.
        }
    }
}
