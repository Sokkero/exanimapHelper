using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ExanimapHelper;

/// <summary>
/// Renders recorded trail points onto a <see cref="Canvas"/>: auto-fits all points
/// with padding, rotates the trail 180° to match the in-game map orientation, and
/// connects consecutive points with a polyline. Uses a single uniform scale so the
/// path keeps its true shape. Non-finite points (NaN/Infinity) are skipped.
/// </summary>
public sealed class GraphRenderer
{
    private const double Padding = 20;
    private const double PointDiameter = 5;

    private static readonly Brush LineBrush = Brushes.SteelBlue;
    private static readonly Brush PointBrush = Brushes.SteelBlue;
    private static readonly Brush StartBrush = Brushes.SeaGreen;  // first point
    private static readonly Brush EndBrush = Brushes.Firebrick;   // latest point

    private readonly Canvas _canvas;
    private IReadOnlyList<TrailPoint> _points = [];

    public GraphRenderer(Canvas canvas)
    {
        _canvas = canvas;
        // Re-fit the trail whenever the canvas size changes.
        _canvas.SizeChanged += (_, _) => Render(_points);
    }

    public void Render(IReadOnlyList<TrailPoint> points)
    {
        _points = points;
        _canvas.Children.Clear();

        // Keep only finite points; NaN/Infinity would corrupt the bounds.
        var finite = new List<TrailPoint>(points.Count);
        foreach (TrailPoint p in points)
            if (TrailPoint.IsFinite(p.X) && TrailPoint.IsFinite(p.Y))
                finite.Add(p);

        if (finite.Count == 0)
            return;

        double w = _canvas.ActualWidth;
        double h = _canvas.ActualHeight;
        if (w <= 0 || h <= 0)
            return; // not laid out yet

        float minX = finite[0].X, maxX = minX;
        float minY = finite[0].Y, maxY = minY;
        foreach (TrailPoint p in finite)
        {
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }

        double availW = Math.Max(1, w - 2 * Padding);
        double availH = Math.Max(1, h - 2 * Padding);
        double rangeX = maxX - minX;
        double rangeY = maxY - minY;

        // Uniform scale preserves the path's aspect ratio; an axis with zero range
        // doesn't constrain the fit, and scale stays 1 for a single/identical point.
        double scale = 1;
        if (rangeX > 0 && rangeY > 0) scale = Math.Min(availW / rangeX, availH / rangeY);
        else if (rangeX > 0)          scale = availW / rangeX;
        else if (rangeY > 0)          scale = availH / rangeY;

        // Center the scaled trail within the canvas.
        double offsetX = Padding + (availW - rangeX * scale) / 2;
        double offsetY = Padding + (availH - rangeY * scale) / 2;

        var screen = new PointCollection(finite.Count);
        foreach (TrailPoint p in finite)
        {
            // Both axes are inverted so the whole trail is rotated 180° to match
            // Exanima's in-game map orientation.
            double sx = offsetX + (maxX - p.X) * scale;
            double sy = h - offsetY - (maxY - p.Y) * scale;
            screen.Add(new Point(sx, sy));
        }

        if (screen.Count >= 2)
            _canvas.Children.Add(new Polyline
            {
                Stroke = LineBrush,
                StrokeThickness = 1.5,
                Points = screen,
            });

        for (int i = 0; i < screen.Count; i++)
        {
            Brush brush = i == 0 ? StartBrush
                        : i == screen.Count - 1 ? EndBrush
                        : PointBrush;
            AddDot(screen[i], brush);
        }
    }

    private void AddDot(Point p, Brush brush)
    {
        var dot = new Ellipse { Width = PointDiameter, Height = PointDiameter, Fill = brush };
        Canvas.SetLeft(dot, p.X - PointDiameter / 2);
        Canvas.SetTop(dot, p.Y - PointDiameter / 2);
        _canvas.Children.Add(dot);
    }
}
