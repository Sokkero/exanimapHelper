using System.Collections.ObjectModel;

namespace ExanimapHelper;

/// <summary>
/// Ordered list of recorded points. <see cref="Points"/> is an
/// <see cref="ObservableCollection{T}"/> so the UI list updates automatically.
/// </summary>
public sealed class TrailModel
{
    // Samples closer than this to the previous recorded point are discarded, so the
    // trail only grows when the player has actually moved.
    private const double MinDistance = 20;

    public ObservableCollection<TrailPoint> Points { get; } = new();

    public int Count => Points.Count;

    /// <summary>
    /// Adds a point unless it is within <see cref="MinDistance"/> units of the
    /// previous one. Returns true if the point was added.
    /// </summary>
    public bool Add(float x, float y)
    {
        if (Points.Count > 0)
        {
            TrailPoint last = Points[^1];
            double dx = x - last.X, dy = y - last.Y;
            if (dx * dx + dy * dy < MinDistance * MinDistance)
                return false;
        }

        Points.Add(new TrailPoint(x, y));
        return true;
    }

    /// <summary>
    /// Replaces the current trail with <paramref name="points"/> verbatim. Unlike
    /// <see cref="Add"/> this applies no distance filtering — imported data is taken
    /// as-is so it round-trips exactly with what was exported.
    /// </summary>
    public void Load(IEnumerable<TrailPoint> points)
    {
        Points.Clear();
        foreach (TrailPoint p in points)
            Points.Add(p);
    }

    public void Clear() => Points.Clear();
}
