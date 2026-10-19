using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace RigKit.Desktop.Views;

public sealed partial class WorkbenchPage : Page
{
    private bool _loaded;

    public WorkbenchPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;

        try
        {
            var devices = await App.CurrentApp.Devices.GetDevicesAsync();
            if (devices.Count == 0)
            {
                StatusText.Text = "未发现 NVIDIA 设备";
                SubtitleText.Text = App.CurrentApp.Devices.LastError;
                return;
            }

            var device = devices[0];
            DeviceNameText.Text = device.Model;
            DeviceMetaText.Text =
                $"{device.Architecture} · {device.DisplayMemory} · 驱动 {device.DriverVersion} · {device.PciIds}";
            SubtitleText.Text = $"当前设备：{device.Model} · {device.PciIds}";

            var sample = await App.CurrentApp.Devices.GetSensorsAsync(device.Id);
            TemperatureText.Text = Format(sample.TemperatureC);
            PowerText.Text = Format(sample.PowerW);
            VramText.Text = sample.MemoryUsedMegabytes?.ToString() ?? "--";
            FanText.Text = sample.FanPercent?.ToString() ?? "--";
            StatusText.Text = "运行正常";
        }
        catch (Exception ex)
        {
            StatusText.Text = "读取失败";
            SubtitleText.Text = ex.Message;
        }
    }

    private static string Format(double? value) => value?.ToString("0.#") ?? "--";
}
