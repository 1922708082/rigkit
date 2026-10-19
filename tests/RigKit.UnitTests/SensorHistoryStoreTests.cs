using Microsoft.Data.Sqlite;
using RigKit.Domain.Devices;
using RigKit.Infrastructure.Storage;

namespace RigKit.UnitTests;

public sealed class SensorHistoryStoreTests
{
    [Fact]
    public async Task AppendAsync_FlushesCompletedMinuteToSqlite()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rigkit-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "history.db");
        try
        {
            var deviceId = new DeviceId("test", "test");
            var start = new DateTimeOffset(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);
            var store = new SensorHistoryStore(path);

            await store.AppendAsync(CreateSample(deviceId, start, 60, 200));
            await store.AppendAsync(CreateSample(deviceId, start.AddSeconds(30), 80, 300));
            await store.AppendAsync(CreateSample(deviceId, start.AddMinutes(1), 70, 250));

            await using var connection = new SqliteConnection($"Data Source={path}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT samples, temp_avg, temp_min, temp_max, power_avg, power_max
                FROM sensor_minute
                WHERE device_id = 'test';
                """;
            await using var reader = await command.ExecuteReaderAsync();

            Assert.True(await reader.ReadAsync());
            Assert.Equal(2, reader.GetInt32(0));
            Assert.Equal(70, reader.GetDouble(1), 3);
            Assert.Equal(60, reader.GetDouble(2), 3);
            Assert.Equal(80, reader.GetDouble(3), 3);
            Assert.Equal(250, reader.GetDouble(4), 3);
            Assert.Equal(300, reader.GetDouble(5), 3);

            var recent = store.GetRecent(deviceId, 10);
            Assert.Equal(3, recent.Count);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static SensorSample CreateSample(
        DeviceId deviceId,
        DateTimeOffset timestamp,
        double temperature,
        double power) =>
        new(
            deviceId,
            timestamp,
            temperature,
            power,
            300,
            100,
            512,
            10240,
            1800,
            9000,
            40,
            "P0",
            2,
            16,
            SensorSource.Nvml);
}
