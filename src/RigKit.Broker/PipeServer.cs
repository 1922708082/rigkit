using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RigKit.Contracts.Ipc;
using Serilog;

namespace RigKit.Broker;

/// <summary>
/// Minimal read-only JSON-RPC server. Write handlers are intentionally absent until
/// the snapshot/rollback transaction layer exists.
/// </summary>
public sealed class PipeServer
{
    private const int MaxFrameBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _pipeName;
    private readonly bool _devMode;
    private readonly TimeSpan _idleTimeout;
    private readonly CommandRegistry _registry = new();
    private readonly AuditStore _audit = new();

    public PipeServer(string? pipeName = null, bool devMode = false, TimeSpan? idleTimeout = null)
    {
        _pipeName = string.IsNullOrWhiteSpace(pipeName) ? IpcMethods.PipeName : pipeName;
        _devMode = devMode;
        _idleTimeout = idleTimeout ?? TimeSpan.FromSeconds(60);
    }

    public async Task RunAsync(CancellationToken cancellationToken, bool once = false)
    {
        Console.WriteLine($"RigKit Broker 已启动：\\\\.\\pipe\\{_pipeName}");
        Console.WriteLine("只读方法：system.info / ping。写操作尚未开放。");
        Log.Information("Broker listening pipe={Pipe} dev={Dev} idle={Idle}s", _pipeName, _devMode, _idleTimeout.TotalSeconds);

        while (!cancellationToken.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            idle.CancelAfter(_idleTimeout);

            try
            {
                await pipe.WaitForConnectionAsync(idle.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine($"空闲 {_idleTimeout.TotalSeconds:0} 秒，Broker 退出。");
                return;
            }

            var caller = CallerValidator.Inspect(pipe, _devMode);
            if (!caller.IsAllowed)
            {
                Console.WriteLine($"拒绝未授权调用方：pid={caller.ProcessId} path={caller.ImagePath}");
                Log.Warning("Caller rejected pid={Pid} process={Process} path={Path}", caller.ProcessId, caller.ProcessName, caller.ImagePath);
                await AuditAsync("connect", caller, "rejected", -32001, "caller not allowed", cancellationToken)
                    .ConfigureAwait(false);
                await TryWriteFrameAsync(
                    pipe,
                    Encoding.UTF8.GetBytes(ErrorResponse(null, -32001, "Caller is not allowed.")),
                    cancellationToken).ConfigureAwait(false);

                if (once)
                {
                    return;
                }

                continue;
            }

            await AuditAsync("connect", caller, "ok", null, null, cancellationToken).ConfigureAwait(false);
            await HandleConnectionAsync(pipe, caller, cancellationToken).ConfigureAwait(false);

            if (once)
            {
                return;
            }
        }
    }

    private async Task HandleConnectionAsync(
        Stream pipe,
        CallerInfo caller,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            byte[]? payload;
            try
            {
                payload = await ReadFrameAsync(pipe, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidDataException)
            {
                await AuditAsync("parse", caller, "error", -32600, "invalid frame", cancellationToken)
                    .ConfigureAwait(false);
                await TryWriteFrameAsync(
                    pipe,
                    Encoding.UTF8.GetBytes(ErrorResponse(null, -32600, "Invalid request frame.")),
                    cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (IOException)
            {
                return;
            }

            if (payload is null)
            {
                return;
            }

            var response = await ProcessAsync(payload, caller, cancellationToken).ConfigureAwait(false);
            if (!await TryWriteFrameAsync(pipe, Encoding.UTF8.GetBytes(response), cancellationToken)
                    .ConfigureAwait(false))
            {
                return;
            }
        }
    }

    private async Task<string> ProcessAsync(
        byte[] payload,
        CallerInfo caller,
        CancellationToken cancellationToken)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            await AuditAsync("parse", caller, "error", -32700, "parse error", cancellationToken)
                .ConfigureAwait(false);
            return ErrorResponse(null, -32700, "Parse error.");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("method", out var methodElement) ||
                methodElement.ValueKind != JsonValueKind.String)
            {
                await AuditAsync("invalid", caller, "error", -32600, "invalid request", cancellationToken)
                    .ConfigureAwait(false);
                return ErrorResponse(null, -32600, "Invalid request.");
            }

            JsonNode? id = null;
            if (root.TryGetProperty("id", out var idElement))
            {
                id = JsonNode.Parse(idElement.GetRawText());
            }

            var method = methodElement.GetString() ?? string.Empty;
            JsonElement? parameters = root.TryGetProperty("params", out var paramsElement)
                ? paramsElement
                : null;

            Console.WriteLine($"{DateTimeOffset.Now:HH:mm:ss} {method} pid={caller.ProcessId}");
            Log.Information("RPC {Method} pid={Pid} process={Process}", method, caller.ProcessId, caller.ProcessName);

            if (!CommandRegistry.AreParametersEmpty(parameters))
            {
                await AuditAsync(method, caller, "error", -32602, "params not accepted", cancellationToken)
                    .ConfigureAwait(false);
                return ErrorResponse(id, -32602, "Invalid params.");
            }

            if (!_registry.TryInvoke(method, out var result))
            {
                await AuditAsync(method, caller, "error", -32601, "method not found", cancellationToken)
                    .ConfigureAwait(false);
                return ErrorResponse(id, -32601, $"Method not found: {method}");
            }

            await AuditAsync(method, caller, "ok", null, null, cancellationToken).ConfigureAwait(false);
            return SuccessResponse(id, result);
        }
    }

    private async Task AuditAsync(
        string method,
        CallerInfo caller,
        string result,
        int? errorCode,
        string? detail,
        CancellationToken cancellationToken)
    {
        try
        {
            await _audit.LogAsync(method, caller, result, errorCode, detail, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Shutting down; audit is best-effort.
        }
    }

    private static string SuccessResponse(JsonNode? id, JsonNode? result)
    {
        var response = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = result
        };
        return response.ToJsonString(JsonOptions);
    }

    private static string ErrorResponse(JsonNode? id, int code, string message)
    {
        var response = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new JsonObject
            {
                ["code"] = code,
                ["message"] = message
            }
        };
        return response.ToJsonString(JsonOptions);
    }

    private static async Task<byte[]?> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        if (!await ReadExactlyAsync(stream, header, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length <= 0 || length > MaxFrameBytes)
        {
            throw new InvalidDataException($"Invalid frame length: {length}");
        }

        var payload = new byte[length];
        return await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false)
            ? payload
            : null;
    }

    private static async Task<bool> ReadExactlyAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[offset..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }

    private static async Task<bool> TryWriteFrameAsync(
        Stream stream,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteFrameAsync(stream, payload, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private static async Task WriteFrameAsync(
        Stream stream,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
