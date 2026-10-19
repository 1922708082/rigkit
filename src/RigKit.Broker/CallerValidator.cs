using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace RigKit.Broker;

internal static class CallerValidator
{
    private static readonly string[] AllowedExecutables =
    {
        "RigKit.App.exe",
        "RigKit.Probe.exe",
        "RigKit.Broker.exe"
    };

    public static CallerInfo Inspect(NamedPipeServerStream pipe, bool allowAny)
    {
        try
        {
            if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var processId) || processId == 0)
            {
                return CallerInfo.Unknown;
            }

            using var process = Process.GetProcessById((int)processId);
            var path = process.MainModule?.FileName ?? string.Empty;
            var allowed = allowAny || IsAllowedExecutable(path);
            return new CallerInfo((int)processId, process.ProcessName, path, allowed);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return CallerInfo.Unknown;
        }
    }

    private static bool IsAllowedExecutable(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var fileName = Path.GetFileName(path);
        if (!AllowedExecutables.Contains(fileName, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var directory = Path.GetDirectoryName(path)?.TrimEnd(Path.DirectorySeparatorChar);
        var brokerDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        return string.Equals(directory, brokerDirectory, StringComparison.OrdinalIgnoreCase);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(
        SafePipeHandle pipe,
        out uint clientProcessId);
}
