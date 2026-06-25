using System.Collections.ObjectModel;

namespace ExanimapHelper;

/// <summary>
/// Ordered list of recorded points. <see cref="Points"/> is an
/// <see cref="ObservableCollection{T}"/> so the UI list updates automatically.
/// </summary>
public sealed class TrailModel
{
    public ObservableCollection<TrailPoint> Points { get; } = new();

    public int Count => Points.Count;

    public void Add(float x, float y) => Points.Add(new TrailPoint(x, y));

    public void Clear() => Points.Clear();
}
