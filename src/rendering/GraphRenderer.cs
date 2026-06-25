using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ExanimapHelper;

/// <summary>
/// Renders the recorded paths and POIs onto a <see cref="Canvas"/>: auto-fits all
/// points with padding, flips the Y axis so the trail matches the in-game
/// north-oriented map, and connects each path's points with its own polyline.
/// A single uniform scale keeps the true shape. Non-finite points (NaN/Infinity)
/// are skipped.
///
/// Every recorded point shares one colour; only the most recently recorded point is
/// drawn red. POIs are hollow red circles. A separate red "live position" dot tracks
/// the current reading even when not recording; it is excluded from PNG export.
/// </summary>
public sealed class GraphRenderer
{
    private const double Padding = 20;
    private const double PointDiameter = 5;
    private const double PoiDiameter = PointDiameter * 10;

    private static readonly Brush LineBrush = Brushes.SteelBlue;
    private static readonly Brush PointBrush = Brushes.SteelBlue;
    private static readonly Brush LatestBrush = Brushes.Firebrick;  // most recent recorded point
    private static readonly Brush PoiBrush = Brushes.Red;           // hollow circle
    private static readonly Brush LiveBrush = Brushes.Red;          // live position dot

    private readonly Canvas _canvas;

    private IReadOnlyList<IReadOnlyList<TrailPoint>> _paths = [];
    private IReadOnlyList<TrailPoint> _pois = [];

    // Transform from data space to canvas space, captured on each Render so the live
    // marker can be repositioned without a full re-render. Null until the first
    // render produces a usable fit (i.e. there is at least one finite point).
    private Transform? _transform;

    // The live marker is tracked separately so it can be moved and hidden
    // independently of the recorded geometry.
    private readonly Ellipse _liveDot = new()
    {
        Width = PointDiameter,
        Height = PointDiameter,
        Fill = LiveBrush,
        Visibility = Visibility.Collapsed,
    };
    private float? _liveX, _liveY;

    private readonly record struct Transform(
        double MinX, double MaxY, double Scale, double OffsetX, double OffsetY, double CanvasH);

    public GraphRenderer(Canvas canvas)
    {
        _canvas = canvas;
        // Attach the live dot up front so it can show before any data is rendered.
        _canvas.Children.Add(_liveDot);
        // Re-fit whenever the canvas size changes.
        _canvas.SizeChanged += (_, _) => Render(_paths, _pois);
    }

    public void Render(IReadOnlyList<IReadOnlyList<TrailPoint>> paths, IReadOnlyList<TrailPoint> pois)
    {
        _paths = paths;
        _pois = pois;
        _canvas.Children.Clear();
        _transform = null;

        double w = _canvas.ActualWidth;
        double h = _canvas.ActualHeight;
        if (w <= 0 || h <= 0)
        {
            // Not laid out yet; still keep the live dot attached for later updates.
            _canvas.Children.Add(_liveDot);
            return;
        }

        // Bounds come from recorded points and POIs only — never the live dot — so the
        // fit stays stable as the player moves and the PNG framing is deterministic.
        bool hasBounds = false;
        float minX = 0, maxX = 0, minY = 0, maxY = 0;
        void Extend(TrailPoint p)
        {
            if (!TrailPoint.IsFinite(p))
                return;
            if (!hasBounds)
            {
                minX = maxX = p.X;
                minY = maxY = p.Y;
                hasBounds = true;
                return;
            }
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }

        foreach (IReadOnlyList<TrailPoint> path in paths)
            foreach (TrailPoint p in path)
                Extend(p);
        foreach (TrailPoint p in pois)
            Extend(p);

        if (!hasBounds)
        {
            // Nothing recorded yet; show only the (centered) live dot.
            _canvas.Children.Add(_liveDot);
            PositionLiveDot();
            return;
        }

        double availW = Math.Max(1, w - 2 * Padding);
        double availH = Math.Max(1, h - 2 * Padding);
        double rangeX = maxX - minX;
        double rangeY = maxY - minY;

        // Uniform scale preserves aspect ratio; an axis with zero range doesn't
        // constrain the fit, and scale stays 1 for a single/identical point.
        double scale = 1;
        if (rangeX > 0 && rangeY > 0) scale = Math.Min(availW / rangeX, availH / rangeY);
        else if (rangeX > 0)          scale = availW / rangeX;
        else if (rangeY > 0)          scale = availH / rangeY;

        double offsetX = Padding + (availW - rangeX * scale) / 2;
        double offsetY = Padding + (availH - rangeY * scale) / 2;

        _transform = new Transform(minX, maxY, scale, offsetX, offsetY, h);

        // Draw each path as its own polyline + interior dots.
        var allDots = new GeometryGroup();
        double radius = PointDiameter / 2;
        foreach (IReadOnlyList<TrailPoint> path in paths)
        {
            var screen = new PointCollection(path.Count);
            foreach (TrailPoint p in path)
            {
                if (!TrailPoint.IsFinite(p))
                    continue;
                screen.Add(ToScreen(p));
            }

            if (screen.Count >= 2)
                _canvas.Children.Add(new Polyline
                {
                    Stroke = LineBrush,
                    StrokeThickness = 1.5,
                    Points = screen,
                });

            // Collapse this path's dots into the shared frozen geometry so the visual
            // tree stays tiny no matter how long the trail grows.
            foreach (Point sp in screen)
                allDots.Children.Add(new EllipseGeometry(sp, radius, radius));
        }

        if (allDots.Children.Count > 0)
        {
            allDots.Freeze();
            _canvas.Children.Add(new Path { Fill = PointBrush, Data = allDots });
        }

        // POIs: hollow red circles at 10x point size.
        foreach (TrailPoint p in pois)
        {
            if (!TrailPoint.IsFinite(p))
                continue;
            Point sp = ToScreen(p);
            var circle = new Ellipse
            {
                Width = PoiDiameter,
                Height = PoiDiameter,
                Stroke = PoiBrush,
                StrokeThickness = 2,
                Fill = null,
            };
            Canvas.SetLeft(circle, sp.X - PoiDiameter / 2);
            Canvas.SetTop(circle, sp.Y - PoiDiameter / 2);
            _canvas.Children.Add(circle);
        }

        // The single most recent recorded point is drawn red, on top of its uniform dot.
        TrailPoint? latest = FindLatest(paths);
        if (latest is TrailPoint lp && TrailPoint.IsFinite(lp))
            AddDot(ToScreen(lp), LatestBrush);

        // Live dot stays on top and is repositioned from the cached transform.
        _canvas.Children.Add(_liveDot);
        PositionLiveDot();
    }

