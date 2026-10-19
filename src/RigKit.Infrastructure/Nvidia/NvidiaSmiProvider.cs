using System.Diagnostics;
using RigKit.Application.Abstractions;
using RigKit.Domain.Devices;

namespace RigKit.Infrastructure.Nvidia;

/// <summary>
/// Read-only provider backed by nvidia-smi. This is the fallback path for cards
/// whose driver does not expose NVML (common with mining-specific drivers).
/// </summary>
public sealed class NvidiaSmiProvider : IDeviceProvider
{
    private const string DeviceQuery =
        "index,name,memory.total,driver_version,pci.bus_id,pci.device_id,pcie.link.gen.max,pcie.link.width.max";

    private const string SensorQuery =
        "temperature.gpu,power.draw,power.limit,utilization.gpu,memory.used,memory.total," +
        "clocks.current.graphics,clocks.current.memory,fan.speed,pstate," +
        "pcie.link.gen.current,pcie.link.width.current";

    private readonly string _executable;

    public NvidiaSmiProvider(string? executablePath = null)
    {
        _executable = string.IsNullOrWhiteSpace(executablePath) ? LocateExecutable() : executablePath;
    }

    public string Name => "nvidia-smi";

    public async Task<IReadOnlyList<Device>> EnumerateAsync(CancellationToken cancellationToken = default)
    {
        var output = await RunAsync(DeviceQuery, deviceId: null, cancellationToken).ConfigureAwait(false);
        var devices = new List<Device>();

        foreach (var line in ReadLines(output))
        {
            var fields = Csv.Split(line);
            if (fields.Length < 8)
            {
                continue;
            }

            var deviceId = NormalizeDeviceId(fields[5]);
            var busId = fields[4];
            var pciIds = string.IsNullOrEmpty(deviceId) ? string.Empty : $"10DE:{deviceId}";
            var pnpId = string.IsNullOrEmpty(deviceId)
                ? $"PCI\\VEN_10DE&DEV_UNKNOWN"
                : $"PCI\\VEN_10DE&DEV_{deviceId}";

            devices.Add(new Device(
                new DeviceId(busId, pnpId),
                fields[1],
                GuessArchitecture(fields[1]),
                DeviceVendor.Nvidia,
                pciIds,
                fields[3],
                Csv.ToInt(fields[2]) ?? 0,
                DeviceState.Ok,
                "nvidia-smi"));
        }

        return devices;
    }

    public async Task<SensorSample> ReadSensorsAsync(
        DeviceId deviceId,
        CancellationToken cancellationToken = default)
    {
        var output = await RunAsync(SensorQuery, deviceId.PciLocation, cancellationToken).ConfigureAwait(false);
        var line = ReadLines(output).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(line))
        {
            return SensorSample.Empty(deviceId, SensorSource.NvidiaSmi);
        }

        var fields = Csv.Split(line);
        if (fields.Length < 12)
        {
            return SensorSample.Empty(deviceId, SensorSource.NvidiaSmi);
        }

        return new SensorSample(
            deviceId,
            DateTimeOffset.Now,
            Csv.ToDouble(fields[0]),
            Csv.ToDouble(fields[1]),
            Csv.ToDouble(fields[2]),
            Csv.ToDouble(fields[3]),
            Csv.ToInt(fields[4]),
            Csv.ToInt(fields[5]),
            Csv.ToInt(fields[6]),
            Csv.ToInt(fields[7]),
            Csv.ToInt(fields[8]),
            string.IsNullOrWhiteSpace(fields[9]) ? null : fields[9],
            Csv.ToInt(fields[10]),
            Csv.ToInt(fields[11]),
            SensorSource.NvidiaSmi);
    }

    private async Task<string> RunAsync(
        string query,
        string? deviceId,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(_executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add($"--query-gpu={query}");
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            startInfo.ArgumentList.Add($"--id={deviceId}");
        }

        startInfo.ArgumentList.Add("--format=csv,noheader,nounits");

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new NvidiaSmiException($"无法启动 nvidia-smi：{ex.Message}", ex);
        }

        if (process is null)
        {
            throw new NvidiaSmiException("无法启动 nvidia-smi：进程对象为空。");
        }

        using (process)
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));

            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                TryKill(process);
                throw new NvidiaSmiException("nvidia-smi 执行超时（10 秒）。");
            }

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                throw new NvidiaSmiException($"nvidia-smi 返回 {process.ExitCode}：{detail.Trim()}");
            }

            return stdout;
        }
    }

    private static IEnumerable<string> ReadLines(string output) =>
        output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0);

    private static string NormalizeDeviceId(string value)
    {
        var normalized = value.Trim();
        if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[2..];
        }

        normalized = normalized.ToUpperInvariant();

        // Some drivers return the combined "device+vendor" form, e.g. 220D10DE.
        if (normalized.Length > 4)
        {
            normalized = normalized.StartsWith("10DE", StringComparison.Ordinal)
                ? normalized[4..]
                : normalized[..4];
        }

        return normalized;
    }

    private static string GuessArchitecture(string model)
    {
        if (model.Contains("CMP 90HX", StringComparison.OrdinalIgnoreCase)) return "GA102";
        if (model.Contains("CMP 70HX", StringComparison.OrdinalIgnoreCase)) return "GA104";
        if (model.Contains("CMP 50HX", StringComparison.OrdinalIgnoreCase)) return "TU102";
        if (model.Contains("CMP 40HX", StringComparison.OrdinalIgnoreCase)) return "TU106";
        if (model.Contains("CMP 30HX", StringComparison.OrdinalIgnoreCase)) return "TU116";
        if (model.Contains("CMP 170HX", StringComparison.OrdinalIgnoreCase)) return "GA100";
        if (model.Contains("P102", StringComparison.OrdinalIgnoreCase)) return "GP102";
        if (model.Contains("P104", StringComparison.OrdinalIgnoreCase)) return "GP104";
        if (model.Contains("P106", StringComparison.OrdinalIgnoreCase)) return "GP106";
        return "Unknown";
    }

    private static string LocateExecutable()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvidia-smi.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "NVIDIA Corporation",
                "NVSMI",
                "nvidia-smi.exe"),
            "nvidia-smi.exe"
        };

        foreach (var candidate in candidates)
        {
            if (!candidate.Contains(Path.DirectorySeparatorChar) || File.Exists(candidate))
            {
                return candidate;
            }
        }

        return "nvidia-smi.exe";
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Process already exited.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Access denied while terminating; the timeout error is still reported.
        }
    }
}

public sealed class NvidiaSmiException : Exception
{
    public NvidiaSmiException(string message)
        : base(message)
    {
    }

    public NvidiaSmiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
