using System.Management;

namespace RigKit.App.Services;

/// <summary>
/// Watches NVIDIA PCI PnP create/delete events and debounces them into a single
/// device-list refresh. WMI is best-effort; a failed watcher must not block the UI.
/// </summary>
public sealed class DeviceChangeWatcher : IDisposable
{
    private const string Query =
        "SELECT * FROM {0} WITHIN 2 " +
        "WHERE TargetInstance ISA 'Win32_PnPEntity' " +
        "AND TargetInstance.PNPDeviceID LIKE 'PCI%VEN_10DE%'";

    private readonly ManagementEventWatcher _creation;
    private readonly ManagementEventWatcher _deletion;
    private readonly object _gate = new();
    private System.Threading.Timer? _debounce;
    private bool _disposed;

    public DeviceChangeWatcher()
    {
        _creation = new ManagementEventWatcher(new WqlEventQuery(string.Format(Query, "__InstanceCreationEvent")));
        _deletion = new ManagementEventWatcher(new WqlEventQuery(string.Format(Query, "__InstanceDeletionEvent")));
        _creation.EventArrived += OnEventArrived;
        _deletion.EventArrived += OnEventArrived;
    }

    public event EventHandler? DevicesChanged;

    public void Start()
    {
        try
        {
            _creation.Start();
            _deletion.Start();
        }
        catch (ManagementException)
        {
            // WMI can be disabled or unavailable; the manual refresh button still works.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _creation.EventArrived -= OnEventArrived;
        _deletion.EventArrived -= OnEventArrived;

        try
        {
            _creation.Stop();
            _deletion.Stop();
        }
        catch (Exception ex) when (ex is ManagementException or InvalidOperationException)
        {
            // Ignore shutdown failures.
        }

        _creation.Dispose();
        _deletion.Dispose();

        lock (_gate)
        {
            _debounce?.Dispose();
            _debounce = null;
        }
    }

    private void OnEventArrived(object sender, EventArrivedEventArgs e)
    {
        lock (_gate)
        {
            _debounce?.Dispose();
            _debounce = new System.Threading.Timer(
                _ => DevicesChanged?.Invoke(this, EventArgs.Empty),
                null,
                dueTime: 1500,
                period: Timeout.Infinite);
        }
    }
}
