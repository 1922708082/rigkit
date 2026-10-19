using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace RigKit.App.Services;

/// <summary>
/// Writes a readable crash report and a minidump. The dump path is best-effort:
/// a failure here must never mask the original exception.
/// </summary>
public static class CrashReporter
{
    private const uint MiniDumpWithDataSegs = 0x00000001;
    private const uint MiniDumpWithHandleData = 0x00000004;
    private const uint MiniDumpWithThreadInfo = 0x00001000;

    private static readonly object Gate = new();

    public static string CrashDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RigKit",
        "crashes");

    public static void Register(System.Windows.Application application)
    {
        application.DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public static string Write(Exception? exception, string source)
    {
        lock (Gate)
        {
            Directory.CreateDirectory(CrashDirectory);
            var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
            var basePath = Path.Combine(CrashDirectory, $"crash-{stamp}");
            var textPath = basePath + ".txt";
            var dumpPath = basePath + ".dmp";

            var dumpResult = TryWriteDump(dumpPath);
            var report = $"""
                RigKit crash report
                time: {DateTimeOffset.Now:O}
                source: {source}
                version: {typeof(App).Assembly.GetName().Version}
                os: {Environment.OSVersion}
                runtime: {Environment.Version}
                process: {Environment.ProcessId}
                dump: {(dumpResult ? dumpPath : "not written")}

                {exception?.ToString() ?? "no exception object"}
                """;

            File.WriteAllText(textPath, report);
            return textPath;
        }
    }

    private static void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        var path = Write(e.Exception, "DispatcherUnhandledException");
        MessageBox.Show(
            $"RigKit 遇到未处理异常，报告已写入：\n{path}",
            "RigKit 崩溃",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
        System.Windows.Application.Current?.Shutdown(1);
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Write(e.ExceptionObject as Exception, "AppDomain.UnhandledException");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Write(e.Exception, "TaskScheduler.UnobservedTaskException");
        e.SetObserved();
    }

    private static bool TryWriteDump(string path)
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            var handle = stream.SafeFileHandle.DangerousGetHandle();
            var type = MiniDumpWithDataSegs | MiniDumpWithHandleData | MiniDumpWithThreadInfo;
            return MiniDumpWriteDump(
                process.Handle,
                (uint)process.Id,
                handle,
                type,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("Dbghelp.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MiniDumpWriteDump(
        IntPtr processHandle,
        uint processId,
        IntPtr fileHandle,
        uint dumpType,
        IntPtr exceptionParam,
        IntPtr userStreamParam,
        IntPtr callbackParam);
}
