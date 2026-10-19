namespace RigKit.App.Services;

/// <summary>
/// Keeps a single UI instance per user session. A second launch signals the first
/// instance to restore and activate its window, then exits.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string SemaphoreName = @"Local\RigKit.App.Instance";
    private const string ActivateEventName = @"Local\RigKit.App.Activate";
    private const string ShutdownEventName = @"Local\RigKit.App.Shutdown";

    private readonly Semaphore _semaphore;
    private readonly EventWaitHandle? _activateEvent;
    private readonly EventWaitHandle? _shutdownEvent;
    private readonly Task? _listener;
    private bool _disposed;

    public SingleInstanceGuard()
    {
        _semaphore = new Semaphore(1, 1, SemaphoreName);
        IsFirstInstance = _semaphore.WaitOne(0);

        if (!IsFirstInstance)
        {
            SignalExistingInstance();
            return;
        }

        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        _shutdownEvent = new EventWaitHandle(false, EventResetMode.ManualReset, ShutdownEventName);
        _listener = Task.Run(ListenForActivation);
    }

    public bool IsFirstInstance { get; }

    public event EventHandler? ActivationRequested;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (!IsFirstInstance)
        {
            _semaphore.Dispose();
            return;
        }

        _shutdownEvent?.Set();
        try
        {
            _listener?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
            // Shutdown best effort.
        }

        _activateEvent?.Dispose();
        _shutdownEvent?.Dispose();
        _semaphore.Release();
        _semaphore.Dispose();
    }

    private static void SignalExistingInstance()
    {
        try
        {
            using var activate = EventWaitHandle.OpenExisting(ActivateEventName);
            activate.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // The first instance is still starting; it will show its window anyway.
        }
    }

    private void ListenForActivation()
    {
        if (_activateEvent is null || _shutdownEvent is null)
        {
            return;
        }

        var handles = new WaitHandle[] { _activateEvent, _shutdownEvent };
        while (true)
        {
            var index = WaitHandle.WaitAny(handles);
            if (index == 1)
            {
                return;
            }

            ActivationRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
