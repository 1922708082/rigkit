namespace RigKit.Domain.Capabilities;

public enum SupportLevel
{
    Unsupported,
    DetectOnly,
    Experimental,
    Supported,
    Verified
}

/// <summary>
/// A card capability resolved from a signed rule pack. Evidence explains why the
/// capability is available (or not) so the UI can stay honest.
/// </summary>
public sealed record Capability(
    string Key,
    SupportLevel Level,
    string Evidence,
    IReadOnlyList<string> Constraints)
{
    public static Capability Unsupported(string key, string reason) =>
        new(key, SupportLevel.Unsupported, reason, Array.Empty<string>());
}
