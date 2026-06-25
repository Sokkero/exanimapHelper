using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace ExanimapHelper;

public partial class MainWindow : Window
{
    // Values beyond this magnitude are treated as suspicious (likely a wrong
    // address). Heuristic only — Exanima's real coordinate scale is unconfirmed.
    private const float SuspiciousMagnitude = 1e7f;

    private static readonly Brush ErrorBrush = Brushes.Firebrick;
    private static readonly Brush WarnBrush = Brushes.DarkOrange;
    private static readonly Brush InfoBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));

    private readonly MemoryReader _reader = new();
    private readonly TrailModel _trail = new();
    private readonly PollingService _polling;
    private readonly GraphRenderer _renderer;

    public MainWindow()
    {
        InitializeComponent();

        _polling = new PollingService(_reader);
        _polling.ValueRead += OnValueRead;
        _polling.ReadFailed += OnReadFailed;

        _renderer = new GraphRenderer(GraphCanvas);

        DataList.ItemsSource = _trail.Points;
    }

    private void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_polling.IsRunning)
            StopRecording("Recording stopped.", InfoBrush);
        else
            StartRecording();
    }

    private void StartRecording()
    {
        // Case 1 — validate input before touching memory.
        if (!MemoryReader.TryParseAddress(XAddressBox.Text, out long xAddress))
        {
            SetStatus("X address is not a valid hex value.", ErrorBrush);
            return;
        }
        if (!MemoryReader.TryParseAddress(YAddressBox.Text, out long yAddress))
        {
            SetStatus("Y address is not a valid hex value.", ErrorBrush);
            return;
        }
        if (!int.TryParse(IntervalBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out int intervalMs) || intervalMs <= 0)
        {
            SetStatus("Poll interval must be a positive whole number of milliseconds.", ErrorBrush);
            return;
        }

        // Attach to Exanima.
        AttachStatus status = _reader.Attach();
        if (status != AttachStatus.Attached)
        {
            SetStatus(status switch
            {
                AttachStatus.ProcessNotFound => "Exanima is not running.",
                AttachStatus.AccessDenied => "Could not open Exanima — try running this app as administrator.",
                _ => "Could not attach to Exanima.",
            }, ErrorBrush);
            return;
        }

        // Case 2 — test-read both addresses before committing to a recording.
        if (!_reader.TryReadFloat(xAddress, out _) || !_reader.TryReadFloat(yAddress, out _))
        {
            _reader.Detach();
            SetStatus("Could not read one of the addresses. Check they are correct.", ErrorBrush);
            return;
        }

        SetInputsEnabled(false);
        StartStopButton.Content = "Stop";
        _polling.Start(xAddress, yAddress, intervalMs);
        SetStatus($"Recording every {intervalMs} ms…", InfoBrush);
    }

    private void StopRecording(string message, Brush brush)
    {
        _polling.Stop();
        _reader.Detach();
        SetInputsEnabled(true);
        StartStopButton.Content = "Start";
        SetStatus(message, brush);
    }

    private void OnValueRead(float x, float y)
    {
        LiveXValue.Text = TrailPoint.Format(x);
        LiveYValue.Text = TrailPoint.Format(y);

        if (_trail.Count == 0)
        {
            ExportTextButton.IsEnabled = true;
            ExportPngButton.IsEnabled = true;
        }

        _trail.Add(x, y);
        DataList.ScrollIntoView(_trail.Points[^1]);
        _renderer.Render(_trail.Points);

        // Case 3 (detectable subset) — flag obviously-broken values, keep recording.
        if (IsSuspicious(x) || IsSuspicious(y))
            SetStatus($"Recording… {_trail.Count} points — ⚠ value looks invalid, check the address.", WarnBrush);
        else
            SetStatus($"Recording… {_trail.Count} points", InfoBrush);
    }

    private void OnReadFailed(string message)
    {
        // The loop already stopped itself; StopRecording resets the rest of the UI
        // (the extra _polling.Stop() is a harmless no-op on a stopped timer).
        StopRecording(message, ErrorBrush);
    }

    private void ExportTextButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Text file (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = ".txt",
            FileName = "exanima-trail.txt",
        };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            ExportService.ExportText(_trail.Points, dialog.FileName);
            SetStatus($"Saved {_trail.Count} points to {dialog.FileName}", InfoBrush);
        }
        catch (Exception ex)
        {
            SetStatus($"Export failed: {ex.Message}", ErrorBrush);
        }
    }

    private void ExportPngButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "PNG image (*.png)|*.png|All files (*.*)|*.*",
            DefaultExt = ".png",
            FileName = "exanima-trail.png",
        };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            ExportService.ExportPng(GraphCanvas, dialog.FileName);
            SetStatus($"Saved graph to {dialog.FileName}", InfoBrush);
        }
        catch (Exception ex)
        {
            SetStatus($"Export failed: {ex.Message}", ErrorBrush);
        }
    }

    private static bool IsSuspicious(float value) =>
        !TrailPoint.IsFinite(value) || Math.Abs(value) > SuspiciousMagnitude;

    private void SetInputsEnabled(bool enabled)
    {
        XAddressBox.IsEnabled = enabled;
        YAddressBox.IsEnabled = enabled;
        IntervalBox.IsEnabled = enabled;
    }

    private void SetStatus(string message, Brush brush)
    {
        StatusText.Foreground = brush;
        StatusText.Text = message;
    }
}
