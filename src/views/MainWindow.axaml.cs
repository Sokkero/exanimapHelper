using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;

namespace ExanimapHelper;

public partial class MainWindow : Window
{
    private enum RecordingState { Idle, Recording }

    // Values beyond this magnitude are treated as suspicious (likely a wrong
    // address). Heuristic only — Exanima's real coordinate scale is unconfirmed.
    private const float SuspiciousMagnitude = 1e7f;

    // Display-row formats for the data list, kept in one place so the separator and
    // POI rows render identically wherever they are produced.
    private const string NewPathRow = "— new path —";
    private const string PoiPrefix = "POI  ";

    private static readonly IBrush ErrorBrush = Brushes.Firebrick;
    private static readonly IBrush WarnBrush = Brushes.DarkOrange;
    private static readonly IBrush InfoBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));

    private static readonly FilePickerFileType TxtType = new("Text file") { Patterns = new[] { "*.txt" } };
    private static readonly FilePickerFileType PngType = new("PNG image") { Patterns = new[] { "*.png" } };
    private static readonly FilePickerFileType AllType = new("All files") { Patterns = new[] { "*" } };

    private readonly MemoryReader _reader = new();
    private readonly TrailModel _trail = new();
    private readonly PollingService _polling;
    private readonly GraphRenderer _renderer;
    private readonly IHotkeyService _hotkey;
    private readonly AppSettings _settings = SettingsService.Load();

    // Flat, display-only view of the recorded data (point rows, path separators and
    // POI rows). Kept in sync incrementally so scroll-to-latest keeps working.
    private readonly ObservableCollection<string> _display = new();

    private RecordingState _state = RecordingState.Idle;
    private bool IsRecording => _state == RecordingState.Recording;

    // Most recent live reading, used to place POIs and the live marker.
    private float? _lastLiveX, _lastLiveY;

    public MainWindow()
    {
        InitializeComponent();

        _polling = new PollingService(_reader);
        _polling.ValueRead += OnValueRead;

        _renderer = new GraphRenderer(GraphCanvas);

        _hotkey = Hotkeys.Create(this);

        XAddressBox.Text = _settings.XAddress;
        YAddressBox.Text = _settings.YAddress;
        IntervalBox.Text = _settings.IntervalMs.ToString(CultureInfo.InvariantCulture);
        DataList.ItemsSource = _display;

        TrySetIcon();
    }

    private void TrySetIcon()
    {
        try
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://ExanimapHelper/assets/appIcon.png")));
        }
        catch
        {
            // Window icon is cosmetic; never let a missing asset break startup.
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // The hotkey and the live readout are independent; start the readout for any
        // restored addresses regardless of whether the hotkey registered.
        RefreshLivePreview();

        var failed = new List<string>();
        if (!_hotkey.Register(Key.F8, ToggleRecording))
            failed.Add("F8");
        if (!_hotkey.Register(Key.F10, MarkPoi))
            failed.Add("F10");
        if (failed.Count > 0)
            SetStatus($"Could not register the {string.Join("/", failed)} hotkey(s) (another app may be using them).", WarnBrush);
    }

    protected override void OnClosed(EventArgs e)
    {
        // Persist the addresses as entered, plus the interval if it is valid.
        _settings.XAddress = XAddressBox.Text ?? "";
        _settings.YAddress = YAddressBox.Text ?? "";
        if (int.TryParse(IntervalBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out int intervalMs) && intervalMs > 0)
            _settings.IntervalMs = intervalMs;
        SettingsService.Save(_settings);

        _hotkey.Dispose();
        _polling.Stop();
        _reader.Dispose();
        base.OnClosed(e);
    }

    private void StartStopButton_Click(object? sender, RoutedEventArgs e) => ToggleRecording();

    // Drives the Start → Stop → Resume cycle. Both the button and F8 call this.
    private void ToggleRecording()
    {
        if (IsRecording)
            StopRecording();
        else
            StartRecording();
    }

    private void Input_LostFocus(object? sender, RoutedEventArgs e) => RefreshLivePreview();

    // Starts/refreshes the live readout when an address or the interval changes.
    // The poll loop runs continuously so Live X/Y stay current without recording.
    private void RefreshLivePreview()
    {
        if (IsRecording)
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
        _lastLiveX = _lastLiveY = null;
        _renderer.UpdateLiveMarker(null, null);
    }

    private void ClearButton_Click(object? sender, RoutedEventArgs e)
    {
        _trail.Clear();
        _display.Clear();

        // While recording, keep going on a fresh path so Add still has a target.
        if (IsRecording)
        {
            _trail.StartNewPath();
        }
        else
        {
            _state = RecordingState.Idle;
            StartStopButton.Content = "Start (F8)";
        }

        _renderer.Render(_trail.Paths, _trail.Pois);
        SetDataActionsEnabled(false);
        SetStatus(IsRecording ? "Cleared — still recording." : "Cleared.", InfoBrush);
    }

    private void StartRecording()
    {
        if (!TryStartPolling(out string error))
        {
            SetStatus(error, ErrorBrush);
            return;
        }

        // Resuming with existing data starts a visually and structurally separate path.
        if (_trail.HasData)
            _display.Add(NewPathRow);

        _trail.StartNewPath();

        _state = RecordingState.Recording;
        SetInputsEnabled(false);
        MarkPoiButton.IsEnabled = true;
        StartStopButton.Content = "Stop (F8)";
        SetStatus("Recording…", InfoBrush);
    }

    private void StopRecording()
    {
        _trail.PruneEmptyCurrentPath();

        // With data, the cycle continues at Resume; with nothing recorded, fall back
        // to the initial Start state rather than offering a resume of an empty trail.
        bool hasData = _trail.HasData;
        _state = RecordingState.Idle;
        SetInputsEnabled(true);
        MarkPoiButton.IsEnabled = false;
        StartStopButton.Content = hasData ? "Resume (F8)" : "Start (F8)";
        // Leave the poll loop running so the live readout and marker keep updating.
        SetStatus("Recording stopped.", InfoBrush);
    }

    private void MarkPoiButton_Click(object? sender, RoutedEventArgs e) => MarkPoi();

    // Marks a POI at the latest live position. Both the button and F10 call this; the
    // hotkey fires while recording, so the recording guard makes a press outside
    // recording a no-op, matching the button being disabled then.
    private void MarkPoi()
    {
        if (!IsRecording)
            return;

        if (_lastLiveX is not float x || _lastLiveY is not float y
            || IsSuspicious(x) || IsSuspicious(y))
        {
            SetStatus("Can't mark a POI — no valid position is being read.", WarnBrush);
            return;
        }

        CommitPoi(x, y);
        SetStatus($"Recording… {_display.Count} entries — POI marked.", InfoBrush);
    }

    // Double-clicking the graph places a POI at the clicked location, after a
    // confirmation prompt. Works whenever there is a rendered coordinate frame to map
    // the click into, regardless of recording.
    private async void GraphCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        PointerPoint point = e.GetCurrentPoint(GraphCanvas);
        if (e.ClickCount != 2 || !point.Properties.IsLeftButtonPressed)
            return;

        if (!_renderer.TryCanvasToData(e.GetPosition(GraphCanvas), out float x, out float y))
        {
            SetStatus("Can't place a POI yet — record or import some points first.", WarnBrush);
            return;
        }

        if (!await ConfirmDialog.ShowAsync(this, "Mark POI", "Mark POI?"))
            return;

        CommitPoi(x, y);
        SetStatus($"POI marked at {new TrailPoint(x, y)}.", InfoBrush);
    }

    // Adds a POI to the model and reflects it in the list and graph. Callers set their
    // own status message afterwards.
    private void CommitPoi(float x, float y)
    {
        _trail.AddPoi(x, y);
        _display.Add($"{PoiPrefix}{new TrailPoint(x, y)}");
        DataList.ScrollIntoView(_display[^1]);
        _renderer.Render(_trail.Paths, _trail.Pois);
        SetDataActionsEnabled(true);
    }

    // Validates the inputs, attaches to Exanima if needed, and (re)starts the poll
    // loop with an immediate read. Returns false with a reason on any failure.
    private bool TryStartPolling(out string error)
    {
        error = "";

        // Case 1 — validate input before touching memory.
        if (!MemoryReader.TryParseAddress(XAddressBox.Text, out AddressSpec xSpec))
        {
            error = "X address is not a valid hex value or Module+offset.";
            return false;
        }
        if (!MemoryReader.TryParseAddress(YAddressBox.Text, out AddressSpec ySpec))
        {
            error = "Y address is not a valid hex value or Module+offset.";
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
                    AttachStatus.Unsupported => "Memory reading is only available on Windows.",
                    _ => "Could not attach to Exanima.",
                };
                return false;
            }
        }

        // Resolve module-relative addresses now that a module base is known.
        if (!_reader.TryResolve(xSpec, out long xAddress) || !_reader.TryResolve(ySpec, out long yAddress))
        {
            error = "Could not read Exanima's module base — try running this app as administrator.";
            return false;
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

        // Cache and reflect the live position on the graph regardless of recording.
        _lastLiveX = x;
        _lastLiveY = y;
        _renderer.UpdateLiveMarker(x, y);

        // A null axis means the address is unset or its read failed.
        bool readFailed = x is null || y is null;

        // Case 3 (detectable subset) — flag obviously-broken values.
        bool suspicious = (x is float sx && IsSuspicious(sx)) || (y is float sy && IsSuspicious(sy));

        if (!IsRecording)
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
            SetStatus($"Recording… {_display.Count} entries — ⚠ could not read an address.", WarnBrush);
            return;
        }

        // Only points far enough from the last one are recorded.
        if (_trail.Add(x.Value, y.Value))
        {
            _display.Add(new TrailPoint(x.Value, y.Value).ToString());
            DataList.ScrollIntoView(_display[^1]);
            SetDataActionsEnabled(true);
            _renderer.Render(_trail.Paths, _trail.Pois);
        }

        SetStatus(suspicious
            ? $"Recording… {_display.Count} entries — ⚠ value looks invalid, check the address."
            : $"Recording… {_display.Count} entries", suspicious ? WarnBrush : InfoBrush);
    }

    // Runs a picker, then resolves the chosen file to a local path. Returns null if the
    // user cancelled, or sets the error status and returns null if the path can't be
    // resolved (e.g. a non-local provider location). Shared by all three handlers.
    private async Task<string?> PickSavePathAsync(FilePickerSaveOptions options) =>
        ResolveLocalPath(await StorageProvider.SaveFilePickerAsync(options));

    private async Task<string?> PickOpenPathAsync(FilePickerOpenOptions options)
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(options);
        return ResolveLocalPath(files.Count > 0 ? files[0] : null);
    }

    private string? ResolveLocalPath(IStorageFile? file)
    {
        if (file is null)
            return null;
        string? path = file.TryGetLocalPath();
        if (path is null)
            SetStatus("Could not resolve the chosen file path.", ErrorBrush);
        return path;
    }

    private async void ExportTextButton_Click(object? sender, RoutedEventArgs e)
    {
        string? path = await PickSavePathAsync(new FilePickerSaveOptions
        {
            Title = "Export Data",
            SuggestedFileName = "exanima-trail.txt",
            DefaultExtension = "txt",
            FileTypeChoices = new[] { TxtType, AllType },
        });
        if (path is null)
            return;

        try
        {
            ExportService.ExportText(_trail.Paths, _trail.Pois, path);
            SetStatus($"Saved trail to {path}", InfoBrush);
        }
        catch (Exception ex)
        {
            SetStatus($"Export failed: {ex.Message}", ErrorBrush);
        }
    }

    private async void ExportPngButton_Click(object? sender, RoutedEventArgs e)
    {
        string? path = await PickSavePathAsync(new FilePickerSaveOptions
        {
            Title = "Export PNG",
            SuggestedFileName = "exanima-trail.png",
            DefaultExtension = "png",
            FileTypeChoices = new[] { PngType, AllType },
        });
        if (path is null)
            return;

        try
        {
            // Hide the live marker so it never lands in the exported image.
            _renderer.SetLiveMarkerVisible(false);
            try
            {
                ExportService.ExportPng(GraphCanvas, path);
            }
            finally
            {
                _renderer.UpdateLiveMarker(_lastLiveX, _lastLiveY);
            }
            SetStatus($"Saved graph to {path}", InfoBrush);
        }
        catch (Exception ex)
        {
            SetStatus($"Export failed: {ex.Message}", ErrorBrush);
        }
    }

    private async void ImportTextButton_Click(object? sender, RoutedEventArgs e)
    {
        if (IsRecording)
        {
            SetStatus("Stop recording before importing.", WarnBrush);
            return;
        }

        string? path = await PickOpenPathAsync(new FilePickerOpenOptions
        {
            Title = "Import Data",
            AllowMultiple = false,
            FileTypeFilter = new[] { TxtType, AllType },
        });
        if (path is null)
            return;

        try
        {
            ExportService.ImportResult result = ExportService.ImportText(path);
            _trail.Load(result.Paths, result.Pois);
            RebuildDisplay();
            _renderer.Render(_trail.Paths, _trail.Pois);

            // Imported data leaves recording idle; reset the cycle.
            _state = RecordingState.Idle;
            StartStopButton.Content = "Start (F8)";

            SetDataActionsEnabled(_trail.HasData);
            if (_display.Count > 0)
                DataList.ScrollIntoView(_display[^1]);
            SetStatus($"Imported trail from {path}", InfoBrush);
        }
        catch (Exception ex)
        {
            SetStatus($"Import failed: {ex.Message}", ErrorBrush);
        }
    }

    // Rebuilds the flat display list from the model: each path's points, a separator
    // between paths, then the POIs.
    private void RebuildDisplay()
    {
        _display.Clear();
        bool firstPath = true;
        foreach (IReadOnlyList<TrailPoint> path in _trail.Paths)
        {
            if (path.Count == 0)
                continue;
            if (!firstPath)
                _display.Add(NewPathRow);
            firstPath = false;
            foreach (TrailPoint p in path)
                _display.Add(p.ToString());
        }

        foreach (TrailPoint p in _trail.Pois)
            _display.Add($"{PoiPrefix}{p}");
    }

    private static bool IsSuspicious(float value) =>
        !TrailPoint.IsFinite(value) || Math.Abs(value) > SuspiciousMagnitude;

    private void SetInputsEnabled(bool enabled)
    {
        XAddressBox.IsEnabled = enabled;
        YAddressBox.IsEnabled = enabled;
        IntervalBox.IsEnabled = enabled;
    }

    // Controls that are only usable once there is recorded data.
    private void SetDataActionsEnabled(bool enabled)
    {
        ClearButton.IsEnabled = enabled;
        ExportTextButton.IsEnabled = enabled;
        ExportPngButton.IsEnabled = enabled;
    }

    private void SetStatus(string message, IBrush brush)
    {
        StatusText.Foreground = brush;
        StatusText.Text = message;
    }
}
