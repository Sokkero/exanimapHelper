using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ExanimapHelper;

/// <summary>
/// Registers system-wide hotkeys via the Win32 RegisterHotKey API, so they fire
/// even while another application (Exanima) has focus. A plain WPF key binding would
/// not — it only works when this window is focused. Multiple hotkeys can be
/// registered; each gets its own callback.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_NONE = 0x0000;
    private const int FirstHotkeyId = 0xB001; // arbitrary; ids stay unique within this window

    private readonly Window _window;
    private readonly Dictionary<int, Action> _callbacks = new();
    private HwndSource? _source;
    private IntPtr _handle;
    private int _nextId = FirstHotkeyId;

    public HotkeyService(Window window) => _window = window;

    /// <summary>
    /// Registers a system-wide hotkey for <paramref name="virtualKey"/>, invoking
    /// <paramref name="onPressed"/> on the UI thread when it fires. The window handle
    /// must already exist (call from OnSourceInitialized or later). Returns false if
    /// registration failed, e.g. the key is already taken globally by another app.
    /// </summary>
    public bool Register(uint virtualKey, Action onPressed)
    {
        if (_source is null)
        {
            _handle = new WindowInteropHelper(_window).Handle;
            _source = HwndSource.FromHwnd(_handle);
            if (_source is null)
                return false;
            _source.AddHook(WndProc);
        }

        int id = _nextId++;
        if (!RegisterHotKey(_handle, id, MOD_NONE, virtualKey))
            return false;

        _callbacks[id] = onPressed;
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _callbacks.TryGetValue(wParam.ToInt32(), out Action? onPressed))
        {
            onPressed();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (int id in _callbacks.Keys)
            UnregisterHotKey(_handle, id);
        _callbacks.Clear();
        _source?.RemoveHook(WndProc);
        _source = null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
