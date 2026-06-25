using System.Windows.Threading;

namespace ExanimapHelper;

/// <summary>
/// </summary>
public sealed class PollingService
{
    private readonly MemoryReader _reader;
    private readonly DispatcherTimer _timer = new();
    private long _xAddress;
    private long _yAddress;

    public PollingService(MemoryReader reader)
    {
        _reader = reader;
        _timer.Tick += OnTick;
    }

    public bool IsRunning => _timer.IsEnabled;

    /// <summary>Raised on each successful tick with the freshly read values.</summary>
    public event Action<float, float>? ValueRead;

    /// <summary>Raised when a read fails; the loop has already been stopped.</summary>
    public event Action<string>? ReadFailed;

    /// <summary>Starts (or restarts) the loop, reading once immediately and then
    /// on every interval tick.</summary>
    public void Start(long xAddress, long yAddress, int intervalMs)
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
        if (!_reader.TryReadFloat(_xAddress, out float x) ||
            !_reader.TryReadFloat(_yAddress, out float y))
        {
            _timer.Stop();
            ReadFailed?.Invoke("Read failed — the process may have closed or the address became invalid. Recording stopped.");
            return;
        }

        ValueRead?.Invoke(x, y);
    }
}
