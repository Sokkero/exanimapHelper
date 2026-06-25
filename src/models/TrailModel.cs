namespace ExanimapHelper;

/// <summary>
/// Holds the recorded data as a list of independent paths plus a separate list of
/// points of interest (POIs). A new path is started on every entry into recording,
/// so pausing and resuming yields visually and structurally distinct paths.
/// </summary>
public sealed class TrailModel
{
    // Samples closer than this to the previous recorded point are discarded, so the
    // trail only grows when the player has actually moved.
    private const double MinDistance = 20;

    private readonly List<List<TrailPoint>> _paths = new();

    public IReadOnlyList<IReadOnlyList<TrailPoint>> Paths => _paths;

    public List<TrailPoint> Pois { get; } = new();

    /// <summary>True once there is something worth exporting or clearing.</summary>
    public bool HasData => Pois.Count > 0 || _paths.Exists(p => p.Count > 0);

    /// <summary>
    /// The last point of the last non-empty path, i.e. the most recently recorded
    /// coordinate overall. Null when nothing has been recorded. Drives the red
    /// "latest" marker.
    /// </summary>
    public TrailPoint? LatestPoint
    {
        get
        {
            for (int i = _paths.Count - 1; i >= 0; i--)
                if (_paths[i].Count > 0)
                    return _paths[i][^1];
            return null;
        }
    }

    /// <summary>Begins a fresh, empty path that subsequent <see cref="Add"/> calls fill.</summary>
    public void StartNewPath() => _paths.Add(new List<TrailPoint>());

    /// <summary>
    /// Adds a point to the current path unless it is within <see cref="MinDistance"/>
    /// units of that path's previous point. Returns true if the point was added.
    /// Assumes a path has been started via <see cref="StartNewPath"/>.
    /// </summary>
    public bool Add(float x, float y)
    {
        List<TrailPoint> current = _paths[^1];
        if (current.Count > 0)
        {
            TrailPoint last = current[^1];
            double dx = x - last.X, dy = y - last.Y;
            if (dx * dx + dy * dy < MinDistance * MinDistance)
                return false;
        }

        current.Add(new TrailPoint(x, y));
        return true;
    }

    public void AddPoi(float x, float y) => Pois.Add(new TrailPoint(x, y));

    /// <summary>
    /// Drops the current path if it is still empty. Called when recording stops so a
    /// path that was started but never filled (e.g. resume with no movement) never
    /// reaches export as a spurious blank line.
    /// </summary>
    public void PruneEmptyCurrentPath()
    {
        if (_paths.Count > 0 && _paths[^1].Count == 0)
            _paths.RemoveAt(_paths.Count - 1);
    }

    /// <summary>
    /// Replaces all data with the given paths and POIs verbatim — no distance
    /// filtering — so imported data round-trips exactly with what was exported.
    /// </summary>
    public void Load(IEnumerable<IReadOnlyList<TrailPoint>> paths, IEnumerable<TrailPoint> pois)
    {
        _paths.Clear();
        foreach (IReadOnlyList<TrailPoint> path in paths)
            _paths.Add(new List<TrailPoint>(path));

        Pois.Clear();
        Pois.AddRange(pois);
    }

    public void Clear()
    {
        _paths.Clear();
        Pois.Clear();
    }
}