    /// <summary>Updates the live-position dot from the latest reading.</summary>
    public void UpdateLiveMarker(float? x, float? y)
    {
        _liveX = x;
        _liveY = y;
        PositionLiveDot();
    }

    /// <summary>Shows/hides the live dot. Export hides it so it never lands in the PNG.</summary>
    public void SetLiveMarkerVisible(bool visible) =>
        _liveDot.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    private static TrailPoint? FindLatest(IReadOnlyList<IReadOnlyList<TrailPoint>> paths)
    {
        for (int i = paths.Count - 1; i >= 0; i--)
            if (paths[i].Count > 0)
                return paths[i][^1];
        return null;
    }

    private Point ToScreen(TrailPoint p)
    {
        Transform t = _transform!.Value;
        // X left-to-right as-is; Y flipped to match Exanima's north-oriented map.
        double sx = t.OffsetX + (p.X - t.MinX) * t.Scale;
        double sy = t.CanvasH - t.OffsetY - (t.MaxY - p.Y) * t.Scale;
        return new Point(sx, sy);
    }

    // Positions the live dot from the current reading. With a transform it maps the
    // true coordinate and clamps to the canvas edge when off-screen, so the dot stays
    // visible even when the player roams away from the recorded trail. Without a
    // transform (nothing recorded yet) it sits at the canvas centre.
    private void PositionLiveDot()
    {
        double w = _canvas.ActualWidth, h = _canvas.ActualHeight;
        if (_liveX is not float lx || _liveY is not float ly
            || !TrailPoint.IsFinite(lx) || !TrailPoint.IsFinite(ly) || w <= 0 || h <= 0)
        {
            _liveDot.Visibility = Visibility.Collapsed;
            return;
        }

        Point p;
        if (_transform is null)
        {
            p = new Point(w / 2, h / 2);
        }
        else
        {
            p = ToScreen(new TrailPoint(lx, ly));
            double r = PointDiameter / 2;
            p.X = Math.Clamp(p.X, r, w - r);
            p.Y = Math.Clamp(p.Y, r, h - r);
        }

        Canvas.SetLeft(_liveDot, p.X - PointDiameter / 2);
        Canvas.SetTop(_liveDot, p.Y - PointDiameter / 2);
        _liveDot.Visibility = Visibility.Visible;
    }

    private void AddDot(Point p, Brush brush)
    {
        var dot = new Ellipse { Width = PointDiameter, Height = PointDiameter, Fill = brush };
        Canvas.SetLeft(dot, p.X - PointDiameter / 2);
        Canvas.SetTop(dot, p.Y - PointDiameter / 2);
        _canvas.Children.Add(dot);
    }
}
