using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ExanimapHelper;

/// <summary>
/// Registers a single system-wide hotkey via the Win32 RegisterHotKey API, so it
/// fires even while another application (Exanima) has focus. A plain WPF key
/// binding would not — it only works when this window is focused.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HotkeyId = 0xB001; // arbitrary, unique within this window
    private const uint MOD_NONE = 0x0000;

    private readonly Window _window;
    private readonly uint _virtualKey;
    private HwndSource? _source;
    private IntPtr _handle;
    private bool _registered;

    /// <summary>Raised on the UI thread when the hotkey is pressed.</summary>
    public event Action? Pressed;

    public HotkeyService(Window window, uint virtualKey)
    {
        _window = window;
        _virtualKey = virtualKey;
    }

    /// <summary>
    /// Registers the hotkey. The window handle must already exist (call from
    /// OnSourceInitialized or later). Returns false if registration failed, e.g.
    /// the key is already taken globally by another app.
    /// </summary>
    public bool Register()
    {
        _handle = new WindowInteropHelper(_window).Handle;
        _source = HwndSource.FromHwnd(_handle);
        if (_source is null)
            return false;

        _source.AddHook(WndProc);
        _registered = RegisterHotKey(_handle, HotkeyId, MOD_NONE, _virtualKey);
        return _registered;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registered)
        {
            UnregisterHotKey(_handle, HotkeyId);
            _registered = false;
        }
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
