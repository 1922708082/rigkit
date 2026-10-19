using System.Runtime.InteropServices;
using System.Text;
using RigKit.Application.Abstractions;
using RigKit.Domain.Devices;

namespace RigKit.Infrastructure.Cuda;

/// <summary>
/// Minimal CUDA Driver API memory test. It fills a device buffer with two
/// patterns, copies it back and counts mismatches. No CUDA toolkit is required.
/// </summary>
public sealed class CudaVramTester : IVramTester
{
    private const int CudaSuccess = 0;

    public VramTestResult Test(
        DeviceId deviceId,
        int megabytes,
        CancellationToken cancellationToken = default)
    {
        if (megabytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(megabytes));
        }

        try
        {
            var init = CudaNative.cuInit(0);
            if (init != CudaSuccess)
            {
                return new VramTestResult(false, false, 0, 0, $"cuInit 失败：{GetError(init)}");
            }

            var deviceOrdinal = FindDevice(deviceId);
            if (deviceOrdinal is null)
            {
                return new VramTestResult(false, false, 0, 0, "CUDA 未找到与 PCI 位置匹配的设备。");
            }

            var contextResult = CudaNative.cuCtxCreate(out var context, 0, deviceOrdinal.Value);
            if (contextResult != CudaSuccess)
            {
                return new VramTestResult(false, false, 0, 0, $"cuCtxCreate 失败：{GetError(contextResult)}");
            }

            try
            {
                var bytes = (long)megabytes * 1024 * 1024;
                var allocate = CudaNative.cuMemAlloc(out var devicePointer, new UIntPtr((ulong)bytes));
                if (allocate != CudaSuccess)
                {
                    return new VramTestResult(false, false, 0, bytes, $"显存分配失败：{GetError(allocate)}");
                }

                try
                {
                    var hostPointer = Marshal.AllocHGlobal((int)bytes);
                    try
                    {
                        var host = new byte[bytes];
                        var mismatches = 0;
                        var patterns = new[] { 0xA5A5A5A5u, 0x5A5A5A5Au };

                        foreach (var pattern in patterns)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var fill = CudaNative.cuMemsetD32(
                                devicePointer,
                                pattern,
                                new UIntPtr((ulong)(bytes / sizeof(uint))));
                            if (fill != CudaSuccess)
                            {
                                return new VramTestResult(true, false, mismatches, bytes, $"写入失败：{GetError(fill)}");
                            }

                            var copy = CudaNative.cuMemcpyDtoH(
                                hostPointer,
                                devicePointer,
                                new UIntPtr((ulong)bytes));
                            if (copy != CudaSuccess)
                            {
                                return new VramTestResult(true, false, mismatches, bytes, $"回读失败：{GetError(copy)}");
                            }

                            Marshal.Copy(hostPointer, host, 0, host.Length);
                            mismatches += CountMismatches(host, pattern);
                        }

                        return mismatches == 0
                            ? new VramTestResult(true, true, 0, bytes, $"{megabytes} MB 双模式读写通过。")
                            : new VramTestResult(true, false, mismatches, bytes, $"{megabytes} MB 校验发现 {mismatches} 个不一致。");
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(hostPointer);
                    }
                }
                finally
                {
                    _ = CudaNative.cuMemFree(devicePointer);
                }
            }
            finally
            {
                _ = CudaNative.cuCtxDestroy(context);
            }
        }
        catch (DllNotFoundException ex)
        {
            return new VramTestResult(false, false, 0, 0, $"找不到 nvcuda.dll：{ex.Message}");
        }
        catch (EntryPointNotFoundException ex)
        {
            return new VramTestResult(false, false, 0, 0, $"CUDA 驱动入口缺失：{ex.Message}");
        }
        catch (Exception ex) when (ex is InvalidOperationException or OutOfMemoryException)
        {
            return new VramTestResult(false, false, 0, 0, ex.Message);
        }
    }

    private static int? FindDevice(DeviceId deviceId)
    {
        if (CudaNative.cuDeviceGetCount(out var count) != CudaSuccess)
        {
            throw new InvalidOperationException("cuDeviceGetCount 失败。");
        }

        var requestedBus = BusKey(deviceId.PciLocation);
        for (var ordinal = 0; ordinal < count; ordinal++)
        {
            if (CudaNative.cuDeviceGet(out var device, ordinal) != CudaSuccess)
            {
                continue;
            }

            var buffer = new StringBuilder(16);
            if (CudaNative.cuDeviceGetPCIBusId(buffer, buffer.Capacity, device) != CudaSuccess)
            {
                continue;
            }

            if (string.Equals(BusKey(buffer.ToString()), requestedBus, StringComparison.OrdinalIgnoreCase))
            {
                return device;
            }
        }

        return count == 1 ? 0 : null;
    }

    private static string BusKey(string value)
    {
        var parts = value.Split(':');
        if (parts.Length != 3)
        {
            return value.TrimStart('0').ToUpperInvariant();
        }

        var domain = parts[0].TrimStart('0');
        if (domain.Length == 0)
        {
            domain = "0";
        }

        return $"{domain}:{parts[1]}:{parts[2]}".ToUpperInvariant();
    }

    private static int CountMismatches(byte[] buffer, uint pattern)
    {
        var values = MemoryMarshal.Cast<byte, int>(buffer.AsSpan());
        var mismatches = 0;
        foreach (var value in values)
        {
            if ((uint)value != pattern)
            {
                mismatches++;
            }
        }

        return mismatches;
    }

    private static string GetError(int error)
    {
        if (CudaNative.cuGetErrorString(error, out var pointer) == CudaSuccess &&
            pointer != IntPtr.Zero)
        {
            return Marshal.PtrToStringAnsi(pointer) ?? $"CUDA error {error}";
        }

        return $"CUDA error {error}";
    }
}

internal static class CudaNative
{
    private const string Dll = "nvcuda.dll";

    [DllImport(Dll)]
    internal static extern int cuInit(uint flags);

    [DllImport(Dll)]
    internal static extern int cuDeviceGetCount(out int count);

    [DllImport(Dll)]
    internal static extern int cuDeviceGet(out int device, int ordinal);

    [DllImport(Dll, CharSet = CharSet.Ansi)]
    internal static extern int cuDeviceGetPCIBusId(StringBuilder pciBusId, int length, int device);

    [DllImport(Dll, EntryPoint = "cuCtxCreate_v2")]
    internal static extern int cuCtxCreate(out IntPtr context, uint flags, int device);

    [DllImport(Dll, EntryPoint = "cuCtxDestroy_v2")]
    internal static extern int cuCtxDestroy(IntPtr context);

    [DllImport(Dll, EntryPoint = "cuMemAlloc_v2")]
    internal static extern int cuMemAlloc(out UIntPtr devicePointer, UIntPtr byteSize);

    [DllImport(Dll, EntryPoint = "cuMemFree_v2")]
    internal static extern int cuMemFree(UIntPtr devicePointer);

    [DllImport(Dll, EntryPoint = "cuMemsetD32_v2")]
    internal static extern int cuMemsetD32(UIntPtr destination, uint value, UIntPtr count);

    [DllImport(Dll, EntryPoint = "cuMemcpyDtoH_v2")]
    internal static extern int cuMemcpyDtoH(IntPtr destination, UIntPtr source, UIntPtr byteCount);

    [DllImport(Dll)]
    internal static extern int cuGetErrorString(int error, out IntPtr errorString);
}
