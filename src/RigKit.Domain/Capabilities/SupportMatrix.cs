namespace RigKit.Domain.Capabilities;

/// <summary>
/// Baseline support policy by model name. Rule packs may add capabilities later,
/// but they cannot promote Pascal mining cards above Experimental in the first release.
/// </summary>
public static class SupportMatrix
{
    public static SupportLevel ForModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return SupportLevel.Unsupported;
        }

        if (IsPascalMiningCard(model))
        {
            return SupportLevel.Experimental;
        }

        if (model.Contains("CMP 170HX", StringComparison.OrdinalIgnoreCase))
        {
            // HBM compute card without display output; treat as experimental for graphics use.
            return SupportLevel.Experimental;
        }

        if (model.Contains("CMP ", StringComparison.OrdinalIgnoreCase))
        {
            return SupportLevel.Supported;
        }

        return SupportLevel.DetectOnly;
    }

    public static bool IsPascalMiningCard(string? model) =>
        model is not null &&
        (model.Contains("P102", StringComparison.OrdinalIgnoreCase) ||
         model.Contains("P104", StringComparison.OrdinalIgnoreCase) ||
         model.Contains("P106", StringComparison.OrdinalIgnoreCase));

    public static bool AllowsGraphicsUnlock(string? model) =>
        !IsPascalMiningCard(model) &&
        !(model?.Contains("CMP 170HX", StringComparison.OrdinalIgnoreCase) ?? false);
}
