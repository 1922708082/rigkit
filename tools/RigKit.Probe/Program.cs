using RigKit.Application.Devices;
using RigKit.Infrastructure.Nvidia;

Console.OutputEncoding = System.Text.Encoding.UTF8;

using var provider = new FallbackDeviceProvider(new NvmlProvider(), new NvidiaSmiProvider());
Console.WriteLine($"RigKit Probe · providers={provider.Name}");
Console.WriteLine(new string('-', 64));

try
{
    var devices = await provider.EnumerateAsync();
    if (devices.Count == 0)
    {
        Console.WriteLine($"未发现 NVIDIA 设备。{provider.LastError}");
        return 1;
    }

    foreach (var device in devices)
    {
        Console.WriteLine($"[{device.Id.PciLocation}] {device.Model}");
        Console.WriteLine(
            $"  架构 {device.Architecture} | 显存 {device.DisplayMemory} | 驱动 {device.DriverVersion} | PCI {device.PciIds}");

        try
        {
            var sample = await provider.ReadSensorsAsync(device.Id);
            Console.WriteLine(
                $"  温度 {Format(sample.TemperatureC, "℃")} | 功耗 {Format(sample.PowerW, "W")} / {Format(sample.PowerLimitW, "W")} | 负载 {Format(sample.UtilizationPercent, "%")}");
            Console.WriteLine(
                $"  显存 {FormatInt(sample.MemoryUsedMegabytes)} / {FormatInt(sample.MemoryTotalMegabytes)} MB | 核心 {Format(sample.CoreClockMhz, "MHz")} | 显存 {Format(sample.MemoryClockMhz, "MHz")}");
            Console.WriteLine(
                $"  风扇 {Format(sample.FanPercent, "%")} | PState {sample.PerformanceState ?? "--"} | PCIe Gen{FormatInt(sample.PcieGeneration)} x{FormatInt(sample.PcieWidth)} | 来源 {sample.Source}");
        }
        catch (NvidiaSmiException ex)
        {
            Console.WriteLine($"  传感器读取失败：{ex.Message}");
        }

        Console.WriteLine();
    }

    return 0;
}
catch (NvidiaSmiException ex)
{
    Console.Error.WriteLine($"探测失败：{ex.Message}");
    return 2;
}

static string Format(double? value, string unit) =>
    value.HasValue ? $"{value.Value:0.#}{unit}" : "--";

static string FormatInt(int? value) =>
    value.HasValue ? value.Value.ToString() : "--";
