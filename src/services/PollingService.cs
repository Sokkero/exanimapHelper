using System.Windows.Threading;

namespace ExanimapHelper;

/// <summary>
/// Drives the poll loop behind both the live readout and recording: on a fixed
/// interval it reads the X and Y addresses from the attached process and reports
/// the latest value of each. The two axes are read independently — an address that
/// is unset or whose read fails is reported as null rather than failing the tick,
/// so the caller decides what each result means (see <see cref="ValueRead"/>).
/// </summary>
public sealed class PollingService
{
    private readonly MemoryReader _reader;
    private readonly DispatcherTimer _timer = new();
    private long? _xAddress;
    private long? _yAddress;

    public PollingService(MemoryReader reader)
    {
        _reader = reader;
        _timer.Tick += OnTick;
    }

    public bool IsRunning => _timer.IsEnabled;

    /// <summary>
    /// Raised each tick with the latest per-axis reads. A value is null when its
    /// address is unset or the read failed.
    /// </summary>
    public event Action<float?, float?>? ValueRead;

    /// <summary>Starts (or restarts) the loop, reading once immediately and then
    /// on every interval tick. A null address simply isn't read.</summary>
    public void Start(long? xAddress, long? yAddress, int intervalMs)
    {
        _xAddress = xAddress;
        _yAddress = yAddress;
        _timer.Interval = TimeSpan.FromMilliseconds(intervalMs);
        _timer.Start();
        ReadOnce();
    }

    public void Stop() => _timer.Stop();

    private void OnTick(object? sender, EventArgs e) => ReadOnce();

    private void ReadOnce()
    {
        float? x = _xAddress is long xa && _reader.TryReadFloat(xa, out float xv) ? xv : null;
        float? y = _yAddress is long ya && _reader.TryReadFloat(ya, out float yv) ? yv : null;
        ValueRead?.Invoke(x, y);
    }
}
