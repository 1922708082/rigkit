using Microsoft.Data.Sqlite;

namespace RigKit.Broker;

public sealed class AuditStore
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public AuditStore(string? databasePath = null)
    {
        var path = string.IsNullOrWhiteSpace(databasePath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RigKit",
                "audit.db")
            : databasePath;

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task LogAsync(
        string method,
        CallerInfo caller,
        string result,
        int? errorCode = null,
        string? detail = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO audit_log
                        (timestamp_utc, method, caller_pid, caller_process, caller_path, result, error_code, detail)
                    VALUES
                        ($timestamp, $method, $pid, $process, $path, $result, $error, $detail);
                    """;
                command.Parameters.AddWithValue("$timestamp", DateTimeOffset.UtcNow.ToString("O"));
                command.Parameters.AddWithValue("$method", method);
                command.Parameters.AddWithValue("$pid", caller.ProcessId);
                command.Parameters.AddWithValue("$process", caller.ProcessName);
                command.Parameters.AddWithValue("$path", caller.ImagePath);
                command.Parameters.AddWithValue("$result", result);
                command.Parameters.AddWithValue("$error", (object?)errorCode ?? DBNull.Value);
                command.Parameters.AddWithValue("$detail", (object?)detail ?? DBNull.Value);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            // Audit is best-effort; never reject a request because the log is unavailable.
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            var dataSource = new SqliteConnectionStringBuilder(_connectionString).DataSource;
            var directory = Path.GetDirectoryName(dataSource);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            SQLitePCL.Batteries_V2.Init();

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS audit_log (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    timestamp_utc TEXT NOT NULL,
                    method TEXT NOT NULL,
                    caller_pid INTEGER,
                    caller_process TEXT,
                    caller_path TEXT,
                    result TEXT NOT NULL,
                    error_code INTEGER,
                    detail TEXT
                );
                """;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }
}
