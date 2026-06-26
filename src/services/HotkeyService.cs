using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Win32;

namespace ExanimapHelper;

/// <summary>
/// Registers hotkeys that toggle recording and mark POIs. Two strategies exist:
/// a Windows <em>global</em> hotkey that fires even while Exanima has focus, and a
/// window-focused fallback for the macOS dev build (where the game isn't running
/// anyway, so only in-window keys are needed).
/// </summary>
public interface IHotkeyService : IDisposable
{
    /// <summary>
    /// Registers <paramref name="key"/> to invoke <paramref name="onPressed"/>.
    /// Returns false if registration failed (e.g. the global key is already taken).
    /// </summary>
    bool Register(Key key, Action onPressed);
}

/// <summary>Creates the hotkey service appropriate for the current OS.</summary>
public static class Hotkeys
{
    public static IHotkeyService Create(Window window) =>
        OperatingSystem.IsWindows()
            ? new Win32HotkeyService(window)
            : new WindowHotkeyService(window);
}

/// <summary>
/// Cross-platform fallback: binds keys on the window itself, so they fire only while
/// the app is focused. Sufficient for the macOS dev build.
/// </summary>
internal sealed class WindowHotkeyService : IHotkeyService
{
    private readonly Window _window;
    private readonly Dictionary<Key, Action> _bindings = new();

    public WindowHotkeyService(Window window)
    {
        _window = window;
        _window.AddHandler(InputElement.KeyDownEvent, OnKeyDown,
            RoutingStrategies.Bubble, handledEventsToo: true);
    }

    public bool Register(Key key, Action onPressed)
    {
        _bindings[key] = onPressed;
        return true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_bindings.TryGetValue(e.Key, out Action? action))
        {
            action();
            e.Handled = true;
        }
    }

    public void Dispose() => _window.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
}

/// <summary>
/// Windows implementation: system-wide hotkeys via the Win32 RegisterHotKey API,
/// receiving WM_HOTKEY through Avalonia's Win32 WndProc hook. Fires even while another
/// application (Exanima) is focused, unlike a window key binding.
/// </summary>
internal sealed class Win32HotkeyService : IHotkeyService
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_NONE = 0x0000;
    private const int FirstHotkeyId = 0xB001; // arbitrary; ids stay unique within this window

    private readonly Window _window;
    private readonly Dictionary<int, Action> _callbacks = new();
    private readonly Win32Properties.CustomWndProcHookCallback _hook;
    private IntPtr _handle;
    private int _nextId = FirstHotkeyId;

    public Win32HotkeyService(Window window)
    {
        _window = window;
        _hook = WndProcHook;
    }

    public bool Register(Key key, Action onPressed)
    {
        if (!TryGetVirtualKey(key, out uint vk))
            return false;

        if (_handle == IntPtr.Zero)
        {
            // The window handle exists once the window is opened; Register is called
            // from OnOpened, so this is safe.
            var handle = _window.TryGetPlatformHandle();
            if (handle is null)
                return false;
            _handle = handle.Handle;
            Win32Properties.AddWndProcHookCallback(_window, _hook);
        }

        int id = _nextId++;
        if (!RegisterHotKey(_handle, id, MOD_NONE, vk))
            return false;

        _callbacks[id] = onPressed;
        return true;
    }

    private IntPtr WndProcHook(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _callbacks.TryGetValue(wParam.ToInt32(), out Action? onPressed))
        {
            onPressed();
            handled = true;
        }
        return IntPtr.Zero;
    }

    // Maps the Avalonia keys this app uses to Win32 virtual-key codes. Function keys
    // are contiguous from VK_F1 (0x70); no other keys are needed here.
    private static bool TryGetVirtualKey(Key key, out uint vk)
    {
        if (key >= Key.F1 && key <= Key.F24)
        {
            vk = (uint)(0x70 + (key - Key.F1));
            return true;
        }
        vk = 0;
        return false;
    }

    public void Dispose()
    {
        foreach (int id in _callbacks.Keys)
            UnregisterHotKey(_handle, id);
        _callbacks.Clear();
        if (_handle != IntPtr.Zero)
        {
            Win32Properties.RemoveWndProcHookCallback(_window, _hook);
            _handle = IntPtr.Zero;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
