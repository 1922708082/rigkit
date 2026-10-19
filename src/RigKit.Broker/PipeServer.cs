using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RigKit.Contracts.Ipc;

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

    public PipeServer(string? pipeName = null)
    {
        _pipeName = string.IsNullOrWhiteSpace(pipeName) ? IpcMethods.PipeName : pipeName;
    }

    public async Task RunAsync(CancellationToken cancellationToken, bool once = false)
    {
        Console.WriteLine($"RigKit Broker 已启动：\\\\.\\pipe\\{_pipeName}");
        Console.WriteLine("只读方法：system.info / ping。写操作尚未开放。");

        while (!cancellationToken.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            await HandleConnectionAsync(pipe, cancellationToken).ConfigureAwait(false);

            if (once)
            {
                return;
            }
        }
    }

    private static async Task HandleConnectionAsync(Stream pipe, CancellationToken cancellationToken)
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
                await WriteFrameAsync(
                    pipe,
                    Encoding.UTF8.GetBytes(ErrorResponse(null, -32600, "Invalid request frame.")),
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            if (payload is null)
            {
                return;
            }

            var response = Process(payload);
            await WriteFrameAsync(pipe, Encoding.UTF8.GetBytes(response), cancellationToken).ConfigureAwait(false);
        }
    }

    private static string Process(byte[] payload)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            return ErrorResponse(null, -32700, "Parse error.");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("method", out var methodElement) ||
                methodElement.ValueKind != JsonValueKind.String)
            {
                return ErrorResponse(null, -32600, "Invalid request.");
            }

            JsonNode? id = null;
            if (root.TryGetProperty("id", out var idElement))
            {
                id = JsonNode.Parse(idElement.GetRawText());
            }

            var method = methodElement.GetString();
            Console.WriteLine($"{DateTimeOffset.Now:HH:mm:ss} {method}");

            return method switch
            {
                "ping" => SuccessResponse(id, new { pong = true, timestamp = DateTimeOffset.Now }),
                "system.info" => SuccessResponse(id, BuildSystemInfo()),
                _ => ErrorResponse(id, -32601, $"Method not found: {method}")
            };
        }
    }

    private static object BuildSystemInfo() => new
    {
        version = typeof(PipeServer).Assembly.GetName().Version?.ToString() ?? "0.0.0",
        processId = Environment.ProcessId,
        user = Environment.UserName,
        elevated = Environment.IsPrivilegedProcess,
        os = Environment.OSVersion.VersionString,
        runtime = Environment.Version.ToString(),
        machine = Environment.MachineName,
        timestamp = DateTimeOffset.Now
    };

    private static string SuccessResponse(JsonNode? id, object result)
    {
        var response = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = JsonSerializer.SerializeToNode(result, JsonOptions)
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
