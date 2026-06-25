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

    // Global hotkey that toggles recording. VK_F8 = 0x77 — change here to rebind.
    private const uint HotkeyVirtualKey = 0x77;

    private static readonly Brush ErrorBrush = Brushes.Firebrick;
    private static readonly Brush WarnBrush = Brushes.DarkOrange;
    private static readonly Brush InfoBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));

    private readonly MemoryReader _reader = new();
    private readonly TrailModel _trail = new();
    private readonly PollingService _polling;
    private readonly GraphRenderer _renderer;
    private readonly HotkeyService _hotkey;
    private readonly AppSettings _settings = SettingsService.Load();
    private bool _isRecording;

    public MainWindow()
    {
        InitializeComponent();

        _polling = new PollingService(_reader);
        _polling.ValueRead += OnValueRead;

        _renderer = new GraphRenderer(GraphCanvas);

        _hotkey = new HotkeyService(this, HotkeyVirtualKey);
        _hotkey.Pressed += ToggleRecording;

        XAddressBox.Text = _settings.XAddress;
        YAddressBox.Text = _settings.YAddress;
        IntervalBox.Text = _settings.IntervalMs.ToString(CultureInfo.InvariantCulture);
        DataList.ItemsSource = _trail.Points;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (!_hotkey.Register())
            SetStatus("Could not register the F8 hotkey (another app may be using it).", WarnBrush);
        else
            RefreshLivePreview(); // resume the live readout for any restored addresses
    }

    protected override void OnClosed(EventArgs e)
    {
        // Persist the addresses as entered, plus the interval if it is valid.
        _settings.XAddress = XAddressBox.Text;
        _settings.YAddress = YAddressBox.Text;
        if (int.TryParse(IntervalBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out int intervalMs) && intervalMs > 0)
            _settings.IntervalMs = intervalMs;
        SettingsService.Save(_settings);

        _hotkey.Dispose();
        _polling.Stop();
        _reader.Dispose();
        base.OnClosed(e);
    }

    private void StartStopButton_Click(object sender, RoutedEventArgs e) => ToggleRecording();

    private void ToggleRecording()
    {
        if (_isRecording)
            StopRecording();
        else
            StartRecording();
    }

    private void Input_LostFocus(object sender, RoutedEventArgs e) => RefreshLivePreview();

    // Starts/refreshes the live readout when an address or the interval changes.
    // The poll loop runs continuously so Live X/Y stay current without recording.
    private void RefreshLivePreview()
    {
        if (_isRecording)
            return; // inputs are locked while recording

        // Stay quiet until both addresses have been entered.
        if (string.IsNullOrWhiteSpace(XAddressBox.Text) || string.IsNullOrWhiteSpace(YAddressBox.Text))
        {
            StopLivePreview();
            return;
        }

        if (!TryStartPolling(out string error))
        {
            StopLivePreview();
            SetStatus(error, ErrorBrush);
        }
    }

    private void StopLivePreview()
    {
        _polling.Stop();
        LiveXValue.Text = "—";
        LiveYValue.Text = "—";
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _trail.Clear();
        _renderer.Render(_trail.Points);
        SetDataActionsEnabled(false);
        SetStatus(_isRecording ? "Cleared — still recording." : "Cleared.", InfoBrush);
    }

    private void StartRecording()
    {
        if (!TryStartPolling(out string error))
        {
            SetStatus(error, ErrorBrush);
            return;
        }

        _isRecording = true;
        SetInputsEnabled(false);
        StartStopButton.Content = "Stop (F8)";
        SetStatus("Recording…", InfoBrush);
    }

    private void StopRecording()
    {
        _isRecording = false;
        SetInputsEnabled(true);
        StartStopButton.Content = "Start (F8)";
        // Leave the poll loop running so the live readout keeps updating.
        SetStatus("Recording stopped.", InfoBrush);
    }

    // Validates the inputs, attaches to Exanima if needed, and (re)starts the poll
    // loop with an immediate read. Returns false with a reason on any failure.
    private bool TryStartPolling(out string error)
    {
        error = "";

        // Case 1 — validate input before touching memory.
        if (!MemoryReader.TryParseAddress(XAddressBox.Text, out long xAddress))
        {
            error = "X address is not a valid hex value.";
            return false;
        }
        if (!MemoryReader.TryParseAddress(YAddressBox.Text, out long yAddress))
        {
            error = "Y address is not a valid hex value.";
            return false;
        }
        if (!int.TryParse(IntervalBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out int intervalMs) || intervalMs <= 0)
        {
            error = "Poll interval must be a positive whole number of milliseconds.";
            return false;
        }

        if (!_reader.IsAttached)
        {
            AttachStatus status = _reader.Attach();
            if (status != AttachStatus.Attached)
            {
                error = status switch
                {
                    AttachStatus.ProcessNotFound => "Exanima is not running.",
                    AttachStatus.AccessDenied => "Could not open Exanima — try running this app as administrator.",
                    _ => "Could not attach to Exanima.",
                };
                return false;
            }
        }

        // Case 2 — test-read both addresses before committing; drop a stale handle
        // so a later attempt can re-attach.
        if (!_reader.TryReadFloat(xAddress, out _) || !_reader.TryReadFloat(yAddress, out _))
        {
            _reader.Detach();
            error = "Could not read one of the addresses. Check they are correct.";
            return false;
        }

        _polling.Start(xAddress, yAddress, intervalMs);
        return true;
    }

    private void OnValueRead(float? x, float? y)
    {
        LiveXValue.Text = x is float xv ? TrailPoint.Format(xv) : "—";
        LiveYValue.Text = y is float yv ? TrailPoint.Format(yv) : "—";

        // A null axis means the address is unset or its read failed.
        bool readFailed = x is null || y is null;

        // Case 3 (detectable subset) — flag obviously-broken values.
        bool suspicious = (x is float sx && IsSuspicious(sx)) || (y is float sy && IsSuspicious(sy));

        if (!_isRecording)
        {
            SetStatus(readFailed
                ? "Live preview — ⚠ could not read an address."
                : suspicious
                    ? "Live preview — ⚠ value looks invalid, check the address."
                    : "Live preview running.", readFailed || suspicious ? WarnBrush : InfoBrush);
            return;
        }

        // Can't record a point without both axes.
        if (readFailed)
        {
            SetStatus($"Recording… {_trail.Count} points — ⚠ could not read an address.", WarnBrush);
            return;
        }

        // Only points far enough from the last one are recorded.
        if (_trail.Add(x.Value, y.Value))
        {
            if (_trail.Count == 1)
                SetDataActionsEnabled(true);
            DataList.ScrollIntoView(_trail.Points[^1]);
            _renderer.Render(_trail.Points);
        }

        SetStatus(suspicious
            ? $"Recording… {_trail.Count} points — ⚠ value looks invalid, check the address."
            : $"Recording… {_trail.Count} points", suspicious ? WarnBrush : InfoBrush);
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

    // Controls that are only usable once a trail has been recorded.
    private void SetDataActionsEnabled(bool enabled)
    {
        ClearButton.IsEnabled = enabled;
        ExportTextButton.IsEnabled = enabled;
        ExportPngButton.IsEnabled = enabled;
    }

    private void SetStatus(string message, Brush brush)
    {
        StatusText.Foreground = brush;
        StatusText.Text = message;
    }
}
