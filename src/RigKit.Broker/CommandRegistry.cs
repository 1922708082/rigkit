using System.Text.Json;
using System.Text.Json.Nodes;
using RigKit.Contracts.Ipc;

namespace RigKit.Broker;

/// <summary>
/// Explicit allowlist of read-only methods. Unknown methods and non-empty params
/// are rejected before any handler runs.
/// </summary>
public sealed class CommandRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<string, Func<JsonNode?>> _handlers = new(StringComparer.Ordinal);

    public CommandRegistry()
    {
        _handlers[IpcMethods.SystemInfo] = () =>
            JsonSerializer.SerializeToNode(BuildSystemInfo(), JsonOptions);
        _handlers["ping"] = () =>
            JsonSerializer.SerializeToNode(new { pong = true, timestamp = DateTimeOffset.Now }, JsonOptions);
    }

    public bool TryInvoke(string method, out JsonNode? result)
    {
        result = null;
        if (!_handlers.TryGetValue(method, out var handler))
        {
            return false;
        }

        result = handler();
        return true;
    }

    public static bool AreParametersEmpty(JsonElement? parameters)
    {
        if (parameters is null)
        {
            return true;
        }

        return parameters.Value.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => true,
            JsonValueKind.Object => !parameters.Value.EnumerateObject().Any(),
            _ => false
        };
    }

    private static object BuildSystemInfo() => new
    {
        version = typeof(CommandRegistry).Assembly.GetName().Version?.ToString() ?? "0.0.0",
        processId = Environment.ProcessId,
        user = Environment.UserName,
        elevated = Environment.IsPrivilegedProcess,
        os = Environment.OSVersion.VersionString,
        runtime = Environment.Version.ToString(),
        machine = Environment.MachineName,
        timestamp = DateTimeOffset.Now
    };
}
