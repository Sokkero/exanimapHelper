using System.Globalization;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace ExanimapHelper;

/// <summary>
/// Writes recorded data out of the app and reads it back. The text format is one
/// "x, y" per line, a blank line between paths, and an optional trailing
/// "--- POI ---" section listing POI coordinates. The graph can also be written as
/// a PNG.
/// </summary>
public static class ExportService
{
    private const string PoiHeader = "--- POI ---";

    // PNG export supersample factor. The canvas is vector content, so rendering at a
    // higher pixel density re-rasterises lines and dots crisply rather than upscaling.
    private const double PngScale = 4;

    /// <summary>Result of parsing a saved trail file.</summary>
    public readonly record struct ImportResult(List<List<TrailPoint>> Paths, List<TrailPoint> Pois);

    /// <summary>
    /// Writes each path as a block of "x, y" lines, separated by a blank line, then a
    /// "--- POI ---" section if any POIs exist. Full precision, invariant culture, so
    /// the values round-trip for reuse.
    /// </summary>
    public static void ExportText(
        IReadOnlyList<IReadOnlyList<TrailPoint>> paths, IReadOnlyList<TrailPoint> pois, string path)
    {
        var sb = new StringBuilder();

        bool firstBlock = true;
        foreach (IReadOnlyList<TrailPoint> p in paths)
        {
            if (p.Count == 0)
                continue;
            if (!firstBlock)
                sb.Append('\n');
            firstBlock = false;
            foreach (TrailPoint pt in p)
                AppendPoint(sb, pt);
        }

        if (pois.Count > 0)
        {
            if (!firstBlock)
                sb.Append('\n');
            sb.Append(PoiHeader).Append('\n');
            foreach (TrailPoint pt in pois)
                AppendPoint(sb, pt);
        }

        File.WriteAllText(path, sb.ToString());
    }

    private static void AppendPoint(StringBuilder sb, TrailPoint p)
    {
        sb.Append(p.X.ToString("R", CultureInfo.InvariantCulture));
        sb.Append(", ");
        sb.Append(p.Y.ToString("R", CultureInfo.InvariantCulture));
        sb.Append('\n');
    }

    /// <summary>
    /// Reads back a file written by <see cref="ExportText"/>. A blank line separates
    /// paths; a "--- POI ---" line switches the remainder of the file to POIs. Empty
    /// paths are dropped. A coordinate line that does not hold two finite numbers
    /// aborts the import with its line number, so a half-parsed trail is never loaded.
    /// Legacy files (no blank lines, no POI section) load as a single path.
    /// </summary>
    public static ImportResult ImportText(string path)
    {
        var paths = new List<List<TrailPoint>>();
        var pois = new List<TrailPoint>();
        var current = new List<TrailPoint>();
        bool inPoiSection = false;

        string[] lines = File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            if (line.Length == 0)
            {
                // Blank line ends the current path (path separator).
                if (!inPoiSection && current.Count > 0)
                {
                    paths.Add(current);
                    current = new List<TrailPoint>();
                }
                continue;
            }

            if (line == PoiHeader)
            {
                if (current.Count > 0)
                {
                    paths.Add(current);
                    current = new List<TrailPoint>();
                }
                inPoiSection = true;
                continue;
            }

            TrailPoint pt = ParsePoint(line, lines[i], i);
            if (inPoiSection)
                pois.Add(pt);
            else
                current.Add(pt);
        }

        if (current.Count > 0)
            paths.Add(current);

        return new ImportResult(paths, pois);
    }

    private static TrailPoint ParsePoint(string trimmed, string raw, int index)
    {
        string[] parts = trimmed.Split(',');
        if (parts.Length != 2
            || !float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
            || !float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
            || !TrailPoint.IsFinite(x) || !TrailPoint.IsFinite(y))
        {
            throw new FormatException($"Line {index + 1} is not a valid \"x, y\" pair: \"{raw}\"");
        }
        return new TrailPoint(x, y);
    }

    /// <summary>
    /// Renders the given canvas to a PNG file at its current on-screen size.
    /// The canvas background is dropped during render so only the trail lines and
    /// points are written, leaving everything else transparent. Callers are
    /// responsible for hiding the live marker beforehand so it does not appear.
    /// </summary>
    public static void ExportPng(Canvas canvas, string path)
    {
        if (canvas.Bounds.Width <= 0 || canvas.Bounds.Height <= 0)
            throw new InvalidOperationException("The graph has no size to export yet.");

        // Pixel dimensions are scaled up and the DPI raised to match, so the canvas's
        // on-screen layout size maps onto more pixels — a sharper image at the same
        // framing.
        int pxW = (int)Math.Ceiling(canvas.Bounds.Width * PngScale);
        int pxH = (int)Math.Ceiling(canvas.Bounds.Height * PngScale);

        IBrush? originalBackground = canvas.Background;
        canvas.Background = null;
        try
        {
            // RenderTargetBitmap maps the canvas's DIP size onto the pixel grid using
            // its DPI; raising both DPI and pixel size by the same factor supersamples
            // the vector content rather than upscaling. Save writes a PNG.
            using var bitmap = new RenderTargetBitmap(
                new PixelSize(pxW, pxH), new Vector(96 * PngScale, 96 * PngScale));
            bitmap.Render(canvas);
            bitmap.Save(path);
        }
        finally
        {
            canvas.Background = originalBackground;
        }
    }
}
