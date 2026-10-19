namespace RigKit.Domain.Capabilities;

/// <summary>
/// Cards with a hardware identity verified on a real machine. Unknown models are
/// not automatically suspicious; they are reported as unverified by the health check.
/// </summary>
public static class VerifiedCardCatalog
{
    private static readonly Dictionary<string, string> VerifiedDeviceIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CMP 90HX"] = "220D"
    };

    public static bool TryGetExpectedDeviceId(string model, out string deviceHex)
    {
        foreach (var pair in VerifiedDeviceIds)
        {
            if (model.Contains(pair.Key, StringComparison.OrdinalIgnoreCase))
            {
                deviceHex = pair.Value;
                return true;
            }
        }

        deviceHex = string.Empty;
        return false;
    }
}
