namespace RigKit.Infrastructure.Nvidia;

internal static class NvidiaModelMap
{
    public static string GuessArchitecture(string model)
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

    /// <summary>
    /// NVML/nvidia-smi expose the PCI device id as device&lt;&lt;16 | vendor.
    /// </summary>
    public static (string PciIds, string DeviceHex) FromPciDeviceId(uint value)
    {
        var device = (value >> 16) & 0xFFFF;
        var vendor = value & 0xFFFF;
        return ($"{vendor:X4}:{device:X4}", $"{device:X4}");
    }
}
