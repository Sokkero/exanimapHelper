using System.Globalization;

namespace ExanimapHelper;

/// <summary>A single recorded coordinate sample.</summary>
public readonly record struct TrailPoint(float X, float Y)
{
    /// <summary>Formats a single coordinate the same way everywhere it is shown.</summary>
    public static string Format(float value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>True when a coordinate is a real, plottable number (not NaN/Infinity).</summary>
    public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    /// <summary>Display form shown in the data list, e.g. "12.34, -5.6".</summary>
    public override string ToString() => $"{Format(X)}, {Format(Y)}";
}
