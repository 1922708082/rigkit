namespace RigKit.Domain.Devices;

/// <summary>
/// Fixed-capacity, thread-safe ring buffer used for the live chart and the
/// per-device recent history. Old samples are dropped without allocating.
/// </summary>
public sealed class SensorRingBuffer
{
    private readonly object _gate = new();
    private readonly Queue<SensorSample> _samples;
    private readonly int _capacity;

    public SensorRingBuffer(int capacity = 300)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _capacity = capacity;
        _samples = new Queue<SensorSample>(capacity);
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _samples.Count;
            }
        }
    }

    public void Add(SensorSample sample)
    {
        lock (_gate)
        {
            if (_samples.Count == _capacity)
            {
                _samples.Dequeue();
            }

            _samples.Enqueue(sample);
        }
    }

    public IReadOnlyList<SensorSample> GetRecent(int count)
    {
        if (count <= 0)
        {
            return Array.Empty<SensorSample>();
        }

        lock (_gate)
        {
            return _samples.Skip(Math.Max(0, _samples.Count - count)).ToArray();
        }
    }
}
