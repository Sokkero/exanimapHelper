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
    /// Renders the given canvas to a PNG file at its current on-screen size.
    /// </summary>
    public static void ExportPng(Canvas canvas, string path)
    {
        int w = (int)Math.Ceiling(canvas.ActualWidth);
        int h = (int)Math.Ceiling(canvas.ActualHeight);
        if (w <= 0 || h <= 0)
            throw new InvalidOperationException("The graph has no size to export yet.");

        var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(canvas);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using FileStream fs = File.Create(path);
        encoder.Save(fs);
    }
}
