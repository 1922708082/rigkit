using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Data.Sqlite;
using RigKit.Application.Abstractions;
using RigKit.Domain.Devices;

namespace RigKit.Infrastructure.Storage;

/// <summary>
/// Keeps a 300-sample in-memory ring per device and downsamples completed minutes
/// into SQLite. One row per device per minute keeps the file small and queryable.
/// </summary>
public sealed class SensorHistoryStore : ISensorHistoryStore
{
    private const int Capacity = 300;

    private readonly string _databasePath;
    private readonly string _connectionString;
    private readonly ConcurrentDictionary<string, SensorRingBuffer> _buffers = new();
    private readonly ConcurrentDictionary<string, DeviceState> _states = new();
    private readonly SemaphoreSlim _databaseGate = new(1, 1);
    private bool _initialized;

    public SensorHistoryStore(string? databasePath = null)
    {
        _databasePath = string.IsNullOrWhiteSpace(databasePath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RigKit",
                "rigkit.db")
            : databasePath;

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public string DatabasePath => _databasePath;

    public async Task AppendAsync(SensorSample sample, CancellationToken cancellationToken = default)
    {
        var key = sample.DeviceId.ToString();
        _buffers.GetOrAdd(key, _ => new SensorRingBuffer(Capacity)).Add(sample);

        var state = _states.GetOrAdd(key, _ => new DeviceState());
        var minute = sample.Timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
        List<SensorSample>? completed = null;
        string? completedMinute = null;

        lock (state)
        {
            if (state.Minute is not null && state.Minute != minute)
            {
                completed = state.Samples.ToList();
                completedMinute = state.Minute;
                state.Samples.Clear();
            }

            state.Minute = minute;
            state.Samples.Add(sample);
        }

        if (completed is { Count: > 0 } && completedMinute is not null)
        {
            await FlushAsync(key, completedMinute, completed, cancellationToken).ConfigureAwait(false);
        }
    }

    public IReadOnlyList<SensorSample> GetRecent(DeviceId deviceId, int count)
    {
        var key = deviceId.ToString();
        return _buffers.TryGetValue(key, out var buffer)
            ? buffer.GetRecent(count)
            : Array.Empty<SensorSample>();
    }

    private async Task FlushAsync(
        string deviceId,
        string minute,
        IReadOnlyList<SensorSample> samples,
        CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var temperatures = samples
            .Where(sample => sample.TemperatureC.HasValue)
            .Select(sample => sample.TemperatureC!.Value)
            .ToArray();
        var powers = samples
            .Where(sample => sample.PowerW.HasValue)
            .Select(sample => sample.PowerW!.Value)
            .ToArray();
        var utilizations = samples
            .Where(sample => sample.UtilizationPercent.HasValue)
            .Select(sample => sample.UtilizationPercent!.Value)
            .ToArray();
        var memory = samples
            .Where(sample => sample.MemoryUsedMegabytes.HasValue)
            .Select(sample => sample.MemoryUsedMegabytes!.Value)
            .ToArray();
        var fans = samples
            .Where(sample => sample.FanPercent.HasValue)
            .Select(sample => sample.FanPercent!.Value)
            .ToArray();

        await _databaseGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO sensor_minute
                    (device_id, minute_utc, samples, temp_avg, temp_min, temp_max,
                     power_avg, power_max, util_avg, mem_used_avg, fan_avg)
                VALUES
                    ($device, $minute, $samples, $tempAvg, $tempMin, $tempMax,
                     $powerAvg, $powerMax, $utilAvg, $memAvg, $fanAvg)
                ON CONFLICT(device_id, minute_utc) DO UPDATE SET
                    samples = excluded.samples,
                    temp_avg = excluded.temp_avg,
                    temp_min = excluded.temp_min,
                    temp_max = excluded.temp_max,
                    power_avg = excluded.power_avg,
                    power_max = excluded.power_max,
                    util_avg = excluded.util_avg,
                    mem_used_avg = excluded.mem_used_avg,
                    fan_avg = excluded.fan_avg;
                """;

            command.Parameters.AddWithValue("$device", deviceId);
            command.Parameters.AddWithValue("$minute", minute);
            command.Parameters.AddWithValue("$samples", samples.Count);
            AddNullable(command, "$tempAvg", Average(temperatures));
            AddNullable(command, "$tempMin", temperatures.Length > 0 ? temperatures.Min() : null);
            AddNullable(command, "$tempMax", temperatures.Length > 0 ? temperatures.Max() : null);
            AddNullable(command, "$powerAvg", Average(powers));
            AddNullable(command, "$powerMax", powers.Length > 0 ? powers.Max() : null);
            AddNullable(command, "$utilAvg", Average(utilizations));
            AddNullable(command, "$memAvg", Average(memory.Select(value => (double)value)));
            AddNullable(command, "$fanAvg", Average(fans.Select(value => (double)value)));

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _databaseGate.Release();
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _databaseGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            var directory = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            SQLitePCL.Batteries_V2.Init();

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS sensor_minute (
                    device_id TEXT NOT NULL,
                    minute_utc TEXT NOT NULL,
                    samples INTEGER NOT NULL,
                    temp_avg REAL,
                    temp_min REAL,
                    temp_max REAL,
                    power_avg REAL,
                    power_max REAL,
                    util_avg REAL,
                    mem_used_avg INTEGER,
                    fan_avg REAL,
                    PRIMARY KEY (device_id, minute_utc)
                );
                """;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            _initialized = true;
        }
        finally
        {
            _databaseGate.Release();
        }
    }

    private static double? Average(IEnumerable<double> values)
    {
        var array = values as double[] ?? values.ToArray();
        return array.Length > 0 ? array.Average() : null;
    }

    private static void AddNullable(SqliteCommand command, string name, double? value)
    {
        command.Parameters.AddWithValue(name, value.HasValue ? value.Value : DBNull.Value);
    }

    private sealed class DeviceState
    {
        public string? Minute { get; set; }

        public List<SensorSample> Samples { get; } = new();
    }
}
