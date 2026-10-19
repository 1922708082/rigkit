using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using RigKit.Application.Devices;
using RigKit.Domain.Capabilities;
using RigKit.Domain.Devices;

namespace RigKit.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private const double RingCircumferenceUnits = 42.41;

    private readonly DeviceService _devices;
    private readonly Queue<double> _temperatures = new();
    private readonly Queue<double> _powers = new();

    private Device? _device;
    private bool _initialized;
    private bool _pcieRiskAdded;

    private string _deviceName = "检测中...";
    private string _architecture = "--";
    private string _memoryLabel = "--";
    private string _driverVersion = "--";
    private string _pciIds = "--";
    private string _deviceMeta = "正在读取设备信息";
    private string _supportLabel = "检测中";
    private bool _isExperimental;
    private string _statusLabel = "检测中";
    private Brush _statusBrush = CreateBrush("#98A2B3");
    private Brush _statusBackground = CreateBrush("#F1F3F6");
    private int _healthScore;
    private string _healthLabel = "等待数据";
    private string _healthSummary = "正在读取传感器";
    private DoubleCollection _scoreDashArray = new() { 0.01, RingCircumferenceUnits };
    private Brush _scoreBrush = CreateBrush("#98A2B3");
    private string _temperatureValue = "--";
    private string _powerValue = "--";
    private string _powerLimitText = string.Empty;
    private string _memoryValue = "--";
    private string _fanValue = "--";
    private string _utilizationValue = "--";
    private string _pcieText = "--";
    private string _lastUpdatedText = "等待数据";
    private string _errorText = string.Empty;
    private bool _isBusy;
    private int _riskCount;
    private PointCollection _temperaturePoints = new();
    private PointCollection _powerPoints = new();

    public MainViewModel(DeviceService devices)
    {
        _devices = devices ?? throw new ArgumentNullException(nameof(devices));
        ProviderText = $"设备数据：{_devices.ProviderName}";
        VersionText = $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0"}";
        Risks = new ObservableCollection<RiskItem>();
        Risks.CollectionChanged += (_, _) => RiskCount = Risks.Count;
    }

    public ObservableCollection<RiskItem> Risks { get; }

    public string ProviderText { get; }

    public string VersionText { get; }

    public string DeviceName
    {
        get => _deviceName;
        private set => SetProperty(ref _deviceName, value);
    }

    public string Architecture
    {
        get => _architecture;
        private set => SetProperty(ref _architecture, value);
    }

    public string MemoryLabel
    {
        get => _memoryLabel;
        private set => SetProperty(ref _memoryLabel, value);
    }

    public string DriverVersion
    {
        get => _driverVersion;
        private set => SetProperty(ref _driverVersion, value);
    }

    public string PciIds
    {
        get => _pciIds;
        private set => SetProperty(ref _pciIds, value);
    }

    public string DeviceMeta
    {
        get => _deviceMeta;
        private set => SetProperty(ref _deviceMeta, value);
    }

    public string SupportLabel
    {
        get => _supportLabel;
        private set => SetProperty(ref _supportLabel, value);
    }

    public bool IsExperimental
    {
        get => _isExperimental;
        private set => SetProperty(ref _isExperimental, value);
    }

    public string StatusLabel
    {
        get => _statusLabel;
        private set => SetProperty(ref _statusLabel, value);
    }

    public Brush StatusBrush
    {
        get => _statusBrush;
        private set => SetProperty(ref _statusBrush, value);
    }

    public Brush StatusBackground
    {
        get => _statusBackground;
        private set => SetProperty(ref _statusBackground, value);
    }

    public int HealthScore
    {
        get => _healthScore;
        private set => SetProperty(ref _healthScore, value);
    }

    public string HealthLabel
    {
        get => _healthLabel;
        private set => SetProperty(ref _healthLabel, value);
    }

    public string HealthSummary
    {
        get => _healthSummary;
        private set => SetProperty(ref _healthSummary, value);
    }

    public DoubleCollection ScoreDashArray
    {
        get => _scoreDashArray;
        private set => SetProperty(ref _scoreDashArray, value);
    }

    public Brush ScoreBrush
    {
        get => _scoreBrush;
        private set => SetProperty(ref _scoreBrush, value);
    }

    public string TemperatureValue
    {
        get => _temperatureValue;
        private set => SetProperty(ref _temperatureValue, value);
    }

    public string PowerValue
    {
        get => _powerValue;
        private set => SetProperty(ref _powerValue, value);
    }

    public string PowerLimitText
    {
        get => _powerLimitText;
        private set => SetProperty(ref _powerLimitText, value);
    }

    public string MemoryValue
    {
        get => _memoryValue;
        private set => SetProperty(ref _memoryValue, value);
    }

    public string FanValue
    {
        get => _fanValue;
        private set => SetProperty(ref _fanValue, value);
    }

    public string UtilizationValue
    {
        get => _utilizationValue;
        private set => SetProperty(ref _utilizationValue, value);
    }

    public string PcieText
    {
        get => _pcieText;
        private set => SetProperty(ref _pcieText, value);
    }

    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        private set => SetProperty(ref _lastUpdatedText, value);
    }

    public string ErrorText
    {
        get => _errorText;
        private set
        {
            if (SetProperty(ref _errorText, value))
            {
                OnPropertyChanged(nameof(ErrorVisibility));
            }
        }
    }

    public Visibility ErrorVisibility =>
        string.IsNullOrWhiteSpace(ErrorText) ? Visibility.Collapsed : Visibility.Visible;

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public int RiskCount
    {
        get => _riskCount;
        private set => SetProperty(ref _riskCount, value);
    }

    public PointCollection TemperaturePoints
    {
        get => _temperaturePoints;
        private set => SetProperty(ref _temperaturePoints, value);
    }

    public PointCollection PowerPoints
    {
        get => _powerPoints;
        private set => SetProperty(ref _powerPoints, value);
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var devices = await _devices.GetDevicesAsync();
            if (devices.Count == 0)
            {
                ErrorText = string.IsNullOrWhiteSpace(_devices.LastError)
                    ? "未发现 NVIDIA 设备。"
                    : _devices.LastError;
                LastUpdatedText = "未连接";
                StatusLabel = "未连接";
                return;
            }

            _device = devices[0];
            DeviceName = _device.Model;
            Architecture = _device.Architecture;
            MemoryLabel = _device.DisplayMemory;
            DriverVersion = _device.DriverVersion;
            PciIds = _device.PciIds;
            DeviceMeta = $"{_device.Architecture} · {_device.DisplayMemory} · {_device.PciIds}";

            var level = SupportMatrix.ForModel(_device.Model);
            SupportLabel = level switch
            {
                SupportLevel.Supported => "已支持",
                SupportLevel.Experimental => "实验性",
                SupportLevel.DetectOnly => "仅检测",
                _ => "不支持"
            };
            IsExperimental = level == SupportLevel.Experimental;

            BuildRisks();
            await RefreshAsync();
            _initialized = true;
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            LastUpdatedText = "读取失败";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RefreshAsync()
    {
        if (_device is null)
        {
            return;
        }
        try
        {
            var sample = await _devices.GetSensorsAsync(_device.Id);
            if (sample.Source == SensorSource.Unknown)
            {
                ErrorText = string.IsNullOrWhiteSpace(_devices.LastError)
                    ? "传感器读取失败。"
                    : _devices.LastError;
                LastUpdatedText = "读取失败";
                return;
            }

            TemperatureValue = Format(sample.TemperatureC);
            PowerValue = Format(sample.PowerW);
            PowerLimitText = sample.PowerLimitW is > 0 ? $"/ {sample.PowerLimitW:0} W" : string.Empty;
            MemoryValue = FormatMemory(sample.MemoryUsedMegabytes, sample.MemoryTotalMegabytes);
            FanValue = sample.FanPercent?.ToString() ?? "--";
            UtilizationValue = Format(sample.UtilizationPercent);
            PcieText = sample.PcieGeneration.HasValue && sample.PcieWidth.HasValue
                ? $"Gen{sample.PcieGeneration} x{sample.PcieWidth}"
                : "--";
            LastUpdatedText = sample.Timestamp.ToString("HH:mm:ss");
            ErrorText = string.Empty;

            UpdateScore(sample);
            AppendPoints(sample);
            UpdatePcieRisk(sample);
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
            LastUpdatedText = "读取失败";
        }
    }

    /// <summary>
    /// Design-preview only. The smoke renderer calls this so the chart has a full
    /// 60-second shape; the live UI never seeds synthetic telemetry.
    /// </summary>
    public void SeedHistoryForPreview()
    {
        var baseTemperature = _temperatures.Count > 0 ? _temperatures.Last() : 70;
        var basePower = _powers.Count > 0 ? _powers.Last() : 250;
        var random = new Random(42);

        for (var index = 0; index < 60; index++)
        {
            _temperatures.Enqueue(baseTemperature + Math.Sin(index / 6.0) * 2.4 + random.NextDouble() * 0.6);
            _powers.Enqueue(basePower + Math.Sin(index / 5.0) * 10 + random.NextDouble() * 3);

            while (_temperatures.Count > 60)
            {
                _temperatures.Dequeue();
            }

            while (_powers.Count > 60)
            {
                _powers.Dequeue();
            }
        }

        TemperaturePoints = BuildPoints(_temperatures, 30, 100);
        PowerPoints = BuildPoints(_powers, 0, Math.Max(100, basePower * 1.15));
    }

    private void BuildRisks()
    {
        Risks.Clear();
        if (_device is null)
        {
            return;
        }

        if (SupportMatrix.IsPascalMiningCard(_device.Model))
        {
            Risks.Add(new RiskItem(
                "P 系列为实验性支持",
                "不承诺图形解锁；驱动需自行从可信来源获取，建议先完成体检。",
                RiskSeverity.Warning));
        }

        Risks.Add(new RiskItem(
            "尚未创建首次快照",
            "M1 快照与回滚开放后，任何改造前都必须先建立恢复点。",
            RiskSeverity.Info));
    }

    private void UpdatePcieRisk(SensorSample sample)
    {
        if (_pcieRiskAdded || _device is null || SupportMatrix.IsPascalMiningCard(_device.Model))
        {
            return;
        }

        if (sample.PcieGeneration is int generation && generation <= 1)
        {
            Risks.Insert(0, new RiskItem(
                "PCIe 链路仍在 Gen1",
                "带宽被限制，游戏与显存交换会受影响；可在改造中心执行解锁。",
                RiskSeverity.Warning));
            _pcieRiskAdded = true;
        }
    }

    private void UpdateScore(SensorSample sample)
    {
        if (sample.TemperatureC is null)
        {
            HealthScore = 0;
            HealthLabel = "数据不足";
            HealthSummary = "缺少温度数据，无法评分";
            ScoreDashArray = new DoubleCollection { 0.01, RingCircumferenceUnits };
            ScoreBrush = CreateBrush("#98A2B3");
            StatusLabel = "数据不足";
            StatusBrush = CreateBrush("#98A2B3");
            StatusBackground = CreateBrush("#F1F3F6");
            return;
        }

        var score = 100.0;
        var temperature = sample.TemperatureC.Value;
        if (temperature > 85) score -= 25;
        else if (temperature > 80) score -= 12;
        else if (temperature > 75) score -= 5;

        if (sample.PowerW is double power &&
            sample.PowerLimitW is double limit &&
            limit > 0 &&
            power > limit * 0.98)
        {
            score -= 5;
        }

        if (sample.UtilizationPercent is > 98 && temperature > 80)
        {
            score -= 5;
        }

        if (sample.MemoryUsedMegabytes is int used &&
            sample.MemoryTotalMegabytes is int total &&
            total > 0 &&
            used > total * 0.95)
        {
            score -= 5;
        }

        var rounded = (int)Math.Round(Math.Clamp(score, 0, 100));
        HealthScore = rounded;
        HealthLabel = rounded switch
        {
            >= 85 => "状态优秀",
            >= 70 => "状态良好",
            >= 50 => "需要注意",
            _ => "存在风险"
        };
        HealthSummary = "基于当前温度、功耗、显存与链路的快照";
        ScoreDashArray = new DoubleCollection
        {
            Math.Max(0.01, rounded / 100.0 * RingCircumferenceUnits),
            RingCircumferenceUnits
        };

        var healthy = rounded >= 70;
        ScoreBrush = CreateBrush(healthy ? "#12805C" : rounded >= 50 ? "#B54708" : "#D92D20");
        StatusLabel = healthy ? "运行正常" : rounded >= 50 ? "需要注意" : "存在风险";
        StatusBrush = ScoreBrush;
        StatusBackground = CreateBrush(healthy ? "#E9F7F1" : rounded >= 50 ? "#FEF4E6" : "#FEF0EE");
    }

    private void AppendPoints(SensorSample sample)
    {
        if (sample.TemperatureC is double temperature)
        {
            _temperatures.Enqueue(temperature);
            while (_temperatures.Count > 60)
            {
                _temperatures.Dequeue();
            }
        }

        if (sample.PowerW is double power)
        {
            _powers.Enqueue(power);
            while (_powers.Count > 60)
            {
                _powers.Dequeue();
            }
        }

        TemperaturePoints = BuildPoints(_temperatures, 30, 100);
        PowerPoints = BuildPoints(_powers, 0, Math.Max(100, sample.PowerLimitW ?? 300));
    }

    private static PointCollection BuildPoints(IEnumerable<double> values, double min, double max)
    {
        var list = values.ToArray();
        var points = new PointCollection(list.Length);
        const double width = 300;
        const double height = 84;

        for (var index = 0; index < list.Length; index++)
        {
            var x = list.Length <= 1 ? 0 : index * width / 59.0;
            var ratio = Math.Clamp((list[index] - min) / (max - min), 0, 1);
            var y = height - 4 - ratio * (height - 8);
            points.Add(new Point(x, y));
        }

        return points;
    }

    private static string Format(double? value) =>
        value.HasValue ? value.Value.ToString("0.#") : "--";

    private static string FormatMemory(int? usedMegabytes, int? totalMegabytes)
    {
        if (usedMegabytes is null || totalMegabytes is null)
        {
            return "--";
        }

        return $"{usedMegabytes.Value / 1024.0:0.#} / {totalMegabytes.Value / 1024.0:0.#} GB";
    }

    private static SolidColorBrush CreateBrush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
