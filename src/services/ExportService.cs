using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ExanimapHelper;

/// <summary>
/// Writes recorded data out of the app: the coordinate list as plain text, and the
/// rendered graph as a PNG.
/// </summary>
public static class ExportService
{
    /// <summary>
    /// Writes every recorded pair to <paramref name="path"/>, one "x, y" per line.
    /// Full precision, invariant culture, so the values round-trip for reuse.
    /// </summary>
    public static void ExportText(IReadOnlyList<TrailPoint> points, string path)
    {
        var sb = new StringBuilder();
        foreach (TrailPoint p in points)
        {
            sb.Append(p.X.ToString("R", CultureInfo.InvariantCulture));
            sb.Append(", ");
            sb.Append(p.Y.ToString("R", CultureInfo.InvariantCulture));
            sb.Append('\n');
        }
        File.WriteAllText(path, sb.ToString());
    }

    /// <summary>
    /// Reads back a file written by <see cref="ExportText"/>: one "x, y" per line.
    /// Blank lines are skipped. A line that does not hold two finite numbers aborts
    /// the import with the offending line number, so a half-parsed trail is never loaded.
    /// </summary>
    public static List<TrailPoint> ImportText(string path)
    {
        var points = new List<TrailPoint>();
        string[] lines = File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0)
                continue;

            string[] parts = line.Split(',');
            if (parts.Length != 2
                || !float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                || !float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
                || !TrailPoint.IsFinite(x) || !TrailPoint.IsFinite(y))
            {
                throw new FormatException($"Line {i + 1} is not a valid \"x, y\" pair: \"{lines[i]}\"");
            }

            points.Add(new TrailPoint(x, y));
        }
        return points;
    }

    /// <summary>
    /// Renders the given canvas to a PNG file at its current on-screen size.
    /// The canvas background is dropped during render so only the trail lines and
    /// points are written, leaving everything else transparent.
    /// </summary>
    public static void ExportPng(Canvas canvas, string path)
    {
        int w = (int)Math.Ceiling(canvas.ActualWidth);
        int h = (int)Math.Ceiling(canvas.ActualHeight);
        if (w <= 0 || h <= 0)
            throw new InvalidOperationException("The graph has no size to export yet.");

        Brush originalBackground = canvas.Background;
        canvas.Background = null;
        try
        {
            // Force a layout pass so the cleared background is reflected before render.
            canvas.UpdateLayout();

            var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(canvas);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using FileStream fs = File.Create(path);
            encoder.Save(fs);
        }
        finally
        {
            canvas.Background = originalBackground;
        }
    }
}
