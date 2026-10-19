using System.Runtime.InteropServices;
using System.Text;

namespace RigKit.Infrastructure.Nvidia;

internal enum NvmlReturn
{
    Success = 0,
    Uninitialized = 1,
    InvalidArgument = 2,
    NotSupported = 3,
    NoPermission = 4,
    AlreadyInitialized = 5,
    NotFound = 6,
    InsufficientSize = 7,
    InsufficientPower = 8,
    DriverNotLoaded = 9,
    Timeout = 10,
    IrqIssue = 11,
    LibraryNotFound = 12,
    FunctionNotFound = 13,
    CorruptedInforom = 14,
    GpuIsLost = 15,
    ResetRequired = 16,
    OperatingSystem = 17,
    LibRmVersionMismatch = 18,
    InUse = 19,
    Memory = 20,
    NoData = 21,
    VgpuEccNotSupported = 22,
    InsufficientResources = 23,
    FreqNotSupported = 24,
    ArgumentVersionMismatch = 25,
    Unknown = 999
}

[StructLayout(LayoutKind.Sequential)]
internal struct NvmlUtilization
{
    public uint Gpu;
    public uint Memory;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NvmlMemory
{
    public ulong Total;
    public ulong Free;
    public ulong Used;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
internal struct NvmlPciInfo
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
    public string BusIdLegacy;

    public uint Domain;
    public uint Bus;
    public uint Device;
    public uint PciDeviceId;
    public uint PciSubSystemId;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string BusId;
}

internal static class NvmlNative
{
    private const string Dll = "nvml.dll";

    [DllImport(Dll, EntryPoint = "nvmlInit_v2")]
    internal static extern NvmlReturn Init();

    [DllImport(Dll, EntryPoint = "nvmlShutdown")]
    internal static extern NvmlReturn Shutdown();

    [DllImport(Dll, EntryPoint = "nvmlDeviceGetCount_v2")]
    internal static extern NvmlReturn GetCount(out uint count);

    [DllImport(Dll, EntryPoint = "nvmlDeviceGetHandleByIndex_v2")]
    internal static extern NvmlReturn GetHandleByIndex(uint index, out IntPtr handle);

    [DllImport(Dll, EntryPoint = "nvmlDeviceGetName", CharSet = CharSet.Ansi)]
    internal static extern NvmlReturn GetName(IntPtr handle, StringBuilder name, uint length);

    [DllImport(Dll, EntryPoint = "nvmlDeviceGetTemperature")]
    internal static extern NvmlReturn GetTemperature(IntPtr handle, uint sensorType, out uint temperatureC);

    [DllImport(Dll, EntryPoint = "nvmlDeviceGetPowerUsage")]
    internal static extern NvmlReturn GetPowerUsage(IntPtr handle, out uint milliwatts);

    [DllImport(Dll, EntryPoint = "nvmlDeviceGetEnforcedPowerLimit")]
    internal static extern NvmlReturn GetEnforcedPowerLimit(IntPtr handle, out uint milliwatts);

    [DllImport(Dll, EntryPoint = "nvmlDeviceGetUtilizationRates")]
    internal static extern NvmlReturn GetUtilizationRates(IntPtr handle, out NvmlUtilization utilization);

    [DllImport(Dll, EntryPoint = "nvmlDeviceGetMemoryInfo")]
    internal static extern NvmlReturn GetMemoryInfo(IntPtr handle, out NvmlMemory memory);

    [DllImport(Dll, EntryPoint = "nvmlDeviceGetClockInfo")]
    internal static extern NvmlReturn GetClockInfo(IntPtr handle, uint clockType, out uint clockMhz);

    [DllImport(Dll, EntryPoint = "nvmlDeviceGetFanSpeed")]
    internal static extern NvmlReturn GetFanSpeed(IntPtr handle, out uint percent);

    [DllImport(Dll, EntryPoint = "nvmlDeviceGetPerformanceState")]
    internal static extern NvmlReturn GetPerformanceState(IntPtr handle, out uint pstate);

    [DllImport(Dll, EntryPoint = "nvmlDeviceGetCurrPcieLinkGeneration")]
    internal static extern NvmlReturn GetPcieLinkGeneration(IntPtr handle, out uint generation);

    [DllImport(Dll, EntryPoint = "nvmlDeviceGetCurrPcieLinkWidth")]
    internal static extern NvmlReturn GetPcieLinkWidth(IntPtr handle, out uint width);

    [DllImport(Dll, EntryPoint = "nvmlSystemGetDriverVersion", CharSet = CharSet.Ansi)]
    internal static extern NvmlReturn GetDriverVersion(StringBuilder version, uint length);

    [DllImport(Dll, EntryPoint = "nvmlDeviceGetPciInfo_v3")]
    internal static extern NvmlReturn GetPciInfo(IntPtr handle, out NvmlPciInfo pciInfo);
}

public sealed class NvmlException : Exception
{
    public NvmlException(string message)
        : base(message)
    {
    }

    public NvmlException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
