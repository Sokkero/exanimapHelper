using System.Globalization;

namespace ExanimapHelper;

/// <summary>A single recorded coordinate sample.</summary>
public readonly record struct TrailPoint(float X, float Y)
{
    /// <summary>Display form shown in the data list, e.g. "12.34, -5.6".</summary>
    public override string ToString() =>
        string.Format(CultureInfo.InvariantCulture, "{0:0.###}, {1:0.###}", X, Y);
}
